# CLAUDE.md: PLC SBOM Builder (`plc-sbom`), Prototyp

> Stand: 31.10.2026 (Meilensteine und CODESYS V2.3/4 ergänzt; M1-Entscheidungen E-007 bis E-011 und TR-Regeln in `docs/regeln-tr03183.md` nachgetragen; M1 und M2 abgeschlossen; übrige Fakten Stand 29.10.2026). Grundlage: Produktseite comquent.de/produkte/plc-sbom-builder (Quelltext `src/app/produkte/inhalte.ts` im Website-Repository), der Entwurf `plc-sbom-builder-claude.md` mit der Recherche vom 25.10.2026, das Comquent-Whitepaper „SBOM für SPS-Projekte mit TIA Portal V21“, die Siemens-Openness-Skills (V21) und drei öffentliche AML-Exporte aus TIA V17, V18 und V21 (geprüft am 29.10.2026). Recherchefakten mit Datum 25.10.2026 sind nicht erneut geprüft; Versionen und Normstände vor der Nutzung nachsehen.
> Produkt der Comquent GmbH (Industrial DevOps, Puchheim). Schwesterwerkzeuge: `plc-build` (../plc-build-tool), `plc-git-bridge` (../plc-git-bridge).

## 0. Start

Erste Sitzung in Claude Code:

1. Diese Datei lesen, dann Meilenstein M0 (Abschnitt 12) planen und mit Tests beginnen.
2. Bis einschließlich M13 läuft alles auf macOS, Linux und Windows, ohne Engineering-Software und ohne Lizenz. Ausnahme: Ergibt der Spike zu CODESYS V2.3 (M3a), dass nur die Umwandlung über CODESYS V3.5 trägt, braucht dieser Teil einen Windows-Rechner mit CODESYS V3.5.
3. Für M14 (Openness-Worker) läuft Claude Code auf der Windows-VM mit TIA Portal V21. Dann die Openness-Skills von Siemens einbinden, wie in `../plc-build-tool/CLAUDE.md` Abschnitt 0 beschrieben. Für dieses Projekt wichtig: `openness-base`, `session-and-project`, `licensing-and-firewall`, `devices-and-hardware`, `global-library`, `object-tree-walking`, `performance-and-caching`, `crash-diagnosis`, `openness-testing`.

## 1. Worum es geht

`plc-sbom` ist ein Kommandozeilenwerkzeug für Windows und Linux, das eine CI-Pipeline (Jenkins, GitLab CI, Azure DevOps) in jedem Build aufruft. Es erzeugt aus den SPS-Projekten einer Maschine **eine** SBOM im Format CycloneDX 1.6 JSON, auch wenn die Maschine Steuerungen mehrerer Hersteller enthält.

Was es tut:

1. Je Engineering-Umgebung liest ein Adapter Geräte, Bestellnummern, projektierte Firmware-Stände, Bibliotheken und das Anwenderprogramm aus.
2. Es führt diese Ergebnisse mit den SBOMs der Komponentenhersteller und mit Teil-SBOMs aus Syft oder cdxgen (Webseiten, Hochsprachen-Anteile) hierarchisch zusammen: Maschine → Station → Komponente.
3. Es prüft die fertige Datei gegen das Schema CycloneDX 1.6 und die Pflichtfelder der BSI TR-03183-2. Ein Fehler beendet den Build.
4. Es schreibt neben der JSON-Datei einen lesbaren Prüfbericht mit Maschinenbaum und Lücken.
5. Liegt ein ausgelesener Ist-Stand vor, etwa aus der Inbetriebnahme, gleicht es ihn mit der SBOM ab und meldet jede Abweichung.

Zielgruppe: Maschinen- und Anlagenbauer, die ihre SBOM für den Cyber Resilience Act aufbauen, mit vielen Releases und Varianten und oft mit Steuerungen mehrerer Hersteller in einer Maschine.

### 1.1 Adapter im Überblick

| Adapter | Umgebung | Eingang | Läuft auf | Meilenstein |
|---|---|---|---|---|
| `codesys` | CODESYS V3.5, V2.3 und CODESYS 4 | V3.5: PLCopen-XML-Export; V2.3 und CODESYS 4: nach Spike | überall (V2.3 je nach Spike) | M2–M4 |
| `studio5000` | Rockwell Studio 5000 | L5X-Export | überall | M5 |
| `twincat` | Beckhoff TwinCAT 3 | `.tsproj`/`.xti` + `.plcproj` | überall | M6 |
| `tia-aml` | Siemens TIA Portal V17–V21 | AML aus dem CAx-Export | überall | M7, M8 |
| `tia-portal` | Siemens TIA Portal V21 | Projekt `.ap21` / Archiv `.zap21` über Openness | Windows mit TIA V21 | M14 |

`tia-aml` ist ein Zusatz zur Produktseite, die für TIA nur Openness nennt. Er deckt Teams ohne Windows-Agent mit TIA ab und ältere TIA-Versionen. `tia-portal` liefert zusätzlich Bibliothekstypen und geschützte Bausteine, die im AML fehlen.

### 1.2 Was der Prototyp beweisen soll

| # | Hypothese | Gemessen an |
|---|---|---|
| H1 | Aus echten Projektdateien der Umgebungen entsteht ohne Engineering-Lizenz eine SBOM, die die TR-Prüfung besteht, ohne einen erfundenen Wert | Golden Files je Adapter; eigener Validator und `sbomqs --bsi` akzeptieren sie |
| H2 | Ein internes Modell trägt alle Umgebungen ohne Sonderfälle im Kern | Kern-Code (Merge, Writer, Validator) enthält keine herstellerspezifischen Verzweigungen |
| H3 | Die Ausgabe ist reproduzierbar je Commit und Release | Zwei Läufe mit gleicher Eingabe ergeben byte-gleiche Dateien, auf Linux und Windows |
| H4 | Die Kennungen tragen für den Abgleich mit CSAF-Advisories | Abdeckungsbericht von `plc-sbom ids` gegen echte Siemens-CSAF-Dateien |
| H5 | Openness liefert für dasselbe Projekt dieselben Geräte wie der AML-Export, dazu Bibliothekstypen | M14: Comquent-Demo-Projekt über `tia-aml` und `tia-portal`, Unterschiede dokumentiert |

Ein Ergebnis „geht nicht“ ist auch ein Ergebnis. Es kommt mit Begründung in `docs/entscheidungen.md` und, wenn es die Produktseite betrifft, in `docs/rueckmeldung-produktseite.md`.

### 1.3 Rechtlicher und fachlicher Rahmen (Recherche 25.10.2026)

- **Cyber Resilience Act**, Verordnung (EU) 2024/2847, Anhang I Teil II Nr. 1: Hersteller erstellen eine SBOM „in einem gängigen maschinenlesbaren Format“, „aus der zumindest die obersten Abhängigkeiten der Produkte hervorgehen“. Pflicht für Produkte, die ab dem **11.12.2027** in Verkehr gebracht werden; Meldepflichten gelten seit 11.09.2026. Die SBOM gehört zur technischen Dokumentation (Anhang VII), muss nicht veröffentlicht werden, geht nur auf begründetes Verlangen an die Marktüberwachung und wird mindestens 10 Jahre aufbewahrt (Art. 13 Abs. 13).
- Die SBOM-Definition (Art. 3 Nr. 39) umfasst nur **Softwareelemente**. Hardware ist Kontext, nicht Pflichtinhalt.
- Leitlinie der Kommission C(2026) 5252 vom 27.07.2026: Maschinen sind Produkte mit digitalen Elementen, der Integrator ist Hersteller des Gesamtprodukts.
- Art. 13 Abs. 24 erlaubt der Kommission, Format und Elemente der SBOM per Durchführungsrechtsakt festzulegen. Einen Entwurf gibt es nicht. **Deshalb formatoffen bauen**: Writer austauschbar, SPDX später möglich.
- Maschinenverordnung (EU) 2023/1230, Anhang III 1.1.9, ab 20.01.2027: Die Maschine muss die für den sicheren Betrieb erforderliche installierte Software kenntlich machen. Ein zweiter Grund für ein Software-Inventar je Maschine.
- **BSI TR-03183-2 v2.1.0** vom 20.08.2025: CycloneDX ab 1.6 oder SPDX ab 3.0.1, JSON oder XML. Schwachstellen in der SBOM sind verboten. Nicht bindend, wird aber in der Beschaffung verlangt.
- **NVD** führt CPEs nur je Gerätefamilie, nicht je Bestellnummer, und ergänzt seit 15.04.2026 die meisten neuen CVEs nicht mehr um CPEs. Siemens-CSAF nennt die Bestellnummer als `model_numbers`. Daraus folgt der Primärschlüssel Hersteller + Bestellnummer + Firmware-Version (Abschnitt 6).
- Keiner von 13 untersuchten Steuerungsherstellern gibt SBOMs je Produkt frei heraus (Werkspilot, 07.10.2026). Neun veröffentlichen CSAF-Advisories.

### 1.4 Positionierung

Wettbewerb: Der Complioty Scanner liest Projekte aus 20 Engineering-Umgebungen und schreibt CycloneDX 1.7 oder SPDX 2.3. Nicht dokumentiert sind dort Kommandozeile für die eigene Pipeline, Hierarchie Maschine → Station → Komponente, TR-Prüfung als Build-Gate, Zusammenführen mit Syft/cdxgen-SBOMs und Bindung an den Git-Commit.

An diesen Punkten misst sich jede Designentscheidung: **pipeline-nativ** (ein Aufruf, ein Exit-Code), **reproduzierbar** je Commit und Release, **TR-Prüfung als Gate**, **offenes Format** ohne Plattform-Abo, **Lücken sichtbar** statt aufgefüllt.

### 1.5 Was nicht dazugehört

Laut Produktseite und für den Prototyp:

- Keine Schwachstellenbewertung, kein CVE-Feld, keine VEX-Dokumente. Das macht ein Überwachungssystem wie Dependency-Track.
- Hersteller-SBOMs werden nicht ersetzt oder geschätzt.
- Kein eigenes Auslesen der Anlage im Netz. Den Ist-Stand liefern Herstellerwerkzeuge (bei Siemens etwa PRONETA oder der Webserver der CPU) als Datei.
- Keine Firmware-Binäranalyse, kein OT-Netzinventar.
- Keine Oberfläche über den HTML-Bericht hinaus.
- Kein Upload zu Dependency-Track im Werkzeug. Das übernimmt das Jenkins-Plugin in der Pipeline.
- Kein Schreiben in Engineering-Projekte. Openness öffnet nur lesend.
- Im Prototyp nicht: SPDX-Writer, CycloneDX 1.7, Signatur, CODESYS-Scripting, Logix Designer SDK, Openness für TIA vor V21, Auswertung von VCI-Dateien (`.libinfo`).

Eine SBOM allein erfüllt den CRA nicht. Das Werkzeug und seine Ausgaben werden nie als „CRA-konform“ bezeichnet.

## 2. Leitplanken (verbindlich)

- **Kein erfundener Wert.** Was eine Quelle nicht hergibt, bekommt keinen Platzhalter, sondern einen Lückenvermerk (Abschnitt 4.4). Kein Dummy-Hash, keine geratene Version, keine aus dem Typnamen geschätzte Firmware, keine aus dem Typnamen abgeleitete CPE. Was der Anwender in `machine.yml` erklärt, ist eine Quelle und wird als `declared` gekennzeichnet.
- **Jedes ausgelesene Feld ist durch eine Fixture belegt.** Bevor ein Adapter ein Attribut liest, liegt eine echte Beispieldatei mit diesem Attribut in `fixtures/` und ein Test, der es findet. Attributnamen nie aus dem Gedächtnis übernehmen. Openness-API-Namen, die nur aus Doku oder Skills stammen, sind in dieser Datei mit **[prüfen]** markiert.
- **Keine Schwachstellen in der SBOM** (TR-03183-2, MUST NOT).
- **Keine Netzdaten in der SBOM.** IP-Adressen, Subnetzmasken, PROFINET-Gerätenamen aus AML oder Openness werden nicht übernommen. Sie gehören nicht in eine SBOM und legen die Netzstruktur der Anlage offen.
- **Deterministische Ausgabe.** Gleiche Eingabe, gleiches Byte-Ergebnis (Abschnitt 8.4).
- **Offline.** Zur Laufzeit kein Netzzugriff. Build-Agenten stehen oft im OT-Segment. Schemas, Taxonomie und Regeln sind eingebettet.
- **XML sicher lesen.** Alle XML-Eingänge (L5X, AML, GSDML, tsproj, PLCopen) mit `DtdProcessing.Prohibit` und ohne `XmlResolver`. Eingaben sind fremde Dateien.
- **Plattformneutral.** Kern, dateibasierte Adapter und CLI laufen auf Windows und Linux (Entwicklung auch macOS). Windows-spezifischer Code nur im Openness-Worker (Abschnitt 5.5).
- **Fixtures nur mit klarer Lizenz einchecken** (MIT, Apache-2.0, 0BSD, BSD oder Comquent-eigen). Dateien ohne Lizenz nur lokal unter `fixtures/_local/` (gitignored).
- **Exit-Codes sind Schnittstelle** (Abschnitt 7.2) und werden nicht ohne Eintrag in `docs/entscheidungen.md` geändert.

## 3. Architektur und Tech-Stack

