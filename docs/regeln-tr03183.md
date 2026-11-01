# Regeln der BSI TR-03183-2 im Validator

Stand 01.11.2026, plc-sbom 0.1.0.

Grundlage:

- BSI TR-03183-2 „Cyber Resilience Requirements for Manufacturers and Products, Part 2: Software Bill of Materials (SBOM)“, Version 2.1.0 vom 20.08.2025. Abgerufen am 31.10.2026 von `https://www.bsi.bund.de/SharedDocs/Downloads/EN/BSI/Publications/TechGuidelines/TR03183/BSI-TR-03183-2_v2_1_0.pdf`.
- BSI CycloneDX Property Taxonomy v0.1.2, `https://github.com/BSI-Bund/tr-03183-cyclonedx-property-taxonomy`, abgerufen am 31.10.2026.

Abschnittsnummern ohne Zusatz beziehen sich auf die TR. Code: [TrValidator.cs](../src/PlcSbom.Core/Validation/TrValidator.cs), [GapCatalog.cs](../src/PlcSbom.Core/Gaps/GapCatalog.cs), [GapRules.cs](../src/PlcSbom.Core/Gaps/GapRules.cs), [CycloneDxWriter.cs](../src/PlcSbom.Core/Output/CycloneDxWriter.cs).

## 1. Grundsätze

- Der Validator prüft das JSON, nicht das interne Modell. Eigene und fremde SBOMs werden gleich geprüft.
- Fehlt ein Pflichtfeld, ist das ein **Fehler**. Ausnahmen: An der Komponente steht ein Lückenvermerk (`<namespace>:gap`), der dieses Feld erklärt, oder die Komponente ist eine logische Komponente (Abschnitt 6 dieser Datei). Dann meldet der Validator die Lücke mit ihrer Schwere.
- Schweregrade wie in CLAUDE.md Abschnitt 4.4: **Fehler** (immer Exit 1), **Warnung** (Exit 1 nur mit `--release-build`), **Hinweis** (ändert den Exit-Code nie).
- CycloneDX-Pfade nach den Mapping-Tabellen 8 bis 12 in Abschnitt 8.2. Für die Namen der `bsi:`-Properties gilt die Taxonomie (docs/entscheidungen.md E-009).
- Jeder Namespace außer `cdx` und `bsi` zählt als eigener Namespace für Lückenvermerke und das Kennzeichen der logischen Komponente.

## 2. Felder der SBOM selbst

| TR-Feld | TR | CycloneDX-Pfad | Quelle im Werkzeug | Code, wenn es fehlt oder falsch ist | Lücke möglich |
|---|---|---|---|---|---|
| Format | 4 | `bomFormat` = `CycloneDX`, `specVersion` ≥ 1.6, gültig gegen das Schema | Writer schreibt CycloneDX 1.6 | `NOT_CYCLONEDX`, `SPEC_VERSION_UNSUPPORTED`, `SCHEMA_INVALID` (Fehler); `SCHEMA_NOT_EMBEDDED` (Warnung bei Version > 1.6, Schema nicht eingebettet) | nein |
| Keine Schwachstellen | 3.1 | `vulnerabilities` fehlt oder ist leer | Writer schreibt keine | `VULNERABILITIES_PRESENT` | nein |
| Ersteller der SBOM | 5.2.1, Tab. 8 | `metadata.manufacturer.contact[].email` oder `metadata.manufacturer.url[]` | Hersteller der Maschine mit Kontakt (`machine` in `machine.yml`, ab M9) | `SBOM_CREATOR_MISSING` | nein |
| Zeitstempel | 5.2.1, Tab. 8 | `metadata.timestamp` | `SOURCE_DATE_EPOCH`, sonst Commit-Zeit, sonst aktuelle Zeit (CLAUDE.md 8.4, ab M9); immer UTC | `TIMESTAMP_MISSING` | nein |
| Hauptkomponente | 3.2.1 | `metadata.component` | die Maschine | `PRIMARY_COMPONENT_MISSING` | nein |
| SBOM-URI | 5.2.3, Tab. 10 | `serialNumber` | UUIDv5 aus Maschine, Release, Commit und Seriennummer | nicht geprüft, Pflicht nur „falls vorhanden“ (Form prüft das Schema) | – |

