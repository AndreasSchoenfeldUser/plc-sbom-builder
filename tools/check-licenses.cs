// License check of all NuGet packages and generator for THIRD-PARTY-NOTICES.md (CLAUDE.md section 11).
//
//   dotnet run tools/check-licenses.cs                    check only
//   dotnet run tools/check-licenses.cs -- --write         check and write THIRD-PARTY-NOTICES.md
//   dotnet run tools/check-licenses.cs -- --verify        check and fail if THIRD-PARTY-NOTICES.md is outdated
//
// Run from the repository root after `dotnet restore`. Exit 0 = ok, 1 = finding, 2 = call/environment.
// Packages of projects under src/ are shipped and go into the notices; all packages are checked.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

const string Solution = "PlcSbom.sln";
const string NoticesFile = "THIRD-PARTY-NOTICES.md";
string[] allowed = ["MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "0BSD"];

var mode = args.FirstOrDefault() ?? "";
if (mode is not ("" or "--write" or "--verify"))
{
    Console.Error.WriteLine("Aufruf: dotnet run tools/check-licenses.cs [-- --write | --verify]");
    return 2;
}
if (!File.Exists(Solution))
{
    Console.Error.WriteLine($"{Solution} nicht gefunden. Aus dem Wurzelverzeichnis des Repositorys aufrufen.");
    return 2;
}

var packagesRoot = Run("dotnet", "nuget", "locals", "global-packages", "--list")
    .Split(':', 2)[1].Trim();
using var listing = JsonDocument.Parse(Run("dotnet", "list", Solution, "package", "--include-transitive", "--format", "json"));

// id|version -> shipped?
var packages = new SortedDictionary<string, (string Id, string Version, bool Shipped)>(StringComparer.OrdinalIgnoreCase);
foreach (var project in listing.RootElement.GetProperty("projects").EnumerateArray())
{
    var path = project.GetProperty("path").GetString()!.Replace('\\', '/');
    var shipped = path.Contains("/src/", StringComparison.Ordinal);
    if (!project.TryGetProperty("frameworks", out var frameworks))
    {
        continue;
    }
    foreach (var framework in frameworks.EnumerateArray())
    {
        foreach (var kind in new[] { "topLevelPackages", "transitivePackages" })
        {
            if (!framework.TryGetProperty(kind, out var list))
            {
                continue;
            }
            foreach (var package in list.EnumerateArray())
            {
                var id = package.GetProperty("id").GetString()!;
                var version = package.GetProperty("resolvedVersion").GetString()!;
                var key = $"{id}|{version}";
                packages[key] = (id, version, shipped || (packages.TryGetValue(key, out var known) && known.Shipped));
            }
        }
    }
}

var failures = 0;
var notices = new List<(string Id, string Version, string License, string? Copyright, string? ProjectUrl)>();
foreach (var (id, version, shipped) in packages.Values)
{
    var nuspecPath = Path.Combine(packagesRoot, id.ToLowerInvariant(), version.ToLowerInvariant(), id.ToLowerInvariant() + ".nuspec");
    if (!File.Exists(nuspecPath))
    {
        Console.Error.WriteLine($"[NUSPEC_MISSING] {id} {version}: {nuspecPath} fehlt. Vorher dotnet restore ausführen.");
        return 2;
    }
    var metadata = XDocument.Load(nuspecPath).Root!.Elements().First(e => e.Name.LocalName == "metadata");
    string? Value(string name) => metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim();
    var licenseElement = metadata.Elements().FirstOrDefault(e => e.Name.LocalName == "license");
    var expression = licenseElement?.Attribute("type")?.Value == "expression" ? licenseElement.Value.Trim() : null;

    var ok = expression is not null && IsAllowed(expression, allowed);
    Console.WriteLine($"{(ok ? "ok    " : "FEHLER")} {id} {version}  {expression ?? "(kein SPDX-Ausdruck: " + (Value("licenseUrl") ?? "keine Angabe") + ")"}{(shipped ? "  [ausgeliefert]" : "")}");
    if (!ok)
    {
        failures++;
        continue;
    }
    if (shipped)
    {
        notices.Add((id, version, expression!, Value("copyright"), Value("projectUrl")));
    }
}

if (failures > 0)
{
    Console.Error.WriteLine($"[LICENSE_NOT_ALLOWED] {failures} Paket(e) ohne erlaubte Lizenz. Erlaubt: {string.Join(", ", allowed)}. Andere Lizenzen nur nach Rückfrage (CLAUDE.md Abschnitt 11).");
    return 1;
}

var text = Notices(notices);
switch (mode)
{
    case "--write":
        File.WriteAllText(NoticesFile, text, new UTF8Encoding(false));
        Console.WriteLine($"{NoticesFile} geschrieben.");
        break;
    case "--verify":
        var current = File.Exists(NoticesFile) ? File.ReadAllText(NoticesFile).ReplaceLineEndings("\n") : "";
        if (current != text)
        {
            Console.Error.WriteLine($"[NOTICES_OUTDATED] {NoticesFile} passt nicht zu den Paketen. Mit `dotnet run tools/check-licenses.cs -- --write` neu erzeugen und einchecken.");
            return 1;
        }
        Console.WriteLine($"{NoticesFile} ist aktuell.");
        break;
}
Console.WriteLine($"{packages.Count} Pakete geprüft, {notices.Count} ausgeliefert.");
return 0;

// SPDX expression check: "A OR B" needs one allowed side, "A AND B" needs all. Parentheses are ignored,
// which is enough for the flat expressions on nuget.org; nested mixes fail closed via the AND rule.
static bool IsAllowed(string expression, string[] allowed)
{
    var flat = expression.Replace("(", " ").Replace(")", " ");
    if (flat.Contains(" WITH ", StringComparison.Ordinal))
    {
        return false;
    }
    if (flat.Contains(" OR ", StringComparison.Ordinal) && !flat.Contains(" AND ", StringComparison.Ordinal))
    {
        return flat.Split(" OR ").Any(part => allowed.Contains(part.Trim()));
    }
    return flat.Split(" AND ").All(part => allowed.Contains(part.Trim()));
}

static string Notices(List<(string Id, string Version, string License, string? Copyright, string? ProjectUrl)> packages)
{
    var b = new StringBuilder();
    b.Append("# Lizenzen von Drittkomponenten\n\n");
    b.Append("Diese Datei wird mit `dotnet run tools/check-licenses.cs -- --write` aus den NuGet-Paketen erzeugt. Nicht von Hand ändern.\n\n");
    b.Append("Die Pakete von plc-sbom sind self-contained und enthalten die .NET-Laufzeit sowie die folgenden NuGet-Pakete.\n\n");
    b.Append("| Komponente | Version | Lizenz | Projekt |\n|---|---|---|---|\n");
    b.Append("| .NET Runtime | wie im Paket | MIT | https://github.com/dotnet/runtime |\n");
    foreach (var p in packages.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
    {
        b.Append($"| {p.Id} | {p.Version} | {p.License} | {p.ProjectUrl ?? ""} |\n");
    }
    b.Append("\n## .NET Runtime\n\n");
    b.Append("Copyright (c) .NET Foundation and Contributors. Lizenz MIT, Text unten. Hinweise zu Drittkomponenten der Laufzeit: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT\n");
    foreach (var p in packages.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase))
    {
        b.Append($"\n## {p.Id} {p.Version}\n\n");
        b.Append($"{(p.Copyright ?? "Copyright: keine Angabe im Paket").TrimEnd('.')}. Lizenz {p.License}");
        b.Append(p.License == "MIT" ? ", Text unten.\n" : $", Text: https://spdx.org/licenses/{p.License}.html\n");
    }
    b.Append("\n## Lizenztext MIT\n\n");
    b.Append("""
        ```text
        Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
        associated documentation files (the "Software"), to deal in the Software without restriction,
        including without limitation the rights to use, copy, modify, merge, publish, distribute,
        sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all copies or
        substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
        NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
        NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
        DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
        OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
        ```

        """.ReplaceLineEndings("\n"));
    return b.ToString();
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
