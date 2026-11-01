# Testfälle: PLC SBOM Builder (`plc-sbom`)

Stand: 01.11.2026, Version 0.1.0 (Meilensteine M1 und M2 abgeschlossen, M5, M6, M7 und M8 umgesetzt). Diese Datei beschreibt alle automatischen Testfälle, die Testläufe und ihre Ergebnisse. Die Bedienung des Werkzeugs steht in [ANLEITUNG.md](ANLEITUNG.md), Ziele und Regeln in [CLAUDE.md](CLAUDE.md).

## 1. Ergebnis

**508 Testfälle, 508 bestanden, 0 fehlgeschlagen, 0 übersprungen.** Letzter Lauf am 01.11.2026, 11:50 Uhr, Laufzeit 1,4 s. `dotnet format --verify-no-changes` ohne Befund. Dazu die Gegenprüfung mit sbomqs ohne Abweichung (Abschnitt 6.1). Fünf Testfälle lesen Dateien aus `fixtures/_local/tia-aml/` (ohne Lizenz, nicht eingecheckt); sie liefen lokal mit und überspringen sich in CI.

| Testklasse | Datei | Testfälle | Bestanden | Prüfgegenstand |
|---|---|---:|---:|---|
| `CliAppTests` | [Cli/CliAppTests.cs](tests/PlcSbom.Tests/Cli/CliAppTests.cs) | 39 | 39 | Kommandozeile, Hilfe, Exit-Codes, Gerüst der Befehle |
| `ValidateCommandTests` | [Cli/ValidateCommandTests.cs](tests/PlcSbom.Tests/Cli/ValidateCommandTests.cs) | 11 | 11 | `plc-sbom validate` mit echten Dateien |
| `SbomValidatorTests` | [Validation/SbomValidatorTests.cs](tests/PlcSbom.Tests/Validation/SbomValidatorTests.cs) | 74 | 74 | Schema und Regeln der BSI TR-03183-2 |
| `GapRulesTests` | [Gaps/GapRulesTests.cs](tests/PlcSbom.Tests/Gaps/GapRulesTests.cs) | 28 | 28 | Lückenregeln am Modell, Lückenkatalog |
| `CycloneDxWriterTests` | [Output/CycloneDxWriterTests.cs](tests/PlcSbom.Tests/Output/CycloneDxWriterTests.cs) | 21 | 21 | CycloneDX-Writer, Golden File, Determinismus |
| `ResultFileTests` | [Output/ResultFileTests.cs](tests/PlcSbom.Tests/Output/ResultFileTests.cs) | 8 | 8 | Ergebnisdatei `<out>.result.json` |
| `IdentifierTests` | [Output/IdentifierTests.cs](tests/PlcSbom.Tests/Output/IdentifierTests.cs) | 11 | 11 | UUIDv5, `bom-ref`, Pfad der Ergebnisdatei |
| `DocsConsistencyTests` | [Docs/DocsConsistencyTests.cs](tests/PlcSbom.Tests/Docs/DocsConsistencyTests.cs) | 47 | 47 | Abgleich von ANLEITUNG.md, CLAUDE.md, README.md mit dem Code |
| `SampleFileTests` | [Samples/SampleFileTests.cs](tests/PlcSbom.Tests/Samples/SampleFileTests.cs) | 1 | 1 | Minimal-SBOM als Golden File für die Gegenprüfung |
| `LibraryReferenceTests` | [Adapters/Codesys/LibraryReferenceTests.cs](tests/PlcSbom.Tests/Adapters/Codesys/LibraryReferenceTests.cs) | 14 | 14 | CODESYS-Bibliotheksangabe `Titel, Version (Hersteller)` |
| `PlcOpenReaderTests` | [Adapters/Codesys/PlcOpenReaderTests.cs](tests/PlcSbom.Tests/Adapters/Codesys/PlcOpenReaderTests.cs) | 15 | 15 | PLCopen-XML-Leser an echten Fixtures, sicheres XML |
| `CodesysV35AdapterTests` | [Adapters/Codesys/CodesysV35AdapterTests.cs](tests/PlcSbom.Tests/Adapters/Codesys/CodesysV35AdapterTests.cs) | 28 | 28 | Adapter `codesys`/`v35`: Geräte, Bibliotheken, Gerätebeschreibungen, Lücken, Golden Files |
| `DeviceDescriptionIndexTests` | [Adapters/Codesys/DeviceDescriptionIndexTests.cs](tests/PlcSbom.Tests/Adapters/Codesys/DeviceDescriptionIndexTests.cs) | 12 | 12 | CODESYS-Gerätebeschreibungen lesen und zuordnen |
| `InspectCommandTests` | [Cli/InspectCommandTests.cs](tests/PlcSbom.Tests/Cli/InspectCommandTests.cs) | 16 | 16 | `plc-sbom inspect` für `codesys`, `studio5000`, `twincat` und `tia-aml`, `--gsdml` |
| `L5xReaderTests` | [Adapters/Studio5000/L5xReaderTests.cs](tests/PlcSbom.Tests/Adapters/Studio5000/L5xReaderTests.cs) | 10 | 10 | L5X-Leser an den echten Exporten |
| `TwinCatReaderTests` | [Adapters/TwinCat/TwinCatReaderTests.cs](tests/PlcSbom.Tests/Adapters/TwinCat/TwinCatReaderTests.cs) | 10 | 10 | Leser für `.tsproj`, `_Config/PLC/*.xti` und `.plcproj` an den echten Projekten |
| `TwinCatAdapterTests` | [Adapters/TwinCat/TwinCatAdapterTests.cs](tests/PlcSbom.Tests/Adapters/TwinCat/TwinCatAdapterTests.cs) | 37 | 37 | Adapter `twincat`: EtherCAT-Teilnehmer, Hersteller, deaktivierte Geräte, Laufzeit, Bibliotheken, nicht gelesene Geräte, Golden Files, keine Netzdaten |
| `Studio5000AdapterTests` | [Adapters/Studio5000/Studio5000AdapterTests.cs](tests/PlcSbom.Tests/Adapters/Studio5000/Studio5000AdapterTests.cs) | 23 | 23 | Adapter `studio5000`: Station, Firmware-Soll, Kodierung, Hersteller, AOIs, Golden Files, keine Netzdaten |
| `AmlReaderTests` | [Adapters/TiaAml/AmlReaderTests.cs](tests/PlcSbom.Tests/Adapters/TiaAml/AmlReaderTests.cs) | 11 | 11 | AML-Leser: Kopf, Rollen, integrierte Teile, IO-System-Links, Steuerzeichen, keine Netzdaten |
| `TiaAmlAdapterTests` | [Adapters/TiaAml/TiaAmlAdapterTests.cs](tests/PlcSbom.Tests/Adapters/TiaAml/TiaAmlAdapterTests.cs) | 58 | 58 | Adapter `tia-aml`: Versionsprüfung, Firmware-Regeln, passive Module, Hersteller, GSD-Geräte mit und ohne GSDML, Stationen über IO-Systeme, Fehlerfälle, Golden Files, keine Netzdaten |
| `GsdmlReaderTests` | [Adapters/Gsdml/GsdmlReaderTests.cs](tests/PlcSbom.Tests/Adapters/Gsdml/GsdmlReaderTests.cs) | 17 | 17 | GSDML-Leser und Suche: Identität, Zugangspunkt, Module, Muster-Bestellnummer, Groß-/Kleinschreibung, mehrdeutige Dateien, Fehlerfälle |
| `WildcardTests` | [WildcardTests.cs](tests/PlcSbom.Tests/WildcardTests.cs) | 17 | 17 | Muster mit `*` und `?` für `devices`, `ignoreDevices`, `passiveOrderNumbers`, `vendorOverrides` |
| **Summe** | | **508** | **508** | |

Bei parametrisierten Tests (xUnit `Theory`) zählt jeder Datensatz als eigener Testfall. Die Tabellen in Abschnitt 4 fassen sie in einer Zeile zusammen und nennen die Anzahl.

## 2. Tests ausführen

Voraussetzung ist das .NET 10 SDK. Die Tests laufen ohne Netz, ohne Engineering-Software und ohne Lizenz auf macOS, Linux und Windows.

```bash
dotnet test                                                        # alle Tests
dotnet test -- --report-xunit-trx --results-directory build/tests  # zusätzlich TRX-Bericht je Testfall
dotnet test -- --filter-class "PlcSbom.Tests.Validation.*"         # nur eine Gruppe
dotnet format PlcSbom.sln --verify-no-changes                      # Formatprüfung wie in CI
PLC_SBOM_UPDATE_GOLDEN=1 dotnet test                               # Golden Files neu schreiben (nur mit Begründung im Commit)
```

Der Testlauf nutzt Microsoft.Testing.Platform mit xunit.v3 (docs/entscheidungen.md, E-002). In CI (GitHub Actions) laufen dieselben Tests auf `ubuntu-latest` und `windows-latest`.

## 3. Testdaten

| Testdaten | Ort | Inhalt |
|---|---|---|
| Handmodell | [Samples/HandModel.cs](tests/PlcSbom.Tests/Samples/HandModel.cs) | Von Hand gebautes Modell der „Maschine XY“ mit zwei Stationen: PLC_1 (TIA AML: Profilschiene, CPU 1511-1 PN V2.8, PM 70W ohne Firmware, IM 155-6 PN HF, Bibliothek LGF, Anwenderprogramm) und Roboterzelle (TwinCAT: EK1100, Runtime ohne Version, Tc2_Standard mit Version `*`), dazu ein nicht zugeordnetes Panel. Gerätedaten nach den Fixtures aus CLAUDE.md Abschnitt 13, alles Übrige erfundene Testdaten |
| Golden File | [golden/hand-model.cdx.json](tests/PlcSbom.Tests/golden/hand-model.cdx.json) | Erwartete SBOM aus dem Handmodell, byte-genau. 11 Komponenten, 2 Stationen, 0 Fehler, 9 Warnungen, 32 Hinweise |
| Minimal-SBOM | [Samples/MinimalSbom.cs](tests/PlcSbom.Tests/Samples/MinimalSbom.cs) | Kleinstes CycloneDX-1.6-Dokument, das Schema und TR-Regeln besteht: logische Hauptkomponente „Maschine XY“ und eine vollständig beschriebene Anwendung `app`. Synthetisch. Jeder Validator-Test ändert genau eine Stelle |
| CODESYS-Fixtures | [fixtures/codesys/v35/](fixtures/codesys/v35/), [fixtures/codesys/devdesc/](fixtures/codesys/devdesc/) | Drei echte PLCopen-XML-Exporte (robin V3.5 SP15, OSC-MQTT V3.5 SP4, ooip V3.5 SP20), zwei synthetische (zwei Steuerungen; Geräte mit Kennungen der Gerätebeschreibungen) und vier echte Gerätebeschreibungen; Herkunft in [fixtures/FIXTURES.md](fixtures/FIXTURES.md) |
| Golden Files CODESYS | [golden/codesys-v35-robin.cdx.json](tests/PlcSbom.Tests/golden/codesys-v35-robin.cdx.json), [golden/codesys-v35-zwei-steuerungen.cdx.json](tests/PlcSbom.Tests/golden/codesys-v35-zwei-steuerungen.cdx.json), [golden/codesys-v35-zwei-steuerungen.inspect.json](tests/PlcSbom.Tests/golden/codesys-v35-zwei-steuerungen.inspect.json), [golden/codesys-v35-devdesc.cdx.json](tests/PlcSbom.Tests/golden/codesys-v35-devdesc.cdx.json) | SBOM aus robin, aus der Steuerung „Verpackung“ der synthetischen Datei und aus der Datei mit Gerätebeschreibungen, eingebettet in die Maschine des Handmodells; Rohinventar von `inspect` |
| Studio-5000-Fixtures | [fixtures/studio5000/](fixtures/studio5000/) | Fünf echte L5X-Exporte (ra-logix-cicd: Controller 1756-L85E V36 und zwei AOI-Exporte; acd: Controller mit 1756-EN2T und AOI, Controller mit Redundanz) und ein synthetischer (Modulbaum mit entferntem Chassis, ausgeschalteter Kodierung, Fremdgerät, AOIs); Herkunft in [fixtures/FIXTURES.md](fixtures/FIXTURES.md) |
| Golden Files Studio 5000 | [golden/studio5000-ra-l85e.cdx.json](tests/PlcSbom.Tests/golden/studio5000-ra-l85e.cdx.json), [golden/studio5000-acd-aoi.cdx.json](tests/PlcSbom.Tests/golden/studio5000-acd-aoi.cdx.json), [golden/studio5000-module-baum.cdx.json](tests/PlcSbom.Tests/golden/studio5000-module-baum.cdx.json), [golden/studio5000-module-baum.inspect.json](tests/PlcSbom.Tests/golden/studio5000-module-baum.inspect.json) | SBOM aus den beiden echten Controller-Exporten mit Modulen bzw. AOI und aus der synthetischen Datei, eingebettet in die Maschine des Handmodells; Rohinventar von `inspect` |
| TwinCAT-Fixtures | [fixtures/twincat/](fixtures/twincat/) | Vier echte Projekte (AixOCAT HeatPump: zehn EtherCAT-Teilnehmer, deaktivierter Master; SPT EtherCAT Diagnostics: SPS-Projekt über `_Config/PLC/*.xti`; TcOpen Cognex: PROFINET-Geräte; TcOpen Core: drei SPS-Projekte, `LibraryReference`) und ein synthetisches (Fremdgerät, ausgelagertes E/A-Gerät, feste Versionen); Herkunft in [fixtures/FIXTURES.md](fixtures/FIXTURES.md) |
| Golden Files TwinCAT | [golden/twincat-*.cdx.json](tests/PlcSbom.Tests/golden/), [golden/twincat-spt-ethercat-diagnostics.inspect.json](tests/PlcSbom.Tests/golden/twincat-spt-ethercat-diagnostics.inspect.json) | SBOM aus allen fünf Projekten, eingebettet in die Maschine des Handmodells; Rohinventar von `inspect` |
| TIA-AML-Fixtures | [fixtures/tia-aml/](fixtures/tia-aml/), lokal `fixtures/_local/tia-aml/` | Ein echter Export aus TIA V17 (S7-1200, GSD-Gerät Leuze BCL248i, MIT) und ein synthetischer mit zwei CPUs; lokal ohne Lizenz die Exporte aus V18 (sieben Geräte am IO-System) und V21 (PM ohne Firmware-Attribut) sowie zwei AML-Dateien anderer Werkzeuge; Herkunft in [fixtures/FIXTURES.md](fixtures/FIXTURES.md) |
| GSDML-Fixture | [fixtures/gsdml/](fixtures/gsdml/) | Echte GSDML des Leuze BCL248i aus dem Repository der V17-Fixture (MIT); Kopien mit verändertem Inhalt entstehen nur im Test in einem Temp-Verzeichnis |
| Golden Files TIA-AML | [golden/tia-aml-v17-ganze-datei.cdx.json](tests/PlcSbom.Tests/golden/tia-aml-v17-ganze-datei.cdx.json), [golden/tia-aml-v17-plc.cdx.json](tests/PlcSbom.Tests/golden/tia-aml-v17-plc.cdx.json), [golden/tia-aml-v17-gsdml.cdx.json](tests/PlcSbom.Tests/golden/tia-aml-v17-gsdml.cdx.json), [golden/tia-aml-zwei-cpus.cdx.json](tests/PlcSbom.Tests/golden/tia-aml-zwei-cpus.cdx.json), [golden/tia-aml-zwei-cpus.inspect.json](tests/PlcSbom.Tests/golden/tia-aml-zwei-cpus.inspect.json) | SBOM aus V17 ohne `plc` (ganze Datei), mit `plc` (GSD-Gerät nicht zugeordnet) und mit GSDML-Verzeichnis, aus der synthetischen Datei mit zwei Stationen, `devices`, `ignoreDevices` und deklarierter Bibliothek, eingebettet in die Maschine des Handmodells; Rohinventar von `inspect` |
| Golden File der Minimal-SBOM | [golden/minimal-synthetisch.cdx.json](tests/PlcSbom.Tests/golden/minimal-synthetisch.cdx.json) | Die Minimal-SBOM als Datei. Einzige Testdatei, in der Dateiname, Hash und Dateieigenschaften vorhanden sind; Eingang der Gegenprüfung mit sbomqs neben `hand-model.cdx.json` |

