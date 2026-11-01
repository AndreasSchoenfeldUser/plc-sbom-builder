// SBOM of plc-sbom itself (CLAUDE.md section 11, ADR E-025): CycloneDX 1.6 for one published package.
//
//   dotnet run tools/tool-sbom.cs -- --exe out/publish/plc-sbom --out out/pkg/plc-sbom.cdx.json [--commit <sha>]
//
// Run from the repository root after `dotnet publish -r <rid>`. The source is the published single-file
// executable: its bundle manifest names every embedded file, the embedded deps.json maps each file to
// its NuGet package or runtime pack. Each embedded assembly becomes a component with the SHA-512 of its
// bytes as they lie in the bundle; name, creator and licence come from the package's nuspec in the
// NuGet cache. Check the result with `plc-sbom validate --release-build`.
// Timestamp: SOURCE_DATE_EPOCH, else the commit time. Commit: --commit, GITHUB_SHA, GIT_COMMIT, git.
// Exit 0 = written, 1 = bundle and packages disagree, 2 = call or environment.

#:project ../src/PlcSbom.Core/PlcSbom.Core.csproj

using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using PlcSbom.Core.Output;

const string Ns = "plcsbom";
const string Vendor = "Comquent GmbH";
const string VendorUrl = "https://comquent.de";
const string OwnLicence = "LicenseRef-comquent-proprietary";
const string ProductName = "PLC SBOM Builder";

string? exePath = null, outPath = null, commit = null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--exe" when i + 1 < args.Length: exePath = args[++i]; break;
        case "--out" when i + 1 < args.Length: outPath = args[++i]; break;
        case "--commit" when i + 1 < args.Length: commit = args[++i]; break;
        default:
            return Fail(2, "CALL_INVALID", "Aufruf: dotnet run tools/tool-sbom.cs -- --exe <plc-sbom[.exe]> --out <plc-sbom.cdx.json> [--commit <sha>]");
    }
}
if (exePath is null || outPath is null)
{
    return Fail(2, "CALL_INVALID", "--exe und --out sind Pflicht.");
}
if (!File.Exists(exePath))
{
    return Fail(2, "EXE_NOT_FOUND", $"{exePath} gibt es nicht. Vorher dotnet publish -r <rid> ausführen.");
}

commit ??= Environment.GetEnvironmentVariable("GITHUB_SHA") ?? Environment.GetEnvironmentVariable("GIT_COMMIT") ?? Git("rev-parse", "HEAD");
if (commit is null || commit.Length is < 7 or > 64 || !commit.All(Uri.IsHexDigit))
{
    return Fail(2, "COMMIT_INVALID", "Der Git-Commit fehlt oder ist kein Hexadezimalwert (--commit, GITHUB_SHA, GIT_COMMIT oder git rev-parse HEAD).");
}
var epochText = Environment.GetEnvironmentVariable("SOURCE_DATE_EPOCH") ?? Git("log", "-1", "--format=%ct", commit);
if (!long.TryParse(epochText, NumberStyles.None, CultureInfo.InvariantCulture, out var epoch))
{
    return Fail(2, "TIMESTAMP_UNKNOWN", "Weder SOURCE_DATE_EPOCH noch die Commit-Zeit sind bekannt.");
}
var timestamp = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

// ---- Bundle manifest (dotnet/runtime, Microsoft.NET.HostModel/Bundle: HostWriter, Manifest, FileEntry) --

