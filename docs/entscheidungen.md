# Entscheidungen

Kurze ADRs, neueste unten. Je Eintrag: Kontext, Entscheidung, Folgen.

## E-001: Exit 2 für Befehle, die im Gerüst noch fehlen (31.10.2026)

**Kontext.** M0 legt alle Befehle aus CLAUDE.md Abschnitt 7.1 mit ihren Optionen an, umgesetzt werden sie erst in späteren Meilensteinen. Ein Aufruf eines solchen Befehls braucht einen Exit-Code aus Abschnitt 7.2. Keiner passt genau: 1 wäre ein Befund am Projekt, 3 ein Problem des Agenten, 4 ein Absturz.

**Entscheidung.** Exit 2 mit Code `NOT_IMPLEMENTED` und dem Meilenstein in der Meldung, z. B. `[NOT_IMPLEMENTED] Der Befehl „build“ ist in plc-sbom 0.1.0 noch nicht umgesetzt. Er folgt mit Meilenstein M9.` Der Aufruf verlangt etwas, das diese Version nicht kann; behoben wird das am Aufruf oder durch eine neuere Version. Die Plattformprüfung der Openness-Befehle (`export-aml`, `doctor --adapter tia-portal`) läuft vorher: außerhalb von Windows Exit 3 mit `OPENNESS_WINDOWS_ONLY`, wie in Abschnitt 7.2 vorgesehen.

**Folgen.** Die Exit-Codes selbst bleiben unverändert. `NOT_IMPLEMENTED` verschwindet Befehl für Befehl mit den Meilensteinen. Unerwartete Ausnahmen ergeben Exit 4 mit Code `UNEXPECTED`, Stacktrace nur mit `--verbose`.

## E-002: Testlauf über Microsoft.Testing.Platform, xunit.v3 4.x (31.10.2026)

**Kontext.** plc-git-bridge nutzt xunit.v3 3.2.2 mit VSTest. Aktuell auf nuget.org ist xunit.v3 4.0.2 (09.10.2026). Diese Version bringt Microsoft.Testing.Platform 2.x mit, und das .NET-10-SDK verweigert damit den VSTest-Weg von `dotnet test`.

**Entscheidung.** xunit.v3 4.0.2, Testlauf über Microsoft.Testing.Platform (`global.json`: `"test": { "runner": "Microsoft.Testing.Platform" }`). `xunit.runner.visualstudio` und `Microsoft.NET.Test.Sdk` entfallen.

**Folgen.** Tests laufen mit `dotnet test` im Wurzelverzeichnis oder `dotnet test --solution PlcSbom.sln`; die Form `dotnet test PlcSbom.sln` geht nicht mehr. xunit.v3 zieht transitiv `Microsoft.Testing.Extensions.Telemetry` und `Microsoft.ApplicationInsights` (beide MIT) nach. Die Telemetrie ist in CI über `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` abgeschaltet; beide Pakete gehören nur zu den Tests und werden nicht ausgeliefert.

## E-003: Abhängigkeiten M0 (31.10.2026)

Geprüft auf nuget.org am 31.10.2026, Versionen in `Directory.Packages.props`.

| Paket | Version | Lizenz | Zweck | Ausgeliefert |
|---|---|---|---|---|
| System.CommandLine | 2.0.14 | MIT | Befehle, Optionen, Hilfe | ja |
| xunit.v3 | 4.0.2 | Apache-2.0 | Tests | nein |

`CycloneDX.Core` und `YamlDotNet` kommen mit M1 bzw. M9 dazu, jeweils mit Eintrag hier. System.CommandLine 3.0 liegt als Release Candidate vor; bis zur stabilen Version bleibt es bei 2.0.x.

## E-004: Lizenzprüfung und THIRD-PARTY-NOTICES.md über ein eigenes Skript (31.10.2026)

**Kontext.** CLAUDE.md Abschnitt 11 verlangt eine Lizenzprüfung aller Pakete in CI und eine aus den Paketen erzeugte `THIRD-PARTY-NOTICES.md`. Fertige Werkzeuge dafür wären eine weitere Abhängigkeit mit eigener Lizenz.

**Entscheidung.** `tools/check-licenses.cs`, eine dateibasierte .NET-10-Anwendung ohne Pakete (`dotnet run tools/check-licenses.cs`). Sie liest die Paketliste aus `dotnet list package --include-transitive --format json` und je Paket den SPDX-Ausdruck aus der `.nuspec` im NuGet-Cache. Erlaubt sind MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause und 0BSD; bei `OR` reicht eine erlaubte Seite, bei `AND` müssen alle erlaubt sein, `WITH` gilt als nicht erlaubt. Pakete ohne SPDX-Ausdruck (nur `licenseUrl` oder Lizenzdatei) sind ein Fehler, bis sie nach Rückfrage geklärt sind. Ausgeliefert sind die Pakete der Projekte unter `src/`; nur sie kommen in die Notices. CI prüft mit `--verify`, dass die eingecheckte Datei aktuell ist.

**Folgen.** Nach jeder Paketänderung `dotnet run tools/check-licenses.cs -- --write` und die Datei einchecken. Offen für M13: Die Hinweise zu Drittkomponenten der .NET-Laufzeit stehen bisher nur als Link in der Datei; für die Release-Pakete die Datei aus dem Runtime-Pack beilegen.

## E-005: Pakete self-contained als Einzeldatei, ohne Trimming (31.10.2026)

**Kontext.** CLAUDE.md Abschnitt 11: Pakete ohne installierte .NET-Laufzeit, `plc-sbom-<version>-linux-x64.tar.gz` und `plc-sbom-<version>-win-x64.zip`.

**Entscheidung.** `dotnet publish -r <rid>` erzeugt eine self-contained Einzeldatei; die Einstellungen stehen in `src/PlcSbom.Cli/PlcSbom.Cli.csproj` und greifen nur mit Runtime-Kennung, Build und Test bleiben davon unberührt. Kein Trimming, weil YamlDotNet und CycloneDX.Core mit Reflection arbeiten. Symbole sind eingebettet (`DebugType=embedded`), damit kein loses `.pdb` im Paket liegt und `--verbose` Zeilennummern zeigt. Ein Paket enthält die Exe und `THIRD-PARTY-NOTICES.md`.

**Folgen.** Die Exe ist rund 74 MB groß (linux-x64, win-x64). `plc-sbom --version` zeigt die Version aus `Directory.Build.props` und nach `+` den Commit, aus dem gebaut wurde. CI baut beide Pakete bei jedem Push, packt sie aus und ruft `--help`, `--version` und einige Exit-Codes auf. Release-Pakete auf Tag, `SHA256SUMS` und die eigene SBOM des Werkzeugs folgen mit M13.

## E-006: Solution im Format .sln (31.10.2026)

Das .NET-10-SDK legt neue Solutions als `.slnx` an. CLAUDE.md nennt `PlcSbom.sln`, die Schwesterprojekte nutzen `.sln`. Deshalb `dotnet new sln --format sln`. Ein Wechsel auf `.slnx` ist später ohne Folgen für das Werkzeug möglich.