```
Jenkins / GitLab / Azure
        │  plc-sbom build --config machine.yml --release R2026.10
        ▼
┌──────────────────────────────────────────────┐
│ plc-sbom (Kern, .NET 10)                     │  machine.yml lesen, Adapter je Station,
│  Modell · Merge · Kennungen · Validator ·    │  Merge, Kennungen, Validierung,
│  CycloneDX-Writer · Bericht · Abgleich       │  Bericht, Ergebnisdatei, Exit-Code
└───────┬──────────────────────────────┬───────┘
        │ in-process                   │ Prozess, request.json / response.json
        ▼                              ▼
  Dateibasierte Adapter          plc-sbom-tia-openness.exe (ab M14)
  studio5000, twincat,           C#, net48, Windows, TIA Portal V21
  tia-aml, codesys               Schritte: doctor · inventory · export-aml
```

- **Dateibasierte Adapter laufen im Kernprozess.** Sie lesen XML, brauchen keine Engineering-Software und keine Lizenz.
- **Openness läuft als eigener Prozess.** TIA Openness V21 verlangt .NET Framework 4.8, die Openness-Firewall prüft den SHA-256 der Exe, und ein hängendes oder abgestürztes TIA darf den Kern nicht mitnehmen. Gleiche Begründung wie bei `plc-build` (dort ADR 0001). Das Protokoll (Abschnitt 5.5.2) orientiert sich an `plc-build` Abschnitt 5; eine Abhängigkeit auf `plc-build` gibt es nicht.
- **Ein Worker für zwei Zwecke:** Schritt `inventory` für den Adapter `tia-portal`, Schritt `export-aml` für den automatischen AML-Export, den danach `tia-aml` liest.
- **Adapter kennen CycloneDX nicht.** Sie liefern nur das interne Modell (Abschnitt 4.2).

| Baustein | Wahl | Begründung |
|---|---|---|
| Sprache | C# / **.NET 10** (LTS), `global.json` mit `10.0.100`, `rollForward: latestFeature` | Wie die Schwesterwerkzeuge. .NET 8 endet am 10.11.2026. |
| Worker | C#, **.NET Framework 4.8**, SDK-Projektformat | Vorgabe von TIA Openness V21 |
| CycloneDX | eigener Writer mit `System.Text.Json`; Schemaprüfung mit NuGet `NJsonSchema` 11.6.1 (MIT) gegen das eingebettete Schema CycloneDX 1.6.2 | `CycloneDX.Core` verworfen: Abhängigkeit JsonSchema.Net steht unter einer kostenpflichtigen EULA (E-008) |
| CLI | `System.CommandLine` | Unterbefehle, Hilfe, Exit-Codes |
| Konfiguration | `YamlDotNet` | `machine.yml` je Maschine |
| JSON | `System.Text.Json` | Feld-Dateien, Worker-Protokoll, Ergebnisdatei |
| XML | `System.Xml.Linq` mit sicheren `XmlReaderSettings` | L5X, AML, GSDML, tsproj/xti, plcproj, PLCopen XML |
| Tests | xUnit v3, Golden Files | Determinismus prüfbar |
| Gegenprüfung | `sbomqs` v2.1.3 (Interlynk, `compliance --bsi-v21`) als feldweiser Abgleich mit `tools/crosscheck-sbomqs.cs` (E-010), nur in CI; `cyclonedx-cli validate` folgt | Unabhängige Prüfung des eigenen Validators |
| CI | GitHub Actions, Matrix `ubuntu-latest` + `windows-latest` | Plattformneutralität belegen |

Versionen vor dem ersten `dotnet add package` auf nuget.org prüfen und zentral in `Directory.Packages.props` festhalten. Versionen der Schwesterprojekte als Ausgangspunkt (`../plc-git-bridge/Directory.Packages.props`). `Directory.Build.props` wie dort: `Nullable`, `TreatWarningsAsErrors`, `InvariantGlobalization`.

### 3.1 Repository-Struktur

```
plc-sbom-builder/
├── CLAUDE.md
├── README.md                       Überblick, Stand der Meilensteine, Bauen und Testen
├── ANLEITUNG.md                    Bedienungsanleitung für Anwender (Befehle, Ausgaben, Lückencodes, Pipeline)
├── TESTFAELLE.md                   alle Testfälle, Testläufe und Ergebnisse
├── THIRD-PARTY-NOTICES.md          Lizenzen aller ausgelieferten Abhängigkeiten
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── PlcSbom.sln                     Kern, CLI, Tests (baut überall)
├── src/
│   ├── PlcSbom.Core/
│   │   ├── Model/                  Machine, Station, Device, SoftwareItem, Gap
│   │   ├── Adapters/
│   │   │   ├── Studio5000/         L5X
│   │   │   ├── TwinCat/            tsproj, xti, plcproj
│   │   │   ├── TiaAml/             AML-Parser, Stationszuordnung
│   │   │   ├── Gsdml/              GSDML-Leser (von tia-aml und tia-portal genutzt)
│   │   │   ├── Codesys/            V35/ (PLCopen XML), V23/, C4/
│   │   │   └── TiaOpenness/        Client für den Worker: Prozessstart, Protokoll, Zeitlimit, PIDs
│   │   ├── Merge/                  Hersteller- und Teil-SBOMs einlesen und zusammenführen
│   │   ├── Identifiers/            Bestellnummer, CPE-Tabelle, CSAF-Index
│   │   ├── FieldState/             Ist-Stände einlesen, Abgleich
│   │   ├── Validation/             Schema, TR-03183-2-Regeln
│   │   └── Output/                 CycloneDX-Writer, Bericht (HTML, Markdown), Ergebnisdatei, CSV
│   └── PlcSbom.Cli/                Befehle, Exit-Codes, Konsolenausgabe
├── workers/
│   └── tia-openness/               eigene Solution, net48, baut nur auf Windows mit TIA V21 (M14)
│       ├── PlcSbom.TiaOpenness/            Exe: Protokoll, Schritte
│       ├── PlcSbom.TiaOpenness.Mapping/    Abbildung Openness-Daten → Modell, ohne Siemens-Typen, testbar
│       └── PlcSbom.TiaOpenness.Tests/
├── tests/
│   ├── PlcSbom.Tests/              xUnit, Golden Files unter golden/
│   └── live/                       nur mit PLC_SBOM_LIVE=1 auf dem Windows-Agenten (M14)
├── fixtures/                       echte Beispieldateien + FIXTURES.md (Herkunft, Commit, Pfad, Lizenz)
│   └── _local/                     gitignored, Dateien ohne Lizenz
├── samples/maschine-xy/            machine.yml, Hersteller-SBOMs, GSDML, Feld-Datei, erwartete Ausgabe
├── examples/Jenkinsfile
├── tools/Register-OpennessFirewall.ps1   ab M14
└── docs/
    ├── entscheidungen.md           kurze ADRs, datiert (Kontext, Entscheidung, Folgen)
    ├── regeln-tr03183.md           TR-Feld → CycloneDX-Pfad → Quelle im Werkzeug → Verhalten bei Lücke
    ├── adapter-<id>.md             was jeder Adapter liest und was nicht
    ├── messungen.md                Laufzeiten, Openness-Läufe
    └── rueckmeldung-produktseite.md
```

## 4. Fachliches Modell

### 4.1 Begriffe

| Begriff | Bedeutung |
|---|---|
| **Maschine** | Das Produkt, das in Verkehr gebracht wird. `metadata.component` der SBOM (Typ `device`), Version = Release-Kennung. |
| **Station** | Eine Steuerung mit allem, was an ihr hängt. Bei TIA: eine CPU und die Geräte ihres IO-Systems (Abschnitt 5.4.4). Genau ein Adapter je Station. |
| **Gerät** | Hardware mit Bestellnummer: CPU, Kommunikationsmodul, Interfacemodul, E/A-Modul, Antrieb, Klemme, Panel, Feldgerät. |
| **Firmware** | Software eines Geräts. In der SBOM eine Komponente vom Typ `firmware`. |
| **Passives Modul** | Gerät ohne eigene Firmware (TIA `FirmwareVersion` = `V0.0`, BaseUnits, einfache DI/DO) oder vom Anwender als passiv erklärt. Steht im Bericht (Hardware-Anhang), nicht in der SBOM. |
| **Anwenderprogramm** | Eigener Steuerungscode der Station. Version = Release + Git-Commit. |
| **Bibliothek** | Hersteller- oder Firmenbibliothek (LGF, Tc2_Standard, SM3_Basic). |
| **Bibliothekstyp** | Versionierter Typ einer TIA-Bibliothek (FB, FC, UDT) mit eigener Typversion. |
| **Geschützter Baustein** | Zugekaufter, know-how-geschützter Baustein eines Dritten. Mit Lieferant und Version, soweit bekannt. |
| **Laufzeit** | Runtime auf IPC oder Steuerung (TwinCAT Runtime, CODESYS Control). |
| **Soll-Stand** | Projektierter Wert aus dem Engineering-Projekt. |
| **Ist-Stand** | Im Feld ausgelesener Wert (Inbetriebnahme, Abnahme, Service). |
| **Lücke** | Pflichtangabe, die keine Quelle liefert. Wird vermerkt, nie aufgefüllt. |

### 4.2 Internes Modell (Skizze, im Code verfeinern)

```csharp
record Machine(string Name, string Release, string? Commit, string? SerialNumber,
               Supplier Supplier, IReadOnlyList<Station> Stations,
               IReadOnlyList<Device> UnassignedDevices);
record Station(string Name, string AdapterId, string SourcePath, string? SourceSha256,
               ToolInfo? EngineeringTool, IReadOnlyList<Device> Devices,
               IReadOnlyList<SoftwareItem> Software, IReadOnlyList<Gap> Gaps);
record ToolInfo(string Name, string Version, string? Release);         // z. B. TIA Portal, V21, 2100.0.121.1
record Device(string? Vendor, ValueOrigin VendorOrigin, string? OrderNumber, bool OrderNumberIsPattern,
              string TypeName, string? DeviceName, string? Position, string? Classification,
              string? Firmware, FirmwareOrigin FirmwareOrigin, bool IsPassive,
              IReadOnlyDictionary<string, string> VendorKeys);           // GSDML, VendorId, ProductCode ...
enum FirmwareOrigin { Projected, AsFound, NotInProject, Unknown }
enum ValueOrigin { Source, Gsdml, Declared, Assumed }
record SoftwareItem(SoftwareKind Kind, string Name, string? Vendor, string? Version,
                    bool VersionResolved, string? Library, ValueOrigin Origin, string? FilePath);
enum SoftwareKind { UserProgram, Library, LibraryType, ProtectedBlock, Runtime, AddOnInstruction }
record Gap(string Code, string ComponentRef, string Message);
```

### 4.3 Abbildung auf CycloneDX

Ausgabe: **CycloneDX 1.6 JSON**, Dateiname nach Vorgabe `sbom-maschine.cdx.json`.

Entschieden für M1: **Variante B** (`docs/entscheidungen.md` E-007). Die beiden Varianten zur Erinnerung:

- **Variante A, verschachtelt:** Station und Gerät als Komponenten vom Typ `device`, Firmware darunter. Lesbar, aber jede `device`-Komponente bräuchte BSI-Pflicht-Properties, die für Hardware keinen Sinn ergeben.
- **Variante B, Software-flach (Vorzug):** Nur Software-Komponenten (`firmware`, `application`, `library`). Gerät und Station als Properties an der Firmware, Struktur über `dependencies`. Deckt sich mit der CRA-Definition und mit dem Beispiel auf der Produktseite.

Der lesbare Baum Maschine → Station → Gerät → Firmware entsteht in jedem Fall im Bericht (Abschnitt 8.2).

**Bibliothekstypen** (nur `tia-portal`): eine Komponente vom Typ `library` je im Programm verwendetem Bibliothekstyp, Name = Typname, Version = Typversion, Zuordnung zur Bibliothek über `<ns>:library` (z. B. `LGF`). Abhängigkeit: Anwenderprogramm → Bibliothekstyp.

Beispiel der Produktseite (verkürzt, Zielbild):

```json
{
  "bomFormat": "CycloneDX", "specVersion": "1.6",
  "metadata": {
    "tools": { "components": [ { "type": "application", "name": "PLC SBOM Builder" } ] },
    "component": { "type": "device", "name": "Maschine XY", "version": "R2026.10" }
  },
  "components": [
    { "bom-ref": "plc1-fw", "type": "firmware", "supplier": { "name": "Siemens AG" },
      "name": "CPU 1511-1 PN, Firmware", "version": "V2.8",
      "cpe": "cpe:2.3:o:siemens:<cpu-typ>_firmware:2.8:*:*:*:*:*:*:*",
      "properties": [ { "name": "firma:device:orderNumber", "value": "6ES7 511-1AK02-0AB0" },
                      { "name": "firma:adapter", "value": "tia-aml" } ] },
    { "bom-ref": "plc1-app", "type": "application",
      "name": "Station PLC_1, Anwenderprogramm", "version": "R2026.10 (git a1b2c3d)" },
    { "bom-ref": "zelle-rt", "type": "application", "supplier": { "name": "Beckhoff Automation" },
      "name": "TwinCAT 3 Runtime", "version": "…" },
    { "bom-ref": "zelle-app", "type": "application",
      "name": "Roboterzelle, Anwenderprogramm", "version": "R2026.10 (git a1b2c3d)" }
  ],
  "dependencies": [
    { "ref": "plc1-app", "dependsOn": [ "plc1-fw" ] },
    { "ref": "zelle-app", "dependsOn": [ "zelle-rt" ] }
  ]
}
```

**Properties** im Namespace des Anwenders (`namespace` in `machine.yml`, Vorgabe `plcsbom`; `cdx:` ist laut Taxonomie für Eigenes verboten). Die Seite nennt Hersteller, Bestellnummer und Firmware-Version als Properties, über die sich CSAF-Advisories zuordnen lassen. Mindestens:

| Property | Inhalt |
|---|---|
| `<ns>:adapter` | `studio5000`, `twincat`, `tia-aml`, `codesys`, `tia-portal` |
| `<ns>:adapter:source` | Quellvariante des Adapters, bei `codesys` `v35`, `v23`, `c4` |
| `<ns>:station` | Stationsname |
| `<ns>:device:vendor` | Hersteller |
| `<ns>:device:vendor:origin` | `source`, `gsdml`, `devdesc`, `declared`, `assumed` |
| `<ns>:device:orderNumber` | Bestell- bzw. Katalognummer, unverändert aus der Quelle |
| `<ns>:device:orderNumber:pattern` | `true`, wenn die Quelle Platzhalter enthält (`6ES7 590-1***0-0AA0`) |
| `<ns>:device:type` | Typname aus der Quelle |
| `<ns>:device:name` | Gerätename im Projekt (z. B. `PLC_1`) |
| `<ns>:device:position` | Pfad aus Station, Rack, Steckplatz |
| `<ns>:device:classification` | `CPU`, `HeadModule` usw., soweit die Quelle es liefert |
| `<ns>:firmware:version` | Firmware-Stand wie in der Quelle |
| `<ns>:firmware:origin` | `projected`, `as-found`, `not-in-project`, `unknown` |
| `<ns>:vendorKey:<name>` | Herstellerschlüssel wie `GSDML`, `VendorId`, `DeviceId`, `ProductCode`, `RevisionNo` |
| `<ns>:gsdml:softwareRelease` | `SoftwareRelease` aus der GSDML; beschreibt die GSDML, **nicht** die Firmware |
| `<ns>:library` | Bibliothek eines Bibliothekstyps |
| `<ns>:library:reference`, `<ns>:library:resolution`, `<ns>:library:company` | CODESYS: Referenz wie im Export (`#SM3_Basic`), Auflösung (`fixed`, `redirection`, `default`, `none`), Herstellerangabe der Referenz |
| `<ns>:version:requested` | angefragte, nicht aufgelöste Version (`*`, Standardauflösung eines Platzhalters) |
| `<ns>:gap` | Lückencode, mehrfach möglich |
| `<ns>:source` | Quelldatei relativ zum Repository |
| `<ns>:source:sha256` | SHA-256 der Quelldatei |
| `<ns>:source:exportedAt` | Exportzeitpunkt aus der Quelle (AML `LastWritingDateTime`) |

**bom-ref**: stabil und lesbar aus Station + Art + Position bzw. Name (`plc1-fw`, `plc1-app`, `plc1-lib-lgf-blinker`), eindeutig über die ganze Maschine. Komponenten ohne CPE bekommen damit eine eindeutige Kennung.

### 4.4 Lückencodes

| Code | Wann | Schwere |
|---|---|---|
| `firmware-not-in-project` | Projekt führt keine Firmware (EtherCAT-Klemme, GSD-Gerät, TIA-Gerät ohne `FirmwareVersion`) | Hinweis; Warnung bei TIA-Gerät ohne Attribut |
| `firmware-projected-only` | Firmware nur als Sollwert, kein Ist-Stand eingelesen | Hinweis |
| `hash-unavailable` | Kein SHA-512 der ausgelieferten Form möglich (Hersteller-Firmware) | Hinweis |
| `filename-unavailable` | Komponente liegt nicht als Datei vor, kein Dateiname bestimmbar | Hinweis |
| `program-source-missing` | Anwenderprogramm ohne `program` in `machine.yml`, kein Hash | Warnung |
| `file-properties-unknown` | Unbekannt, ob ausführbar, Archiv oder strukturiert | Warnung |
| `creator-contact-unknown` | Für den Hersteller weder E-Mail noch URL bekannt | Warnung |
| `version-unresolved` | Bibliotheksversion `*` bzw. „neueste installierte“ | Warnung |
| `libraries-not-read` | Quelle enthält keine Bibliotheken (`tia-aml`) und keine sind deklariert | Warnung |
| `runtime-version-unknown` | Laufzeitversion steht nicht im Projekt (TwinCAT) | Warnung |
| `ekey-disabled` | Rockwell: elektronische Kodierung aus, Feld kann beliebig abweichen | Warnung |
| `license-unknown` | Keine Distributionslizenz bekannt | Warnung |
| `vendor-sbom-missing` | Für eine Firmware liegt keine Hersteller-SBOM vor | Hinweis |
| `protected-block-unknown` | Geschützter Baustein ohne bekannten Lieferanten oder Version | Warnung |
| `gsdml-not-found` | GSDML-Datei aus dem `TypeIdentifier` liegt in keinem `gsdml`-Verzeichnis | Warnung |
| `devdesc-not-found` | CODESYS: zur Gerätekennung keine oder mehrere Gerätebeschreibungen in den `deviceDescriptions`-Verzeichnissen | Warnung |
| `order-number-not-in-project` | Gerät ohne Bestellnummer in Projekt, GSDML bzw. Gerätebeschreibung | Warnung |
| `supplier-unknown` | Hersteller nicht ermittelbar | Warnung |
| `device-unassigned` | Gerät gehört zu keiner Station (kein IO-System, nicht in `devices`) | Hinweis |
| `program-not-read` | Quelle enthält keine Bausteine (`tia-aml`); geschützte Bausteine unbekannt | Hinweis |

Vollständigkeit zusätzlich über `compositions[].aggregate` (`incomplete`, `unknown`), wie TR §8.2 es für die Vollständigkeit der Abhängigkeiten vorsieht.

**Schweregrade:**

- **Fehler**: Schema verletzt, Pflichtfeld fehlt ohne Lückenvermerk, Eingabe-SBOM unlesbar. Immer Exit 1.
- **Warnung**: Pflichtangabe fehlt, Lücke ist vermerkt. Im Entwicklungs-Build Exit 0, mit `--release-build` Exit 1.
- **Hinweis**: Lücke, die die TR ausdrücklich zulässt (etwa Hash nicht verfügbar, §3.2.1) oder die erst der Ist-Stand schließt. Ändert den Exit-Code nie.

Die Zuordnung Code → Schwere steht in `docs/regeln-tr03183.md` und wird dort mit der TR-Stelle begründet. Die Tabelle oben ist der Startpunkt, nicht das Ergebnis.

## 5. Adapter

Reihenfolge der Umsetzung: `codesys` (V3.5, dann V2.3, dann CODESYS 4) → `studio5000` → `twincat` → `tia-aml` → `tia-portal`.

Vor jedem Adapter: Fixture besorgen, `docs/adapter-<id>.md` mit der Feldtabelle anlegen, dann Test, dann Code. Die Tabellen unten sind Stand der Recherche bzw. der geprüften Fixtures und werden bei jeder neuen Fixture überprüft.

### 5.1 CODESYS: `codesys` (M2–M4)

Ein Adapter für drei Generationen, unterschieden über `source` in `machine.yml`: `v35` (CODESYS V3.5), `v23` (CODESYS V2.3) und `c4` (CODESYS 4). Umsetzung in dieser Reihenfolge: zuerst V3.5, weil Format und Beispieldateien belegt sind, dann V2.3, dann CODESYS 4. V2.3 und CODESYS 4 beginnen je mit einem Spike, der das Eingangsformat an echten Dateien klärt; erst danach entsteht der Adapter.

Für alle drei gilt: Die Version einer Gerätebeschreibung oder Zielsystemdatei ist **nicht** die Firmware des Geräts (`firmware:origin = unknown`). Die Engineering-Version (z. B. „CODESYS V3.5 SP21“, „CODESYS V2.3.9.x“, „CODESYS 4 1.0.0.0“) kommt in `metadata.tools`. Hersteller-IDEs auf CODESYS-Basis (etwa WAGO-I/O-PRO auf V2.3) laufen über dieselbe Quelle; der Name der IDE kommt zusätzlich in `metadata.tools`.

#### 5.1.1 CODESYS V3.5: Quelle `v35` (M2)

Eingang: PLCopen-XML-Export des ganzen Projekts (die `.project`-Datei ist binär). Umgesetzt mit M2; vollständige Feldtabelle, Regeln und Fixtures in `docs/adapter-codesys.md`.

| Datum | Quelle | Hinweis |
|---|---|---|
| Steuerung | Konfiguration der obersten Ebene mit `addData` → `Device/DeviceType/DeviceIdentification` (Type, Id, Version) | Version ist die der **Gerätebeschreibung**, nicht die Firmware → `firmware:origin = unknown`, als `<ns>:vendorKey:DeviceDescriptionVersion`. Kein Produktname, Hersteller, Bestellnummer im Export → Typname `Gerät <Type>/<Id>`, Lücken `order-number-not-in-project`, `supplier-unknown` (E-014) |
| Name, Hersteller, Bestellnummer | `*.devdesc.xml` aus `deviceDescriptions`: `Device/DeviceIdentification` (exakt Type, Id, Version) → `DeviceInfo/Name`, `/Vendor`, `/OrderNumber` | nur bei genau einer passenden Datei; `vendor:origin = devdesc`, Dateiname und SHA-256 als `vendorKey`. Bestellnummer `-` gilt als fehlend. Keine oder mehrere Dateien → `devdesc-not-found` (E-015) |
| Untergeordnete Geräte | verschachtelte Konfigurationen mit Gerätedaten unter `data[@name="configurations"]` | bisher nur synthetisch belegt |
| Bibliotheken | `…/plcopenxml/libraries` je Anwendung (`resource`) und im POU-Pool; `Library@Name`, `@DefaultResolution`, `PlaceholderRedirection` | feste Version nur bei direkter Referenz oder Umleitung; Platzhalter nur mit Standardauflösung und `*` → `version-unresolved`, Standardversion als `<ns>:version:requested` (E-012). Hersteller `System` ist kein Hersteller (E-013) |
| Engineering-Version | `fileHeader@productName`, `@productVersion` | in `metadata.tools` („CODESYS“, „V3.5 SP15“) |

Mehrere Steuerungen in einem Export: `plc` in `machine.yml` wählt eine über den Konfigurationsnamen, sonst Exit 2 (`CODESYS_PLC_REQUIRED`).

Nach dem Prototyp: CODESYS-Scripting mit `--noUI --runscript` (`get_device_identification()`, `librarymanager.references` mit `effective_resolution`), dann kommen Bibliotheken direkt aus dem Bibliotheksverwalter, wie die Produktseite es beschreibt.

#### 5.1.2 CODESYS V2.3: Quelle `v23` (M3)

Gemeint ist die Linie CODESYS V2.3 (letzte Version 2.3.9.x) und darauf aufbauende Hersteller-IDEs; eine Version 2.5 gibt es nicht. Die Projektdatei `.pro` ist binär und nicht öffentlich beschrieben, öffentliche Beispielprojekte mit Lizenz gibt es nicht. Comquent liefert ein Demo-Projekt (Abschnitt 13).

**Spike (M3a).** Ergebnis als ADR in `docs/entscheidungen.md` und als Feldtabelle in `docs/adapter-codesys.md`. Zu klären ist, welcher Weg Steuerung bzw. Zielsystem, Steuerungskonfiguration (E/A, Feldbusgeräte) und Bibliotheken liefert:

| Weg | Eingang | Zu prüfen | Bewertung |
|---|---|---|---|
| A | Textexport aus V2.3 (Projekt → Exportieren, `.exp`) | Welche Teile enthält der Export: Bausteine, Bibliotheksverweise, Steuerungskonfiguration, Zielsystemeinstellungen? **[prüfen]** | Vorzug, wenn Bibliotheken und Zielsystem enthalten sind; plattformneutral |
| B | PLCopen-XML-Export, falls die eingesetzte V2.3-Version ihn anbietet **[prüfen]** | wie A; Abgleich mit dem V3.5-Parser | Vorzug, wenn vorhanden; Parser teilweise wiederverwendbar |
| C | Umwandlung mit dem „CODESYS V2.3 Converter“ in CODESYS V3.5 (ab V3.5 SP5), danach Quelle `v35` | Welche Angaben ändert die Konvertierung (Gerät wird ersetzt, Bibliotheken werden neu zugeordnet)? | Nur wenn A und B nicht tragen; braucht Windows mit CODESYS V3.5; Ergebnis mit `<ns>:adapter:source = v23-converted` kennzeichnen |
| D | `.pro` direkt lesen | | Nicht ohne ausdrückliche Freigabe; kein Nachbau eines undokumentierten Binärformats auf Verdacht |

An der Fixture zu belegen:

- Bibliotheken sind in V2.3 `.lib`-Dateien ohne Versionsauflösung wie in V3. Liefert die Quelle keine Version, gilt nach TR-03183-2 Abschnitt 5.2.2 das Änderungsdatum der Datei als Version (RFC 3339), aber nur, wenn die Bibliotheksdatei selbst vorliegt (Pfad unter `libraries` in `machine.yml`). Sonst `version-unresolved`.
- Das Zielsystem beschreibt die Steuerung (Name, Version der Zielsystemdatei), nicht deren Firmware.
- Feldbusgeräte der Steuerungskonfiguration stammen aus Gerätebeschreibungen (EDS, GSD); deren Version ist nicht die Firmware.

Fertig, wenn: Weg entschieden und begründet, Golden File aus dem Comquent-V2.3-Demo, Lücken korrekt.

#### 5.1.3 CODESYS 4: Quelle `c4` (M4)