var exe = File.ReadAllBytes(exePath);
List<BundleEntry> entries;
try
{
    entries = ReadBundle(exe);
}
catch (InvalidDataException e)
{
    return Fail(2, "NOT_SINGLE_FILE", $"{exePath}: {e.Message}");
}
if (entries.FirstOrDefault(e => e.Compressed) is { } packed)
{
    return Fail(1, "BUNDLE_COMPRESSED", $"{packed.Path} liegt komprimiert im Bundle; dann ist der Hash der Datei nicht der Hash der Bytes im Bundle. EnableCompressionInSingleFile abschalten.");
}
var depsEntry = entries.SingleOrDefault(e => e.Type == BundleFileType.DepsJson);
if (depsEntry is null)
{
    return Fail(1, "DEPS_JSON_MISSING", "Das Bundle enthält keine deps.json.");
}
using var deps = JsonDocument.Parse(exe.AsMemory((int)depsEntry.Offset, (int)depsEntry.Size));
var runtimeTarget = deps.RootElement.GetProperty("runtimeTarget").GetProperty("name").GetString()!;
var rid = runtimeTarget.Split('/').ElementAtOrDefault(1);
if (rid is null)
{
    return Fail(1, "RID_UNKNOWN", $"deps.json nennt kein Laufzeitziel mit RID ({runtimeTarget}).");
}
var targets = deps.RootElement.GetProperty("targets").GetProperty(runtimeTarget);
var libraries = deps.RootElement.GetProperty("libraries");

var packagesRoot = Run("dotnet", "nuget", "locals", "global-packages", "--list").Split(':', 2)[1].Trim();
var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");

// library key ("YamlDotNet/18.1.0") → description; file name in the bundle → library and asset
var libs = new Dictionary<string, Library>(StringComparer.Ordinal);
var files = new Dictionary<string, (Library Library, string Asset, string? FileVersion, string? AssemblyVersion)>(StringComparer.OrdinalIgnoreCase);
foreach (var target in targets.EnumerateObject())
{
    var key = target.Name;
    var info = libraries.GetProperty(key);
    var kind = info.GetProperty("type").GetString()!;
    var (id, version) = (key[..key.IndexOf('/')], key[(key.IndexOf('/') + 1)..]);
    Nuspec? nuspec = null;
    if (kind is "package" or "runtimepack")
    {
        var packageId = kind == "runtimepack" ? id["runtimepack.".Length..] : id;
        var dir = new[]
        {
            info.TryGetProperty("path", out var p) ? Path.Combine(packagesRoot, p.GetString()!) : null,
            Path.Combine(packagesRoot, packageId.ToLowerInvariant(), version.ToLowerInvariant()),
            dotnetRoot is null ? null : Path.Combine(dotnetRoot, "packs", packageId, version),
        }.FirstOrDefault(d => d is not null && Directory.Exists(d));
        if (dir is null)
        {
            return Fail(2, "PACKAGE_NOT_FOUND", $"{packageId} {version} liegt weder im NuGet-Cache ({packagesRoot}) noch unter DOTNET_ROOT/packs. Vorher dotnet publish -r {rid} ausführen.");
        }
        var nuspecFile = Directory.GetFiles(dir, "*.nuspec").SingleOrDefault();
        if (nuspecFile is null)
        {
            return Fail(2, "NUSPEC_MISSING", $"{dir} enthält keine .nuspec-Datei.");
        }
        nuspec = ReadNuspec(nuspecFile, packageId, dir);
    }
    var library = new Library(key, id, version, kind, nuspec,
        target.Value.TryGetProperty("dependencies", out var d) ? d.EnumerateObject().Select(x => $"{x.Name}/{x.Value.GetString()}").ToList() : []);
    libs[key] = library;
    foreach (var section in new[] { "runtime", "native" })
    {
        if (!target.Value.TryGetProperty(section, out var assets))
        {
            continue;
        }
        foreach (var asset in assets.EnumerateObject())
        {
            string? Version(string name) => asset.Value.TryGetProperty(name, out var v) && v.GetString() is { } s && s != "0.0.0.0" ? s : null;
            files[Path.GetFileName(asset.Name)] = (library, asset.Name, Version("fileVersion"), Version("assemblyVersion"));
        }
    }
}