## 4. Testfälle

Spalte „Ergebnis“: Ergebnis des letzten Laufs. „bestanden“ heißt, dass alle Zusicherungen des Testfalls zutrafen.

### 4.1 Kommandozeile: `CliAppTests` (39)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| CLI-01 | `Exit_codes_match_the_documented_interface` | Die Exit-Codes haben die Werte aus CLAUDE.md 7.2: 0 Ok, 1 Befund, 2 Aufruf, 3 Umgebung, 4 Abbruch | 1 | bestanden |
| CLI-02 | `Help_lists_all_commands` | `--help` endet mit Exit 0 und nennt alle acht Befehle sowie `--verbose` und `--timeout` | 1 | bestanden |
| CLI-03 | `Help_of_each_command_succeeds` | `<befehl> --help` endet mit Exit 0, für `build`, `validate`, `report`, `compare`, `ids`, `inspect`, `export-aml`, `doctor` | 8 | bestanden |
| CLI-04 | `Build_help_shows_the_options_of_section_7_1` | Die Hilfe zu `build` nennt `--config`, `--release`, `--commit`, `--out`, `--report`, `--release-build` | 1 | bestanden |
| CLI-05 | `Version_prints_the_tool_version` | `--version` gibt die Version im Format `x.y.z` aus, Exit 0 | 1 | bestanden |
| CLI-06 | `No_command_is_a_usage_error` | Aufruf ohne Befehl: Exit 2 und Hinweis auf `plc-sbom --help` | 1 | bestanden |
| CLI-07 | `Invalid_calls_end_with_exit_2` | Falsche Aufrufe enden mit Exit 2 und `[USAGE]`: unbekannter Befehl, fehlendes `--config` bzw. `--release`, `--report pdf`, unbekannte Option, `report --format pdf`, unbekannte Adapter-ID, fehlendes `--adapter`, `doctor --adapter twincat`, `validate` ohne Datei, `--timeout 0`, `--timeout zehn` | 12 | bestanden |
| CLI-08 | `Commands_not_yet_built_name_their_milestone` | Noch nicht umgesetzte Befehle enden mit Exit 2 und `[NOT_IMPLEMENTED]` und nennen ihren Meilenstein: `build` M9, `report` M10, `compare` M11, `ids` M12, `doctor` M14, `inspect` für `tia-portal` M14 | 6 | bestanden |
| CLI-09 | `Openness_commands_off_windows_are_an_environment_problem` | `export-aml`, `doctor --adapter tia-portal` und `doctor --adapter tia-portal --deep` außerhalb von Windows: Exit 3 und `[OPENNESS_WINDOWS_ONLY]` | 3 | bestanden |
| CLI-10 | `Openness_commands_on_windows_pass_the_platform_check` | Auf Windows kommt `export-aml` an der Plattformprüfung vorbei und meldet Meilenstein M14 | 1 | bestanden |
| CLI-11 | `Common_options_are_accepted_after_the_command` | `--verbose` und `--timeout` werden auch nach dem Befehl angenommen | 1 | bestanden |
| CLI-12 | `Known_errors_map_to_their_exit_code` | Eine bekannte Ausnahme (hier `TIA_NOT_INSTALLED`) ergibt ihren Exit-Code und die Meldung `[CODE] Text` | 1 | bestanden |
| CLI-13 | `Unexpected_errors_abort_with_exit_4` | Eine unerwartete Ausnahme ergibt Exit 4 und `[UNEXPECTED]`, ohne Stacktrace | 1 | bestanden |
| CLI-14 | `Verbose_shows_the_stack_trace_of_unexpected_errors` | Mit `--verbose` erscheint der Stacktrace | 1 | bestanden |

### 4.2 Befehl `validate` von Anfang bis Ende: `ValidateCommandTests` (11)

Jeder Testfall legt ein eigenes temporäres Verzeichnis an, ruft die Kommandozeile mit echten Dateien auf und löscht das Verzeichnis danach.

| ID | Testfall | Eingabe | Erwartet | Fälle | Ergebnis |
|---|---|---|---|---:|---|
| VAL-01 | `Golden_sbom_passes_the_development_build` | Golden File | Exit 0, keine Ausgabe auf stderr, Zeilen „Schema CycloneDX 1.6 … OK“ und „Pflichtfelder BSI TR-03183-2 … OK, 9 Warnungen, 32 Hinweise“, Zeile `[ausgabe]` | 1 | bestanden |
| VAL-02 | `Result_file_is_written_next_to_the_sbom` | Golden File | `sbom-maschine.result.json` liegt neben der SBOM, mit `command` = `validate`, `exitCode` 0, 9 Warnungen, 32 Hinweisen, 2 Stationen | 1 | bestanden |
| VAL-03 | `Release_build_turns_warnings_into_a_finding` | Golden File, `--release-build` | Exit 1, „NICHT BESTANDEN“, `exitCode` 1 in der Ergebnisdatei | 1 | bestanden |
| VAL-04 | `Notes_are_hidden_without_verbose` | Golden File | Warnungen sichtbar, Hinweise ausgeblendet, Zeile „32 Hinweise ausgeblendet, mit --verbose anzeigen.“ | 1 | bestanden |
| VAL-05 | `Verbose_shows_the_notes` | Golden File, `--verbose` bzw. `-v` | Hinweise sichtbar, kein „ausgeblendet“ | 2 | bestanden |
| VAL-06 | `Clean_sbom_passes_the_release_build` | Minimal-SBOM, `--release-build` | Exit 0, „OK, 0 Warnungen, 1 Hinweis“ | 1 | bestanden |
| VAL-07 | `Sbom_with_errors_ends_with_exit_1_and_lists_them` | Minimal-SBOM mit Schwachstelle | Exit 1, „NICHT BESTANDEN, 1 Fehler“, Zeile `Fehler: [VULNERABILITIES_PRESENT]`, `errors` 1 | 1 | bestanden |
| VAL-08 | `Invalid_json_is_a_finding_with_result_file` | abgeschnittenes JSON | Exit 1, `Fehler: [JSON_INVALID]`, Ergebnisdatei mit `schemaValid` false | 1 | bestanden |
| VAL-09 | `Missing_file_is_a_usage_error_without_result_file` | nicht vorhandene Datei | Exit 2, `[SBOM_NOT_FOUND]` auf stderr, nichts auf stdout, keine Ergebnisdatei | 1 | bestanden |
| VAL-10 | `Same_input_gives_the_same_result_file` | Golden File, zweimal | Beide Ergebnisdateien sind byte-gleich | 1 | bestanden |

### 4.3 Validator: `SbomValidatorTests` (74)

Grundlage ist die Minimal-SBOM. Ohne Änderung ergibt sie nur den Hinweis `LOGICAL_COMPONENT`. Jeder Testfall ändert eine Stelle und prüft die erwartete Meldung.

**Grundfall und Format**

| ID | Testfall | Änderung | Erwartet | Fälle | Ergebnis |
|---|---|---|---|---:|---|
| SV-01 | `Minimal_sbom_passes_with_only_the_logical_component_note` | keine | Schema gültig, nur Hinweis `LOGICAL_COMPONENT`, Exit 0 auch im Release-Build, 2 Komponenten, 1 Station | 1 | bestanden |
| SV-02 | `Invalid_json_is_an_error` | leerer Text, abgeschnittenes Objekt, Array statt Objekt, Komma am Ende | nur Fehler `JSON_INVALID`, Exit 1 | 4 | bestanden |
| SV-03 | `Other_formats_are_not_cyclonedx` | `bomFormat` = `SPDX` | Fehler `NOT_CYCLONEDX` | 1 | bestanden |
| SV-04 | `Versions_before_1_6_do_not_meet_the_tr` | `specVersion` 1.5, 1.4, leer | Fehler `SPEC_VERSION_UNSUPPORTED` | 3 | bestanden |
| SV-05 | `Newer_version_skips_the_schema_with_a_warning_but_checks_the_rules` | `specVersion` 1.7, Zeitstempel entfernt | Warnung `SCHEMA_NOT_EMBEDDED`, TR-Regeln laufen trotzdem (Fehler `TIMESTAMP_MISSING`) | 1 | bestanden |
| SV-06 | `Newer_version_alone_fails_only_the_release_build` | `specVersion` 1.7 | Exit 0 im Entwicklungs-Build, Exit 1 im Release-Build | 1 | bestanden |
| SV-07 | `Schema_violation_is_an_error_with_its_path` | Komponententyp `hardware` | Fehler `SCHEMA_INVALID`, Pfad nennt `components` | 1 | bestanden |
| SV-08 | `Malformed_sha512_violates_the_schema` | SHA-512 = `abc` | Fehler `SCHEMA_INVALID` | 1 | bestanden |

**Ganze SBOM (TR-03183-2 Abschnitte 3.1 und 5.2.1)**