Stand 31.10.2026: CODESYS 4 Version 1.0.0.0 ist seit dem 24.09.2026 verfügbar, als zweites Werkzeug neben CODESYS V3.5, nicht als Ersatz. Es nutzt denselben Compiler, dieselben Bibliotheken und dieselbe Laufzeit wie V3, speichert Projekte dateibasiert und lässt sich über die Kommandozeile aufrufen. Projekte aus V3.5 lassen sich nicht direkt öffnen. Die erste Version zielt vor allem auf Bibliotheksentwicklung; Visualisierung, Motion und Safety bleiben vorerst in V3.5. Neue Versionen sind alle sechs bis acht Wochen geplant. Das Dateiformat ist nicht öffentlich beschrieben.

**Spike (M4a).** Ergebnis als ADR und Feldtabelle wie bei V2.3. An einem Comquent-Demo-Projekt (Gerät, E/A, zwei Bibliotheken) klären:

- Wie ist die Projektablage aufgebaut (Dateien, Format, Text oder binär)? Wo stehen Gerät, Gerätebeschreibung und Bibliotheksreferenzen mit Version?
- Bietet die Kommandozeile einen Export, der stabiler ist als die interne Ablage, etwa PLCopen XML? Dann diesen Weg nehmen.
- Wie ändert sich das Format zwischen zwei CODESYS-4-Versionen? Dafür das Demo mit zwei aufeinanderfolgenden Versionen speichern.

Regeln für den Adapter:

- Die CODESYS-4-Version der Quelle steht in `metadata.tools`. Unbekannte Versionen ergeben eine Warnung, keinen Abbruch; je unterstützter Version eine Fixture.
- Felder, die das Format nicht belegt, werden nicht gelesen, sondern als Lücke vermerkt.

Fertig, wenn: Weg entschieden, Golden File aus dem Comquent-Demo, Versionsprüfung mit Warnung getestet.

### 5.2 Rockwell Studio 5000: `studio5000` (M5)

Eingang: vollständiger L5X-Export (Studio 5000 „Speichern unter … L5X“).

| Datum | Quelle in L5X | Hinweis |
|---|---|---|
| Steuerung | `Controller@ProcessorType`, `@MajorRev`, `@MinorRev` | Firmware-Soll = Major.Minor |
| Module | `Module@CatalogNumber`, `@Vendor`, `@ProductType`, `@ProductCode`, `@Major`, `@Minor`, `@ParentModule` | `Vendor="1"` = Rockwell |
| Kodierung | `Module/EKey@State` | `Disabled` → `ekey-disabled` |
| Add-On Instructions | `AddOnInstructionDefinition@Name`, `@Revision`, `@RevisionExtension`, `@Vendor` | als `library` |

### 5.3 Beckhoff TwinCAT 3: `twincat` (M6)

Eingang: `.tsproj` (oder `.xti`-Dateien) plus `.plcproj` je SPS-Projekt. Ohne Start der Umgebung.

| Datum | Quelle | Hinweis |
|---|---|---|
| EtherCAT-Teilnehmer | `Box`-Elemente mit `EtherCAT@VendorId`, `@ProductCode`, `@RevisionNo`, Typname (z. B. `EK1100`) | Hex-Werte (`#x00000002`) normalisieren |
| Firmware der Klemmen | **nicht im Projekt** | `firmware-not-in-project`; Ist-Stand nur aus dem Feld (CoE 0x100A) |
| XAE-Version | `TcSmProject@TcVersion` | Engineering-Version, **nicht** Runtime; Runtime → `runtime-version-unknown` |
| Bibliotheken | `.plcproj`: `PlaceholderReference/DefaultResolution` („Tc2_Standard, * (Beckhoff Automation GmbH)“), feste Version in `PlaceholderResolution/Resolution` | `*` → `version-unresolved` |
| Antriebe | teils `ProductRevision` (z. B. `AX5203-0000-0214`) | nur übernehmen, wenn vorhanden |

### 5.4 Siemens TIA Portal V17–V21, AML-Export: `tia-aml` (M7, M8)

Liest die AutomationML-Datei aus dem CAx-Export von TIA Portal. Kein TIA, keine Lizenz, läuft auf jedem Agenten.

#### 5.4.1 Eingang

- **Herkunft, zwei Wege:**
  1. *Eingecheckt (Standard):* Der Entwickler exportiert in TIA über Extras → CAx-Daten exportieren und checkt die Datei ein (z. B. `export/Linie1.aml`).
  2. *Automatisch:* `plc-sbom export-aml` lässt den Openness-Worker die Datei in der Pipeline aus dem Projekt erzeugen (Abschnitt 5.5.5). Danach liest `tia-aml` sie wie eine eingecheckte Datei.
- Eine AML-Datei kann ein ganzes Projekt mit mehreren CPUs enthalten. Mehrere Stationen dürfen auf dieselbe Datei zeigen; sie wird einmal gelesen.
- **Format, belegt durch Fixtures aus V17, V18 und V21:** `CAEXFile@SchemaVersion="2.15"`, kein XML-Namespace, `AdditionalInformation AutomationMLVersion="2.0"`, Dokument `AR APC` Version 1.2.0. Den Parser trotzdem namespace-unabhängig schreiben (CAEX 3.0 hat einen Namespace) und über lokale Namen arbeiten.
- **Versionsprüfung** über `WriterHeader`:
  - `WriterName` = `Totally Integrated Automation Portal`, sonst Exit 2 („keine AML-Datei aus TIA Portal“).
  - `WriterVersion` `1700` … `2100` → TIA V17 … V21. Andere Werte: Warnung, Verarbeitung läuft weiter.
  - `WriterRelease` (z. B. `2100.0.121.1`) in `metadata.tools` als Version von „TIA Portal“.
  - `LastWritingDateTime` → `<ns>:source:exportedAt` und in den Bericht, damit ein veralteter Export auffällt.

#### 5.4.2 Elemente erkennen

`InternalElement` ist tief verschachtelt. Den Baum rekursiv durchlaufen und Elemente über ihre Rolle erkennen, nicht über feste Pfade. Die Rolle steht in `SupportedRoleClass@RefRoleClassPath` (in allen geprüften Dateien; `RoleRequirements` zusätzlich tolerieren). Vergleich auf den letzten Pfadteil:

| Rolle (`AutomationProjectConfigurationRoleClassLib/…`) | Bedeutung | Verwendung |
|---|---|---|
| `AutomationProject` | Projekt | Projektname für den Bericht |
| `Device` | Station bzw. Gerät (S7-1500-Station, ET 200SP, G120C, GSD-Gerät) | Wurzel eines Geräts |
| `DeviceItem` | Rack, Modul, Teilmodul | Kandidat für Gerät/Firmware |
| `CommunicationInterface`, `CommunicationPort`, `Node` | Schnittstellen, Ports, Netzknoten | nur für die Stationszuordnung |
| `IoSystem` | PROFINET-IO-System einer Steuerung | Stationszuordnung |
| `Subnet` | Subnetz | ignorieren |
| `DeviceUserFolder` | Ordner, z. B. „Ungrouped devices“ | durchlaufen |
| `TagTable`, `TagUserFolder` | Variablentabellen | ignorieren |

#### 5.4.3 Attribute und Regeln

Attribute stehen als `Attribute@Name` mit Kind-Element `Value`.

| Attribut | Beispiele aus Fixtures | Regel |
|---|---|---|
| Element-`Name` | `PLC_1`, `IO_Device_Main`, `Pump_Water` | → `<ns>:device:name`; Steuerzeichen (in einer Fixture `\t_0`) entfernen |
| `TypeIdentifier` | `OrderNumber:6ES7 511-1AK02-0AB0`, `System:Rack.S71500`, `GSD:GSDML-V2.41-LEUZE-BCL248I-20211213.XML/DAP/DAP` | Präfix entscheidet, siehe unten |
| `TypeName` | `CPU 1511-1 PN`, `IM 155-6 PN HF`, `G120C PN` | Komponentenname: `<TypeName>, Firmware` |
| `FirmwareVersion` | `V2.8`, `V3.3`, `V0.0`, `4.7.10` (G120C, ohne „V“) | unverändert übernehmen; nur für Vergleiche normalisieren (führendes `V` weg) |
| `DeviceItemType` | `CPU`, `HeadModule` | → `<ns>:device:classification`; `CPU` markiert Stationswurzeln |
| `PositionNumber` | `0`, `1`, `32768` | Teil von `<ns>:device:position` |
| `BuiltIn` | `true` bei integrierten DI/DQ, HSC, OPC UA, Schnittstellen | `true` → Teil des übergeordneten Geräts, keine eigene Komponente |
| `InstallationDate`, `Label`, `IoType`, `StartAddress`, `Length` | | ignorieren |
| `NetworkAddress`, `SubnetMask`, `ProfinetDeviceName`, `IpProtocolSelection` | | **nie übernehmen** (Leitplanke Netzdaten) |

**`TypeIdentifier` auswerten:**

- `OrderNumber:<MLFB>[/<Version>][//<Zusatz>]`: Siemens-Katalogeintrag. Präfix abtrennen, Bestellnummer bis zum ersten `/` mit Leerzeichen unverändert übernehmen. Ein `/V…`-Anhang (in Katalog-Kennungen üblich, in den AML-Fixtures nicht vorhanden) wird als `<ns>:vendorKey:catalogVersion` mitgeführt; weicht er von `FirmwareVersion` ab, Warnung. Enthält die Bestellnummer `*` (Profilschiene `6ES7 590-1***0-0AA0`), `orderNumber:pattern = true`; die CPE-Tabelle behandelt `*` in der Quelle nicht als Platzhalter.
- `System:Device.<Familie>`, `System:Rack.<Familie>`: Struktur, keine Komponente.
- `GSD:<GSDML-Datei>/<Art>/<Id>`: Gerät aus einer GSDML-Datei. Beobachtete Arten: `D` (Gerät), `R/DAP` (Rack), `DAP/DAP` (Kopfmodul, Device Access Point), `M/<ModulId>` (Modul). Weiter in 5.4.5.
- Racks (`TypeName` = `Rack`, Profilschienen) sind Struktur, auch wenn sie eine Bestellnummer tragen; sie kommen in den Hardware-Anhang.

**Firmware und passive Module:**

| Fall | Ergebnis |
|---|---|
| `FirmwareVersion` vorhanden, nicht `V0.0` | Firmware-Komponente, `firmware:origin = projected`, Lücke `firmware-projected-only` bis zum Ist-Stand |
| `FirmwareVersion` = `V0.0` | passives Modul → Hardware-Anhang |
| Attribut fehlt (Fixture: Stromversorgung PM 70W `6EP1332-4BA00`) | Firmware-Komponente ohne Version, Lücke `firmware-not-in-project`, **Warnung**. Passt die Bestellnummer auf ein Muster in `passiveOrderNumbers` (`machine.yml`), stattdessen passiv mit `<ns>:device:passive = declared` |
| `BuiltIn` = `true` | keine eigene Komponente |

**Hersteller:** Für `OrderNumber:`-Geräte `Siemens AG`, `vendor:origin = assumed`. Begründung: `OrderNumber:`-Kennungen stammen aus dem Siemens-Hardwarekatalog bzw. Siemens-HSPs; Geräte Dritter kommen über GSD. Annahme in `docs/adapter-tia-aml.md` festhalten und an jeder neuen Fixture prüfen. `vendorOverrides` in `machine.yml` (Muster → Hersteller) überschreibt mit `origin = declared`. GSD-Geräte: Abschnitt 5.4.5.

**Antriebe und Panels:** Die V18-Fixture enthält zwei SINAMICS G120C mit Bestellnummer und Firmware `4.7.10`; Antriebe werden also wie andere Geräte behandelt. Für HMI-Panels liegt noch keine Fixture vor; das Comquent-Demo-Projekt (Abschnitt 13) nimmt eines auf, danach entscheiden.

**Bibliotheken und Programm:** Der CAx-Export enthält weder Bibliotheken noch Bausteine.

- Ohne Angabe: Lücke `libraries-not-read` (Warnung) und `program-not-read` (Hinweis) an der Station.
- Mit `libraries` in der Station (`machine.yml`): je Eintrag eine `library`-Komponente mit `<ns>:source = declared`; `libraries-not-read` entfällt.
- Das Anwenderprogramm entsteht wie bei allen Adaptern aus `program` (Abschnitt 5.6).
- VCI-Dateien (`.libinfo`) als Quelle für Bibliothekstypen erst nach dem Prototyp prüfen, nur mit Fixture.

#### 5.4.4 Stationszuordnung

Eine Station ist **eine CPU und die Geräte ihres IO-Systems**.

1. Stationswurzeln sind `DeviceItem`s mit `DeviceItemType = CPU`. In `machine.yml` wählt `plc: <Name>` die CPU über den Element-Namen. Enthält die Datei genau eine CPU, darf `plc` fehlen; bei mehreren ohne `plc` Exit 2 mit Liste der gefundenen CPUs.
2. Zur Station gehören das `Device` der CPU (Rack, Module, Stromversorgung) und alle Geräte, deren Schnittstelle über ein `InternalLink` „Link To IoSystem“ mit dem `IoSystem` unter einer Schnittstelle dieser CPU verbunden ist. Die Fixture `CIP_cs_HardwareNetwork.aml` (V18) hat sieben solche Links; welche Seite (`RefPartnerSideA`/`B`, Form `<ID>:<Schnittstellenname>`) zum Gerät und welche zum IO-System gehört, an der Fixture belegen und in `docs/adapter-tia-aml.md` festhalten.
3. `devices: [<Name oder Muster>]` in der Station nimmt weitere Geräte auf, die an keinem IO-System hängen (z. B. ein Panel nur am Subnetz).
4. Alle übrigen Geräte der Datei kommen nur dann in die SBOM, wenn keine Station der Maschine auf diese Datei ein `plc` setzt (dann ist die ganze Datei eine Station). Sonst gehen sie unter die Maschine mit `device-unassigned` (Hinweis), damit nichts unbemerkt fehlt. Mit `ignoreDevices: [<Muster>]` lassen sie sich bewusst ausschließen; der Bericht führt sie dann als ausgeschlossen.
5. Ein Gerät, das zwei Stationen beansprucht, ergibt Exit 2.