var exeName = Path.GetFileName(exePath);
var primaryLibrary = libs.Values.FirstOrDefault(l => l.Kind == "project" && l.Id == Path.GetFileNameWithoutExtension(exeName));
if (primaryLibrary is null)
{
    return Fail(1, "APP_UNKNOWN", $"deps.json nennt kein Projekt {Path.GetFileNameWithoutExtension(exeName)}.");
}
var toolVersion = primaryLibrary.Version;
var runtimePack = libs.Values.SingleOrDefault(l => l.Kind == "runtimepack");
if (runtimePack?.Nuspec is null)
{
    return Fail(1, "RUNTIME_PACK_MISSING", "deps.json nennt kein Laufzeitpaket; plc-sbom ist dann nicht self-contained gebaut.");
}

// ---- Components ------------------------------------------------------------------------------

var exeSha512 = Convert.ToHexStringLower(SHA512.HashData(exe));
var components = new List<(string Ref, JsonObject Json)>();
var dependsOn = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
var completeness = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
void Complete(string aggregate, string bomRef)
{
    if (!completeness.TryGetValue(aggregate, out var set))
    {
        completeness[aggregate] = set = new(StringComparer.Ordinal);
    }
    set.Add(bomRef);
}
void Depend(string from, string? to)
{
    if (!dependsOn.TryGetValue(from, out var set))
    {
        dependsOn[from] = set = new(StringComparer.Ordinal);
    }
    if (to is not null)
    {
        set.Add(to);
    }
}

const string primaryRef = "plc-sbom";
const string runtimeRef = "dotnet-runtime";
var libraryRefs = new Dictionary<string, string>(StringComparer.Ordinal);
var failures = 0;

foreach (var entry in entries.Where(e => e.Type is BundleFileType.Assembly or BundleFileType.NativeBinary).OrderBy(e => e.Path, StringComparer.Ordinal))
{
    if (!files.TryGetValue(entry.Path, out var file))
    {
        Console.Error.WriteLine($"[BUNDLE_FILE_UNKNOWN] {entry.Path} liegt im Bundle, deps.json nennt die Datei nicht.");
        failures++;
        continue;
    }
    var bytes = exe.AsSpan((int)entry.Offset, (int)entry.Size);
    var sha512 = Convert.ToHexStringLower(SHA512.HashData(bytes));
    var library = file.Library;
    var isRuntime = library.Kind == "runtimepack";
    if (library.Kind == "package")
    {
        // The bundler copies assemblies unchanged (no trimming, no ReadyToRun): bytes must equal the package file.
        var packaged = Path.Combine(library.Nuspec!.Directory, file.Asset);
        if (!File.Exists(packaged) || !bytes.SequenceEqual(File.ReadAllBytes(packaged)))
        {
            Console.Error.WriteLine($"[BUNDLE_FILE_DIFFERS] {entry.Path} im Bundle weicht von {packaged} ab.");
            failures++;
        }
    }

    var stem = Path.GetFileNameWithoutExtension(entry.Path);
    var packageFiles = files.Values.Count(f => f.Library == library);
    var bomRef = library.Kind switch
    {
        "runtimepack" => $"dotnet-{BomRef.Slug(stem)}",
        "package" => $"nuget-{BomRef.Slug(packageFiles == 1 ? library.Id : stem)}",
        _ => $"plc-sbom-{BomRef.Slug(stem)}",
    };
    // Name of the creator: package id for a one-file package, else the assembly name (TR 5.2.2).
    var name = library.Kind == "package" && packageFiles == 1 ? library.Id : stem;
    var version = isRuntime ? runtimePack.Version : library.Version;
    var properties = new List<(string, string)>
    {
        ("bsi:component:archive", "no archive"),
        ("bsi:component:executable", "executable"),
        ("bsi:component:filename", entry.Path),
        ("bsi:component:structured", "unstructured"),
        ($"{Ns}:bundle:path", entry.Path),
    };
    if (file.FileVersion is { } fv)
    {
        properties.Add(($"{Ns}:file:version", fv));
    }
    if (file.AssemblyVersion is { } av)
    {
        properties.Add(($"{Ns}:assembly:version", av));
    }
    var json = Component(bomRef, "library", name, version,
        library.Nuspec is { } n ? (n.Authors, n.ProjectUrl) : (Vendor, VendorUrl),
        library.Nuspec?.License, library.Nuspec is null,
        library.Kind == "package" ? $"pkg:nuget/{library.Id}@{library.Version}" : null,
        (entry.Path, $"Datei im Single-File-Bundle von {exeName}; SHA-512 der Bytes, wie sie im Bundle liegen", sha512),
        properties);
    components.Add((bomRef, json));

    if (isRuntime)
    {
        Depend(runtimeRef, bomRef);
        Depend(bomRef, null);
        Complete("unknown", bomRef);
    }
    else
    {
        libraryRefs[library.Key] = bomRef;
        Depend(primaryRef, bomRef);
        Depend(bomRef, runtimeRef);
        Complete("complete", bomRef);
    }
}
if (failures > 0)
{
    return Fail(1, "BUNDLE_MISMATCH", $"{failures} Datei(en) im Bundle passen nicht zu deps.json oder den Paketen.");
}
foreach (var library in libs.Values.Where(l => libraryRefs.ContainsKey(l.Key)))
{
    foreach (var dependency in library.Dependencies)
    {
        Depend(libraryRefs[library.Key], libs.TryGetValue(dependency, out var target) && target.Kind == "runtimepack" ? runtimeRef : libraryRefs.GetValueOrDefault(dependency));
    }
}