| ID | Testfall | Änderung | Erwartet | Fälle | Ergebnis |
|---|---|---|---|---:|---|
| SV-09 | `Vulnerabilities_are_forbidden` | eine Schwachstelle eingetragen | Fehler `VULNERABILITIES_PRESENT` | 1 | bestanden |
| SV-10 | `Empty_vulnerability_list_is_allowed` | leere Liste `vulnerabilities` | keine Meldung dazu | 1 | bestanden |
| SV-11 | `Timestamp_is_required` | `metadata.timestamp` entfernt | Fehler `TIMESTAMP_MISSING` | 1 | bestanden |
| SV-12 | `Sbom_creator_needs_email_or_url` | Ersteller ohne Kontakt | Fehler `SBOM_CREATOR_MISSING` | 1 | bestanden |
| SV-13 | `Sbom_creator_with_url_is_enough` | Ersteller nur mit URL | keine Meldung dazu | 1 | bestanden |
| SV-14 | `Primary_component_is_required` | `metadata.component` entfernt | Fehler `PRIMARY_COMPONENT_MISSING` | 1 | bestanden |

**Pflichtfelder je Komponente (TR-03183-2 Abschnitt 5.2.2)**

| ID | Testfall | Änderung | Erwartet | Fälle | Ergebnis |
|---|---|---|---|---:|---|
| SV-15 | `Missing_component_field_without_gap_is_an_error` | `manufacturer`, `version`, `licenses`, `externalReferences` je einzeln entfernt | Fehler `CREATOR_MISSING`, `VERSION_MISSING`, `DISTRIBUTION_LICENCE_MISSING`, `HASH_MISSING` an `app`, Text „Kein Lückenvermerk“ | 4 | bestanden |
| SV-16 | `Missing_bsi_property_without_gap_is_an_error` | `bsi:component:filename`, `executable`, `archive`, `structured` je einzeln entfernt | genau ein Fehler `FILENAME_MISSING`, `EXECUTABLE_PROPERTY_MISSING`, `ARCHIVE_PROPERTY_MISSING`, `STRUCTURED_PROPERTY_MISSING` | 4 | bestanden |
| SV-17 | `Component_creator_without_contact_is_missing` | Hersteller ohne E-Mail und URL | Fehler `CREATOR_MISSING` | 1 | bestanden |
| SV-18 | `Declared_licence_is_not_a_distribution_licence` | Lizenz mit `acknowledgement` = `declared` | Fehler `DISTRIBUTION_LICENCE_MISSING` | 1 | bestanden |
| SV-19 | `Concluded_licence_expression_is_a_distribution_licence` | SPDX-Ausdruck `MIT OR Apache-2.0` mit `concluded` | keine Meldung | 1 | bestanden |
| SV-48 | `Licence_expressions_of_spdx_identifiers_and_licence_refs_are_valid` | `LicenseRef-siemens-proprietary`, Ausdruck mit Klammern und `LicenseRef-…`, `… WITH Classpath-exception-2.0`, `mit` in Kleinbuchstaben, `DocumentRef-…:LicenseRef-…` | keine Meldung | 5 | bestanden |
| SV-49 | `Free_text_as_licence_expression_is_invalid` | `Proprietär`, `Siemens Software License`, `MIT OR Hausrecht`, `LicenseRef-` ohne Namen | Fehler `DISTRIBUTION_LICENCE_INVALID` an `app` mit Feld `distributionLicence` | 4 | bestanden |
| SV-50 | `Licence_name_counts_only_as_licence_ref` | `license.name` = `Proprietär` bzw. `LicenseRef-musterbau-proprietary` | Fehler `DISTRIBUTION_LICENCE_INVALID` nur beim Freitext; nie `DISTRIBUTION_LICENCE_MISSING` | 2 | bestanden |
| SV-20 | `Sha256_alone_is_not_the_required_hash` | nur SHA-256 statt SHA-512 | Fehler `HASH_MISSING` | 1 | bestanden |
| SV-21 | `Hash_outside_a_distribution_reference_does_not_count` | Referenztyp `website` statt `distribution` | Fehler `HASH_MISSING` | 1 | bestanden |
| SV-22 | `Bsi_property_with_wrong_value_is_invalid` | `bsi:component:executable` = `yes` | Fehler `BSI_PROPERTY_INVALID` | 1 | bestanden |
| SV-23 | `Bsi_property_twice_is_an_error` | `filename`, `archive`, `effectiveLicence` doppelt | Fehler `BSI_PROPERTY_DUPLICATE` | 3 | bestanden |
| SV-24 | `Effective_licence_with_pdf_spelling_is_a_warning` | `bsi:component:effectiveLicense` (Schreibweise des TR-PDF) | Warnung `EFFECTIVE_LICENCE_SPELLING` | 1 | bestanden |
| SV-25 | `Associated_licences_are_deprecated` | `bsi:component:associatedLicences` | Warnung `ASSOCIATED_LICENCES_DEPRECATED` | 1 | bestanden |
| SV-26 | `Nested_components_are_checked_and_counted` | Unterkomponente ohne Version | 3 Komponenten gezählt, Fehler `VERSION_MISSING` an der Unterkomponente | 1 | bestanden |

**Referenzen, Abhängigkeiten, Vollständigkeit**

| ID | Testfall | Änderung | Erwartet | Fälle | Ergebnis |
|---|---|---|---|---:|---|
| SV-27 | `Component_without_bom_ref_is_an_error` | `bom-ref` entfernt | Fehler `BOM_REF_MISSING` | 1 | bestanden |
| SV-28 | `Duplicate_bom_ref_is_an_error` | `app` bekommt die `bom-ref` der Maschine | Fehler `BOM_REF_DUPLICATE` | 1 | bestanden |
| SV-29 | `Dependency_on_an_unknown_ref_is_an_error` | Abhängigkeit auf `gibt-es-nicht` | Fehler `DEPENDENCY_REF_UNKNOWN` mit dem Namen | 1 | bestanden |
| SV-30 | `Every_component_needs_a_dependency_entry` | Eintrag von `app` in `dependencies` entfernt | Fehler `DEPENDENCIES_MISSING` an `app` | 1 | bestanden |
| SV-31 | `Every_component_needs_a_completeness` | `app` fehlt in `compositions` | Fehler `COMPLETENESS_MISSING` an `app` | 1 | bestanden |
| SV-32 | `Composition_without_aggregate_does_not_state_completeness` | `aggregate` entfernt | zwei Fehler `COMPLETENESS_MISSING` | 1 | bestanden |
| SV-51 | `Component_not_reachable_from_the_primary_component_is_an_error` | Maschine hängt von nichts ab | Fehler `COMPONENT_UNREACHABLE` an `app` mit Feld `dependencyGraph` | 1 | bestanden |
| SV-52 | `Component_reachable_over_another_component_is_fine` | zusätzliche Bibliothek, erreichbar über `app` | keine Meldung | 1 | bestanden |

**Lückenvermerke**

| ID | Testfall | Änderung | Erwartet | Fälle | Ergebnis |
|---|---|---|---|---:|---|
| SV-33 | `Note_gap_excuses_the_missing_hash` | Hash entfernt, `plcsbom:gap` = `hash-unavailable` | nur Hinweis `HASH_UNAVAILABLE` mit Feld `hash`, Exit 0 auch im Release-Build | 1 | bestanden |
| SV-34 | `Warning_gap_excuses_the_missing_version_but_fails_the_release_build` | Version entfernt, `version-unresolved` | nur Warnung `VERSION_UNRESOLVED`, Exit 0 bzw. 1 im Release-Build | 1 | bestanden |
| SV-35 | `One_gap_can_excuse_several_fields` | drei Dateieigenschaften entfernt, `file-properties-unknown` | nur Warnung `FILE_PROPERTIES_UNKNOWN` mit den Feldern `executable`, `archive`, `structured` | 1 | bestanden |
| SV-36 | `Gap_for_another_field_does_not_excuse` | Hash entfernt, `vendor-sbom-missing` | Fehler `HASH_MISSING` bleibt, dazu Hinweis `VENDOR_SBOM_MISSING` | 1 | bestanden |
| SV-37 | `Gap_in_the_users_own_namespace_counts` | `firma:gap` = `hash-unavailable` | nur Hinweis `HASH_UNAVAILABLE` | 1 | bestanden |
| SV-38 | `Gap_in_a_reserved_namespace_does_not_count` | `cdx:gap` bzw. `bsi:gap` | Fehler `HASH_MISSING` bleibt | 2 | bestanden |
| SV-39 | `Unknown_gap_code_is_a_warning` | Lückencode `firmware-geraten` | genau eine Warnung `GAP_UNKNOWN` | 1 | bestanden |
| SV-40 | `Same_gap_twice_is_reported_once` | `vendor-sbom-missing` doppelt | eine Meldung | 1 | bestanden |

**Logische Komponenten (TR-03183-2 Abschnitt 3.2.2)**

| ID | Testfall | Änderung | Erwartet | Fälle | Ergebnis |
|---|---|---|---|---:|---|
| SV-41 | `Logical_component_needs_no_file_name_hash_or_file_properties` | keine | Hinweis `LOGICAL_COMPONENT` an `maschine` mit den Feldern Dateiname, Hash, ausführbar, Archiv, strukturiert | 1 | bestanden |
| SV-42 | `Logical_component_still_needs_a_version` | Version der Maschine entfernt | Fehler `VERSION_MISSING` an `maschine` | 1 | bestanden |
| SV-43 | `Logical_flag_must_be_true` | `logicalComponent` = `false` | kein Hinweis, stattdessen Fehler `HASH_MISSING` und `FILENAME_MISSING` | 1 | bestanden |

**Reihenfolge, Zählung, Golden File**

| ID | Testfall | Änderung | Erwartet | Fälle | Ergebnis |
|---|---|---|---|---:|---|
| SV-44 | `Findings_are_sorted_errors_first_then_by_component_and_code` | ein Fehler, eine Warnung, ein Hinweis | Fehler zuerst, absteigend nach Schwere | 1 | bestanden |
| SV-45 | `Validation_is_deterministic` | zwei fehlende Felder, zwei Läufe | gleiche Meldungen in gleicher Reihenfolge | 1 | bestanden |
| SV-46 | `Stations_are_counted_from_the_own_namespace_only` | `cdx:station` und `firma:station` ergänzt | 2 Stationen (`cdx:` zählt nicht) | 1 | bestanden |
| SV-47 | `Hand_model_golden_file_has_no_errors` | Golden File | Schema gültig, 0 Fehler, 11 Komponenten | 1 | bestanden |

### 4.4 Lückenregeln und Katalog: `GapRulesTests` (28)

Die Lückenregeln setzen die Lücken, die aus dem Modell allein folgen, unabhängig vom Adapter.

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| GR-01 | `Projected_firmware_gets_the_gaps_vendor_firmware_always_has` | Projektierte Firmware bekommt `firmware-projected-only`, `hash-unavailable`, `filename-unavailable`, `vendor-sbom-missing`, in dieser Reihenfolge | 1 | bestanden |
| GR-02 | `As_found_firmware_is_not_projected_only` | Im Feld gelesene Firmware bekommt kein `firmware-projected-only` | 1 | bestanden |
| GR-03 | `Device_without_firmware_gets_firmware_not_in_project` | Gerät ohne Firmware: `firmware-not-in-project`, nicht `firmware-projected-only` | 1 | bestanden |
| GR-04 | `Passive_device_gets_no_gaps` | Passives Modul (Profilschiene) bekommt keine Lücken | 1 | bestanden |
| GR-05 | `Unknown_vendor_is_supplier_unknown` | Ohne Hersteller: `supplier-unknown` | 1 | bestanden |
| GR-06 | `Vendor_without_contact_is_creator_contact_unknown` | Hersteller ohne E-Mail und URL: `creator-contact-unknown`, nicht `supplier-unknown` | 1 | bestanden |
| GR-07 | `Firmware_without_licence_is_licence_unknown` | Firmware ohne Lizenz: `license-unknown` | 1 | bestanden |
| GR-08 | `Fully_described_library_gets_no_gaps` | Vollständig beschriebene Bibliothek bekommt keine Lücke | 1 | bestanden |
| GR-09 | `Placeholder_version_is_version_unresolved` | Version `*` oder keine Version: `version-unresolved` | 1 | bestanden |
| GR-10 | `Runtime_without_version_is_runtime_version_unknown` | Laufzeit ohne Version: `runtime-version-unknown`, nicht `version-unresolved` | 1 | bestanden |
| GR-11 | `Unknown_file_traits_are_file_properties_unknown` | Unbekannte Dateieigenschaften: `file-properties-unknown` | 1 | bestanden |
| GR-12 | `Missing_file_name_is_filename_unavailable` | Ohne Dateinamen: `filename-unavailable` | 1 | bestanden |
| GR-13 | `Library_without_hash_is_hash_unavailable` | Bibliothek ohne Hash: `hash-unavailable`, nicht `program-source-missing` | 1 | bestanden |
| GR-14 | `Program_without_source_directory_is_program_source_missing` | Anwenderprogramm ohne Quellordner: `program-source-missing`, nicht `hash-unavailable` | 1 | bestanden |
| GR-15 | `Program_with_source_hash_is_only_hash_unavailable` | Anwenderprogramm mit Quell-Hash, ohne Hash der ausgelieferten Form: nur `hash-unavailable` | 1 | bestanden |
| GR-16 | `Machine_without_licence_is_licence_unknown` | Maschine ohne Lizenz: `license-unknown` an der Maschine | 1 | bestanden |
| GR-17 | `Unassigned_device_is_device_unassigned_unless_passive` | Nicht zugeordnetes Gerät: `device-unassigned`; passives Modul ohne Lücke | 1 | bestanden |
| GR-18 | `Existing_gaps_are_kept_and_not_duplicated` | Lücken vom Adapter bleiben mit ihrem Text erhalten, keine Doppelung | 1 | bestanden |
| GR-19 | `Applying_twice_changes_nothing` | Zweimal angewendet auf das Handmodell: gleiches Ergebnis | 1 | bestanden |
| GR-20 | `Catalog_codes_are_unique_kebab_case` | Lückencodes sind eindeutig und klein mit Bindestrich geschrieben | 1 | bestanden |
| GR-21 | `No_catalog_gap_is_an_error` | Keine Lücke hat die Schwere „Fehler“ (CLAUDE.md 4.4) | 1 | bestanden |
| GR-22 | `Every_catalog_gap_has_a_german_description` | Jede Lücke hat einen Beschreibungssatz | 1 | bestanden |
| GR-23 | `Severities_follow_section_4_4` | Schwere nach CLAUDE.md 4.4: `hash-unavailable`, `firmware-not-in-project`, `device-unassigned` Hinweis; `version-unresolved`, `libraries-not-read` Warnung | 5 | bestanden |
| GR-24 | `Unknown_code_is_not_found` | Unbekannter Code wird im Katalog nicht gefunden | 1 | bestanden |

