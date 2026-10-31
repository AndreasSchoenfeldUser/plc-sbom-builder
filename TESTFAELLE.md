# Testfälle: PLC SBOM Builder (`plc-sbom`)

Stand: 31.10.2026, Version 0.1.0 (Meilensteine M1 und M2 abgeschlossen, M5 umgesetzt). Diese Datei beschreibt alle automatischen Testfälle, die Testläufe und ihre Ergebnisse. Die Bedienung des Werkzeugs steht in [ANLEITUNG.md](ANLEITUNG.md), Ziele und Regeln in [CLAUDE.md](CLAUDE.md).

## 1. Ergebnis

**347 Testfälle, 347 bestanden, 0 fehlgeschlagen, 0 übersprungen.** Letzter Lauf am 31.10.2026, 23:36 Uhr, Laufzeit 0,93 s. `dotnet format --verify-no-changes` ohne Befund. Dazu die Gegenprüfung mit sbomqs ohne Abweichung (Abschnitt 6.1).

| Testklasse | Datei | Testfälle | Bestanden | Prüfgegenstand |
|---|---|---:|---:|---|
| `CliAppTests` | [Cli/CliAppTests.cs](tests/PlcSbom.Tests/Cli/CliAppTests.cs) | 41 | 41 | Kommandozeile, Hilfe, Exit-Codes, Gerüst der Befehle |
| `ValidateCommandTests` | [Cli/ValidateCommandTests.cs](tests/PlcSbom.Tests/Cli/ValidateCommandTests.cs) | 11 | 11 | `plc-sbom validate` mit echten Dateien |
| `SbomValidatorTests` | [Validation/SbomValidatorTests.cs](tests/PlcSbom.Tests/Validation/SbomValidatorTests.cs) | 74 | 74 | Schema und Regeln der BSI TR-03183-2 |
| `GapRulesTests` | [Gaps/GapRulesTests.cs](tests/PlcSbom.Tests/Gaps/GapRulesTests.cs) | 28 | 28 | Lückenregeln am Modell, Lückenkatalog |
| `CycloneDxWriterTests` | [Output/CycloneDxWriterTests.cs](tests/PlcSbom.Tests/Output/CycloneDxWriterTests.cs) | 20 | 20 | CycloneDX-Writer, Golden File, Determinismus |
| `ResultFileTests` | [Output/ResultFileTests.cs](tests/PlcSbom.Tests/Output/ResultFileTests.cs) | 8 | 8 | Ergebnisdatei `<out>.result.json` |
| `IdentifierTests` | [Output/IdentifierTests.cs](tests/PlcSbom.Tests/Output/IdentifierTests.cs) | 11 | 11 | UUIDv5, `bom-ref`, Pfad der Ergebnisdatei |
| `DocsConsistencyTests` | [Docs/DocsConsistencyTests.cs](tests/PlcSbom.Tests/Docs/DocsConsistencyTests.cs) | 43 | 43 | Abgleich von ANLEITUNG.md, CLAUDE.md, README.md mit dem Code |
| `SampleFileTests` | [Samples/SampleFileTests.cs](tests/PlcSbom.Tests/Samples/SampleFileTests.cs) | 1 | 1 | Minimal-SBOM als Golden File für die Gegenprüfung |
| `LibraryReferenceTests` | [Adapters/Codesys/LibraryReferenceTests.cs](tests/PlcSbom.Tests/Adapters/Codesys/LibraryReferenceTests.cs) | 14 | 14 | CODESYS-Bibliotheksangabe `Titel, Version (Hersteller)` |
| `PlcOpenReaderTests` | [Adapters/Codesys/PlcOpenReaderTests.cs](tests/PlcSbom.Tests/Adapters/Codesys/PlcOpenReaderTests.cs) | 15 | 15 | PLCopen-XML-Leser an echten Fixtures, sicheres XML |
| `CodesysV35AdapterTests` | [Adapters/Codesys/CodesysV35AdapterTests.cs](tests/PlcSbom.Tests/Adapters/Codesys/CodesysV35AdapterTests.cs) | 28 | 28 | Adapter `codesys`/`v35`: Geräte, Bibliotheken, Gerätebeschreibungen, Lücken, Golden Files |
| `DeviceDescriptionIndexTests` | [Adapters/Codesys/DeviceDescriptionIndexTests.cs](tests/PlcSbom.Tests/Adapters/Codesys/DeviceDescriptionIndexTests.cs) | 12 | 12 | CODESYS-Gerätebeschreibungen lesen und zuordnen |
| `InspectCommandTests` | [Cli/InspectCommandTests.cs](tests/PlcSbom.Tests/Cli/InspectCommandTests.cs) | 8 | 8 | `plc-sbom inspect --adapter codesys` und `--adapter studio5000` |
| `L5xReaderTests` | [Adapters/Studio5000/L5xReaderTests.cs](tests/PlcSbom.Tests/Adapters/Studio5000/L5xReaderTests.cs) | 10 | 10 | L5X-Leser an den echten Exporten |
| `Studio5000AdapterTests` | [Adapters/Studio5000/Studio5000AdapterTests.cs](tests/PlcSbom.Tests/Adapters/Studio5000/Studio5000AdapterTests.cs) | 23 | 23 | Adapter `studio5000`: Station, Firmware-Soll, Kodierung, Hersteller, AOIs, Golden Files, keine Netzdaten |
| **Summe** | | **312** | **312** | |

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
| Golden File der Minimal-SBOM | [golden/minimal-synthetisch.cdx.json](tests/PlcSbom.Tests/golden/minimal-synthetisch.cdx.json) | Die Minimal-SBOM als Datei. Einzige Testdatei, in der Dateiname, Hash und Dateieigenschaften vorhanden sind; Eingang der Gegenprüfung mit sbomqs neben `hand-model.cdx.json` |