// .NET runtime as logical component (TR 3.2.2, 8.1.3: framework). Its native part is linked into the host
// of the executable and not listed file by file, hence "incomplete".
var runtime = Component(runtimeRef, "framework", ".NET Runtime", runtimePack.Version,
    (runtimePack.Nuspec.Authors, runtimePack.Nuspec.ProjectUrl), runtimePack.Nuspec.License, false,
    $"pkg:nuget/{runtimePack.Nuspec.Id}@{runtimePack.Version}", null,
    [($"{Ns}:logicalComponent", "true"), ($"{Ns}:runtimeIdentifier", rid)]);
components.Add((runtimeRef, runtime));
Depend(primaryRef, runtimeRef);
Complete("incomplete", runtimeRef);

var primary = Component(primaryRef, "application", ProductName, toolVersion, (Vendor, VendorUrl), null, true, null,
    (exeName, "Datei im Paket neben dieser SBOM", exeSha512),
    [
        ("bsi:component:archive", "archive"),
        ("bsi:component:executable", "executable"),
        ("bsi:component:filename", exeName),
        ("bsi:component:structured", "structured"),
        ($"{Ns}:bundle:files", entries.Count.ToString(CultureInfo.InvariantCulture)),
        ($"{Ns}:git:commit", commit),
        ($"{Ns}:runtimeIdentifier", rid),
    ]);
Complete("complete", primaryRef);

var bom = new JsonObject
{
    ["$schema"] = "http://cyclonedx.org/schema/bom-1.6.schema.json",
    ["bomFormat"] = "CycloneDX",
    ["specVersion"] = "1.6",
    ["serialNumber"] = $"urn:uuid:{Uuid5.Create(Uuid5.Namespace, $"tool-sbom|{toolVersion}|{rid}|{commit}|{exeSha512}")}",
    ["version"] = 1,
    ["metadata"] = new JsonObject
    {
        ["timestamp"] = timestamp,
        ["tools"] = new JsonObject
        {
            ["components"] = new JsonArray(new JsonObject
            {
                ["type"] = "application",
                ["manufacturer"] = new JsonObject { ["name"] = Vendor },
                ["name"] = $"{ProductName}, tools/tool-sbom.cs",
                ["version"] = toolVersion,
            }),
        },
        ["manufacturer"] = Party(Vendor, VendorUrl),
        ["component"] = primary,
    },
    ["components"] = new JsonArray(components.OrderBy(c => c.Ref, StringComparer.Ordinal).Select(c => (JsonNode)c.Json).ToArray()),
    ["dependencies"] = new JsonArray(dependsOn.Select(d => (JsonNode)new JsonObject
    {
        ["ref"] = d.Key,
        ["dependsOn"] = new JsonArray(d.Value.Select(v => (JsonNode)JsonValue.Create(v)).ToArray()),
    }).ToArray()),
    ["compositions"] = new JsonArray(completeness.Select(c => (JsonNode)new JsonObject
    {
        ["aggregate"] = c.Key,
        ["dependencies"] = new JsonArray(c.Value.Select(v => (JsonNode)JsonValue.Create(v)).ToArray()),
    }).ToArray()),
};

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
using (var stream = File.Create(outPath))
{
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, IndentSize = 2, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
    {
        bom.WriteTo(writer);
    }
    stream.WriteByte((byte)'\n');
}
Console.WriteLine($"{outPath}: {ProductName} {toolVersion} ({rid}), {components.Count + 1} Komponenten, {entries.Count} Dateien im Bundle.");
return 0;