`plc-sbom inspect <datei.aml> --adapter tia-aml` gibt CPUs, IO-Systeme und die Zuordnung aller Geräte aus. Damit schreibt der Anwender seine `machine.yml`.

#### 5.4.5 GSD-Geräte und GSDML-Dateien

Im AML steht für GSD-Geräte nur `TypeIdentifier` (GSDML-Dateiname, Art, Id) und `TypeName`, keine Bestellnummer, kein Hersteller, keine Firmware (belegt durch Fixture `sample_device.aml`, Leuze BCL248i).

- `machine.yml` nennt unter `gsdml` Verzeichnisse mit GSDML-Dateien (im Repository der Maschine oder auf dem Agenten). Gesucht wird der Dateiname aus dem `TypeIdentifier`, ohne Groß-/Kleinschreibung.
- Aus der GSDML-Datei (PROFINET GSDML, XML) lesen: `DeviceIdentity@VendorID` und `@DeviceID`, `DeviceIdentity/VendorName@Value` (Hersteller), das `DeviceAccessPointItem` mit der `ID` aus dem `TypeIdentifier` und daraus `ModuleInfo/OrderNumber@Value`, `ModuleInfo/SoftwareRelease@Value`, `ModuleInfo/HardwareRelease@Value`; für Module das `ModuleItem` mit der Modul-Id. Element- und Attributnamen an einer echten GSDML-Fixture belegen, bevor sie im Code stehen.
- Ergebnis: Hersteller mit `vendor:origin = gsdml`, Bestellnummer aus der GSDML, `VendorID`/`DeviceID` und Dateiname als `vendorKey`, SHA-256 der GSDML-Datei als `<ns>:vendorKey:gsdmlSha256`.
- **`SoftwareRelease` ist nicht die Firmware des Geräts**, sondern der Stand, den die GSDML beschreibt. Er geht nur als `<ns>:gsdml:softwareRelease` in die SBOM. Die Firmware bleibt `firmware-not-in-project`, bis der Ist-Stand vorliegt.
- GSD-Module (`M/…`) ohne eigene Firmware-Angabe sind passiv und kommen in den Hardware-Anhang.
- Datei nicht gefunden: `gsdml-not-found` und `order-number-not-in-project` (Warnungen), Hersteller `supplier-unknown`, außer `vendorOverrides` greift.
- Der GSDML-Leser liegt in `Adapters/Gsdml/` und wird später auch von `tia-portal` genutzt.

#### 5.4.6 Fehlerfälle

| Fall | Exit |
|---|---|
| Datei fehlt, kein gültiges XML, nicht von TIA Portal geschrieben | 2 |
| `plc` nicht gefunden, mehrere CPUs ohne `plc`, Gerät in zwei Stationen | 2 |
| Unbekannte `WriterVersion`, unbekannte Rolle, unbekannter `TypeIdentifier`-Präfix | Warnung, weiter |

### 5.5 Siemens TIA Portal V21 über Openness: `tia-portal` (M14)

Der Weg, den die Produktseite beschreibt: Geräte, Artikelnummern und Firmware-Stände über TIA Portal Openness, dazu Bibliothekstypen wie die LGF mit Typversion. Kommt im Prototyp als letzter Adapter (M14). Vor jeder Funktion die Seite im Openness-Handbuch V21 und den passenden Siemens-Skill lesen; alles mit **[prüfen]** ist dort noch nicht belegt.

#### 5.5.1 Worker

- Exe `plc-sbom-tia-openness.exe`, C#, net48, SDK-Projektformat. Referenzen aus `<TIA-Pfad>\PublicAPI\V21\net48\` (`Siemens.Engineering.Base.dll` immer, Fachbereiche wie `Siemens.Engineering.Step7.dll` je nach Bedarf), Pfad über Registry, nie fest verdrahtet. DLLs nicht ins Repository und nicht ins Auslieferungspaket.
- Assembly-Auflösung wie bei `plc-build` (`../plc-build-tool/CLAUDE.md` Abschnitt 7.2): Resolver-Paket Version 2, Registrierung in einer Methode ohne Siemens-Typen, Openness-Code in eigener Methode mit `[MethodImpl(MethodImplOptions.NoInlining)]`.
- Abbildung Openness → Modell in `PlcSbom.TiaOpenness.Mapping` ohne Siemens-Typen, damit sie ohne TIA testbar ist. Der Worker liest nur in eigene, einfache Datenklassen (Skill `performance-and-caching`: Openness hat keinen Cache, jeder Zugriff ist ein Prozessaufruf).
- Start: `new TiaPortal(TiaPortalMode.WithoutUserInterface)`, eine Instanz je Lauf.

#### 5.5.2 Protokoll

Der Kern schreibt `request.json`, startet den Worker mit dem Pfad als Argument und erwartet `response.json`. Fehlt die Antwort, ist sie ungültig oder läuft das Zeitlimit ab: Exit 4. Der Exit-Code des Worker-Prozesses wird nur protokolliert. Arbeitsverzeichnis: `build/.plc-sbom/<station>/` mit `request-<schritt>.json`, `response-<schritt>.json`, `pids.json`, `worker-<schritt>.log`, `breadcrumb-<schritt>.log`.

```json
{
  "protocol": 1,
  "step": "inventory",
  "project": { "path": "C:\\ws\\repo\\tia\\Linie1.zap21", "workDir": "C:\\ws\\repo\\build\\.plc-sbom\\PLC_2\\work",
               "allowUpgrade": false, "plc": "PLC_2", "devices": [], "ignoreDevices": [] },
  "globalLibraries": [ "C:\\ws\\repo\\libs\\LGF_V21.zal21" ],
  "export": null,
  "timeoutSeconds": 1800,
  "secrets": { "umacUserEnv": "PLC_SBOM_UMAC_USER", "umacPasswordEnv": "PLC_SBOM_UMAC_PASSWORD" }
}
```

Antwort: `protocol`, `step`, `outcome` (`ok` · `failed` · `environment` · `error`), `summary`, `durationMs`, `tool` (TIA-Version und installierte Optionen), `stations` (internes Modell als JSON, Schema in `docs/adapter-tia-portal.md`), `gaps`, `environmentIssues` (je `code`, `message`, Behebung), `processes` (PID, Name, Startzeit jedes gestarteten Prozesses), `artifacts` (bei `export-aml`: Pfad und SHA-256). Zuordnung `outcome` → Exit: `ok` 0, `failed` 1, `environment` 3, `error` 4. Geheimnisse stehen nie im Request, nur Namen von Umgebungsvariablen; nie loggen. Unbekannte `protocol`-Version: Exit 4.

#### 5.5.3 Schritt `inventory`

1. **Projekt öffnen, nur lesend.**
   - `.zap21`: `Projects.Retrieve(archiv, workDir)` in ein frisches Arbeitsverzeichnis.
   - `.ap21`: Projektordner zuerst nach `workDir` kopieren, dann `Projects.Open`. Nie das Original öffnen.
   - Ältere Version nur mit `allowUpgrade: true`, dann `OpenWithUpgrade`/`RetrieveWithUpgrade`, und nur auf der Kopie (die Hochrüstung schreibt auf die Platte). Sonst `outcome: failed` mit klarer Meldung.
   - UMAC-geschützte Projekte: `UmacDelegate` mit Daten aus den Umgebungsvariablen.
   - Nie `Project.Save()`.
2. **Geräte sammeln.** Alle drei Orte durchlaufen: `project.Devices`, `project.DeviceGroups` rekursiv über `.Groups`, `project.UngroupedDevicesGroup.Devices` (Skill `devices-and-hardware`). `DeviceItems` rekursiv; das V21-Extensions-Paket bietet `AllDeviceItems()` **[prüfen]**.
3. **Attribute lesen.** Je `DeviceItem` die Namen über `GetAttributeInfos()` ermitteln, dann **ein** `GetAttributes(names)` je Objekt; schlägt ein Name fehl, nur diesen einzeln nachlesen. Gelesen werden `Name`, `TypeIdentifier`, `OrderNumber`, `FirmwareVersion`, `TypeName`, `PositionNumber`, `Classification`, Kennzeichen für integrierte Teilmodule und für GSD-Geräte die GSD-Attribute (`GsdName`, `GsdType`, `GsdId`) **[prüfen]**. Gleiche Regeln wie `tia-aml` (5.4.3): `V0.0` passiv, Racks Struktur, `BuiltIn` keine eigene Komponente, Netzdaten nie.
   - Nicht `GetAttributes(AttributeAccessOptions)` verwenden (laut Skill `crash-diagnosis` harter Absturz bei G120C in V21, außerdem andere Attributmenge).
   - Nicht `GetAttribute("IsBuiltIn")` auf Dienst-Objekten von Antriebs-`DeviceItem`s aufrufen (Absturz in V21).
4. **Stationen bilden** wie in 5.4.4: CPU über `plc`, Geräte über das IO-System ihrer Schnittstelle (`NetworkInterface` → `IoControllers` → `IoSystem` → verbundene IO-Geräte) **[prüfen]**, dazu `devices`, Rest unter der Maschine mit `device-unassigned`.
5. **GSD-Geräte**: Bestellnummer und Hersteller, soweit Openness sie liefert; sonst mit dem GSDML-Leser aus 5.4.5 im Kern nachschlagen.
6. **Bibliothekstypen.** Je PLC-Software (`DeviceItem` → `SoftwareContainer` → `PlcSoftware`) alle Bausteine (`BlockGroup` rekursiv) und PLC-Datentypen (`TypeGroup` rekursiv) durchlaufen, je Objekt `GetService<LibraryTypeInstanceInfo>()` (Skill `global-library`). Ist der Dienst gesetzt: `LibraryTypeVersion.VersionNumber` und `LibraryTypeVersion.Parent` als `LibraryType` (Name). Eine Komponente je verwendetem Typ mit Typversion (Abschnitt 4.3). Instanzen aus Kopiervorlagen (MasterCopy) liefern `null` und sind keine Bibliothekstypen.
7. **Zugehörige Bibliothek** (`<ns>:library`) ermitteln, in dieser Reihenfolge, Ergebnis mit Herkunft:
   - Ordnerpfad des Typs in der Projektbibliothek (`TypeFolder` → `LibraryTypeUserFolder`, Traversal mit Parameter vom Basistyp `LibraryTypeFolder`, `Types` liefert nur direkte Kinder).
   - Abgleich mit den in `globalLibraries` genannten globalen Bibliotheken: `.zal21` über `GlobalLibraries.Retrieve(datei, tempDir, OpenMode.ReadOnly)`, sonst Kopie öffnen; Abgleich über Typname und Typversion, Kennung (`Guid`) **[prüfen]**.
   - Sonst `<ns>:library` leer und Hinweis im Bericht.
8. **Geschützte Bausteine.** Bausteine mit Know-how-Schutz (`IsKnowHowProtected` **[prüfen]**) als `ProtectedBlock` mit Kopfangaben `HeaderAuthor`, `HeaderFamily`, `HeaderName`, `HeaderVersion` **[prüfen]**. Lieferant über `protectedBlockVendors` in `machine.yml` (Muster auf Autor/Familie → Hersteller), sonst `protected-block-unknown`.
9. **Werkzeugversionen** in `tool`: TIA-Version mit Update und installierte Optionen, etwa über die installierte Software des TIA-Prozesses **[prüfen]**.
10. **Aufräumen:** Projekt schließen, `TiaPortal.Dispose()`, nur selbst gestartete PIDs beenden (PID + Startzeit). Nie `Dispose()` auf Handles aus `TiaPortal.GetProcesses()` (beendet in V21 die laufende Instanz).

Breadcrumb-Log im Append-Modus vor und nach jedem Openness-Aufruf in Schleifen (Skill `crash-diagnosis`): Bei einem TIA-Absturz meldet sich erst der nächste Aufruf mit einem Kanalfehler; der Breadcrumb zeigt den wahren Ort. Absturz → `outcome: error` mit letztem offenen Breadcrumb in `summary`.

#### 5.5.4 Umgebung und `doctor`

`plc-sbom doctor --adapter tia-portal` ruft den Worker mit Schritt `doctor` auf und prüft, ohne ein Projekt zu öffnen. Je Befund ein Code und der Schritt zur Behebung, Exit 3.

| Code | Prüfung | Hintergrund |
|---|---|---|
| `TIA_NOT_INSTALLED` | TIA Portal V21 installiert, `PublicAPI\V21\net48` vorhanden | Pfad über Registry ermitteln |
| `NOT_IN_OPENNESS_GROUP` | Konto in der lokalen Gruppe „Siemens TIA Openness“ | sonst `EngineeringSecurityException`; nach Aufnahme neu anmelden bzw. Dienst neu starten |
| `LOCAL_SYSTEM` | Prozess läuft nicht als LocalSystem | eigenes Dienstkonto verwenden |
| `FIREWALL_NOT_ALLOWED` | Worker-Exe dauerhaft in der Openness-Firewall freigegeben, Hash passt | Freigabe hängt an Pfad und SHA-256; Registry-Format aus der Handbuchseite „TIA Portal Openness firewall“, nicht raten; `tools/Register-OpennessFirewall.ps1` (Administrator), nach jedem Update erneut |
| `LICENSE_MISSING` | STEP 7 Professional verfügbar | Openness selbst braucht keine Lizenz, die Produkte schon; sicher nur durch Probe: TIA ohne UI starten, `LicenseNotFoundException` abfangen (`doctor --deep`) |
| `RUNNING_TIA_SAME_USER` | keine fremde TIA-Instanz desselben Kontos | Openness erreicht nur Prozesse desselben Benutzers |
| `LOW_MEMORY` | ≥ 16 GB RAM | Systemvoraussetzung V21 |