Die TR verlangt als Ersteller die E-Mail-Adresse und nur ersatzweise eine URL. Der Validator akzeptiert beides, weil er nicht erkennen kann, ob eine vorhandene E-Mail-Adresse weggelassen wurde.

## 3. Pflichtfelder je Komponente (5.2.2, Tabelle 9)

| TR-Feld | CycloneDX-Pfad | Quelle im Werkzeug | Code ohne Lückenvermerk | Lückencodes, die das Fehlen erklären |
|---|---|---|---|---|
| Ersteller | `manufacturer.contact[].email` oder `manufacturer.url[]` | Hersteller aus Quelle, GSDML oder `vendorOverrides`; Kontakt aus `machine.yml` (ab M9) | `CREATOR_MISSING` | `supplier-unknown`, `creator-contact-unknown`, `protected-block-unknown` |
| Name | `name` | `<Typname>, Firmware`, Bibliotheksname, `<Station>, Anwenderprogramm` | `SCHEMA_INVALID` (CycloneDX verlangt `name`) | keine |
| Version | `version` | Firmware wie im Projekt, Bibliotheksversion, `<Release> (git <kurz-hash>)` | `VERSION_MISSING` | `firmware-not-in-project`, `version-unresolved`, `runtime-version-unknown`, `protected-block-unknown` |
| Dateiname | Property `bsi:component:filename`, höchstens einmal | Dateiname der ausgelieferten Form, wenn eine Datei vorliegt; bei Firmware nie | `FILENAME_MISSING`; doppelt: `BSI_PROPERTY_DUPLICATE` | `filename-unavailable` |
| Abhängigkeiten | Eintrag in `dependencies` mit `ref` und `dependsOn` (auch leer); verschachtelte Komponenten zählen als enthalten | Writer: Maschine → alle Firmware-Komponenten und Programme; Programm → Firmware der Steuerung, Bibliotheken, Laufzeit; Gerät ohne Station → Maschine | `DEPENDENCIES_MISSING`, `DEPENDENCY_REF_UNKNOWN`, `COMPONENT_UNREACHABLE` | keine |
| Vollständigkeit der Abhängigkeiten | Komponente in `compositions[].dependencies` eines Eintrags mit `aggregate` | Writer: Maschine `complete`; Programm `complete`, mit `libraries-not-read` oder `program-not-read` `incomplete`; Firmware, Bibliotheken, Laufzeit `unknown` | `COMPLETENESS_MISSING` | keine |
| Distributionslizenz | `licenses[]` mit `acknowledgement` = `concluded`: `expression`, `license.id` oder `license.name` = `LicenseRef-…` | `licenses` in `machine.yml` (ab M9) | `DISTRIBUTION_LICENCE_MISSING`; keine SPDX-Kennung (6.1): `DISTRIBUTION_LICENCE_INVALID` | `license-unknown` |
| Hash der ausgelieferten Form | `externalReferences[type=distribution].hashes[alg=SHA-512].content` | SHA-512 der Datei, wenn sie vorliegt; nie erfunden | `HASH_MISSING` | `hash-unavailable`, `program-source-missing` |
| ausführbar | Property `bsi:component:executable` = `executable` / `non-executable`, genau einmal | Firmware fest `executable`; Software aus der Quelle | `EXECUTABLE_PROPERTY_MISSING`; falscher Wert `BSI_PROPERTY_INVALID`; doppelt `BSI_PROPERTY_DUPLICATE` | `file-properties-unknown` |
| Archiv | Property `bsi:component:archive` = `archive` / `no archive`, genau einmal | Firmware fest `archive` | `ARCHIVE_PROPERTY_MISSING` usw. wie oben | `file-properties-unknown` |
| strukturiert | Property `bsi:component:structured` = `structured` / `unstructured`, genau einmal | Firmware fest `unstructured` | `STRUCTURED_PROPERTY_MISSING` usw. wie oben | `file-properties-unknown` |