## 4. Testfälle

Spalte „Ergebnis“: Ergebnis des letzten Laufs. „bestanden“ heißt, dass alle Zusicherungen des Testfalls zutrafen.

### 4.1 Kommandozeile: `CliAppTests` (41)

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| CLI-01 | `Exit_codes_match_the_documented_interface` | Die Exit-Codes haben die Werte aus CLAUDE.md 7.2: 0 Ok, 1 Befund, 2 Aufruf, 3 Umgebung, 4 Abbruch | 1 | bestanden |
| CLI-02 | `Help_lists_all_commands` | `--help` endet mit Exit 0 und nennt alle acht Befehle sowie `--verbose` und `--timeout` | 1 | bestanden |
| CLI-03 | `Help_of_each_command_succeeds` | `<befehl> --help` endet mit Exit 0, für `build`, `validate`, `report`, `compare`, `ids`, `inspect`, `export-aml`, `doctor` | 8 | bestanden |
| CLI-04 | `Build_help_shows_the_options_of_section_7_1` | Die Hilfe zu `build` nennt `--config`, `--release`, `--commit`, `--out`, `--report`, `--release-build` | 1 | bestanden |
| CLI-05 | `Version_prints_the_tool_version` | `--version` gibt die Version im Format `x.y.z` aus, Exit 0 | 1 | bestanden |
| CLI-06 | `No_command_is_a_usage_error` | Aufruf ohne Befehl: Exit 2 und Hinweis auf `plc-sbom --help` | 1 | bestanden |
| CLI-07 | `Invalid_calls_end_with_exit_2` | Falsche Aufrufe enden mit Exit 2 und `[USAGE]`: unbekannter Befehl, fehlendes `--config` bzw. `--release`, `--report pdf`, unbekannte Option, `report --format pdf`, unbekannte Adapter-ID, fehlendes `--adapter`, `doctor --adapter twincat`, `validate` ohne Datei, `--timeout 0`, `--timeout zehn` | 12 | bestanden |
| CLI-08 | `Commands_not_yet_built_name_their_milestone` | Noch nicht umgesetzte Befehle enden mit Exit 2 und `[NOT_IMPLEMENTED]` und nennen ihren Meilenstein: `build` M9, `report` M10, `compare` M11, `ids` M12, `doctor` M14, `inspect` für `twincat` M6, `tia-aml` M7, `tia-portal` M14 | 8 | bestanden |
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

### 4.5 CycloneDX-Writer: `CycloneDxWriterTests` (20)

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

### 4.8 Doku-Abgleich: `DocsConsistencyTests` (43)

Diese Tests sichern die Regel aus CLAUDE.md Abschnitt 14 ab, soweit sie sich maschinell prüfen lässt. Fehlt nach einer Änderung im Code ein Lückencode in der Doku, schlägt der Build fehl.

