using System.Globalization;
using System.Text.RegularExpressions;
using PlcSbom.Core.Adapters;
using PlcSbom.Core.Adapters.Codesys;
using PlcSbom.Core.Adapters.Codesys.V35;
using PlcSbom.Core.Adapters.Gsdml;
using PlcSbom.Core.Adapters.Studio5000;
using PlcSbom.Core.Adapters.TiaAml;
using PlcSbom.Core.Adapters.TwinCat;
using PlcSbom.Core.Config;
using PlcSbom.Core.Gaps;
using PlcSbom.Core.Identifiers;
using PlcSbom.Core.Merge;
using PlcSbom.Core.Model;
using PlcSbom.Core.Output;
using PlcSbom.Core.Validation;

namespace PlcSbom.Core.Build;

/// <summary>One call of plc-sbom build.</summary>
public sealed record BuildRequest
{
    public required MachineConfig Config { get; init; }

    /// <summary>Release identifier, e.g. R2026.10.</summary>
    public required string Release { get; init; }

    /// <summary>--commit; without it GIT_COMMIT, then git rev-parse HEAD.</summary>
    public string? Commit { get; init; }

    /// <summary>
    /// Base of every path written into the SBOM. Null: the Git repository above machine.yml (a directory
    /// with .git), otherwise the directory of machine.yml.
    /// </summary>
    public string? RepositoryRoot { get; init; }
}

/// <summary>What the console line of a station shows.</summary>
public sealed record StationSummary(string Adapter, string Name, int Devices, int Libraries, ToolInfo? Tool, DateTimeOffset? ExportedAt);

/// <summary>The assembled machine and everything the command reports about the run.</summary>
public sealed record BuildOutcome
{
    public required Machine Machine { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public string? Commit { get; init; }

    public required IReadOnlyList<StationSummary> Stations { get; init; }

    /// <summary>Vendor SBOMs found by the patterns, and how many describe a firmware of the machine.</summary>
    public int VendorSboms { get; init; }

    public int VendorSbomsUsed { get; init; }

    /// <summary>Tool named in each partial SBOM (null if it names none).</summary>
    public IReadOnlyList<string?> PartialSbomTools { get; init; } = [];

    public int GsdmlFiles { get; init; }

    /// <summary>Devices left out on purpose (ignoreDevices); for the report, not in the SBOM.</summary>
    public IReadOnlyList<Device> IgnoredDevices { get; init; } = [];

    /// <summary>Warnings and errors of the run itself: adapter warnings, input SBOMs, commit and timestamp.</summary>
    public required IReadOnlyList<Finding> Findings { get; init; }
}

/// <summary>
/// plc-sbom build without output (CLAUDE.md sections 5.6, 5.7, 6, 7.4, 8.4): reads every station with
/// its adapter, applies what machine.yml declares, adds the user programs with their source hash, merges
/// vendor and partial SBOMs and records the gaps. The core knows no vendor: adapters deliver the model,
/// declarations and merge work on the model only (H2).
/// </summary>
public static partial class MachineBuilder
{
    /// <summary>A PLC program is downloaded as compiled code: executable, not an archive, unstructured (E-021).</summary>
    public static readonly FileTraits UserProgramTraits = new(Executable: true, Archive: false, Structured: false);