**Gerätebeschreibungen:** Die Produktseite verlangt auf dem Agenten dieselben HSP, GSDML und ESI wie an den Arbeitsplätzen. Fehlt ein HSP, kann TIA das Gerät nicht vollständig darstellen. Ob Openness die installierten HSP auflisten kann, ist offen **[prüfen]**; wenn ja, Code `HSP_MISSING` in `doctor`. Sonst im Schritt `inventory`: Geräte ohne `TypeIdentifier` bzw. ohne Bestellnummer als `environmentIssues` mit Code `DEVICE_DESCRIPTION_MISSING` melden (Exit 3), nicht als Lücke.

Betriebsmodell des Agenten (Dienst unter eigenem Konto oder angemeldete Sitzung) wie bei `plc-build` testen und in `docs/messungen.md` festhalten.

#### 5.5.5 Schritt `export-aml`

Für den automatischen Weg des Adapters `tia-aml`:

```text
plc-sbom export-aml --project tia/Linie1.zap21 --out export/Linie1.aml [--allow-upgrade]
```

- Projekt öffnen wie in 5.5.3, dann CAx-Export über den Dienst `CaxProvider` des Projekts mit Exportdatei und Protokolldatei **[prüfen: Dienst, Methode, Parameter im Handbuch V21]**.
- Das CAx-Protokoll auswerten: Fehler → `outcome: failed` (Exit 1), Warnungen in `summary`.
- Ergebnis in `artifacts` mit SHA-256. Die Pipeline ruft danach `plc-sbom build` auf, `tia-aml` liest die frische Datei.
- Gleicher Worker, gleiche Voraussetzungen und `doctor` wie `inventory`.

#### 5.5.6 Fertig, wenn

H5 ist beantwortet: Comquent-Demo-Projekt über `tia-aml` (exportiert mit `export-aml`) und über `tia-portal` gelesen, Geräteliste identisch oder jede Abweichung erklärt, Bibliothekstypen mit Typversion enthalten, Unterschiede in `docs/adapter-tia-portal.md`. Messwerte (Öffnen, Auslesen, Speicher) in `docs/messungen.md`.

### 5.6 Anwenderprogramm (alle Adapter)

- Name = Station + „, Anwenderprogramm“, Version = Release + ` (git <kurz-hash>)`.
- Hash: SHA-512 über das Quellverzeichnis der Station (`program` in `machine.yml`), Verfahren wie `../plc-build-tool/docs/adr/0005-hash-ueber-quellen.md`, nur mit SHA-512 statt SHA-256: alle Dateien rekursiv, Pfad relativ mit `/`, ordinal sortiert, je Datei `<pfad>\0<sha512 des inhalts>\n`, darüber SHA-512. Eine SPS liefert kein stabiles Binärartefakt; das Verfahren steht in `docs/regeln-tr03183.md`.
- Ohne `program` in der Konfiguration: Lücke `program-source-missing` mit Warnung, kein erfundener Hash.

### 5.7 Hersteller- und Teil-SBOMs

- Hersteller-SBOMs liegen als `*.cdx.json` im Repository neben den Projektquellen (Glob in `machine.yml`), Teil-SBOMs aus Syft oder cdxgen als Build-Ergebnis.
- Jede Eingangs-SBOM braucht eine `metadata.component`. Sie wird einer Station oder der Maschine zugeordnet (Konfiguration), ihre `bom-ref` werden mit Präfix eindeutig gemacht.
- Hersteller-SBOMs werden einer Firmware zugeordnet über Hersteller + Bestellnummer + Version. Passt keine, bleibt `vendor-sbom-missing` stehen.
- Fremde SBOMs werden selbst gegen die TR geprüft (TR §5.1: Verweise nur auf konforme SBOMs); Abweichungen sind Warnungen, keine Fehler.
- Effektive Lizenz beim Zusammenführen neu setzen (TR §3.2.8).
- Hierarchisch zusammenführen (Maschine → Station → Komponente), nicht flach anhängen.

## 6. Kennungen

1. **Primär:** Hersteller + Bestellnummer + Firmware-Version, immer als Properties.
2. **CPE über Zuordnungstabelle** `kennungen/cpe-map.yml` im Repository der Maschine, vom Anwender gepflegt:
   ```yaml
   - vendor: Siemens AG
     orderNumber: "6ES7 511-1AK0?-0AB0"      # Wildcard ? und *
     cpe: "cpe:2.3:o:siemens:simatic_s7-1500_cpu_1511-1_pn_firmware:{version}:*:*:*:*:*:*:*"
   ```
   Nur gesetzt, wenn ein Eintrag passt. `{version}` ist die Firmware ohne führendes `V`. Nie aus dem Typnamen ableiten.
3. **bom-ref** für alles ohne CPE (Abschnitt 4.3).
4. **PURL** nur für echte Softwarepakete (aus Teil-SBOMs). purl-spec kennt keinen Typ für Hardware oder Firmware; kein `pkg:generic` für Firmware erfinden.
5. **Kennungsprüfung** `plc-sbom ids`: Prüft je Komponente, ob sie in einem lokalen Verzeichnis mit CSAF-Dokumenten (`product_identification_helper.model_numbers`, `cpe`) oder in der CPE-Tabelle auffindbar ist, und meldet die Abdeckung. Bestellnummern werden dabei ohne Leerzeichen verglichen. Das ist **keine** Schwachstellenbewertung, sondern ein Test, ob die Kennungen tragen. CSAF-Quellen: Siemens ProductCERT, CERT@VDE (Phoenix Contact, WAGO, Beckhoff, CODESYS), CISA. Rockwell-Advisories tragen dort keine Identifikationshilfe, nur Namen.

## 7. Kommandozeile

### 7.1 Befehle

```text
plc-sbom build      --config machine.yml --release <kennung> [--commit <sha>]
                    [--out build/sbom-maschine.cdx.json] [--report html|md|none] [--release-build]
plc-sbom validate   <sbom> [--release-build]
plc-sbom report     <sbom> --format html|md --out build/sbom-bericht.html
plc-sbom compare    <sbom> --field feld/1042.json [--as-built build/sbom-1042.cdx.json] [--csv <datei>]
plc-sbom ids        <sbom> --csaf <verzeichnis> [--cpe-map kennungen/cpe-map.yml]
plc-sbom inspect    <projektdatei> --adapter <id> [--gsdml <verzeichnis>]
plc-sbom export-aml --project <.ap21|.zap21> --out <datei.aml> [--allow-upgrade]      (Windows, TIA V21)
plc-sbom doctor     [--adapter tia-portal] [--deep]                                     (Windows, TIA V21)
```

- `build` liest alle Stationen, führt Hersteller- und Teil-SBOMs zusammen, setzt Kennungen, validiert, schreibt SBOM, Bericht und Ergebnisdatei. Ein Aufruf in der Pipeline reicht.
- `validate` prüft eine vorhandene SBOM, auch fremde.
- `inspect` gibt das Rohinventar eines Adapters als JSON aus; bei `tia-aml` mit CPUs, IO-Systemen und Gerätezuordnung (5.4.4).
- `export-aml` und `doctor` laufen über den Openness-Worker; auf anderen Plattformen Exit 3 mit klarer Meldung.
- `--commit` nach Vorgabe aus `GIT_COMMIT`, sonst `git rev-parse HEAD`.
- Gemeinsame Optionen: `--verbose`, `--timeout <min>` (für den Worker).

Konsolenausgabe je Phase eine Zeile mit Kennzeichen, wie im Oberflächenentwurf der Produktseite:

```text
> plc-sbom build --config machine.yml --release R2026.10
[adapter tia-aml]     Station PLC_1: 23 Geräte, 0 Bibliotheken (Export 22.10.2026, TIA V21)
[adapter twincat]     Roboterzelle: 11 Geräte, 6 Bibliotheken
[quellen]   3 Hersteller-SBOMs, 1 Teil-SBOM (Syft), 2 GSDML-Dateien
[merge]     Maschine > 2 Stationen > 142 Komponenten
[prüfung]   Schema CycloneDX 1.6 ............ OK
[prüfung]   Pflichtfelder BSI TR-03183-2 .... OK, 1 Warnung, 2 Hinweise
            Warnung: PLC_1 Bibliotheken nicht gelesen (AML enthält keine)
            Hinweis: 2 Firmware-Stände ohne Hash, im Feld vermerkt
[ausgabe]   sbom-maschine.cdx.json
Fertig in 38 s
```

### 7.2 Exit-Codes

Gleich wie bei `plc-build`, damit die ganze Werkzeugfamilie in Pipelines gleich behandelt wird.

| Code | Bedeutung | Beispiel | Wer ist dran |
|---|---|---|---|
| 0 | Erfolgreich | SBOM gültig; Warnungen im Entwicklungs-Build, Hinweise | – |
| 1 | Befund | Schemafehler, Pflichtfeld fehlt; mit `--release-build` auch Warnungen; `compare` findet Abweichungen; CAx-Export mit Fehlern | Entwickler, Projekt |
| 2 | Aufruf oder Konfiguration falsch | `machine.yml` ungültig, Quelldatei fehlt oder ist kein gültiges XML, unbekannte Adapter-ID, `plc` nicht im AML | Repository |
| 3 | Umgebung | TIA nicht installiert, Konto nicht in der Openness-Gruppe, Firewall, Lizenz fehlt, Gerätebeschreibung fehlt, Openness-Befehl auf Linux | Agent-Betreuer |
| 4 | Abbruch | Zeitlimit, Worker abgestürzt oder ohne gültige Antwort, unerwarteter Fehler | Agent-Betreuer |

- Staffelung nach Whitepaper Abschnitt 6.5: Entwicklungs-Build → fehlende Pflichtfelder sind Warnungen, Release-Build (`--release-build`, in Jenkins bei gesetztem `TAG_NAME`) → Befund.
- Meldungen an den Anwender auf Deutsch, in ganzen Sätzen, ohne Ausrufezeichen, mit stabilem englischem Code davor: `[GSDML_NOT_FOUND] Die GSDML-Datei GSDML-V2.41-LEUZE-BCL248I-20211213.XML liegt in keinem der Verzeichnisse aus „gsdml“.`

### 7.3 Ergebnisdatei

`build`, `validate`, `compare` und `export-aml` schreiben zusätzlich `<out>.result.json` (z. B. `sbom-maschine.result.json`): Exit-Code, Anzahl Fehler, Warnungen, Hinweise, Komponenten, Stationen, Lücken je Code, Abweichungen. Damit kann die Pipeline einen Entwicklungs-Build mit Warnungen als „instabil“ markieren (Jenkins: `readJSON` + `unstable`), ohne den Exit-Code zu überladen.

### 7.4 `machine.yml`

```yaml
machine:
  name: Maschine XY
  supplier: Musterbau GmbH
  contact: sbom@musterbau.example        # TR: Ersteller per E-Mail, sonst URL
namespace: firma                          # eigener Property-Namespace, Vorgabe plcsbom

gsdml: [gsdml/]                           # Verzeichnisse mit GSDML-Dateien
deviceDescriptions: [codesys/devdesc/]    # Verzeichnisse mit CODESYS-Gerätebeschreibungen (*.devdesc.xml)
passiveOrderNumbers: ["6EP1*"]            # Geräte ohne Firmware-Attribut bewusst als passiv erklären
vendorOverrides:                          # Muster auf Bestellnummer → Hersteller
  "3RK*": Siemens AG
protectedBlockVendors:                    # Muster auf Bausteinautor/-familie → Lieferant (tia-portal)
  "ACME*": ACME Automation GmbH

stations:
  - name: Verpackung
    adapter: codesys
    source: v35                           # v35 | v23 | c4
    path: codesys/Verpackung.xml          # PLCopen-XML-Export
    plc: Verpackung                       # Gerätename im Projekt; Pflicht bei mehreren Steuerungen im Export
    program: codesys/Verpackung

  - name: PLC_1
    adapter: tia-aml
    path: export/Linie1.aml               # eingecheckt oder von export-aml erzeugt
    plc: PLC_1                            # CPU-Name im AML; Pflicht bei mehreren CPUs in der Datei
    devices: ["HMI_1"]                    # zusätzliche Geräte ohne IO-System
    ignoreDevices: ["Testaufbau_*"]       # bewusst ausgeschlossen, im Bericht genannt
    libraries:                            # optional, deklariert (AML enthält keine)
      - { name: LGF, vendor: Siemens AG, version: "7.0.1" }
    program: tia/PLC_1                    # Quellordner für den Programm-Hash

  - name: PLC_2
    adapter: tia-portal
    path: tia/Linie1.zap21                # .ap21 oder .zap21
    plc: PLC_2
    allowUpgrade: false
    globalLibraries: [libs/LGF_V21.zal21]
    program: tia/PLC_2

  - name: Roboterzelle
    adapter: twincat
    path: twincat/Zelle/Zelle.tsproj
    program: twincat/Zelle

vendorSboms: sbom/lieferanten/*.cdx.json
partialSboms:
  - path: build/teil/hmi-web.cdx.json
    station: PLC_1                        # ohne Angabe: unter der Maschine
cpeMap: kennungen/cpe-map.yml
licenses:                                 # Distributionslizenz je Hersteller, falls bekannt
  "Siemens AG": LicenseRef-siemens-proprietary
```