### 4.5 CycloneDX-Writer: `CycloneDxWriterTests` (21)

Eingabe ist das Handmodell nach Anwendung der Lückenregeln, Zeitstempel fest auf 31.10.2026 12:00 UTC.

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| W-01 | `Hand_model_matches_the_golden_file` | Ausgabe ist byte-gleich mit `golden/hand-model.cdx.json` | 1 | bestanden |
| W-02 | `Output_is_byte_identical_across_runs` | Zwei Läufe ergeben dieselben Bytes (Hypothese H3) | 1 | bestanden |
| W-03 | `Output_is_utf8_without_bom_with_lf_only` | UTF-8 ohne BOM, nur LF, Zeilenende am Schluss | 1 | bestanden |
| W-04 | `Hand_model_passes_schema_and_tr_rules_without_errors` | Eigener Validator: Schema gültig, keine Fehler, Exit 0 bzw. 1 im Release-Build, 2 Stationen | 1 | bestanden |
| W-05 | `Only_software_components_are_written` | Nur `application`, `firmware`, `library`; die Maschine ist `device` (Variante B) | 1 | bestanden |
| W-06 | `Passive_devices_and_racks_are_not_components` | Profilschiene erscheint nicht als Komponente | 1 | bestanden |
| W-07 | `Controller_firmware_carries_device_properties_and_projected_origin` | CPU-Firmware: Name, Version V2.8, Bestellnummer, Herkunft `assumed`, `projected`, Station, Adapter, Lücke, BSI-Eigenschaften | 1 | bestanden |
| W-08 | `Firmware_missing_in_the_project_gets_no_version_but_a_gap` | PM 70W: keine Version, Lücke `firmware-not-in-project`, Herkunft `not-in-project` | 1 | bestanden |
| W-09 | `Placeholder_version_is_not_written_as_version` | Tc2_Standard: keine Version, `version:requested` = `*`, Lücke `version-unresolved` | 1 | bestanden |
| W-10 | `Program_depends_on_controller_firmware_and_libraries` | `plc-1-app` hängt von `plc-1-fw` und `plc-1-lib-lgf` ab | 1 | bestanden |
| W-11 | `Every_component_has_a_dependency_entry_and_a_completeness` | Jede Komponente hat einen Eintrag in `dependencies` und in `compositions` | 1 | bestanden |
| W-12 | `Program_without_blocks_is_marked_incomplete` | `plc-1-app` steht in `compositions` als `incomplete` | 1 | bestanden |
| W-13 | `Serial_number_is_stable_per_machine_release_and_commit` | `serialNumber` ist eine `urn:uuid:` und ändert sich mit dem Release | 1 | bestanden |
| W-14 | `Namespace_comes_from_the_options` | Namespace `firma` ersetzt `plcsbom` vollständig | 1 | bestanden |
| W-15 | `Reserved_namespaces_are_refused` | Namespace `cdx`, `bsi` oder leer wird abgelehnt | 3 | bestanden |
| W-16 | `A_hash_without_uri_is_a_model_error` | Hash ohne zugehörige URI ist ein Modellfehler | 1 | bestanden |
| W-17 | `Distribution_and_original_licence_are_written_as_two_licences` | Distributionslizenz als `concluded`, Originallizenz als `declared` | 1 | bestanden |
| W-18 | `Network_data_never_reaches_the_sbom` | Keine IP-Adresse in der Ausgabe (Leitplanke Netzdaten) | 1 | bestanden |
| W-19 | `Station_gap_without_program_and_controller_goes_to_the_machine` | Stationslücke einer Station ohne Anwenderprogramm und Steuerung (TwinCAT vor M9) steht an der Maschine, an keiner Komponente; schlug vor dem Fix in Lauf 14 fehl | 1 | bestanden |

### 4.6 Ergebnisdatei: `ResultFileTests` (8)

Eingabe ist ein fester Prüfbericht mit einem Fehler, zwei Warnungen und zwei Hinweisen.

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| RF-01 | `Counts_and_status_are_written` | `format` 1, `command`, `exitCode`, `schemaValid`, Anzahl Fehler, Warnungen, Hinweise, Komponenten, Stationen | 1 | bestanden |
| RF-02 | `Field_order_is_fixed` | Feste Reihenfolge der Felder (Schnittstelle) | 1 | bestanden |
| RF-03 | `Gaps_are_counted_by_gap_code_without_other_findings` | `gaps` zählt je Lückencode, andere Meldungen nicht | 1 | bestanden |
| RF-04 | `Findings_carry_severity_code_component_fields_and_message` | Jede Meldung mit Schwere, Code, Komponente, Feldern, Text | 1 | bestanden |
| RF-05 | `Finding_without_fields_omits_the_field_list` | Meldung ohne Felder hat kein `fields` | 1 | bestanden |
| RF-06 | `Windows_paths_are_written_with_slashes` | `build\sbom…` wird als `build/sbom…` geschrieben | 1 | bestanden |
| RF-07 | `Output_is_utf8_with_lf_readable_umlauts_and_trailing_newline` | UTF-8 ohne BOM, LF, Umlaute und „“ unverändert lesbar | 1 | bestanden |
| RF-08 | `Output_is_byte_identical_across_runs` | Zwei Läufe ergeben dieselben Bytes | 1 | bestanden |

### 4.7 Kennungen: `IdentifierTests` (11)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| ID-01 | `Uuid5_matches_the_rfc_9562_example` | UUIDv5 stimmt mit dem Beispiel aus RFC 9562 überein | 1 | bestanden |
| ID-02 | `Slug_is_lower_case_ascii` | Teile der `bom-ref`: `PLC_1` → `plc-1`, `Förderband Süd` → `foerderband-sued`, `0/1` → `0-1`, `Tc2_Standard` → `tc2-standard`, `Roboterzelle` → `roboterzelle`, `___` → `x` | 6 | bestanden |
| ID-03 | `Registry_makes_refs_unique_in_call_order` | Doppelte `bom-ref` werden in Aufrufreihenfolge eindeutig gemacht | 1 | bestanden |
| ID-04 | `Result_file_sits_next_to_the_sbom` | `sbom-maschine.cdx.json` → `sbom-maschine.result.json`, `x.json` → `x.result.json`, `x.sbom` → `x.sbom.result.json` | 3 | bestanden |

### 4.8 Doku-Abgleich: `DocsConsistencyTests` (47)

Diese Tests sichern die Regel aus CLAUDE.md Abschnitt 14 ab, soweit sie sich maschinell prüfen lässt. Fehlt nach einer Änderung im Code ein Lückencode in der Doku, schlägt der Build fehl.

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| DOC-01 | `Anleitung_lists_every_gap_code_with_its_severity` | Jeder der 22 Lückencodes steht genau einmal in der Tabelle von ANLEITUNG.md, mit derselben Schwere wie im Code | 22 | bestanden |
| DOC-02 | `Claude_md_lists_every_gap_code` | Jeder der 22 Lückencodes steht in einer Tabelle von CLAUDE.md | 22 | bestanden |
| DOC-03 | `Anleitung_lists_no_gap_code_the_tool_does_not_know` | ANLEITUNG.md nennt keinen Lückencode, den das Werkzeug nicht kennt | 1 | bestanden |
| DOC-04 | `User_docs_state_the_tool_version` | ANLEITUNG.md und README.md nennen die aktuelle Version (0.1.0) | 2 | bestanden |

### 4.9 Beispieldateien: `SampleFileTests` (1)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| SF-01 | `Minimal_sbom_matches_its_golden_file` | Die Minimal-SBOM aus `MinimalSbom.cs` ist byte-gleich mit `golden/minimal-synthetisch.cdx.json` | 1 | bestanden |

### 4.10 CODESYS-Bibliotheksangabe: `LibraryReferenceTests` (14)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| LR-01 | `Parses_title_version_and_company` | Sechs Angaben aus den Fixtures und eine mit Komma im Titel werden in Titel, Version und Hersteller zerlegt und unverändert wieder zusammengesetzt | 7 | bestanden |
| LR-02 | `Rejects_text_that_does_not_follow_the_pattern` | `null`, leer, `Kaputt`, `#SM3_Basic`, ohne Hersteller, ohne Version ergeben keine Angabe | 6 | bestanden |
| LR-03 | `Star_means_newest_installed_version` | `*` gilt als „neueste installierte Version“ | 1 | bestanden |

### 4.11 PLCopen-XML-Leser: `PlcOpenReaderTests` (15)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| PO-01 | `Reads_file_and_content_header` | Produkt, Produktversion, Exportzeit, Projektname und -version aus robin, OSC-MQTT, ooip; `companyName` ist überall leer | 3 | bestanden |
| PO-02 | `Reads_the_device_identification` | Gerätename, `Type`, `Id`, `Version` und Anwendung aus robin und ooip | 2 | bestanden |
| PO-03 | `Configuration_without_device_data_is_no_device` | Der Achspool in robin ist kein Gerät | 1 | bestanden |
| PO-04 | `Library_project_has_no_device_and_libraries_in_the_pou_pool` | OSC-MQTT: kein Gerät, 11 feste Bibliotheken im POU-Pool, eine mit `*` | 1 | bestanden |
| PO-05 | `Placeholder_without_redirection_keeps_the_default_resolution` | robin: 10 Bibliotheken, `#SM3_Basic` mit Auflösung `default` | 1 | bestanden |
| PO-06 | `Direct_reference_is_fixed` | `Robin, 0.1.2 (INESC TEC)` ist fest aufgelöst | 1 | bestanden |
| PO-07 | `Redirection_in_the_same_library_manager_wins_over_the_default` | ooip: `#Standard` mit Standard `*` und Umleitung auf 3.5.18.0 | 1 | bestanden |
| PO-08 | `Two_controllers_with_nested_device_and_pou_pool` | synthetisch: zwei Steuerungen, untergeordnetes Gerät, zwei Anwendungen, POU-Pool | 1 | bestanden |
| PO-09 | `Not_plcopen_ends_with_exit_2` | AML statt PLCopen: `NOT_PLCOPEN`, Exit 2 | 1 | bestanden |
| PO-10 | `Invalid_xml_ends_with_exit_2` | abgeschnittenes XML und Text: `XML_INVALID` | 2 | bestanden |
| PO-11 | `Dtd_is_refused` | Datei mit DTD und externer Entität wird abgelehnt, nichts wird aufgelöst | 1 | bestanden |