    public static async Task<BuildOutcome> BuildAsync(BuildRequest request, IBuildEnvironment environment)
    {
        var config = request.Config;
        if (string.IsNullOrWhiteSpace(request.Release) || request.Release.Any(char.IsWhiteSpace))
        {
            throw new UsageException("RELEASE_INVALID", $"Die Release-Kennung „{request.Release}“ ist leer oder enthält Leerzeichen, etwa R2026.10 oder dev-142.");
        }
        var run = new Run(config, request.RepositoryRoot ?? FindRepositoryRoot(config.Directory));
        var commit = Commit(request, environment, run);
        var timestamp = Timestamp(environment, commit, run);

        var gsdml = config.Gsdml.Count == 0 ? null : GsdmlIndex.Load(config.Gsdml.Select(d => (run.Full(d), run.Relative(run.Full(d)))));
        var deviceDescriptions = config.DeviceDescriptions.Count == 0 ? null : DeviceDescriptionIndex.Load(config.DeviceDescriptions.Select(d => (run.Full(d), run.Relative(run.Full(d)))));
        var cpeMap = config.CpeMap is null ? null : CpeMap.Load(run.Full(config.CpeMap), run.Relative(run.Full(config.CpeMap)));

        var (stations, unassigned, ignored) = ReadStations(run, gsdml, deviceDescriptions);

        var machineParty = run.WithContact(MachineConfigLoader.ToParty(config.Machine.Supplier, config.Machine.Contact));
        stations = stations.Select(s => s with
        {
            Devices = s.Devices.Select(d => run.Enrich(run.Declare(d), cpeMap)).ToList(),
            Software = [Program(run, config.Stations.Single(c => c.Name == s.Name), request.Release, commit, machineParty), .. s.Software.Select(run.Enrich)],
        }).ToList();
        unassigned = unassigned.Select(d => run.Enrich(run.Declare(d), cpeMap)).ToList();

        var (inputs, vendorFiles, tools) = await ReadInputSboms(run).ConfigureAwait(false);
        var used = new HashSet<string>(StringComparer.Ordinal);
        Device Link(Device device)
        {
            var matches = inputs.Where(i => i.Key is not null && Describes(i.Key, device)).ToList();
            if (matches.Count == 0)
            {
                return device;
            }
            used.UnionWith(matches.Select(i => i.Sbom.Id));
            // File name and hash only if every matching vendor SBOM states the same.
            var files = matches.Select(i => i.File).Distinct().ToList();
            var file = files.Count == 1 ? files[0] : null;
            return device with
            {
                VendorSboms = matches.Select(i => i.Sbom.Id).ToList(),
                FirmwareFileName = file?.FileName,
                FirmwareUri = file?.Sha512 is null ? null : file.Uri,
                FirmwareSha512 = file?.Sha512,
            };
        }
        stations = stations.Select(s => s with { Devices = s.Devices.Select(Link).ToList() }).ToList();
        unassigned = unassigned.Select(Link).ToList();
        foreach (var input in inputs.Where(i => i.Key is not null && !used.Contains(i.Sbom.Id)))
        {
            run.Findings.Add(new Finding(Severity.Warning, "VENDOR_SBOM_UNUSED",
                $"Die Hersteller-SBOM {input.Sbom.SourcePath} beschreibt {Describe(input.Key!)}. Keine Firmware der Maschine passt dazu (Hersteller, Bestellnummer und Version oder CPE); sie ist nicht übernommen."));
        }

        var machine = GapRules.Apply(new Machine
        {
            Name = config.Machine.Name,
            Release = request.Release,
            Commit = commit,
            Supplier = machineParty,
            Licensing = run.Licence(machineParty.Name, null),
            Stations = stations,
            UnassignedDevices = unassigned,
            InputSboms = inputs.Where(i => i.Key is null || used.Contains(i.Sbom.Id)).Select(i => i.Sbom).ToList(),
        });

        var allDevices = machine.Stations.SelectMany(s => s.Devices).Concat(machine.UnassignedDevices).Concat(ignored);
        return new BuildOutcome
        {
            Machine = machine,
            Timestamp = timestamp,
            Commit = commit,
            Stations = machine.Stations.Select(s => new StationSummary(
                s.AdapterId,
                s.Name,
                s.Devices.Count,
                s.Software.Count(i => i.Kind is SoftwareKind.Library or SoftwareKind.LibraryType or SoftwareKind.AddOnInstruction),
                s.EngineeringTool,
                s.SourceExportedAt)).ToList(),
            VendorSboms = vendorFiles,
            VendorSbomsUsed = used.Count,
            PartialSbomTools = tools,
            GsdmlFiles = allDevices.Select(d => d.VendorKeys.GetValueOrDefault("gsdmlFile")).OfType<string>().Distinct(StringComparer.Ordinal).Count(),
            IgnoredDevices = ignored,
            Findings = run.Findings,
        };
    }