// ---- Helpers ---------------------------------------------------------------------------------

JsonObject Component(string bomRef, string type, string name, string version, (string Name, string? Url) creator,
    string? licence, bool own, string? purl, (string Url, string Comment, string Sha512)? distribution, List<(string Name, string Value)> properties)
{
    // What the nuspec does not say stays a gap (CLAUDE.md section 2), the validator then reports it.
    var c = new JsonObject { ["bom-ref"] = bomRef, ["type"] = type };
    if (creator.Name.Length > 0)
    {
        c["supplier"] = new JsonObject { ["name"] = creator.Name };
        c["manufacturer"] = Party(creator.Name, creator.Url);
        if (creator.Url is null)
        {
            properties.Add(($"{Ns}:gap", "creator-contact-unknown"));
        }
    }
    else
    {
        properties.Add(($"{Ns}:gap", "supplier-unknown"));
    }
    c["name"] = name;
    c["version"] = version;
    if (own)
    {
        c["licenses"] = new JsonArray(new JsonObject { ["expression"] = OwnLicence, ["acknowledgement"] = "concluded" });
    }
    else if (licence is not null)
    {
        c["licenses"] = Licences(licence);
    }
    else
    {
        properties.Add(($"{Ns}:gap", "license-unknown"));
    }
    if (purl is not null)
    {
        c["purl"] = purl;
    }
    if (distribution is { } d)
    {
        c["externalReferences"] = new JsonArray(new JsonObject
        {
            ["url"] = d.Url,
            ["comment"] = d.Comment,
            ["type"] = "distribution",
            ["hashes"] = new JsonArray(new JsonObject { ["alg"] = "SHA-512", ["content"] = d.Sha512 }),
        });
    }
    c["properties"] = new JsonArray(properties
        .OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Value, StringComparer.Ordinal)
        .Select(p => (JsonNode)new JsonObject { ["name"] = p.Name, ["value"] = p.Value }).ToArray());
    return c;
}

// The nuspec licence is the original licence of the creator (declared). plc-sbom passes the packages on
// under the same licence (concluded). A compound SPDX expression can only be written once (CycloneDX 1.6).
static JsonArray Licences(string expression) =>
    expression.Contains(' ', StringComparison.Ordinal)
        ? new JsonArray(new JsonObject { ["expression"] = expression, ["acknowledgement"] = "concluded" })
        : new JsonArray(
            new JsonObject { ["license"] = new JsonObject { ["id"] = expression, ["acknowledgement"] = "declared" } },
            new JsonObject { ["license"] = new JsonObject { ["id"] = expression, ["acknowledgement"] = "concluded" } });

static JsonObject Party(string name, string? url)
{
    var party = new JsonObject { ["name"] = name };
    if (url is not null)
    {
        party["url"] = new JsonArray(url);
    }
    return party;
}

static Nuspec ReadNuspec(string file, string id, string directory)
{
    var metadata = XDocument.Load(file).Root!.Elements().First(e => e.Name.LocalName == "metadata");
    string? Value(string name) => metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() is { Length: > 0 } v ? v : null;
    var license = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "license");
    return new Nuspec(Value("id") ?? id, Value("authors") ?? "", Value("projectUrl"),
        license?.Attribute("type")?.Value == "expression" ? license.Value.Trim() : null, directory);
}