### 4.12 Adapter `codesys`, Quelle `v35`: `CodesysV35AdapterTests` (28)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| CV-01 | `Station_carries_adapter_source_hash_and_engineering_tool` | Adapter `codesys`, Quelle `v35`, SHA-256 der Datei, Werkzeug „CODESYS V3.5 SP15“, kein Exportzeitpunkt (ohne Zeitzone) | 1 | bestanden |
| CV-02 | `Device_description_version_is_not_the_firmware` | Gerät ohne Firmware, `firmware:origin` unknown, Version der Gerätebeschreibung als Herstellerschlüssel, drei Lücken | 1 | bestanden |
| CV-03 | `Placeholder_with_default_resolution_only_is_unresolved` | `#SM3_Basic`: Version nicht aufgelöst, `version-unresolved` mit Grund | 1 | bestanden |
| CV-04 | `Direct_reference_has_a_resolved_version` | `Robin 0.1.2` aufgelöst, ohne Adapterlücke | 1 | bestanden |
| CV-05 | `Redirected_placeholder_has_a_resolved_version` | ooip: `Standard 3.5.18.0` aufgelöst | 1 | bestanden |
| CV-06 | `Star_version_is_unresolved` | `*` ergibt `version-unresolved` | 1 | bestanden |
| CV-07 | `Company_system_is_no_vendor` | Hersteller `System` wird nicht übernommen, `supplier-unknown` | 1 | bestanden |
| CV-08 | `Library_project_gives_a_station_without_devices` | OSC-MQTT: Station ohne Geräte, 11 Bibliotheken, 2 mit `*` | 1 | bestanden |
| CV-09 | `Several_controllers_need_plc` | Zwei Steuerungen ohne `plc`: `CODESYS_PLC_REQUIRED` mit Namen | 1 | bestanden |
| CV-10 | `Unknown_plc_lists_the_devices_found` | Unbekanntes `plc`: `CODESYS_PLC_NOT_FOUND` mit Liste | 1 | bestanden |
| CV-11 | `Plc_selects_the_controller_its_sub_devices_and_libraries` | Steuerung, untergeordnetes Gerät mit Position, Bibliotheken aus POU-Pool und beiden Anwendungen ohne Dubletten | 1 | bestanden |
| CV-12 | `Other_controller_gets_only_its_own_libraries` | Zweite Steuerung erhält nur POU-Pool und eigene Anwendung | 1 | bestanden |
| CV-13 | `Reference_off_pattern_is_kept_with_a_gap` | `Kaputt` bleibt als Bibliothek ohne Version und Hersteller, mit Lücke | 1 | bestanden |
| CV-14 | `Missing_file_ends_with_exit_2` | fehlende Datei: `SOURCE_NOT_FOUND` | 1 | bestanden |
| CV-15 | `Sbom_matches_the_golden_file` | SBOM aus robin und aus „Verpackung“ byte-gleich mit den Golden Files | 2 | bestanden |
| CV-16 | `Sbom_passes_schema_and_tr_rules_without_errors` | SBOM aus allen vier Fixtures (synthetisch mit beiden Steuerungen): Schema gültig, kein Fehler, zwei Läufe byte-gleich | 5 | bestanden |
| CV-17 | `Sbom_writes_codesys_properties` | `adapter:source`, `firmware:origin` unknown, `vendorKey:DeviceDescriptionVersion`, `library:resolution`; Version der Gerätebeschreibung nie als Version | 1 | bestanden |
| CV-18 | `Unique_device_description_gives_name_and_vendor` | `I2C_Mux` → `TCA9548`, Hersteller „Stefan Dreyer“ mit Herkunft `devdesc`, Datei und SHA-256 als Herstellerschlüssel, keine Bestellnummer (`-`) | 1 | bestanden |
| CV-19 | `Ambiguous_device_description_is_not_used` | `Expander` passt auf `PCF8575IN` und `SSD1306`: nichts übernommen, `devdesc-not-found` nennt beide Dateien | 1 | bestanden |
| CV-20 | `Missing_device_description_is_a_gap` | `Lichtsensor` (andere Version) und die Steuerung: `devdesc-not-found` und die Lücken wie ohne Gerätebeschreibung | 2 | bestanden |
| CV-21 | `Without_configured_descriptions_there_is_no_devdesc_gap` | ohne `deviceDescriptions` keine `devdesc-not-found` | 1 | bestanden |
| CV-22 | `Sbom_with_device_descriptions_matches_the_golden_file_and_passes` | SBOM byte-gleich mit `golden/codesys-v35-devdesc.cdx.json`, Schema gültig, kein Fehler, `vendor:origin` = `devdesc` | 1 | bestanden |

### 4.14 CODESYS-Gerätebeschreibungen: `DeviceDescriptionIndexTests` (12)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| DD-01 | `Reads_identification_name_and_vendor` | Kennung, Name, Hersteller aus allen vier Fixtures; `-` als Bestellnummer ergibt keine | 4 | bestanden |
| DD-02 | `Load_reads_all_files_sorted_with_repository_paths` | alle Dateien sortiert, Pfade relativ zum Repository | 1 | bestanden |
| DD-03 | `Unique_identification_is_found_case_insensitive_in_the_id` | `ffff 0001` findet `TCA9548` | 1 | bestanden |
| DD-04 | `Shared_identification_is_ambiguous` | `500/FFFF 0007/1.0.1.1` ist mehrdeutig (`PCF8575IN`, `SSD1306`) | 1 | bestanden |
| DD-05 | `Other_identification_is_not_found` | andere Version, andere Kennung, fehlender Type | 3 | bestanden |
| DD-06 | `Missing_directory_ends_with_exit_2` | `DEVDESC_DIR_NOT_FOUND` | 1 | bestanden |
| DD-07 | `Other_xml_is_no_device_description` | `NOT_DEVDESC` | 1 | bestanden |

### 4.13 Befehl `inspect`: `InspectCommandTests` (16)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| IN-01 | `Codesys_export_gives_the_raw_inventory_as_json` | robin: Exit 0, JSON mit Adapter, Quelle, SHA-256, Gerät und Auflösung | 1 | bestanden |
| IN-02 | `Inventory_matches_the_golden_file` | Rohinventar der synthetischen Datei byte-gleich mit dem Golden File | 1 | bestanden |
| IN-03 | `Missing_file_ends_with_exit_2` | `[SOURCE_NOT_FOUND]`, Exit 2 | 1 | bestanden |
| IN-04 | `Non_plcopen_file_ends_with_exit_2` | `[NOT_PLCOPEN]`, Exit 2 | 1 | bestanden |
| IN-05 | `Gsdml_is_refused_for_codesys` | `--gsdml` mit `codesys`: `[USAGE]`, Exit 2 | 1 | bestanden |
| IN-06 | `Studio5000_inventory_matches_the_golden_file` | Rohinventar der synthetischen L5X-Datei byte-gleich mit dem Golden File, keine IP-Adresse in der Ausgabe | 1 | bestanden |
| IN-07 | `Studio5000_component_export_can_be_inspected` | AOI-Export (WetBulbTemperature): Exit 0, `targetType` `AddOnInstructionDefinition` | 1 | bestanden |
| IN-08 | `Non_l5x_file_ends_with_exit_2` | PLCopen-Datei mit `--adapter studio5000`: `[NOT_L5X]`, Exit 2 | 1 | bestanden |
| IN-09 | `Twincat_inventory_matches_the_golden_file` | Rohinventar von SPT EtherCAT Diagnostics byte-gleich mit dem Golden File, keine AMS-Net-ID in der Ausgabe | 1 | bestanden |
| IN-10 | `Non_tsproj_file_ends_with_exit_2` | PLCopen-Datei mit `--adapter twincat`: `[NOT_TSPROJ]`, Exit 2 | 1 | bestanden |
| IN-11 | `Tia_aml_inventory_matches_the_golden_file` | synthetische AML-Datei mit zwei CPUs: Exit 0, Rohinventar byte-gleich mit dem Golden File, keine IP-Adresse, Warnung `[TIA_AML_CATALOG_VERSION_DIFFERS]` auf stderr | 1 | bestanden |
| IN-12 | `Tia_aml_inventory_shows_cpus_and_assignment` | V17: TIA-Version, CPU `PLC_1`, GSD-Gerät ohne Zuordnung mit Behandlung `gsdDevice`, stderr leer | 1 | bestanden |
| IN-13 | `Non_aml_file_ends_with_exit_2` | PLCopen-Datei mit `--adapter tia-aml`: `[NOT_AML]`, Exit 2 | 1 | bestanden |
| IN-14 | `Gsdml_adds_vendor_and_order_number_to_gsd_items` | V17 mit `--gsdml fixtures/gsdml`: Kopfmodul mit Hersteller, `501xxxxx`, `V1.*.*` und Dateipfad, Module mit `gsdml`, Struktur ohne, stderr leer | 1 | bestanden |
| IN-15 | `Gsdml_not_found_is_a_warning_on_stderr` | Verzeichnis ohne die Datei: Exit 0, `[GSDML_NOT_FOUND]` auf stderr und im JSON | 1 | bestanden |
| IN-16 | `Missing_gsdml_directory_ends_with_exit_2` | fehlendes Verzeichnis: `[GSDML_DIR_NOT_FOUND]`, Exit 2 | 1 | bestanden |

### 4.15 L5X-Leser: `L5xReaderTests` (10)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| LX-01 | `Controller_export_header_and_controller` | ra-L85E: `SoftwareRevision` 36.00, `TargetType` Controller, Exportdatum, Controller 1756-L85E 36.11, keine AOI | 1 | bestanden |
| LX-02 | `Controller_module_sits_in_slot_0_of_its_own_backplane` | Modul `Local` mit allen Attributen, `EKey` Disabled, Steckplatz 0 über den Port mit `Id = ParentModPortId` | 1 | bestanden |
| LX-03 | `Module_in_the_chassis_has_its_slot_and_keying` | acd: 1756-EN2T 11.1 unter `Local`, `CompatibleModule`, Upstream-Port `ICP`, Steckplatz 1 | 1 | bestanden |
| LX-04 | `Module_behind_ethernet_has_no_slot` | synthetisch: Upstream-Port Ethernet, kein Steckplatz, Adresse wird nicht gelesen | 1 | bestanden |
| LX-05 | `Aoi_with_revision_extension_and_vendor` | acd: AOI 1.7, `ExtendedText`, `ACD-TOOLS` | 1 | bestanden |
| LX-06 | `Aoi_export_has_a_context_controller` | beide AOI-Exporte: Controller mit `Use` Context, keine Module, Ziel-AOI mit Revision und Hersteller bzw. ohne | 2 | bestanden |
| LX-07 | `Redundancy_flag_is_read` | acd: `RedundancyInfo Enabled="true"` | 1 | bestanden |
| LX-08 | `Other_xml_is_not_an_l5x` | PLCopen-Datei: `NOT_L5X` | 1 | bestanden |
| LX-09 | `Dtd_is_refused` | Datei mit DTD: `XML_INVALID` | 1 | bestanden |

### 4.16 Adapter `studio5000`: `Studio5000AdapterTests` (23)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| S5-01 | `Station_carries_adapter_hash_and_engineering_tool` | Adapter `studio5000`, SHA-256 der Datei, Werkzeug „Studio 5000 Logix Designer 36.00“, kein Exportzeitpunkt (ohne Zeitzone) | 1 | bestanden |
| S5-02 | `Controller_firmware_is_the_project_revision` | Steuerung 1756-L85E, Position `Local/0`, Firmware 36.11 projected, Hersteller Rockwell Automation (`assumed`), Herstellerschlüssel, `device:ekey` Disabled ohne Lücke | 1 | bestanden |
| S5-03 | `Module_in_the_chassis_has_slot_revision_and_keying` | 1756-EN2T: Position `Local/1`, Firmware 11.1 projected, `CompatibleModule`, keine Adapterlücke | 1 | bestanden |
| S5-04 | `Controller_comes_first_then_its_modules_depth_first` | Reihenfolge und Positionen im Modulbaum, auch hinter Ethernet | 1 | bestanden |
| S5-05 | `Disabled_keying_on_a_module_is_a_warning_gap` | E/A-Modul mit `Disabled`: `ekey-disabled` (Warnung) mit der projektierten Revision in der Meldung | 1 | bestanden |
| S5-06 | `Other_vendor_id_gives_no_vendor_name` | Kennung 65000: kein Hersteller, `supplier-unknown` mit Kennung, Firmware trotzdem gelesen | 1 | bestanden |
| S5-07 | `Module_without_revision_has_no_firmware` | ohne `Major`: keine Firmware, `not-in-project` | 1 | bestanden |
| S5-08 | `Aois_become_add_on_instructions_with_revision` | AOI als `AddOnInstruction`, Version 1.7, Hersteller aus der Quelle, Revisionszusatz als Property | 1 | bestanden |
| S5-09 | `Aoi_without_vendor_gets_supplier_unknown` | AOI ohne `Vendor` nach den Lückenregeln: `supplier-unknown` | 1 | bestanden |
| S5-10 | `Component_export_is_refused` | beide AOI-Exporte: `STUDIO5000_NOT_CONTROLLER` | 2 | bestanden |
| S5-11 | `Missing_file_ends_with_exit_2` | fehlende Datei: `SOURCE_NOT_FOUND` | 1 | bestanden |
| S5-12 | `Sbom_matches_the_golden_file` | SBOM aus ra-L85E, acd und der synthetischen Datei byte-gleich mit den Golden Files | 3 | bestanden |
| S5-13 | `Sbom_passes_schema_and_tr_rules_without_errors` | SBOM aus allen vier Controller-Exporten: Schema gültig, kein Fehler, zwei Läufe byte-gleich | 4 | bestanden |
| S5-14 | `Sbom_contains_no_network_addresses` | IP-Adressen aus Ports und `CommPath` stehen in der Quelle, nicht in der SBOM | 3 | bestanden |
| S5-15 | `Sbom_writes_rockwell_properties` | `device:ekey`, `vendor:origin` assumed, `vendorKey:ProductCode`, `aoi:revisionExtension`, bom-ref `…-aoi-…`, Lücke `ekey-disabled` | 1 | bestanden |