Dazu kommt die `bom-ref`, ohne die sich in CycloneDX keine Abhängigkeit angeben lässt: `BOM_REF_MISSING`, `BOM_REF_DUPLICATE` (Fehler).

Einzelheiten:

- **Firmware** ist nach Tabelle 7 (Abschnitt 8.1.7) ein ausführbares, unstrukturiertes Abbild; beim Archiv lässt die Tabelle beides zu. Das Werkzeug schreibt `executable`, `archive`, `unstructured` (Abschnitt 8.1.6: Firmware-Abbilder als Beispiel für unstrukturierte Archive).
- **Distributionslizenz** nach 6.1: SPDX-Kennung, SPDX-Ausdruck oder `LicenseRef-<inventarisierende Stelle>-…`. SPDX-Kennungen werden wie bei SPDX ohne Groß-/Kleinschreibung verglichen; die Kennung nach `WITH` ist eine Ausnahme und wird nicht geprüft. Freitext wie „Proprietär“ ist keine Lizenzkennung. `license.id` prüft schon das Schema gegen die SPDX-Liste aus CycloneDX 1.6.2.
- **Erreichbarkeit**: 5.1 verlangt die Auflösung der Abhängigkeiten von der Hauptkomponente aus. Eine Komponente, zu der kein Weg über `dependsOn` oder Verschachtelung führt, zeigt, dass eine Abhängigkeitsliste fehlt oder unvollständig ist (`COMPONENT_UNREACHABLE`, Fehler).
- **Hash**: Nur `distribution` zählt, wie in Tabelle 9. Ein SHA-512 unter `distribution-intake` oder ein SHA-256 genügt nicht.

## 4. Zusätzliche Felder: Pflicht, falls vorhanden (5.2.3, 5.2.4)

| TR-Feld | CycloneDX-Pfad | Quelle im Werkzeug | Prüfung |
|---|---|---|---|
| SBOM-URI | `serialNumber` | siehe Abschnitt 2 | keine |
| Quellcode-URI | `externalReferences[type=source-distribution].url` | Programm: URL des Quellverzeichnisses im Repository beim Commit (ab M9) | keine |
| URI der ausgelieferten Form | `externalReferences[type=distribution].url` | nur, wenn die Quelle sie nennt | keine |
| Andere eindeutige Kennungen | `cpe`, `purl`, `swid.tagId` | CPE nur aus `kennungen/cpe-map.yml` (CLAUDE.md Abschnitt 6), PURL nur aus Teil-SBOMs | keine |
| Originallizenzen | `licenses[]` mit `acknowledgement` = `declared` | `licenses` in `machine.yml` (ab M9) | keine |

Ob ein solcher Wert existiert, weiß nur die Quelle. Der Writer schreibt ihn, sobald eine Quelle ihn liefert; der Validator kann sein Fehlen nicht bewerten.

## 5. Optionale Felder (5.2.5)

| TR-Feld | CycloneDX-Pfad | Quelle im Werkzeug | Prüfung |
|---|---|---|---|
| Effektive Lizenz | Property `bsi:component:effectiveLicence` | `Licensing.Effective` | doppelt: `BSI_PROPERTY_DUPLICATE` (Fehler); Schreibweise `effectiveLicense` aus dem TR-PDF: `EFFECTIVE_LICENCE_SPELLING` (Warnung); veraltete Property `bsi:component:associatedLicences`: `ASSOCIATED_LICENCES_DEPRECATED` (Warnung) |
| Hash des Quellcodes | `externalReferences[type=source-distribution].hashes[alg=SHA-512]` | Programm-Hash (Abschnitt 9 dieser Datei) | keine |
| URL der security.txt | `externalReferences[type=rfc-9116].url` | wird nicht erzeugt | keine |