    /// <summary>Directory above <paramref name="start"/> that contains .git (directory or file of a worktree).</summary>
    public static string FindRepositoryRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git))
            {
                return dir.FullName;
            }
        }
        return start;
    }

    // ---- Commit and timestamp (CLAUDE.md 7.1, 8.4) -----------------------------------------------

    private static string? Commit(BuildRequest request, IBuildEnvironment environment, Run run)
    {
        var (value, origin) = request.Commit is { } option ? (option, "--commit")
            : environment.Variable("GIT_COMMIT") is { Length: > 0 } variable ? (variable, "GIT_COMMIT")
            : (environment.Git(run.Config.Directory, "rev-parse", "HEAD"), "git rev-parse HEAD");
        if (value is null)
        {
            run.Findings.Add(new Finding(Severity.Warning, "COMMIT_UNKNOWN",
                "Der Git-Commit ist unbekannt: weder --commit noch GIT_COMMIT ist gesetzt, und git rev-parse HEAD im Verzeichnis der Maschinenbeschreibung lieferte nichts. Die Anwenderprogramme tragen nur das Release als Version."));
            return null;
        }
        value = value.Trim();
        if (!CommitRegex().IsMatch(value))
        {
            throw new UsageException("COMMIT_INVALID", $"„{value}“ aus {origin} ist kein Git-Commit (7 bis 64 Hexadezimalzeichen).");
        }
        return value.ToLowerInvariant();
    }

    private static DateTimeOffset Timestamp(IBuildEnvironment environment, string? commit, Run run)
    {
        if (environment.Variable("SOURCE_DATE_EPOCH") is { Length: > 0 } epoch)
        {
            return long.TryParse(epoch.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds <= 253402300799
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : throw new UsageException("SOURCE_DATE_EPOCH_INVALID", $"SOURCE_DATE_EPOCH „{epoch}“ ist keine Zahl von Sekunden seit 1970-01-01 UTC.");
        }
        if (commit is not null
            && environment.Git(run.Config.Directory, "log", "-1", "--format=%ct", commit) is { } text
            && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var commitTime))
        {
            return DateTimeOffset.FromUnixTimeSeconds(commitTime);
        }
        run.Findings.Add(new Finding(Severity.Warning, "TIMESTAMP_FROM_CLOCK",
            "Der Zeitstempel der SBOM ist die aktuelle Uhrzeit, weil weder SOURCE_DATE_EPOCH gesetzt noch die Commit-Zeit über git bestimmbar ist. Zwei Läufe ergeben dann verschiedene Dateien."));
        return DateTimeOffset.FromUnixTimeSeconds(environment.Now.ToUnixTimeSeconds());
    }

    [GeneratedRegex("^[0-9a-fA-F]{7,64}$")]
    private static partial Regex CommitRegex();

    // ---- Stations ------------------------------------------------------------------------------

    private static (List<Station> Stations, List<Device> Unassigned, List<Device> Ignored) ReadStations(
        Run run,
        GsdmlIndex? gsdml,
        DeviceDescriptionIndex? deviceDescriptions)
    {
        var config = run.Config;
        var read = new Dictionary<string, Station>(StringComparer.Ordinal);
        var unassigned = new List<Device>();
        var ignored = new List<Device>();
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var amlFilesRead = new HashSet<string>(pathComparer);

        foreach (var station in config.Stations)
        {
            var full = run.Full(station.Path);
            var relative = run.Relative(full);
            switch (station.Adapter)
            {
                case AdapterIds.Codesys:
                    read[station.Name] = station.Source switch
                    {
                        CodesysV35Adapter.Source => CodesysV35Adapter.Read(full, new CodesysStationOptions
                        {
                            StationName = station.Name,
                            SourcePath = relative,
                            Plc = station.Plc,
                            DeviceDescriptions = deviceDescriptions,
                        }),
                        "v23" => throw NotImplemented("Die Quelle v23 des Adapters codesys (CODESYS V2.3)", "M3"),
                        _ => throw NotImplemented("Die Quelle c4 des Adapters codesys (CODESYS 4)", "M4"),
                    };
                    break;
                case AdapterIds.Studio5000:
                    read[station.Name] = Studio5000Adapter.Read(full, new Studio5000StationOptions { StationName = station.Name, SourcePath = relative });
                    break;
                case AdapterIds.TwinCat:
                    read[station.Name] = TwinCatAdapter.Read(full, new TwinCatStationOptions { StationName = station.Name, SourcePath = relative });
                    break;
                case AdapterIds.TiaAml:
                    // All stations of one AML file are read together (CLAUDE.md 5.4.1).
                    if (!amlFilesRead.Add(full))
                    {
                        break;
                    }
                    var result = TiaAmlAdapter.Read(full, new TiaAmlOptions
                    {
                        SourcePath = relative,
                        Stations = config.Stations
                            .Where(s => s.Adapter == AdapterIds.TiaAml && pathComparer.Equals(run.Full(s.Path), full))
                            .Select(s => new TiaAmlStationOptions { StationName = s.Name, Plc = s.Plc, Devices = s.Devices, IgnoreDevices = s.IgnoreDevices, Libraries = s.Libraries })
                            .ToList(),
                        PassiveOrderNumbers = config.PassiveOrderNumbers,
                        VendorOverrides = config.VendorOverrides,
                        Gsdml = gsdml,
                    });
                    foreach (var s in result.Stations)
                    {
                        read[s.Name] = s;
                    }
                    unassigned.AddRange(result.UnassignedDevices);
                    ignored.AddRange(result.IgnoredDevices);
                    run.Findings.AddRange(result.Warnings.Select(Warning));
                    break;
                default:
                    throw NotImplemented("Der Adapter tia-portal (TIA Portal V21 über Openness)", "M14", " Bis dahin den AML-Export mit dem Adapter tia-aml lesen.");
            }
        }
        return (config.Stations.Select(s => read[s.Name]).ToList(), unassigned, ignored);
    }

    private static Finding Warning(AdapterWarning warning) => new(Severity.Warning, warning.Code, warning.Message);

    private static UsageException NotImplemented(string what, string milestone, string hint = "") =>
        new("NOT_IMPLEMENTED", $"{what} ist in plc-sbom {ToolVersion.Short} noch nicht umgesetzt. Das folgt mit Meilenstein {milestone}.{hint}");

    // ---- User program (CLAUDE.md 5.6) ----------------------------------------------------------

    private static SoftwareItem Program(Run run, StationConfig station, string release, string? commit, Party machineParty)
    {
        ProgramHashResult? hash = null;
        string? relative = null;
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (station.Program is not null)
        {
            var full = run.Full(station.Program);
            if (!File.Exists(full) && !Directory.Exists(full))
            {
                throw new UsageException("PROGRAM_NOT_FOUND", $"Das Quellverzeichnis {station.Program} aus „program“ der Station {station.Name} gibt es nicht.");
            }
            hash = ProgramHash.Compute(full);
            relative = run.Relative(full);
            properties["sourceCode:path"] = relative;
            properties["sourceCode:files"] = hash.Files.ToString(CultureInfo.InvariantCulture);
        }
        var uri = hash is not null && commit is not null && run.Config.SourceUrl is { } template
            ? template.Replace("{commit}", commit, StringComparison.Ordinal)
                .Replace("{path}", string.Join('/', relative!.Split('/').Select(Uri.EscapeDataString)), StringComparison.Ordinal)
            : null;
        return new SoftwareItem
        {
            Kind = SoftwareKind.UserProgram,
            Name = $"{station.Name}, Anwenderprogramm",
            Vendor = machineParty,
            VendorOrigin = ValueOrigin.Declared,
            Origin = ValueOrigin.Declared,
            Version = commit is null ? release : $"{release} (git {commit[..7]})",
            Traits = UserProgramTraits,
            Licensing = run.Licence(machineParty.Name, null),
            SourceSha512 = hash?.Sha512,
            SourceUri = uri,
            SourceProperties = properties,
            Gaps = station.Program is null
                ? [new Gap(GapCatalog.ProgramSourceMissing, $"Für die Station {station.Name} ist kein Quellverzeichnis angegeben („program“ in machine.yml); ohne Quellen gibt es keinen Hash.")]
                : [],
        };
    }

    // ---- Vendor and partial SBOMs (CLAUDE.md 5.7) -------------------------------------------------

    private sealed record LinkedInput(InputSbom Sbom, VendorSbomKey? Key, DeployableFile? File);

    private static async Task<(List<LinkedInput> Inputs, int VendorFiles, List<string?> Tools)> ReadInputSboms(Run run)
    {
        var config = run.Config;
        var ids = new BomRef.Registry();
        var inputs = new List<LinkedInput>();
        var files = new List<string>();
        foreach (var pattern in config.VendorSboms)
        {
            var found = Glob.Find(config.Directory, pattern);
            if (found.Count == 0)
            {
                run.Findings.Add(new Finding(Severity.Warning, "VENDOR_SBOMS_NOT_FOUND", $"Das Muster „{pattern}“ unter „vendorSboms“ trifft keine Datei."));
            }
            files.AddRange(found.Where(f => !files.Contains(f)).ToList());
        }
        foreach (var file in files.OrderBy(run.Relative, StringComparer.Ordinal))
        {
            var read = await InputSbomReader.ReadAsync(file, run.Relative(file), InputSbomKind.Vendor, ids.Claim($"vendor-{BomRef.Slug(Stem(file))}"), station: null).ConfigureAwait(false);
            run.Findings.AddRange(read.Findings);
            if (read.Sbom is null)
            {
                continue;
            }
            if (read.Key is not { } key || (key.OrderNumber is null && key.Cpe is null))
            {
                run.Findings.Add(new Finding(Severity.Warning, "VENDOR_SBOM_NO_KEY",
                    $"Die Hersteller-SBOM {read.Sbom.SourcePath} nennt an metadata.component weder eine CPE noch eine Bestellnummer (Property <namespace>:device:orderNumber). Sie lässt sich keiner Firmware zuordnen und ist nicht übernommen."));
                continue;
            }
            inputs.Add(new LinkedInput(read.Sbom, key, read.File));
        }

        var tools = new List<string?>();
        foreach (var partial in config.PartialSboms)
        {
            var full = run.Full(partial.Path);
            if (!File.Exists(full))
            {
                throw new UsageException("PARTIAL_SBOM_NOT_FOUND", $"Die Teil-SBOM {partial.Path} aus „partialSboms“ gibt es nicht. Sie entsteht in einem früheren Schritt der Pipeline (Syft, cdxgen).");
            }
            var read = await InputSbomReader.ReadAsync(full, run.Relative(full), InputSbomKind.Partial, ids.Claim($"partial-{BomRef.Slug(Stem(full))}"), partial.Station).ConfigureAwait(false);
            run.Findings.AddRange(read.Findings);
            if (read.Sbom is not null)
            {
                inputs.Add(new LinkedInput(read.Sbom, null, null));
                tools.Add(read.ToolName);
            }
        }
        return (inputs, files.Count, tools);
    }

    /// <summary>
    /// A vendor SBOM describes a firmware if its CPE equals the firmware's CPE, or if vendor, order number
    /// (without spaces) and version (without leading "V") are equal. Nothing is guessed: without a
    /// firmware version in the project there is no match.
    /// </summary>
    private static bool Describes(VendorSbomKey key, Device device)
    {
        if (device.IsPassive)
        {
            return false;
        }
        if (key.Cpe is not null && device.Cpe is not null)
        {
            return key.Cpe == device.Cpe;
        }
        return key.Vendor is not null && key.OrderNumber is not null && key.Version is not null
            && device.Vendor?.Name == key.Vendor
            && device.OrderNumber is not null && device.Firmware is not null
            && NoSpaces(device.OrderNumber) == NoSpaces(key.OrderNumber)
            && CpeMap.VersionWithoutPrefix(device.Firmware) == CpeMap.VersionWithoutPrefix(key.Version);
    }

    private static string Describe(VendorSbomKey key) =>
        string.Join(", ", new[]
        {
            key.Vendor is null ? null : $"Hersteller {key.Vendor}",
            key.OrderNumber is null ? null : $"Bestellnummer {key.OrderNumber}",
            key.Version is null ? null : $"Version {key.Version}",
            key.Cpe is null ? null : $"CPE {key.Cpe}",
        }.OfType<string>());

    private static string NoSpaces(string text) => text.Replace(" ", "", StringComparison.Ordinal);

    private static string Stem(string file)
    {
        var name = Path.GetFileName(file);
        foreach (var suffix in new[] { ".cdx.json", ".json" })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^suffix.Length];
            }
        }
        return name;
    }

    /// <summary>State of one run: configuration, path base, findings and what machine.yml declares.</summary>
    private sealed class Run(MachineConfig config, string repositoryRoot)
    {
        public MachineConfig Config { get; } = config;

        public List<Finding> Findings { get; } = [];

        private readonly Dictionary<string, string> contacts = config.VendorContacts.GroupBy(c => c.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);

        private readonly Dictionary<string, string> licences = config.Licenses.GroupBy(c => c.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);

        /// <summary>Absolute path of a path from machine.yml.</summary>
        public string Full(string path) => Path.GetFullPath(Path.Combine(Config.Directory, path));

        /// <summary>Path as written into the SBOM: relative to the repository, with '/'.</summary>
        public string Relative(string fullPath) => Path.GetRelativePath(repositoryRoot, fullPath).Replace('\\', '/');

        public Party WithContact(Party party) =>
            party.HasContact || !contacts.TryGetValue(party.Name, out var contact) ? party : MachineConfigLoader.ToParty(party.Name, contact);

        public Licensing? Licence(string? vendor, Licensing? existing) =>
            existing?.Distribution is not null || vendor is null || !licences.TryGetValue(vendor, out var licence)
                ? existing
                : (existing ?? new Licensing(null)) with { Distribution = licence };

        /// <summary>
        /// vendorOverrides and passiveOrderNumbers on any adapter's device. Patterns match the order
        /// number, a device without one by its type name (e.g. EtherCAT short designation). Only a device
        /// without firmware version that is not the controller can be declared passive.
        /// </summary>
        public Device Declare(Device device)
        {
            var text = device.OrderNumber ?? device.TypeName;
            bool Match(string pattern) => device.OrderNumber is not null ? Wildcard.IsOrderNumberMatch(pattern, text) : Wildcard.IsMatch(pattern, text);

            var vendor = Config.VendorOverrides.FirstOrDefault(o => Match(o.Key)).Value;
            if (vendor is not null && !(device.Vendor?.Name == vendor && device.VendorOrigin == ValueOrigin.Declared))
            {
                device = device with
                {
                    Vendor = new Party(vendor),
                    VendorOrigin = ValueOrigin.Declared,
                    Gaps = device.Gaps.Where(g => g.Code != GapCatalog.SupplierUnknown).ToList(),
                };
            }
            if (!device.IsPassive && device.Firmware is null && !device.IsController && Config.PassiveOrderNumbers.Any(Match))
            {
                var properties = new SortedDictionary<string, string>(device.SourceProperties.ToDictionary(), StringComparer.Ordinal) { ["device:passive"] = "declared" };
                device = device with { IsPassive = true, Gaps = [], SourceProperties = properties };
            }
            return device;
        }

        /// <summary>Contact and licence of the vendor from machine.yml, CPE from the CPE table.</summary>
        public Device Enrich(Device device, CpeMap? cpeMap)
        {
            if (device.Vendor is not null)
            {
                device = device with { Vendor = WithContact(device.Vendor), FirmwareLicensing = Licence(device.Vendor.Name, device.FirmwareLicensing) };
            }
            return device.IsPassive || device.Cpe is not null || cpeMap?.Find(device) is not { } cpe ? device : device with { Cpe = cpe };
        }

        public SoftwareItem Enrich(SoftwareItem item) =>
            item.Vendor is null ? item : item with { Vendor = WithContact(item.Vendor), Licensing = Licence(item.Vendor.Name, item.Licensing) };
    }
}