### 4.17 TwinCAT-Leser: `TwinCatReaderTests` (10)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| TR-01 | `Ethercat_tree_with_identity_and_disabled_master` | AixOCAT: TcVersion, deaktivierter Master, EK1100 mit neun Klemmen, Kennung und Beschreibung | 1 | bestanden |
| TR-02 | `Plc_project_with_placeholders_is_read` | AixOCAT: SPS-Projekt mit Pfad und Programmversion, 14 Platzhalter, `system_visuinputs` vollständig | 1 | bestanden |
| TR-03 | `Plc_project_via_independent_file_in_config_folder` | SPT: SPS-Projekt über `_Config/PLC/*.xti`, 16 Bibliotheken, EL9011 ohne `RevisionNo` | 1 | bestanden |
| TR-04 | `Profinet_boxes_have_no_ethercat_identity` | TcOpen Cognex: PROFINET-Boxen ohne EtherCAT-Kennung | 1 | bestanden |
| TR-05 | `Direct_references_resolutions_and_several_plc_projects` | TcOpen Core: drei SPS-Projekte, `LibraryReference`, Auflösung zu Platzhalter und ohne Platzhalter | 1 | bestanden |
| TR-06 | `Device_in_own_file_is_kept_as_reference` | synthetisch: `Device@File` ohne Inhalt | 1 | bestanden |
| TR-07 | `Other_xml_is_not_a_tsproj` | PLCopen-Datei: `NOT_TSPROJ` | 1 | bestanden |
| TR-08 | `Missing_plc_project_ends_with_exit_2` | fehlende `.plcproj`: `TWINCAT_FILE_NOT_FOUND` mit Pfad | 1 | bestanden |
| TR-09 | `Missing_independent_plc_file_ends_with_exit_2` | fehlende `_Config/PLC/*.xti`: `TWINCAT_FILE_NOT_FOUND` mit Pfad | 1 | bestanden |
| TR-10 | `Dtd_is_refused` | Datei mit DTD: `XML_INVALID` | 1 | bestanden |

### 4.18 Adapter `twincat`: `TwinCatAdapterTests` (37)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| TC-01 | `Station_carries_adapter_hash_and_xae_version` | Adapter `twincat`, SHA-256, Werkzeug „TwinCAT XAE 3.1.4022.4“, keine Stationslücke | 1 | bestanden |
| TC-02 | `Terminals_have_ethercat_identity_and_no_firmware` | EL3204-0200: Typname, keine Bestellnummer, keine Firmware, Position `Device 1 (EtherCAT)/1/3`, Kennung in Hex, zwei Lücken mit RevisionNo und CoE 0x100A in der Meldung | 1 | bestanden |
| TC-03 | `Vendor_id_2_is_beckhoff_assumed` | Hersteller Beckhoff mit Herkunft `assumed` | 1 | bestanden |
| TC-04 | `Disabled_master_marks_its_terminals` | deaktivierter Master: alle Teilnehmer mit `device:disabled`; aktiver Master ohne | 1 | bestanden |
| TC-05 | `Other_vendor_id_gives_no_vendor_name` | IndraDrive mit `0x00000024`: kein Hersteller, `supplier-unknown` mit Kennung | 1 | bestanden |
| TC-06 | `Terminal_without_revision_has_no_revision_key` | EL9011: kein `RevisionNo`-Schlüssel, Meldung ohne Revision | 1 | bestanden |
| TC-07 | `Runtime_is_assumed_and_has_no_version` | „TwinCAT 3 Runtime“ ohne Version, Herkunft `assumed`, `runtime-version-unknown` nennt XAE | 1 | bestanden |
| TC-08 | `Star_placeholder_is_unresolved` | `Tc2_Standard, *`: nicht aufgelöst, Auflösung `default` | 1 | bestanden |
| TC-09 | `Default_resolution_with_version_is_unresolved` | `VisuElems 3.5.6.10` nur als Standardauflösung: nicht aufgelöst | 1 | bestanden |
| TC-10 | `Company_system_is_no_vendor_in_any_case` | `System` und `system` sind kein Hersteller | 2 | bestanden |
| TC-11 | `Placeholder_resolution_with_version_is_resolved` | TcoCore 0.6.0.913 über `PlaceholderResolution`: aufgelöst, Auflösung `redirection` | 1 | bestanden |
| TC-12 | `Direct_reference_newest_and_star_placeholder_are_one_component` | `Tc2_EtherCAT` als `newest` und `*`: eine Komponente | 1 | bestanden |
| TC-13 | `Resolution_without_reference_is_an_indirect_library` | `Tc2_Utilities 3.3.47.0`: Auflösung `indirect` | 1 | bestanden |
| TC-14 | `Direct_reference_with_version_is_resolved` | `LibraryReference` mit Version: aufgelöst, Hersteller aus `Include` | 1 | bestanden |
| TC-15 | `Libraries_of_all_plc_projects_appear_once` | drei SPS-Projekte, jede Bibliothek einmal | 1 | bestanden |
| TC-16 | `Profinet_devices_and_external_io_are_a_station_gap` | PROFINET-Geräte und `Device@File`: `devices-not-read` (Warnung) mit Namen | 1 | bestanden |
| TC-17 | `Missing_file_ends_with_exit_2` | fehlende Datei: `SOURCE_NOT_FOUND` | 1 | bestanden |
| TC-18 | `Hex_values_are_normalised` | `#x…` → `0x…` klein, andere Werte unverändert | 3 | bestanden |
| TC-19 | `Sbom_matches_the_golden_file` | SBOM aus allen fünf Projekten byte-gleich mit den Golden Files | 5 | bestanden |
| TC-20 | `Sbom_passes_schema_and_tr_rules_without_errors` | Schema gültig, kein Fehler, zwei Läufe byte-gleich | 5 | bestanden |
| TC-21 | `Sbom_contains_no_network_data` | AMS-Net-IDs, `TargetNetId` und BIC stehen in der Quelle, nicht in der SBOM | 5 | bestanden |
| TC-22 | `Sbom_writes_twincat_properties` | `vendorKey:RevisionNo`, `device:disabled`, `firmware:origin` not-in-project, bom-ref `…-rt`, Lücke `runtime-version-unknown` | 1 | bestanden |

### 4.19 AML-Leser: `AmlReaderTests` (11)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| AR-01 | `Header_and_project_of_a_v17_export` | V17: `SchemaVersion` 2.15, `WriterHeader` vollständig, Projektname, zwei Geräte, keine unbekannte Rolle | 1 | bestanden |
| AR-02 | `Built_in_items_are_not_listed_and_positions_follow_the_rack` | S7-1200: nur Rack und CPU, integrierte E/A und HSC fehlen; CPU mit Position `0/1`, `TypeIdentifier`, `TypeName`, `FirmwareVersion`, `DeviceItemType` | 1 | bestanden |
| AR-03 | `Gsd_device_in_folder_with_modules` | GSD-Gerät im Ordner „Ungrouped devices“, vier Teile mit Positionen, Kopfmodul ohne Firmware, nicht mit dem IO-System verbunden | 1 | bestanden |
| AR-04 | `Io_system_belongs_to_its_cpu` | IO-System der S7-1200 hängt unter der CPU | 1 | bestanden |
| AR-05 | `Link_to_io_system_connects_the_device_of_the_interface` | synthetisch: ET 200SP (Schnittstelle unter dem Busadapter), G120C und GSD-Gerät mit dem IO-System ihrer CPU verbunden, G120C ohne Link ohne | 1 | bestanden |
| AR-06 | `Control_characters_are_removed_from_names` | `&#x9;_0` → `_0`, `null` → leer | 1 | bestanden |
| AR-07 | `Network_data_and_tag_tables_are_not_read` | IP-Adresse steht in der Quelle, nicht im gelesenen Projekt; keine Subnetzmaske, keine Variablentabelle, keine Element-IDs im JSON | 1 | bestanden |
| AR-08 | `Other_root_element_ends_with_exit_2` | anderes Wurzelelement: `NOT_AML` | 1 | bestanden |
| AR-09 | `Dtd_is_refused` | Datei mit DTD: `XML_INVALID` | 1 | bestanden |
| AR-10 | `Namespace_and_unknown_roles_do_not_stop_the_reader` | CAEX mit Namespace, Element mit fremder Rolle: Projekt und Gerät darunter gefunden, Rolle gemeldet, Position ohne `PositionNumber` aus dem Namen | 1 | bestanden |
| AR-11 | `Local_v18_export_has_seven_devices_on_the_io_system` | lokal, V18: acht Geräte, sieben mit IO-System-Link, Racks der G120C ohne Steuerzeichen | 1 | bestanden (überspringt sich ohne Datei) |