| ID | Testfall | Prüft | Fälle | Ergebnis |
|---|---|---|---:|---|
| DOC-01 | `Anleitung_lists_every_gap_code_with_its_severity` | Jeder der 19 Lückencodes steht genau einmal in der Tabelle von ANLEITUNG.md, mit derselben Schwere wie im Code | 19 | bestanden |
| DOC-02 | `Claude_md_lists_every_gap_code` | Jeder der 19 Lückencodes steht in einer Tabelle von CLAUDE.md | 19 | bestanden |
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

### 4.13 Befehl `inspect`: `InspectCommandTests` (8)

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
| 12 | 31.10.2026, 23:36, M5 | 347 Tests mit TRX-Bericht, Formatprüfung; neu: L5X-Leser und Adapter `studio5000` (E-017); Gegenprüfung mit sbomqs v2.1.3 über alle acht Golden Files | 347 bestanden, 0,93 s, Format ohne Befund; sbomqs ohne Abweichung (ra-L85E 16 gleich, 9 erklärt; acd 25/20; Modulbaum 39/36; übrige wie Lauf 11). Zwischenstand: 4 Golden Files fehlten und wurden erzeugt und von Hand geprüft; das JSON-Feld `eKey` in `inspect` in `ekey` umbenannt | Grundlage dieser Datei |

Die Tests selbst haben keinen Fehler im Werkzeug aufgedeckt, die Gegenprüfung in Lauf 5 zwei.

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
- Adapter außer `codesys` (Quelle `v35`) und `studio5000`, `inspect` für andere Adapter, `build`, `report`, `compare`, `ids` und der Openness-Worker: noch nicht umgesetzt; die Tests prüfen nur, dass sie mit `[NOT_IMPLEMENTED]` bzw. `[OPENNESS_WINDOWS_ONLY]` enden.
- Self-contained-Paket: Der Aufruf aus dem ausgepackten Paket läuft in CI als Rauchtest, nicht lokal.

### 6.1 Gegenprüfung mit sbomqs

Kein xUnit-Test, sondern [tools/crosscheck-sbomqs.cs](tools/crosscheck-sbomqs.cs), in CI im Job `crosscheck`. Methode und Feldzuordnung: [docs/regeln-tr03183.md](docs/regeln-tr03183.md) Abschnitt 10, Begründung: docs/entscheidungen.md E-010. Lauf am 31.10.2026 lokal mit sbomqs v2.1.3 (Darwin arm64, Prüfsumme gegen `checksums.txt` des Releases geprüft):

| Datei | gleich, vorhanden | gleich, fehlt | erklärt | Abweichung |
|---|---:|---:|---:|---:|
| `codesys-v35-devdesc.cdx.json` | 33 | 0 | 32 | 0 |
| `codesys-v35-robin.cdx.json` | 36 | 0 | 89 | 0 |
| `codesys-v35-zwei-steuerungen.cdx.json` | 36 | 0 | 59 | 0 |
| `hand-model.cdx.json` | 74 | 0 | 41 | 0 |
| `minimal-synthetisch.cdx.json` | 20 | 0 | 5 | 0 |
| `studio5000-acd-aoi.cdx.json` | 25 | 0 | 20 | 0 |
| `studio5000-module-baum.cdx.json` | 39 | 0 | 36 | 0 |
| `studio5000-ra-l85e.cdx.json` | 16 | 0 | 9 | 0 |

Gegenprobe mit sechs absichtlich veränderten Kopien der Minimal-SBOM (nur lokal, nicht eingecheckt):

| Veränderung | Ergebnis |
|---|---|
| Dateiname entfernt | gleich, fehlt (`FILENAME_MISSING`) |
| Lizenz-ID, die es bei SPDX nicht gibt | gleich, fehlt (`SCHEMA_INVALID`) |
| Lizenz als Freitext in `license.name` | gleich, fehlt (`DISTRIBUTION_LICENCE_INVALID`); vor Lauf 5 Abweichung |
| Maschine ohne Abhängigkeiten | gleich, fehlt (`COMPONENT_UNREACHABLE`); vor Lauf 5 Abweichung |
| Hash unter `distribution-intake` | Abweichung, bekannt: Validator folgt TR-Tabelle 9 |
| SPDX-Ausdruck `MIT OR Apache-2.0` | gleich, vorhanden |