Die Taxonomie v0.1.2 schreibt zu `bsi:component:effectiveLicence` „MUST occur exactly once per component“, die TR führt die effektive Lizenz in 5.2.5 als optionales Feld. Der Validator folgt der TR (E-009, offen).

## 6. Logische Komponenten (3.2.2)

- Kennzeichen: Property `<namespace>:logicalComponent` = `true`. Der Writer setzt es nur an der Maschine. Die Maschine ist das Produkt und keine Datei; sie fasst die Software der Stationen zusammen, wie es Abschnitt 8.1.3 für logische Komponenten beschreibt.
- Nach 3.2.2 entfallen Dateiname, Hash, ausführbar, Archiv und strukturiert. Der Validator meldet das als Hinweis `LOGICAL_COMPONENT` und nennt die entfallenen Felder.
- Es bleiben Ersteller, Name, Version, Abhängigkeiten mit Vollständigkeit und Distributionslizenz.

## 7. Lückencodes

Spalte „erklärt“: das Pflichtfeld aus Abschnitt 3, dessen Fehlen der Code erklärt. „–“: Der Code erklärt kein Feld, sondern macht eine Einschränkung sichtbar.

| Code | Schwere | erklärt | Gesetzt von | Begründung |
|---|---|---|---|---|
| `firmware-not-in-project` | Hinweis | Version | Lückenregeln: Gerät ohne Firmware-Angabe im Projekt | 5.2.2 Version. Die Firmware hat eine Version, nur steht sie nicht in der Quelle; das Änderungsdatum einer Datei (5.2.2 Nr. 3) gibt es nicht. Erst der Ist-Stand schließt die Lücke. Für TIA-Geräte ohne Attribut setzt der Adapter `tia-aml` zusätzlich `firmware-attribute-missing` (Warnung, E-019). |
| `firmware-attribute-missing` | Warnung | – | Adapter `tia-aml` (M7): Gerät mit Bestellnummer ohne Attribut `FirmwareVersion` (V21: PM 70W), nicht in `passiveOrderNumbers` (E-019) | Kein TR-Feld; die Version erklärt `firmware-not-in-project`. Ob das Gerät überhaupt Firmware hat, ist offen: ist es passiv, gehört es nicht in die SBOM (Art. 3 Nr. 39 CRA: nur Softwareelemente), sonst fehlt seine Firmware-Version. Warnung, bis der Anwender das Gerät als passiv erklärt oder ein Ist-Stand vorliegt. |
| `firmware-projected-only` | Hinweis | – | Lückenregeln: Firmware mit Herkunft `projected` | Die Version ist ein Sollwert aus dem Projekt. 5.1 verlangt die Informationen, die beim Build vorliegen; der Ist-Stand kommt über `compare` (M11). |
| `hash-unavailable` | Hinweis | Hash | Lückenregeln: jede Firmware; Software ohne SHA-512 der ausgelieferten Form | 3.2.1: Angaben, die wegen der Art der Zusammensetzung nicht verfügbar sind, entfallen, auch Hash und Dateiname. Hersteller-Firmware steckt im Gerät und erreicht das Werkzeug nie als Datei; ein SPS-Programm hat kein stabiles Binärartefakt (CLAUDE.md 5.6). Auslegung, siehe E-011. |
| `filename-unavailable` | Hinweis | Dateiname | wie `hash-unavailable` | 3.2.1 wie oben. |
| `version-unresolved` | Warnung | Version | Lückenregeln: Version fehlt oder ist ein Platzhalter (`*`, „neueste installierte“) | 5.2.2 Version; 3.2.1 verlangt für ausgelieferte Komponenten die genaue Version. Beim Übersetzen gab es eine feste Version, die Quelle nennt sie nicht. |
| `runtime-version-unknown` | Warnung | Version | Lückenregeln: Laufzeit ohne Version; Adapter `twincat` (M6) | 5.2.2 Version. TwinCAT führt nur die XAE-Version im Projekt. |
| `libraries-not-read` | Warnung | – | Adapter `tia-aml` (M7): Station mit CPU ohne `libraries` (E-019) | 5.2.2 Abhängigkeiten, 5.1. Das Programm wird `incomplete` gekennzeichnet; Bibliotheken sind eingebundene Fremdkomponenten (3.2.1, kompilierter Code). |
| `program-not-read` | Hinweis | – | Adapter `tia-aml` (M7): jede Station mit CPU | 5.2.2 Vollständigkeit: Das Programm wird `incomplete` gekennzeichnet, wie die TR es für unvollständige Aufzählungen vorsieht. |
| `ekey-disabled` | Warnung | – | Adapter `studio5000` (M5), nur an Modulen außer der Steuerung (E-017) | Kein TR-Feld. Das Gerät im Feld kann vom projektierten abweichen; der Soll-Stand ist dann wenig belastbar. |
| `license-unknown` | Warnung | Distributionslizenz | Lückenregeln: keine Distributionslizenz bekannt | 5.2.2, 6.1. Pflichtfeld fehlt, Lücke vermerkt. |
| `vendor-sbom-missing` | Hinweis | – | Lückenregeln: jede Firmware, bis eine Hersteller-SBOM zugeordnet ist (M9) | 5.1: Die Firmware gehört zum Lieferumfang, ihre Bestandteile kennt nur der Hersteller. Die Abhängigkeiten der Firmware sind deshalb `unknown`; das ist eine zulässige Angabe der Vollständigkeit. |
| `protected-block-unknown` | Warnung | Ersteller, Version | Adapter `tia-portal` (M14) | 5.2.2 Ersteller und Version. |
| `gsdml-not-found` | Warnung | – | Adapter `tia-aml` (M8) | Ohne GSDML fehlen Hersteller und Bestellnummer; die Pflichtfeld-Lücke selbst trägt `supplier-unknown`. |
| `devdesc-not-found` | Warnung | – | Adapter `codesys`, Quelle `v35`, wenn `deviceDescriptions` gesetzt ist (E-015) | Kein TR-Feld. Ohne eindeutige Gerätebeschreibung fehlen Hersteller und Produktname; die Pflichtfeld-Lücke selbst trägt `supplier-unknown`. |
| `order-number-not-in-project` | Warnung | – | Adapter `tia-aml` (M7, GSD-Gerät ohne GSDML; ab M8 nur, wenn auch die GSDML keine nennt), `codesys` (M2), `studio5000` (M5, Modul ohne `CatalogNumber`), `twincat` (M6, jeder EtherCAT-Teilnehmer, E-018) | Kein TR-Pflichtfeld. Die Bestellnummer ist der Schlüssel für CSAF-Advisories (CLAUDE.md Abschnitt 6). |
| `supplier-unknown` | Warnung | Ersteller | Lückenregeln: kein Hersteller bekannt | 5.2.2 Ersteller. |
| `creator-contact-unknown` | Warnung | Ersteller | Lückenregeln: Hersteller ohne E-Mail und URL | 5.2.2: E-Mail-Adresse, sonst URL. |
| `device-unassigned` | Hinweis | – | Lückenregeln: Gerät ohne Station; Adapter `tia-aml` (M7): Gerät an keinem IO-System einer Station und in keinem `devices` | Kein TR-Feld. Die Firmware hängt direkt an der Maschine. |
| `devices-not-read` | Warnung | – | Adapter `twincat` (M6): PROFINET-Boxen, E/A-Gerät in eigener Datei (E-018) | Kein TR-Feld. Die Firmware dieser Geräte fehlt in der SBOM; 5.1 verlangt die Komponenten bis zur ersten außerhalb des eigenen Lieferumfangs. Warnung, damit ein Release-Build die Lücke nicht übersieht. |
| `program-source-missing` | Warnung | Hash | Lückenregeln: Anwenderprogramm ohne `program` in `machine.yml` | Ohne Quellverzeichnis gibt es nicht einmal den Hash über die Quellen (Abschnitt 9). |
| `file-properties-unknown` | Warnung | ausführbar, Archiv, strukturiert | Lückenregeln: Software ohne Angabe der Dateieigenschaften | 5.2.2. |