static List<BundleEntry> ReadBundle(byte[] exe)
{
    // The host carries the offset of the bundle header in 8 bytes before this signature.
    ReadOnlySpan<byte> signature =
    [
        0x8b, 0x12, 0x02, 0xb9, 0x6a, 0x61, 0x20, 0x38, 0x72, 0x7b, 0x93, 0x02, 0x14, 0xd7, 0xa0, 0x32,
        0x13, 0xf5, 0xb9, 0xe6, 0xef, 0xae, 0x33, 0x18, 0xee, 0x3b, 0x2d, 0xce, 0x24, 0xb3, 0x6a, 0xae,
    ];
    var at = exe.AsSpan().IndexOf(signature);
    if (at < 8)
    {
        throw new InvalidDataException("keine Single-File-Anwendung (.NET-Bundle-Signatur fehlt).");
    }
    var position = BinaryPrimitives.ReadInt64LittleEndian(exe.AsSpan(at - 8));
    if (position <= 0 || position >= exe.Length)
    {
        throw new InvalidDataException("keine Single-File-Anwendung (Bundle-Kopf fehlt).");
    }
    var p = (int)position;
    var major = BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(p));
    var count = BinaryPrimitives.ReadInt32LittleEndian(exe.AsSpan(p + 8));
    p += 12;
    if (major < 6)
    {
        throw new InvalidDataException($"Bundle-Format {major} wird nicht gelesen, erwartet ab 6 (.NET 6).");
    }
    _ = ReadString(exe, ref p);             // bundle id
    p += 8 * 4 + 8;                          // deps.json and runtimeconfig.json offset/size, flags
    var result = new List<BundleEntry>(count);
    for (var i = 0; i < count; i++)
    {
        var offset = BinaryPrimitives.ReadInt64LittleEndian(exe.AsSpan(p));
        var size = BinaryPrimitives.ReadInt64LittleEndian(exe.AsSpan(p + 8));
        var compressed = BinaryPrimitives.ReadInt64LittleEndian(exe.AsSpan(p + 16));
        var type = (BundleFileType)exe[p + 24];
        p += 25;
        var path = ReadString(exe, ref p);
        if (offset < 0 || size < 0 || offset + size > exe.Length)
        {
            throw new InvalidDataException($"Eintrag {path} liegt außerhalb der Datei.");
        }
        result.Add(new BundleEntry(path, offset, size, compressed != 0, type));
    }
    return result;
}

// BinaryWriter string: 7-bit encoded length, then UTF-8.
static string ReadString(byte[] data, ref int p)
{
    int length = 0, shift = 0;
    byte b;
    do
    {
        b = data[p++];
        length |= (b & 0x7F) << shift;
        shift += 7;
    }
    while ((b & 0x80) != 0);
    var text = Encoding.UTF8.GetString(data, p, length);
    p += length;
    return text;
}

static string? Git(params string[] arguments)
{
    try
    {
        var text = Run("git", arguments).Trim();
        return text.Length == 0 ? null : text;
    }
    catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
    {
        return null;
    }
}

static string Run(string file, params string[] arguments)
{
    var info = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var argument in arguments)
    {
        info.ArgumentList.Add(argument);
    }
    using var process = Process.Start(info)!;
    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"{file} {string.Join(' ', arguments)} ist fehlgeschlagen ({process.ExitCode}): {error}");
    }
    return output;
}

static int Fail(int exit, string code, string message)
{
    Console.Error.WriteLine($"[{code}] {message}");
    return exit;
}

enum BundleFileType : byte { Unknown, Assembly, NativeBinary, DepsJson, RuntimeConfigJson, Symbols }

sealed record BundleEntry(string Path, long Offset, long Size, bool Compressed, BundleFileType Type);

sealed record Nuspec(string Id, string Authors, string? ProjectUrl, string? License, string Directory);

sealed record Library(string Key, string Id, string Version, string Kind, Nuspec? Nuspec, List<string> Dependencies);