Validierung mit klaren Meldungen und Exit 2. Pfade relativ zur Konfigurationsdatei. Unbekannte Schlüssel sind Fehler, damit Tippfehler nicht still ignoriert werden.

## 8. Ausgaben

### 8.1 SBOM

CycloneDX 1.6 JSON. `metadata.tools` nennt PLC SBOM Builder mit Version und die Engineering-Versionen aus den Quellen. `metadata.component` ist die Maschine. Bei `compare --as-built` eine eigene SBOM je Seriennummer mit Ist-Firmware (`firmware:origin = as-found`).

### 8.2 Prüfbericht

HTML (Vorgabe) oder Markdown, neben der JSON-Datei. Inhalt nach dem Oberflächenentwurf der Produktseite:

- Kopf: Maschine, Release, Dateiname, Format, Erzeugungszeit, Prüfstatus (bestanden / nicht bestanden), Anzahl Warnungen und Hinweise.
- Kennzahlen: Komponenten, Stationen, Hersteller-SBOMs, Komponenten ohne Hash.
- Quellen je Station: Adapter, Datei, SHA-256, Engineering-Version, bei AML Exportdatum.
- Baum Maschine → Station → Firmware, Anwenderprogramm, E/A-Module, Bibliotheken; nicht zugeordnete und ausgeschlossene Geräte eigens.
- Je Station eine Tabelle: Typ, Name, Version, Kennung (CPE oder bom-ref), Pflichtfeld-Status („vollständig“ oder Lücke mit Text), Herkunft von Hersteller und Werten (`source`, `gsdml`, `declared`, `assumed`).
- Lückenliste mit Code, Komponente, Erklärung und was sie schließt.
- Passive Module und Racks als Hardware-Anhang.

HTML als eine Datei ohne externe Ressourcen (Agent offline, Datei wird archiviert). Deutsch.

### 8.3 Abgleich mit dem Feld

`compare` ordnet über Station + Position zu, sonst über Bestellnummer (ohne Leerzeichen). Ergebnis je Gerät `gleich`, `abweichend`, `nur-im-projekt`, `nur-im-feld`. Jede Abweichung ergibt Exit 1. Ausgabe auf der Konsole, im Bericht (Abschnitt „Abgleich mit dem Feld“: SBOM-Stand neben Ist-Stand, Kennzahlen geprüft / gleich / abweichend / nicht gefunden) und mit `--csv` als CSV. `--as-built` schreibt die SBOM für diese Seriennummer auch bei Abweichungen.

Eingang im Prototyp sind Dateien, keine Geräte:

- **Generisches Format** `feld/<seriennummer>.json`:
  ```json
  { "machine": "Maschine XY", "serialNumber": "1042", "date": "2026-10-22", "source": "inbetriebnahme",
    "devices": [ { "station": "PLC_1", "position": "0/1", "orderNumber": "6ES7 511-1AK02-0AB0",
                   "firmware": "V2.9", "hardwareRevision": "3", "serial": "S C-X4U12345", "via": "webapi" } ] }
  ```
- **Parser für die S7-1500 Web-API** (`Modules.ReadIdentificationMaintenance`, Antworten mit `Actual` und `Configured`), Fixtures aus `github.com/siemens/simatic-s7-webserver-api` (MIT).
- Nach dem Prototyp, jeweils erst mit Fixture: PRONETA-Export, PROFINET I&M0, EtherCAT CoE 0x100A/0x1018, CIP Identity, OPC UA DI.

### 8.4 Determinismus

- Komponenten, Properties, Abhängigkeiten sortiert; feste Feldreihenfolge.
- `bom-ref` stabil aus Station + Art + Position bzw. Name.
- Zeitstempel aus `SOURCE_DATE_EPOCH`, sonst Commit-Zeit von `HEAD`, sonst aktuelle Zeit mit Warnung.
- `serialNumber` als UUIDv5 aus Maschine + Release + Commit (bei `--as-built` zusätzlich Seriennummer der Maschine).
- In Tests Uhr und Umgebung injiziert.

## 9. TR-03183-2 v2.1.0: Regeln im Validator

Verbindlich ist `docs/regeln-tr03183.md` (angelegt mit M1 nach dem PDF v2.1.0, abgerufen 31.10.2026): TR-Feld → CycloneDX-Pfad → Quelle im Werkzeug → Verhalten bei Lücke → Schwere. Die folgende Liste ist der Stand der Recherche und bleibt als Überblick.

- **SBOM:** Ersteller (E-Mail, sonst URL), Zeitstempel.
- **Je Komponente:** Ersteller, Name, Version, Dateiname, Abhängigkeiten mit Angabe der Vollständigkeit, Distributionslizenzen, SHA-512 der ausgelieferten Form, `executable`, `archive`, `structured`.
- **MUSS, falls vorhanden:** SBOM-URI, Quellcode-URI, URI der ausgelieferten Form, andere Kennungen (CPE, PURL), Originallizenzen.
- **Properties** nach Taxonomie `BSI-Bund/tr-03183-cyclonedx-property-taxonomy` (v0.1.2): `bsi:component:executable`, `bsi:component:archive`, `bsi:component:structured`, `bsi:component:filename`, `bsi:component:effectiveLicence`. Das TR-PDF schreibt `effectiveLicense`, die Taxonomie `effectiveLicence`. Die Taxonomie gilt (E-009). Die Taxonomie verlangt `effectiveLicence` genau einmal je Komponente, die TR führt die effektive Lizenz als optional; der Validator folgt der TR (E-009, offen).
- **Hash:** `externalReferences[type=distribution].hashes[alg=SHA-512]`. Nach TR §3.2.1 entfallen Hash und Dateiname, wenn sie durch die Art der Zusammensetzung nicht verfügbar sind. Für Hersteller-Firmware und das Anwenderprogramm legt das Werkzeug das als nicht verfügbar aus, lässt Hash und Dateiname weg und setzt `hash-unavailable` und `filename-unavailable` als Hinweis. Das ist eine Auslegung, kein Wortlaut (E-011, offen).
- **Firmware** gilt als `executable` und `unstructured` (TR §8.1.6/8.1.7).
- **Hardware** regelt die TR nicht.
- Eine SBOM je Softwareversion; rekursiv bis zur ersten Komponente außerhalb des eigenen Lieferumfangs.

Der eigene Validator ist die Laufzeitprüfung, Schema eingebettet (offline). In CI läuft zusätzlich `sbomqs compliance --bsi-v21` über alle Golden Files, verglichen je Komponente und Pflichtfeld mit `tools/crosscheck-sbomqs.cs` (E-010; sbomqs vergibt Punkte und kennt keine Lückenvermerke). Weichen beide ab, gilt das als Fehler im eigenen Validator, bis das Gegenteil belegt ist.

## 10. Pipeline

`examples/Jenkinsfile` zeigt die Stufen nach dem Whitepaper: optional AML exportieren → SBOM erzeugen und prüfen → archivieren → optional Dependency-Track (Plugin „OWASP Dependency-Track“, Projektname = Maschinentyp, Projektversion = Release). Muster:

```groovy
stage('AML exportieren') {            // nur wenn tia-aml mit automatischem Export genutzt wird
  agent { label 'tia-v21' }
  steps {
    bat 'plc-sbom export-aml --project tia\\Linie1.zap21 --out export\\Linie1.aml'
    stash name: 'aml', includes: 'export/*.aml'
  }
}
stage('SBOM') {
  steps {
    unstash 'aml'
    script {
      def release = env.TAG_NAME ?: "dev-${env.BUILD_NUMBER}"
      def gate = env.TAG_NAME ? '--release-build' : ''
      def rc = sh(returnStatus: true,
                  script: "plc-sbom build --config machine.yml --release ${release} ${gate}")
      def result = readJSON file: 'build/sbom-maschine.result.json'
      if (rc == 1) error "SBOM-Prüfung fehlgeschlagen: ${result.errors} Fehler"
      if (rc >= 2) error "SBOM-Lauf abgebrochen (Exit ${rc}), siehe Log"
      if (result.warnings > 0) unstable "SBOM mit ${result.warnings} Warnungen"
    }
  }
}
stage('Archiv') {
  steps { archiveArtifacts artifacts: 'build/sbom-*.cdx.json, build/sbom-*.html, build/*.result.json' }
}
```

Auf Windows-Agenten `bat` statt `sh`. Zugangsdaten für Dependency-Track nur als Jenkins-Credential, nie im Jenkinsfile oder im Werkzeug.

## 11. Auslieferung und Lizenz

- **Lizenz:** proprietär, Comquent GmbH. Erlaubt sind nur Abhängigkeiten mit permissiver Lizenz (MIT, Apache-2.0, BSD, 0BSD); keine GPL, LGPL, AGPL, MPL ohne Rückfrage. CI prüft die Lizenzen aller Pakete gegen diese Liste. `THIRD-PARTY-NOTICES.md` wird aus den Paketen erzeugt und mit ausgeliefert.
- **Pakete:** self-contained Single-File-Executables, keine installierte .NET-Runtime nötig:
  - `plc-sbom-<version>-linux-x64.tar.gz` mit `plc-sbom`
  - `plc-sbom-<version>-win-x64.zip` mit `plc-sbom.exe` und ab M14 `plc-sbom-tia-openness.exe` (net48, nutzt das .NET Framework von Windows) samt `Register-OpennessFirewall.ps1`
- **SBOM des Werkzeugs selbst:** Jedes Paket enthält `plc-sbom.cdx.json` (CycloneDX 1.6, erzeugt in CI aus den NuGet-Abhängigkeiten) und besteht die eigene TR-Prüfung.
- **Version:** SemVer, eine Quelle in `Directory.Build.props`, im Programm über `plc-sbom --version` und in `metadata.tools` der erzeugten SBOMs.
- **Prüfsummen:** SHA-256 je Paket in `SHA256SUMS`. Code-Signatur der Windows-Exes später; sie würde die Openness-Firewall-Freigabe nicht ersetzen (die braucht ein Siemens-Zertifikat).
- Release-Pakete entstehen in CI auf Tag, nicht lokal. Nichts veröffentlichen ohne ausdrückliche Anweisung.

## 12. Meilensteine

Jeder Meilenstein endet mit grünen Tests auf ubuntu und windows, `dotnet format --verify-no-changes` und einem Commit. Kein Adapter-Meilenstein ohne Fixture.

| # | Inhalt | Fertig, wenn |
|---|---|---|
| M0 | Gerüst: Solution, `global.json`, CLI-Skelett mit allen Befehlen und Exit-Codes, CI-Matrix, Single-File-Pakete für win-x64 und linux-x64, Lizenzprüfung der Abhängigkeiten, `.gitignore`, `fixtures/FIXTURES.md` | `plc-sbom --help` läuft aus dem CI-Paket auf beiden Plattformen |
| M1 | Modell, CycloneDX-Writer, Validator (Schema + TR-Regeln), Ergebnisdatei, Entscheidung Variante A/B, `docs/regeln-tr03183.md` | Hand gebautes Modell ergibt eine SBOM, die der eigene Validator **und** `sbomqs --bsi` akzeptieren (E-010: Validator ohne Fehler, feldweiser Abgleich ohne Abweichung) |
| M2 | Adapter `codesys`, Quelle `v35` (CODESYS V3.5, PLCopen XML) | Gerät und Bibliotheken aus PLCopen-Fixture, Golden File, Lücken korrekt |
| M3 | Adapter `codesys`, Quelle `v23` (CODESYS V2.3): M3a Spike zum Eingangsformat (5.1.2), M3b Adapter | ADR mit gewähltem Weg; Golden File aus dem Comquent-V2.3-Demo |
| M4 | Adapter `codesys`, Quelle `c4` (CODESYS 4): M4a Spike zu Projektablage und Kommandozeile (5.1.3), M4b Adapter | ADR mit gewähltem Weg; Golden File aus dem Comquent-Demo; unbekannte CODESYS-4-Version ergibt Warnung |
| M5 | Adapter `studio5000` (Rockwell) | Golden File aus `ra-logix-cicd`-Fixture, alle Module und AOIs, Lücken korrekt |
| M6 | Adapter `twincat` (Beckhoff) | EtherCAT-Klemmen mit `firmware-not-in-project`, Bibliotheken mit `*` als `version-unresolved` |
| M7 | Adapter `tia-aml` (TIA AML aus dem CAx-Export): Format- und Versionsprüfung, Geräte, Firmware-Regeln, passive Module, Stationszuordnung über IO-System, `inspect` | Golden Files für V17-Fixture (S7-1200 + GSD-Gerät) und Comquent-V21-Export; V18/V21-Dateien aus `_local` laufen lokal ohne Fehler; Stationen mit zwei CPUs richtig getrennt |
| M8 | GSDML-Leser, GSD-Geräte in `tia-aml` | GSD-Gerät mit Hersteller und Bestellnummer aus GSDML-Fixture; fehlende GSDML ergibt die richtigen Lücken |
| M9 | `build` mit `machine.yml`, Merge mit Hersteller- und Teil-SBOMs, Programm-Hash, Determinismus | `samples/maschine-xy` (codesys + tia-aml + twincat + eine Syft-SBOM) ergibt bei zwei Läufen und auf beiden Plattformen dieselbe Datei |
| M10 | `report` (HTML, Markdown) | Baum, Tabellen, Quellen, Lückenliste und Hardware-Anhang wie in 8.2 |
| M11 | `compare`, `--as-built`, `--csv` | Web-API-Fixture gegen AML-Soll, Abweichungen erkannt, Exit 1 bei Abweichung |
| M12 | `ids` | Abdeckungsbericht gegen eine Handvoll echter Siemens-CSAF-Dateien |
| M13 | `examples/Jenkinsfile`, README und ANLEITUNG.md auf Release-Stand, Release-Pakete mit eigener SBOM | Pipeline-Stufen Build → Archiv → optional Dependency-Track, Staffelung Entwicklungs-/Release-Build |
| M14 | Adapter `tia-portal` (TIA über Openness): Openness-Worker mit `doctor`, `inventory` und `export-aml`, Firewall-Skript, Live-Tests | 5.5.6 erfüllt |