Unbekannte Lückencodes ergeben die Warnung `GAP_UNKNOWN` und erklären kein Feld.

## 8. Übersicht aller Meldungscodes des Validators

| Code | Schwere | Regel |
|---|---|---|
| `JSON_INVALID` | Fehler | Datei ist kein JSON-Objekt |
| `NOT_CYCLONEDX` | Fehler | `bomFormat` fehlt oder ist nicht `CycloneDX` (4) |
| `SPEC_VERSION_UNSUPPORTED` | Fehler | CycloneDX älter als 1.6 (4) |
| `SCHEMA_INVALID` | Fehler | Schema CycloneDX 1.6.2 verletzt (4) |
| `SCHEMA_NOT_EMBEDDED` | Warnung | CycloneDX > 1.6, Schemaprüfung entfällt |
| `VULNERABILITIES_PRESENT` | Fehler | 3.1 |
| `TIMESTAMP_MISSING`, `SBOM_CREATOR_MISSING` | Fehler | 5.2.1 |
| `PRIMARY_COMPONENT_MISSING` | Fehler | 3.2.1 |
| `BOM_REF_MISSING`, `BOM_REF_DUPLICATE` | Fehler | Voraussetzung für Abhängigkeiten |
| `CREATOR_MISSING`, `VERSION_MISSING`, `FILENAME_MISSING`, `DISTRIBUTION_LICENCE_MISSING`, `HASH_MISSING`, `EXECUTABLE_PROPERTY_MISSING`, `ARCHIVE_PROPERTY_MISSING`, `STRUCTURED_PROPERTY_MISSING` | Fehler | 5.2.2, Feld fehlt ohne Lückenvermerk |
| `DISTRIBUTION_LICENCE_INVALID` | Fehler | 6.1, Lizenz ist keine SPDX-Kennung, kein SPDX-Ausdruck und kein `LicenseRef-…` |
| `DEPENDENCIES_MISSING`, `DEPENDENCY_REF_UNKNOWN` | Fehler | 5.2.2, CycloneDX 1.6 |
| `COMPLETENESS_MISSING` | Fehler | 5.2.2 |
| `COMPONENT_UNREACHABLE` | Fehler | 5.1 |
| `BSI_PROPERTY_DUPLICATE`, `BSI_PROPERTY_INVALID` | Fehler | Taxonomie v0.1.2 |
| `EFFECTIVE_LICENCE_SPELLING`, `ASSOCIATED_LICENCES_DEPRECATED` | Warnung | Taxonomie v0.1.2 |
| `GAP_UNKNOWN` | Warnung | unbekannter Lückencode |
| `LOGICAL_COMPONENT` | Hinweis | 3.2.2 |
| Lückencodes in Großbuchstaben (`HASH_UNAVAILABLE` usw.) | wie Abschnitt 7 | vermerkte Lücke |