### 4.20 Adapter `tia-aml`: `TiaAmlAdapterTests` (58)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| TA-01 | `Station_carries_tia_version_export_time_and_hash` | Adapter, Quellpfad, SHA-256, Werkzeug „TIA Portal V17“ mit Release und Hersteller, Exportzeit | 1 | bestanden |
| TA-02 | `Writer_other_than_tia_portal_ends_with_exit_2` | `WriterName` „TIA Selection Tool“: `NOT_TIA_AML` | 1 | bestanden |
| TA-03 | `Unknown_writer_version_is_a_warning_and_kept_as_written` | `WriterVersion` 2200: Version `2200`, Warnung `TIA_AML_WRITER_VERSION_UNKNOWN` | 1 | bestanden |
| TA-04 | `Unknown_type_identifier_prefix_is_a_warning_and_no_component` | Präfix `Catalog:`: CPU fehlt, Warnung `TIA_AML_TYPE_IDENTIFIER_UNKNOWN` mit dem Wert | 1 | bestanden |
| TA-05 | `Type_identifier_is_split_by_prefix` | `OrderNumber:` mit und ohne `/V…` und `//…`, `System:`, `GSD:` mit DAP, M und D, unbekanntes Präfix | 8 | bestanden |
| TA-06 | `Cpu_is_the_controller_with_projected_firmware` | CPU 1214C: Steuerung, Firmware `V4.5` projected, Klassifizierung, Position, Siemens AG `assumed`, keine Lücke | 1 | bestanden |
| TA-07 | `Firmware_v0_0_and_racks_are_passive` | `BA 2xRJ45` (`V0.0`) und Profilschiene passiv, Muster-Bestellnummer, `System:Rack` gar nicht | 1 | bestanden |
| TA-08 | `Device_without_firmware_attribute_is_a_warning` | PM ohne `FirmwareVersion`: nicht passiv, ohne Firmware, `firmware-attribute-missing` (Warnung) und `firmware-not-in-project` | 1 | bestanden |
| TA-09 | `Passive_order_numbers_declare_devices_without_firmware_attribute` | `6EP1*` erklärt das PM als passiv (`device:passive` = `declared`), ein Gerät mit Firmware bleibt Firmware | 1 | bestanden |
| TA-10 | `Vendor_overrides_win_over_the_assumption` | erstes passendes Muster gilt, Herkunft `declared`, Firmware `4.7.10` unverändert | 1 | bestanden |
| TA-11 | `Catalogue_version_is_a_vendor_key_and_a_mismatch_warns` | `/V2.1` als `catalogVersion`, Firmware `V2.2`, Warnung `TIA_AML_CATALOG_VERSION_DIFFERS` | 1 | bestanden |
| TA-12 | `Gsd_head_module_without_gsdml_directory_has_no_order_number_vendor_or_firmware` | BCL248i ohne `gsdml`: kein Hersteller, keine Bestellnummer, keine Firmware, `GSDML`/`GsdType`/`GsdId`, vier Lücken beginnend mit `gsdml-not-found` („kein Verzeichnis“), Hinweis auf `SoftwareRelease` | 1 | bestanden |
| TA-13 | `Gsd_modules_are_passive` | Module M10 und M11 passiv, ohne Lücken | 1 | bestanden |
| TA-14 | `Without_plc_the_whole_file_is_the_station` | V17 ohne `plc`: CPU, GSD-Gerät und Module in einer Station, nichts unter der Maschine | 1 | bestanden |
| TA-15 | `With_plc_devices_off_the_io_system_are_unassigned` | V17 mit `plc`: GSD-Gerät unter der Maschine mit `device-unassigned` und Quell-Properties; passive Module ohne die Lücke | 1 | bestanden |
| TA-16 | `Two_cpus_give_two_stations_split_by_io_system` | synthetisch: zwei Stationen mit genau ihren Geräten, je eine Steuerung, zwei Geräte ohne Station | 1 | bestanden |
| TA-17 | `Devices_pattern_adds_a_device_without_io_system` | `devices` nimmt den G120C ohne IO-System auf | 1 | bestanden |
| TA-18 | `Devices_pattern_matches_item_names_too` | `Pump_?` trifft auch den G120C der anderen Station: `TIA_AML_DEVICE_IN_TWO_STATIONS` | 1 | bestanden |
| TA-19 | `Ignored_devices_are_listed_separately` | `Testaufbau_*` ausgeschlossen, nicht unter der Maschine | 1 | bestanden |
| TA-20 | `Unused_patterns_are_warnings` | Muster ohne Treffer in `devices` und `ignoreDevices`: zwei Warnungen `TIA_AML_PATTERN_UNUSED` | 1 | bestanden |
| TA-21 | `Device_claimed_by_two_stations_ends_with_exit_2` | `devices` auf ein Gerät am IO-System der anderen CPU: Exit 2 mit Gerätename | 1 | bestanden |
| TA-22 | `Same_cpu_for_two_stations_ends_with_exit_2` | zwei Stationen mit derselben CPU: Exit 2 | 1 | bestanden |
| TA-23 | `Unknown_plc_ends_with_exit_2_and_lists_the_cpus` | `PLC_9`: `TIA_AML_PLC_NOT_FOUND` mit „PLC_1, PLC_2“ | 1 | bestanden |
| TA-24 | `Several_cpus_without_plc_end_with_exit_2` | zwei CPUs ohne `plc`: `TIA_AML_PLC_REQUIRED` mit Liste | 1 | bestanden |
| TA-25 | `Two_stations_on_one_file_need_plc_each` | zwei Stationen, eine ohne `plc`: `TIA_AML_PLC_REQUIRED` | 1 | bestanden |
| TA-26 | `Missing_file_ends_with_exit_2` | fehlende Datei: `SOURCE_NOT_FOUND` | 1 | bestanden |
| TA-27 | `Station_without_declared_libraries_carries_both_gaps` | keine Software, `libraries-not-read` und `program-not-read` | 1 | bestanden |
| TA-28 | `Declared_libraries_replace_libraries_not_read` | LGF 7.0.1 und eine Bibliothek ohne Hersteller und Version: Herkunft `declared`, nur noch `program-not-read` | 1 | bestanden |
| TA-29 | `Sbom_matches_the_golden_file` | V17 ohne und mit `plc` byte-gleich mit den Golden Files | 2 | bestanden |
| TA-30 | `Sbom_of_two_stations_matches_the_golden_file` | zwei Stationen mit `devices`, `ignoreDevices` und deklarierter Bibliothek byte-gleich mit dem Golden File | 1 | bestanden |
| TA-31 | `Sbom_passes_schema_and_tr_rules_without_errors` | V17 ohne und mit `plc`: Schema gültig, kein Fehler, zwei Läufe byte-gleich | 2 | bestanden |
| TA-32 | `Sbom_of_two_stations_passes_without_errors` | synthetisch: kein Fehler, `FIRMWARE_ATTRIBUTE_MISSING` als Warnung | 1 | bestanden |
| TA-33 | `Sbom_contains_no_network_data` | IP-Adresse, Subnetzmaske und `IpProtocolSelection` stehen in der Quelle, nicht in der SBOM | 3 | bestanden |
| TA-34 | `Sbom_writes_tia_properties` | bom-ref `linie-1-fw`, `source:exportedAt`, `device:position`, `vendorKey:catalogVersion`, Lücken `firmware-attribute-missing` und `libraries-not-read`, kein passives Modul | 1 | bestanden |
| TA-35 | `Local_v18_export_runs_without_errors` | lokal, V18: eine Station, alle sieben Geräte am IO-System zugeordnet, drei G120C mit `4.7.10`, vier IM 155-6 mit `V3.3`, keine IP-Adresse, SBOM ohne Fehler, keine Warnung | 1 | bestanden (überspringt sich ohne Datei) |
| TA-36 | `Local_v21_export_runs_without_errors` | lokal, V21: Werkzeug mit Hersteller „Siemens“, PM mit `firmware-attribute-missing`, Profilschiene als Muster, CPU `V2.8`, SBOM ohne Fehler | 1 | bestanden (überspringt sich ohne Datei) |
| TA-37 | `Local_exports_from_other_tools_end_with_exit_2` | lokal: TIA Selection Tool und Beckhoff-Export ergeben `NOT_TIA_AML` | 2 | bestanden (überspringt sich ohne Datei) |
| TA-38 | `Gsdml_gives_vendor_and_order_number_of_the_head_module` | mit GSDML: Leuze als Hersteller (`gsdml`), `501xxxxx` als Muster, `VendorId`, `DeviceId`, `gsdmlFile`, `gsdmlSha256`, keine Warnung | 1 | bestanden |
| TA-39 | `Software_release_of_the_gsdml_is_not_the_firmware` | keine Firmware, `not-in-project`, `gsdml:softwareRelease` `V1.*.*` und `gsdml:hardwareRelease` `3`, nur `firmware-not-in-project` | 1 | bestanden |
| TA-40 | `Gsd_modules_get_the_vendor_and_stay_passive` | Module passiv mit Hersteller aus der GSDML, ohne Bestellnummer | 1 | bestanden |
| TA-41 | `Gsdml_not_in_the_directories_gives_the_gaps` | leeres Verzeichnis: `gsdml-not-found` (Warnung) mit Dateiname, `order-number-not-in-project`, `supplier-unknown`, `firmware-not-in-project` | 1 | bestanden |
| TA-42 | `Ambiguous_gsdml_gives_gsdml_not_found_with_the_candidates` | zwei Dateien gleichen Namens mit verschiedenem Inhalt: `gsdml-not-found` mit beiden Fundstellen | 1 | bestanden |
| TA-43 | `Access_point_missing_in_the_gsdml_is_a_warning_and_leaves_the_order_number_open` | Id `DAP_X`: Hersteller bleibt, keine Bestellnummer, Warnung `TIA_AML_GSDML_ITEM_NOT_FOUND` | 1 | bestanden |
| TA-44 | `Vendor_overrides_win_over_the_gsdml` | `501*` → erklärter Hersteller, Herkunft `declared` | 1 | bestanden |
| TA-45 | `Sbom_with_gsdml_matches_the_golden_file` | V17 mit GSDML byte-gleich mit dem Golden File | 1 | bestanden |
| TA-46 | `Sbom_with_gsdml_passes_schema_and_tr_rules_without_errors` | Schema gültig, kein Fehler, Properties `gsdml:softwareRelease`, `vendor:origin` = `gsdml`, `orderNumber:pattern` | 1 | bestanden |

### 4.22 GSDML-Leser: `GsdmlReaderTests` (17)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| GS-01 | `Reads_identity_and_vendor` | Pfad, SHA-256, `VendorID`, `DeviceID`, `VendorName` | 1 | bestanden |
| GS-02 | `Reads_the_device_access_point_from_its_own_module_info` | `DAP` mit `501xxxxx`, `V1.*.*`, `3` aus dem direkten `ModuleInfo` | 1 | bestanden |
| GS-03 | `Reads_modules_without_order_number` | 57 Module, `ID_MOD_Activation` ohne Bestellnummer | 1 | bestanden |
| GS-04 | `Module_order_number_is_read_where_the_module_info_has_one` | Kopie mit Bestellnummer im Modul: wird gelesen | 1 | bestanden |
| GS-05 | `Order_number_pattern_is_recognised` | `xx`, `*`, `?` sind Muster; echte Bestellnummern mit einzelnem `X` oder kleinem `i` nicht | 6 | bestanden |
| GS-06 | `Other_xml_ends_with_exit_2` | AML-Datei: `NOT_GSDML` | 1 | bestanden |
| GS-07 | `Index_finds_the_file_without_case` | Name aus dem AML in Großbuchstaben findet die Datei | 1 | bestanden |
| GS-08 | `Index_without_the_file_finds_nothing` | unbekannter Name: nichts, nicht mehrdeutig | 1 | bestanden |
| GS-09 | `Index_searches_subdirectories_and_parses_only_the_file_asked_for` | Unterverzeichnis, kaputte andere Datei stört nicht | 1 | bestanden |
| GS-10 | `Same_file_in_two_directories_takes_the_first` | gleicher Inhalt zweimal: erste nach Pfad | 1 | bestanden |
| GS-11 | `Same_name_with_different_content_is_ambiguous` | verschiedener Inhalt: mehrdeutig, beide Fundstellen | 1 | bestanden |
| GS-12 | `Missing_directory_ends_with_exit_2` | `GSDML_DIR_NOT_FOUND` | 1 | bestanden |

### 4.21 Muster: `WildcardTests` (17)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| WC-01 | `Patterns_match_the_whole_text` | `*`, `?`, ganze Zeichenkette, Groß-/Kleinschreibung, Rücksprung bei mehreren `*` | 12 | bestanden |
| WC-02 | `Order_numbers_match_with_or_without_spaces` | Muster mit und ohne Leerzeichen, `?` in der Bestellnummer, `*` als Zeichen in der Bestellnummer (`1***0`) | 5 | bestanden |

## 5. Testläufe

Alle Läufe lokal auf macOS 26.6.2 (arm64) mit .NET SDK 10.0.403, Ausgangsstand Commit `914bc98` plus die neuen, noch nicht committeten Testdateien.