Die Adapter-Meilensteine M2 bis M8 und M14 folgen der festgelegten Reihenfolge: CODESYS (V3.5, V2.3, CODESYS 4), Rockwell, Beckhoff, TIA AML, TIA Openness. Fehlt für einen Spike die Comquent-Fixture, geht die Arbeit mit dem nächsten Adapter weiter und der Spike wird nachgeholt; das wird in `docs/entscheidungen.md` vermerkt.

**Nach dem Prototyp:** VCI-Dateien als Bibliotheksquelle für `tia-aml`, CODESYS-Scripting, Logix Designer SDK für automatischen L5X-Export, weitere Feld-Formate (PRONETA, I&M0, CoE), Openness für V19/V20 (eigene Worker-Binaries je Version), SPDX-3.0.1-Writer, CycloneDX 1.7, Signatur (cyclonedx-cli signiert nur XML; für JSON eine abgelöste Signatur über cosign oder OpenPGP, seit TR v2.1 nur Empfehlung), eigene Adapter für weitere Umgebungen.

## 13. Fixtures

Öffentliche Beispieldateien, vor Übernahme Lizenz und Commit prüfen und in `fixtures/FIXTURES.md` eintragen (Repository, Commit, Pfad, Lizenz, Abrufdatum):

| Umgebung | Repository / Pfad | Inhalt | Lizenz |
|---|---|---|---|
| L5X | `github.com/RockwellAutomation/ra-logix-cicd` | 1756-L85E, V36.11 | MIT |
| L5X | `github.com/hutcheb/acd`, `resources/*.L5X` | AOI, 1756-EN2T | Apache-2.0 |
| L5X | `github.com/Colt-H/L5X-Creator`, `github.com/reh3376/acd-l5x-tool-lib` | | MIT |
| TIA-AML | `github.com/vformi/PLC.Commissioning.Lib`, `src/PLC.Commissioning.Lib.App/samples/sample_device.aml` | **TIA V17** (`WriterVersion 1700`), S7-1200 CPU 1214C `V4.5`, GSD-Gerät Leuze BCL248i | MIT (geprüft 29.10.2026) |
| TIA-AML | `github.com/frattam-eng/openess_CIP`, `openess_CIP/CIP_cs_HardwareNetwork.aml` | **TIA V18**, S7-1500, vier ET 200SP mit passiven und firmwarebehafteten Modulen, zwei G120C, sieben IO-System-Links | keine → nur `_local` |
| TIA-AML | `github.com/JBuschkind/Interoperabilit-tsprojekt`, `Testing-Files/TIA_Export.aml` u. a. | **TIA V21** (`WriterVersion 2100`), S7-1500 CPU 1511-1 PN `V2.8`, PM ohne Firmware-Attribut, Profilschiene mit `***` | keine → nur `_local` |
| TIA-AML, GSDML | **Comquent-Demo-Projekt** (von Comquent zu liefern) | TIA V21, S7-1500, ET 200SP, ein GSD-Gerät mit weitergabefähiger GSDML, ein Antrieb, ein HMI-Panel, eine LGF-Instanz, ein know-how-geschützter Baustein; als `.zap21` für M14 und als AML-Export für M7/M8 | Comquent-eigen |
| TwinCAT | `github.com/RWTH-EBC/AixOCAT`, `Examples/HeatPumpTestBenchControl` | | MIT |
| TwinCAT | `github.com/Roald87/TcBlack`, `github.com/Inxton/TcOpen`, `github.com/Beckhoff/ADS` (`PLC-TestProject`) | | MIT |
| TwinCAT | `github.com/Beckhoff-USA-Community/SPT_V4_Samples` | | 0BSD |
| CODESYS V3.5 | `github.com/ScalABLE40/robin`, `robin_updater/cfg/codesys_project.xml` | V3.5 SP15, Platzhalter mit Standardauflösung (eingecheckt) | Apache-2.0 |
| CODESYS V3.5 | `github.com/Jake1510/ooip-tutorial-library`, `examples/scripting/Python Scripting/Script Engine Example.xml` | V3.5 SP20, `PlaceholderRedirection` (eingecheckt) | MIT |
| CODESYS V3.5 | `github.com/kmpm/OSC-MQTT` (Apache-2.0, Bibliotheksprojekt, eingecheckt), `github.com/stefandreyer/I2C-Multiplex` (`*.devdesc.xml`, MIT, vier Dateien eingecheckt; Bestellnummer überall `-`, zwei Kennungen doppelt) | | |
| CODESYS V2.3 | **Comquent-Demo-Projekt** (von Comquent zu liefern) | `.pro` plus alle Exporte, die V2.3 anbietet (`.exp`, PLCopen XML falls vorhanden); Steuerung, E/A-Konfiguration mit einem Feldbusgerät, zwei Bibliotheken samt `.lib`-Dateien | Comquent-eigen |
| CODESYS 4 | **Comquent-Demo-Projekt** (von Comquent zu liefern) | Projektablage gespeichert mit zwei aufeinanderfolgenden CODESYS-4-Versionen; Gerät, E/A, zwei Bibliotheken | Comquent-eigen |
| Feld (S7 Web-API) | `github.com/siemens/simatic-s7-webserver-api` (v3.3.92) | | MIT |
| CSAF | `cert-portal.siemens.com/productcert/csaf/ssa-452276.json` | mit `model_numbers` | öffentlich |

Solange die Comquent-Demo-Projekte fehlen, M7 mit der V17-Fixture beginnen, M3 und M4 zurückstellen und die `_local`-Dateien nur lokal nutzen. Synthetische Fixtures sind erlaubt, wenn sie als solche benannt sind (`*-synthetisch.*`) und ein echtes Gegenstück für dasselbe Attribut existiert.

## 14. Arbeitsweise in diesem Repository

- **Doku mitziehen (verbindlich).** Jede Änderung am Werkzeug (Befehl, Option, Exit-Code, Meldungscode, Lückencode, Prüfregel, Ausgabeformat, Adapter, `machine.yml`-Schlüssel, Meilensteinstand) passt im selben Commit **CLAUDE.md, README.md und ANLEITUNG.md** an. CLAUDE.md beschreibt Ziel und Regeln, README.md Überblick und Stand, ANLEITUNG.md die Bedienung für Anwender. Beispielausgaben in der Anleitung aus einem echten Lauf übernehmen, nicht ausdenken.
- **Tests zuerst.** Golden Files unter `tests/PlcSbom.Tests/golden/`; Änderung an einem Golden File nur mit Begründung im Commit.
- **Live-Tests** gegen TIA Portal nur mit `PLC_SBOM_LIVE=1`, nur auf dem Windows-Agenten, immer über den Worker-Prozess. Jeder Live-Lauf prüft, dass danach kein von ihm gestarteter Prozess übrig ist und die Eingabedatei unverändert ist (SHA-256 vorher/nachher).
- **Herstellerfakten:** Vor dem Einbau einer Schnittstelle Handbuchseite, Siemens-Skill oder Fixture prüfen. Was nicht belegt ist, als Annahme im Code-Kommentar oder in `docs/entscheidungen.md` kennzeichnen und **[prüfen]** in dieser Datei erst entfernen, wenn es belegt ist. Nicht kompilierender Openness-Code ist schlimmer als ein offenes TODO.
- **Rechts- und Normaussagen** in Code und Doku nur mit Quelle und Abrufdatum.
- **Widersprüche** zwischen dieser Datei, der TR und einer Fixture nicht still entscheiden: Optionen in `docs/entscheidungen.md` aufschreiben und nachfragen.
- **Rückwirkung auf die Produktseite** in `docs/rueckmeldung-produktseite.md` sammeln. Offene Punkte von Anfang an:
  - Die Seite nennt vier Adapter; dazu kommt `tia-aml` für den AML-Export (TIA V17–V21, ohne TIA-Installation).
  - Mit `tia-aml` fehlen Bibliotheken und geschützte Bausteine; die Seite verspricht Bibliothekstypen nur für den Openness-Weg.
  - GSD-Geräte haben im AML keine Bestellnummer; der SBOM Builder braucht dafür die GSDML-Dateien im Repository.
  - Die Seite nennt den Befehlsnamen nicht; die Oberflächenentwürfe zeigen `plc-sbom build --maschine …`, das Werkzeug heißt `plc-sbom build --config …`.
  - TR-03183-2 v2.1.0 erlaubt auch SPDX 3.0.1; die Seite nennt nur CycloneDX 1.6.
  - Die Seite nennt CODESYS V3.5; der Adapter deckt zusätzlich CODESYS V2.3 und CODESYS 4 ab (V2.3 und 4 nach Spike).
- **Sicherheit:** keine Lizenzdateien, keine Hersteller-DLLs, keine Kundenprojekte, keine Zugangsdaten im Repository. `.gitignore` wie in `../plc-build-tool/.gitignore`, ergänzt um `build/`, `fixtures/_local/`. Secrets nie loggen.
- **Sprache:** Code, Bezeichner, Commit-Nachrichten Englisch; Doku, Meldungen und Berichte Deutsch (Zielgruppe DACH).
- **Abhängigkeiten** klein halten: .NET 10, `NJsonSchema` (statt `CycloneDX.Core`, E-008), `System.CommandLine`, `YamlDotNet`, `System.Text.Json`. Jede neue Abhängigkeit mit Lizenz in `docs/entscheidungen.md`.
- Nichts nach außen pushen oder veröffentlichen ohne ausdrückliche Anweisung.

## 15. Quellen

- Produktseite: comquent.de/produkte/plc-sbom-builder (Quelltext: `comquent.de website/comquent-website/src/app/produkte/inhalte.ts`, Schlüssel `plc-sbom-builder`; Oberflächenentwürfe: `comquent.de website/Prompts/Produkte-SBOM-Builder-Oberflaechen.md`)
- Comquent Whitepaper „SBOM für SPS-Projekte mit TIA Portal V21“ (`comquent.de website/Whitepaper/SBOM/Comquent_Whitepaper_SBOM_SPS_TIA_Portal_V21.pdf`), Kapitel 4 (Openness-Inventar) und 6 (Jenkins-Pipeline, Quality Gates)
- Siemens TIA Portal AI Extensions, Openness-Skills V21: `github.com/siemens/tia-portal-ai-extensions` (lokal in `../plc-build-tool/.claude/skills/`)
- TIA Portal Openness V21 Handbuch: `docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/` (Seiten: Firewall, Benutzergruppe, Hardware-Objekte, Bibliotheken, CAx-Export)
- AutomationML, Application Recommendation „Automation Project Configuration“ (AR APC) 1.2.0: `automationml.org`
- PROFINET GSDML-Spezifikation: `profibus.com` (Downloads, GSDML Specification for PROFINET)
- CRA, Verordnung (EU) 2024/2847: `publications.europa.eu/resource/celex/32024R2847` (EUR-Lex blockt automatisierte Abrufe; CELLAR mit `Accept-Language: de`)
- Leitlinie der Kommission zum CRA, C(2026) 5252 vom 27.07.2026: `ec.europa.eu/newsroom/dae/redirection/document/131456`
- Maschinenverordnung (EU) 2023/1230: `publications.europa.eu/resource/celex/32023R1230`, Berichtigung `…/32023R1230R(01)`
- BSI TR-03183-2 v2.1.0: `bsi.bund.de/SharedDocs/Downloads/EN/BSI/Publications/TechGuidelines/TR03183/BSI-TR-03183-2_v2_1_0.pdf`
- BSI CycloneDX-Property-Taxonomie: `github.com/BSI-Bund/tr-03183-cyclonedx-property-taxonomy`
- ENISA Technical Advisory SBOM v1.10 (22.10.2026): `enisa.europa.eu`
- CycloneDX 1.6 Spezifikation und Schema: `cyclonedx.org/docs/1.6/json/`
- cyclonedx-cli: `github.com/CycloneDX/cyclonedx-cli` · CycloneDX.Core: `nuget.org/packages/CycloneDX.Core`
- sbomqs: `github.com/interlynk-io/sbomqs`
- CODESYS Scripting: `content.helpme-codesys.com/en/ScriptingEngine/`
- CODESYS 4: `codesys.com/company/news-center/latest-news/codesys-4-version-1000-is-available/`, `codesys.com/ecosystem/release-lifecycle/releases-updates/codesys-4/` (abgerufen 31.10.2026)
- CODESYS V2.3 Converter: `store.codesys.com` (Produktblatt „CODESYS V2.3 Converter“)
- Beckhoff Infosys (CoE-Objekte, Automation Interface): `infosys.beckhoff.com`
- NVD-Betrieb seit 15.04.2026: `nist.gov/news-events/news/2026/04/nist-updates-nvd-operations-address-record-cve-growth`
- Wettbewerb: `complioty.de/en/apps/scanner/` · Herstellervergleich SBOM/CSAF: `werkspilot.de/en/blog/sbom-by-manufacturer/`