## 9. Hash über die Programmquellen

Ein SPS-Programm hat keine stabile ausgelieferte Datei (CLAUDE.md 5.6). Die Version des Programms ist Release und Commit; zusätzlich belegt ein Hash, aus welchen Quellen es stammt. Verfahren wie `../plc-build-tool/docs/adr/0005-hash-ueber-quellen.md`, mit SHA-512 statt SHA-256:

- Datei: SHA-512 des Inhalts, hexadezimal, Kleinbuchstaben.
- Verzeichnis (`program` in `machine.yml`): alle Dateien rekursiv, Pfad relativ zum Verzeichnis mit `/`, ordinal sortiert. Je Datei eine Zeile `<pfad>\0<sha512 des inhalts>\n`. Der Hash ist SHA-512 über die UTF-8-Bytes aller Zeilen. Leere Verzeichnisse zählen nicht.
- In der SBOM als Hash des Quellcodes (5.2.5): `externalReferences[type=source-distribution]` mit der Quellcode-URI und `hashes[alg=SHA-512]`. Die TR legt für diesen Hash noch kein Verfahren fest.
- Der Hash der ausgelieferten Form fehlt, Lückenvermerk `hash-unavailable` (Hinweis). Ohne `program` gibt es auch keinen Quell-Hash: `program-source-missing` (Warnung).