| Lauf | Zeitpunkt | Umfang | Ergebnis | Befund und Maßnahme |
|---|---|---|---|---|
| 0 | 31.10.2026, vor den neuen Tests | 70 bestehende Tests | 70 von 70 bestanden | Ausgangslage |
| 1 | 31.10.2026 | 219 Tests | Build abgebrochen | Analyseregel xUnit1051: Ein Dateizugriff im Test übergab kein `TestContext.Current.CancellationToken`. Im Test ergänzt |
| 2 | 31.10.2026 | 219 Tests | 218 bestanden, 1 fehlgeschlagen | `Validation_is_deterministic` verglich Meldungen als Records; deren Feldliste wird nur als Referenz verglichen. Fehler im Test, nicht im Werkzeug. Vergleich auf Inhalt umgestellt |
| 3 | 31.10.2026 | 219 Tests, Formatprüfung | 219 bestanden, Format ohne Befund | – |
| 4 | 31.10.2026, 22:25 | 219 Tests mit TRX-Bericht | 219 bestanden, 1,08 s | Stand vor dem Abschluss von M1 |
| 5 | 31.10.2026, ab 22:40 | Gegenprüfung mit sbomqs an veränderten Kopien der Minimal-SBOM | 2 Abweichungen | Validator akzeptierte Lizenz als Freitext und eine verwaiste Komponente. Fehler im Werkzeug; Regeln `DISTRIBUTION_LICENCE_INVALID` und `COMPONENT_UNREACHABLE` ergänzt (SV-48 bis SV-52) |
| 6 | 31.10.2026, 22:54 | 233 Tests, Formatprüfung, Gegenprüfung über beide Golden Files | 233 bestanden, Format ohne Befund, keine Abweichung | Stand M1 |
| 7 | 31.10.2026, M2 | neue CODESYS-Tests | Build abgebrochen | Analyseregeln xUnit1051 (fehlendes `CancellationToken`) und xUnit2029 (`Assert.Empty` mit Filter) in den neuen Tests. Im Test behoben |
| 8 | 31.10.2026, 22:56 | 292 Tests mit TRX-Bericht, Formatprüfung; Gegenprüfung mit sbomqs über die beiden CODESYS-Golden-Files | 292 bestanden, 0,55 s, Format ohne Befund; sbomqs: robin 36 gleich, 89 erklärt, 0 Abweichungen; zwei Steuerungen 36/59/0 | M2 |
| 9 | 31.10.2026, CI-Fix | 292 Tests, gebaut wie in CI (`-c Release -p:ContinuousIntegrationBuild=true`), vorher und nachher | vorher 207 bestanden, 85 fehlgeschlagen; nachher 292 bestanden; Debug-Build ebenfalls 292, Format ohne Befund | Fehler in den Tests, nicht im Werkzeug: Pfadsuche über `[CallerFilePath]` durch `RepoPaths` ersetzt |
| 10 | 31.10.2026, 23:10 | 312 Tests mit TRX-Bericht, Formatprüfung; neu: Leser für Gerätebeschreibungen (E-015) | 312 bestanden, 0,61 s, Format ohne Befund. Zwischenlauf: 2 Doku-Abgleiche fehlgeschlagen, weil `devdesc-not-found` noch nicht in ANLEITUNG.md und CLAUDE.md stand; Doku ergänzt | Stand M2 |
| 11 | 31.10.2026, 23:15, CI | GitHub Actions, Lauf 41247352670 auf Commit `37ce75b`: Build, 312 Tests, Format und Lizenzen auf `ubuntu-latest` und `windows-latest`, Rauchtest aus beiden Paketen, Gegenprüfung mit sbomqs über alle fünf Golden Files | alle Jobs grün; sbomqs ohne Abweichung (devdesc 33 gleich, 32 erklärt; robin 36/89; zwei Steuerungen 36/59; Hand-Modell 74/41; Minimal 20/5) | Abschluss M1 und M2; bestätigt die Pfadsuche aus Lauf 9 |
| 12 | 31.10.2026, 23:36, M5 | 347 Tests mit TRX-Bericht, Formatprüfung; neu: L5X-Leser und Adapter `studio5000` (E-017); Gegenprüfung mit sbomqs v2.1.3 über alle acht Golden Files | 347 bestanden, 0,93 s, Format ohne Befund; Tabelle der Gegenprüfung ohne Abweichung (ra-L85E 16 gleich, 9 erklärt; acd 25/20; Modulbaum 39/36; übrige wie Lauf 11), der Abgleich endete aber mit Exit 1; beim Ablesen übersehen, siehe Lauf 13. Zwischenstand: 4 Golden Files fehlten und wurden erzeugt und von Hand geprüft; das JSON-Feld `eKey` in `inspect` in `ekey` umbenannt | M5 |
| 13 | 01.11.2026, CI und lokal | CI-Lauf 41250633501 auf Commit `d92b7cd`: Build und Tests auf ubuntu und windows grün, Job `crosscheck` rot. Danach Gegenprüfung lokal mit sbomqs v2.1.3, Exit-Code geprüft | Fehler im Abgleichwerkzeug, nicht im Validator: Die beiden baugleichen 1756-EN2T der synthetischen Datei haben bei sbomqs dieselbe Kennung, der Abgleich brach dort mit „Abgleich nicht eindeutig“ ab und ließ die Einträge aus der Tabelle weg. Behoben in `tools/crosscheck-sbomqs.cs`: Eintrag gegen jede Komponente mit dieser Kennung prüfen, zählen nur bei gleicher Bewertung. Danach Exit 0, Modulbaum 51 gleich, 44 erklärt, 0 Abweichungen; Gegenprobe mit einer Kopie, in der nur einer der beiden EN2T keinen Hersteller hat: Exit 1 mit „Abgleich nicht eindeutig“ | M5-Fix |
| 14 | 01.11.2026, 00:22, M6 | 398 Tests mit TRX-Bericht, Formatprüfung; neu: TwinCAT-Leser und Adapter `twincat` (E-018), Lückencode `devices-not-read`; Gegenprüfung mit sbomqs v2.1.3 über alle 13 Golden Files, Exit-Code geprüft | 398 bestanden, 0,95 s, Format ohne Befund; sbomqs Exit 0, keine Abweichung (HeatPump 90 gleich, 175 erklärt; SPT 59/156; Cognex 36/79; Core 28/67; synthetisch 37/58). Zwischenstände: Konstante `Core` kollidierte mit dem Namensraum (Test umbenannt); die Stationslücke `devices-not-read` fehlte in der SBOM, weil der Writer Stationslücken ohne Programm und Steuerung verwarf. Fehler im Werkzeug, behoben, Test W-19 | M6 |
| 15 | 01.11.2026, CI | GitHub Actions, Lauf 41251063889 auf Commit `6dac068` (M5 mit Abgleich-Fix): Build, Tests, Format und Lizenzen auf ubuntu und windows, Paket-Rauchtest, `crosscheck` | alle Jobs grün | bestätigt M5 |
| 16 | 01.11.2026, 10:42, M7 | 480 Tests mit TRX-Bericht, Formatprüfung; neu: AML-Leser, Adapter `tia-aml` (E-019), Muster, Lückencode `firmware-attribute-missing`, `inspect --adapter tia-aml`; Gegenprüfung mit sbomqs v2.1.3 über alle 16 Golden Files, Exit-Code geprüft; `inspect` von Hand über alle sechs AML-Dateien | 480 bestanden, 1,0 s, Format ohne Befund; sbomqs Exit 0, keine Abweichung (V17 ganze Datei 21 gleich, 14 erklärt; V17 mit `plc` 21/14; zwei CPUs 71/54). Zwischenstände: Ein `*` im Muster passte nicht auf ein `*` im Text (Profilschiene `6ES7 590-1***0-0AA0`), weil der Vergleich das Zeichen zuerst wörtlich nahm. Fehler im Werkzeug, behoben, Test WC-02; ein Test erwartete einen falschen Gerätenamen der synthetischen Datei (Fehler im Test); zwei Doku-Abgleiche schlugen fehl, bis `firmware-attribute-missing` in ANLEITUNG.md und CLAUDE.md stand. Gegenprobe wie in CI (`-c Release -p:ContinuousIntegrationBuild=true`, `fixtures/_local/tia-aml/` entfernt): 475 bestanden, 5 übersprungen, Exit 0 | M7 |
| 17 | 01.11.2026, 11:50, M8 | 508 Tests, Formatprüfung; neu: GSDML-Leser, GSD-Geräte mit GSDML (E-020), `inspect --gsdml`; Gegenprüfung mit sbomqs v2.1.3 über alle 17 Golden Files, Exit-Code geprüft; `inspect --gsdml` von Hand mit und ohne passende Datei | 508 bestanden, 1,4 s, Format ohne Befund; sbomqs Exit 0, keine Abweichung (V17 mit GSDML 21 gleich, 14 erklärt; übrige wie Lauf 16). Zwischenstände: fünf erwartete Fehlschläge, weil die drei M7-Golden-Files die neue Lücke `gsdml-not-found` am GSD-Gerät noch nicht hatten (begründet in E-020, neu erzeugt und im Diff geprüft: je eine Zeile), das neue Golden File fehlte und der alte M8-Platzhaltertest von `inspect` überholt war; eine Importreihenfolge korrigierte `dotnet format`. Gegenprobe wie in CI (`-c Release -p:ContinuousIntegrationBuild=true`, `fixtures/_local/tia-aml/` entfernt): 503 bestanden, 5 übersprungen | M8, Grundlage dieser Datei |

Die Gegenprüfung in Lauf 5 hat zwei Fehler im Werkzeug aufgedeckt, die Tests in Lauf 14 (Writer, W-19) und Lauf 16 (Mustervergleich, WC-02) je einen.

### 5.1 Manuelle Prüfläufe mit der Kommandozeile

Zusätzlich zu den automatischen Tests am 31.10.2026 von Hand aufgerufen (`dotnet run --project src/PlcSbom.Cli -- …`):

| Aufruf | Exit | Ausgabe |
|---|---:|---|
| `--version` | 0 | `0.1.0+adc95d96840fb7e4fdba173199afce7706d4c93d` |
| `validate hand-model.cdx.json` | 0 | Schema OK, „OK, 9 Warnungen, 32 Hinweise“, Ergebnisdatei geschrieben |
| `validate hand-model.cdx.json --release-build --verbose` | 1 | „NICHT BESTANDEN, 9 Warnungen, 32 Hinweise“, alle 32 Hinweise aufgelistet |
| `validate gibtsnicht.json` | 2 | `[SBOM_NOT_FOUND] Die SBOM-Datei gibtsnicht.json gibt es nicht.` |
| `validate --foo x` | 2 | `[USAGE] Unrecognized command or argument 'x'.` |
| `build --config x.yml --release R1` | 2 | `[NOT_IMPLEMENTED] Der Befehl „build“ ist in plc-sbom 0.1.0 noch nicht umgesetzt. Er folgt mit Meilenstein M9.` |

Die Meldung zu `--foo` kommt auf Englisch aus der Bibliothek System.CommandLine; die übrigen Meldungen sind wie vorgesehen deutsch.

## 6. Nicht abgedeckt

- `cyclonedx-cli validate` als zweite, unabhängige Schemaprüfung: noch nicht eingerichtet.
- Lauf auf Windows: nur in CI (`windows-latest`), nicht lokal.
- Test, dass die Schemaprüfung ohne Netz auskommt: Das Schema ist eingebettet und fremde Verweise werden abgelehnt, ein eigener Test mit gesperrtem Netz fehlt.
- Adapter `tia-portal`, `inspect` dafür, `build`, `report`, `compare`, `ids` und der Openness-Worker: noch nicht umgesetzt; die Tests prüfen nur, dass sie mit `[NOT_IMPLEMENTED]` bzw. `[OPENNESS_WINDOWS_ONLY]` enden.
- Adapter `tia-aml`: Die Exporte aus V18 und V21 haben keine Lizenz und liegen nur lokal; ihre fünf Testfälle laufen in CI nicht. Ein Golden File aus dem Comquent-V21-Demo-Export fehlt (E-019). HMI-Panels und IO-Systeme an Kommunikationsmodulen sind ohne Fixture, GSDML-Dateien mit mehreren Zugangspunkten ebenso (E-020).
- Self-contained-Paket: Der Aufruf aus dem ausgepackten Paket läuft in CI als Rauchtest, nicht lokal.

### 6.1 Gegenprüfung mit sbomqs

Kein xUnit-Test, sondern [tools/crosscheck-sbomqs.cs](tools/crosscheck-sbomqs.cs), in CI im Job `crosscheck`. Methode und Feldzuordnung: [docs/regeln-tr03183.md](docs/regeln-tr03183.md) Abschnitt 10, Begründung: docs/entscheidungen.md E-010. Lauf am 01.11.2026 lokal mit sbomqs v2.1.3 (Darwin arm64, Prüfsumme gegen `checksums.txt` des Releases geprüft), Exit 0:

| Datei | gleich, vorhanden | gleich, fehlt | erklärt | Abweichung |
|---|---:|---:|---:|---:|
| `codesys-v35-devdesc.cdx.json` | 33 | 0 | 32 | 0 |
| `codesys-v35-robin.cdx.json` | 36 | 0 | 89 | 0 |
| `codesys-v35-zwei-steuerungen.cdx.json` | 36 | 0 | 59 | 0 |
| `hand-model.cdx.json` | 74 | 0 | 41 | 0 |
| `minimal-synthetisch.cdx.json` | 20 | 0 | 5 | 0 |
| `studio5000-acd-aoi.cdx.json` | 25 | 0 | 20 | 0 |
| `studio5000-module-baum.cdx.json` | 51 | 0 | 44 | 0 |
| `studio5000-ra-l85e.cdx.json` | 16 | 0 | 9 | 0 |
| `tia-aml-v17-ganze-datei.cdx.json` | 21 | 0 | 14 | 0 |
| `tia-aml-v17-gsdml.cdx.json` | 21 | 0 | 14 | 0 |
| `tia-aml-v17-plc.cdx.json` | 21 | 0 | 14 | 0 |
| `tia-aml-zwei-cpus.cdx.json` | 71 | 0 | 54 | 0 |
| `twincat-aixocat-heatpump.cdx.json` | 90 | 0 | 175 | 0 |
| `twincat-spt-ethercat-diagnostics.cdx.json` | 59 | 0 | 156 | 0 |
| `twincat-tcopen-cognex.cdx.json` | 36 | 0 | 79 | 0 |
| `twincat-tcopen-core.cdx.json` | 28 | 0 | 67 | 0 |
| `twincat-zelle-synthetisch.cdx.json` | 37 | 0 | 58 | 0 |

Gegenprobe mit sechs absichtlich veränderten Kopien der Minimal-SBOM (nur lokal, nicht eingecheckt):

| Veränderung | Ergebnis |
|---|---|
| Dateiname entfernt | gleich, fehlt (`FILENAME_MISSING`) |
| Lizenz-ID, die es bei SPDX nicht gibt | gleich, fehlt (`SCHEMA_INVALID`) |
| Lizenz als Freitext in `license.name` | gleich, fehlt (`DISTRIBUTION_LICENCE_INVALID`); vor Lauf 5 Abweichung |
| Maschine ohne Abhängigkeiten | gleich, fehlt (`COMPONENT_UNREACHABLE`); vor Lauf 5 Abweichung |
| Hash unter `distribution-intake` | Abweichung, bekannt: Validator folgt TR-Tabelle 9 |
| SPDX-Ausdruck `MIT OR Apache-2.0` | gleich, vorhanden |