Berechnet wird der Hash mit `build` (M9); das Modell und der Writer tragen ihn schon.

## 10. Gegenprüfung mit sbomqs

CLAUDE.md Abschnitt 9 verlangt eine unabhängige Prüfung des eigenen Validators. Sie läuft mit [tools/crosscheck-sbomqs.cs](../tools/crosscheck-sbomqs.cs) über alle Golden Files, in CI im Job `crosscheck` mit sbomqs v2.1.3 (Interlynk, Apache-2.0, Prüfsumme des Pakets im Workflow festgehalten):

```bash
dotnet run tools/crosscheck-sbomqs.cs -- --sbomqs <pfad-zu-sbomqs>
```

sbomqs vergibt je Feld Punkte, kennt aber weder Lückenvermerke noch logische Komponenten. Eine ehrliche SBOM mit Hersteller-Firmware erreicht deshalb nie die volle Punktzahl. Der Abgleich vergleicht stattdessen je Komponente und Pflichtfeld (E-010):

| sbomqs | eigener Validator | Ergebnis |
|---|---|---|
| vorhanden | kein Fehler | gleich |
| fehlt | Fehler | gleich |
| fehlt | Lückenvermerk oder logische Komponente | erklärt |
| vorhanden | Fehler | **Abweichung** |
| fehlt | keine Meldung | **Abweichung** |

Jede Abweichung lässt den Job scheitern. Sie gilt als Fehler im eigenen Validator, bis das Gegenteil belegt ist.

### 10.1 Feldzuordnung

| sbomqs-Feld (`--bsi-v21`) | TR-Feld im Validator | verglichen |
|---|---|---|
| `vuln` | `sbom.vulnerabilities` | ja |
| `specification version` | `sbom.format` | ja |
| `creator of SBOM` | `sbom.creator` | ja |
| `timestamp` | `sbom.timestamp` | ja |
| `dependency graph completeness` | `dependencies`, `dependencyCompleteness`, `dependencyGraph` aller Komponenten | ja |
| `component creator`, `component name`, `component version`, `filename`, `dependencies`, `distribution licence (concluded)`, `hash of deployable component`, `executable property`, `archive property`, `structured property` | gleichnamiges Feld aus Abschnitt 3 | ja |
| `SBOM-URI`, `source code URI`, `URI of deployable form`, `other unique identifiers`, `original licence (declared)` | – | nein, Pflicht nur „falls vorhanden“ |
| `effective licence`, `hash of source code`, `security.txt URL`, `BOM links` | – | nein, optional |

sbomqs benennt Komponenten mit gekürztem `<name>-<version>`; der Abgleich bildet diese Kürzung nach (`common.UniqueElementID` in sbomqs). Ist eine Kennung unbekannt, scheitert der Abgleich mit Meldung. Baugleiche Module haben dieselbe Kennung (zwei 1756-EN2T mit Revision 11.1 heißen beide „1756-EN2T, Firmware-11.1“), und welcher Komponente ein Eintrag von sbomqs gilt, ist dann nicht erkennbar. Der Abgleich prüft den Eintrag deshalb gegen jede dieser Komponenten und zählt ihn nur, wenn der Validator alle gleich bewertet; sonst scheitert er mit der Meldung „Abgleich nicht eindeutig“.

### 10.2 Bekannte Unterschiede

| Unterschied | Behandlung |
|---|---|
| sbomqs kennt keine logischen Komponenten (3.2.2) und vermisst an der Maschine Dateiname, Hash und Dateieigenschaften | erklärt durch `LOGICAL_COMPONENT` |
| sbomqs kennt keine Lückenvermerke | erklärt durch den Lückencode |
| sbomqs zählt auch einen SHA-512 unter `distribution-intake` als Hash | Validator bleibt bei Tabelle 9 (`distribution`). Der Writer schreibt nie `distribution-intake`; käme es in einem Golden File vor, meldete der Abgleich eine Abweichung |
| sbomqs prüft Graph und Vollständigkeitsangabe einmal je Dokument, der Validator je Komponente | Zuordnung in 10.1 |
| sbomqs führt die Felder aus 5.2.3 und 5.2.4 in seiner Tabelle als „required“ und gibt 0 Punkte, wenn sie fehlen | nicht verglichen, siehe Abschnitt 4 |
| sbomqs nummeriert einige Felder anders als die TR (Quellcode-URI als 5.2.3, andere Kennungen als 5.2.5; in der TR beide 5.2.4) | ohne Folgen |
| sbomqs akzeptiert `effectiveLicence` und `effectiveLicense` | Validator warnt bei `effectiveLicense` (E-009) |
| sbomqs unterscheidet Komponenten nur über Name und Version, bom-ref spielt keine Rolle | baugleiche Module wie in 10.1 beschrieben |

### 10.3 Ergebnis am 31.10.2026

Lauf mit sbomqs v2.1.3 über die beiden Golden Files von M1 (Ergebnisse späterer Golden Files in TESTFAELLE.md, Abschnitt 5):

| Datei | gleich, vorhanden | gleich, fehlt | erklärt | Abweichung | sbomqs-Punkte (Pflichtfelder) |
|---|---:|---:|---:|---:|---:|
| `hand-model.cdx.json` | 74 | 0 | 41 | 0 | 6,5 von 10 |
| `minimal-synthetisch.cdx.json` | 20 | 0 | 5 | 0 | 8,1 von 10 |

Die 41 erklärten Stellen im Handmodell sind die Lücken aus TESTFAELLE.md (Hersteller-Firmware ohne Datei und Hash, TwinCAT-Laufzeit und Tc2_Standard ohne Version, Lizenz und Dateieigenschaften) sowie die Maschine als logische Komponente.

Vor dem ersten Lauf ohne Abweichung hat der Abgleich an absichtlich veränderten Kopien der Minimal-SBOM zwei Lücken im eigenen Validator aufgedeckt. Beide sind behoben und durch Tests abgesichert:

| Veränderung | sbomqs | Validator vorher | Validator jetzt |
|---|---|---|---|
| Distributionslizenz als Freitext (`license.name` = „Proprietär“) | non-compliant | keine Meldung | `DISTRIBUTION_LICENCE_INVALID` |
| Maschine hängt von nichts ab, die Anwendung ist verwaist | 0 Punkte für den Graphen | keine Meldung | `COMPONENT_UNREACHABLE` |

## 11. Abweichungen zwischen TR, Taxonomie und CLAUDE.md

| Punkt | Stand | Entscheidung |
|---|---|---|
| `bsi:component:effectiveLicence` (Taxonomie) gegen `effectiveLicense` (TR-PDF, Tabelle 12) | Writer schreibt die Taxonomie-Schreibweise, Validator warnt bei der PDF-Schreibweise | E-009 |
| Effektive Lizenz: Taxonomie „genau einmal je Komponente“, TR optional (5.2.5) | Validator folgt der TR | E-009, offen |
| Hash und Dateiname bei Hersteller-Firmware als Hinweis | Auslegung von 3.2.1 | E-011, offen |
| CLAUDE.md nennt „TR §8.2“ für die Vollständigkeit | Die Pflicht steht in 5.2.2, Abschnitt 8.2 ist die Mapping-Tabelle | ohne Folgen |
