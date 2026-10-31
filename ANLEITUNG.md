# Anleitung: PLC SBOM Builder (`plc-sbom`)

Stand: 31.10.2026, Version 0.1.0 (Prototyp). Diese Anleitung beschreibt, was das Werkzeug in dieser Version kann und wie man es aufruft. Was erst mit späteren Meilensteinen kommt, ist als „folgt“ gekennzeichnet. Hintergrund, Architektur und Planung stehen in [CLAUDE.md](CLAUDE.md), ein Überblick in [README.md](README.md).

## 1. Wofür das Werkzeug da ist

`plc-sbom` erzeugt aus den SPS-Projekten einer Maschine eine SBOM im Format CycloneDX 1.6 JSON und prüft sie gegen das CycloneDX-Schema und die Pflichtfelder der BSI TR-03183-2 v2.1.0. Es ist für den Aufruf in einer CI-Pipeline gebaut (Jenkins, GitLab CI, Azure DevOps): ein Aufruf, ein Exit-Code, eine Ergebnisdatei für die Pipeline.

Was eine Quelle nicht hergibt, schätzt das Werkzeug nicht. Fehlt eine Angabe, steht an der Komponente ein Lückenvermerk (Abschnitt 6), und die Prüfung meldet ihn als Warnung oder Hinweis.

Eine SBOM allein erfüllt den Cyber Resilience Act nicht. Das Werkzeug bewertet keine Schwachstellen; das übernimmt ein Überwachungssystem wie Dependency-Track.

## 2. Was in Version 0.1.0 geht

| Befehl | Zweck | Stand |
|---|---|---|
| `validate` | vorhandene SBOM prüfen (Schema und TR-03183-2), Ergebnisdatei schreiben | **nutzbar** |
| `build` | SBOM aus `machine.yml` und den SPS-Projekten erzeugen | folgt (M9) |
| `inspect` | Rohinventar eines Adapters ausgeben | **nutzbar für `codesys`** (CODESYS V3.5, PLCopen XML) und **`studio5000`** (Rockwell, L5X); andere Adapter folgen (M6–M14) |
| `report` | Prüfbericht als HTML oder Markdown | folgt (M10) |
| `compare` | SBOM mit dem Ist-Stand aus dem Feld abgleichen | folgt (M11) |
| `ids` | Kennungen gegen CSAF-Advisories prüfen | folgt (M12) |
| `export-aml`, `doctor` | TIA Portal Openness (nur Windows mit TIA V21) | folgt (M14) |

Ein Befehl, der noch fehlt, endet mit Exit 2 und nennt den Meilenstein:

```text
[NOT_IMPLEMENTED] Der Befehl „build“ ist in plc-sbom 0.1.0 noch nicht umgesetzt. Er folgt mit Meilenstein M9.
```

Die Optionen aller Befehle sind schon angelegt und erscheinen in der Hilfe (`plc-sbom <befehl> --help`).

## 3. Installation

Das Werkzeug ist eine einzelne ausführbare Datei, eine .NET-Runtime auf dem Rechner ist nicht nötig.

**Aus dem CI-Paket.** Die CI (GitHub Actions) baut je Lauf `plc-sbom-<version>-linux-x64.tar.gz` und `plc-sbom-<version>-win-x64.zip`. Paket auspacken, die Datei `plc-sbom` bzw. `plc-sbom.exe` in den Pfad legen. Veröffentlichte Releases gibt es noch nicht.

**Selbst bauen.** Voraussetzung ist das .NET 10 SDK.

```bash
dotnet publish src/PlcSbom.Cli/PlcSbom.Cli.csproj -c Release -r linux-x64 -o out/publish   # oder win-x64, osx-arm64
out/publish/plc-sbom --version
```

Prüfen, ob es läuft:

```text
> plc-sbom --version
0.1.0+adc95d96840fb7e4fdba173199afce7706d4c93d
```

Hinter dem `+` steht der Git-Commit, aus dem das Werkzeug gebaut wurde.

## 4. Eine SBOM prüfen: `validate`

```text
plc-sbom validate <sbom> [--release-build] [--verbose]
```

| Option | Wirkung |
|---|---|
| `<sbom>` | CycloneDX-Datei im JSON-Format, eigene oder fremde |
| `--release-build` | Warnungen zählen als Befund, Exit 1. Für Release-Builds (in Jenkins z. B. bei gesetztem `TAG_NAME`) |
| `--verbose`, `-v` | zeigt auch die Hinweise und bei unerwarteten Fehlern den Stacktrace |

Beispiel:

```text
> plc-sbom validate build/sbom-maschine.cdx.json
[prüfung]   Schema CycloneDX 1.6 ............ OK
[prüfung]   Pflichtfelder BSI TR-03183-2 .... OK, 9 Warnungen, 32 Hinweise
            Warnung: [LICENSE_UNKNOWN] roboterzelle-rt: Keine Distributionslizenz bekannt.
            Warnung: [RUNTIME_VERSION_UNKNOWN] roboterzelle-rt: Die Version der Laufzeit steht nicht im Projekt.
            ...
            32 Hinweise ausgeblendet, mit --verbose anzeigen.
[ausgabe]   build/sbom-maschine.result.json
```

Jede Meldung beginnt mit einem stabilen Code in eckigen Klammern, danach die `bom-ref` der Komponente und der Text. Nach dem Code lässt sich in Logs und Pipelines suchen; er ändert sich zwischen Versionen nicht ohne Eintrag in `docs/entscheidungen.md`.

### 4.1 Was geprüft wird

**Schema.** Die Datei muss gültiges JSON sein, `bomFormat` = `CycloneDX` und `specVersion` = `1.6` haben und das eingebettete Schema CycloneDX 1.6 erfüllen. Die Prüfung läuft offline. Bei einer neueren Version (z. B. 1.7) entfällt die Schemaprüfung mit Warnung `SCHEMA_NOT_EMBEDDED`, die TR-Regeln laufen trotzdem. Ältere Versionen als 1.6 erfüllen die TR nicht (Fehler `SPEC_VERSION_UNSUPPORTED`).

**Ganze SBOM** (TR-03183-2 Abschnitt 5.2.1):

- `metadata.timestamp` vorhanden
- Ersteller in `metadata.manufacturer` mit E-Mail-Adresse (`contact[].email`) oder URL
- Hauptkomponente `metadata.component` vorhanden
- keine Schwachstellen (`vulnerabilities` muss fehlen oder leer sein, TR Abschnitt 3.1)

**Je Komponente** (TR-03183-2 Abschnitt 5.2.2):

| TR-Feld | Erwartet in CycloneDX | Code, wenn es fehlt |
|---|---|---|
| Ersteller | `manufacturer` mit E-Mail oder URL | `CREATOR_MISSING` |
| Version | `version` | `VERSION_MISSING` |
| Dateiname | Property `bsi:component:filename` | `FILENAME_MISSING` |
| Distributionslizenz | `licenses[]` mit `acknowledgement: concluded`, als SPDX-Kennung, SPDX-Ausdruck oder `LicenseRef-…` | `DISTRIBUTION_LICENCE_MISSING`; Freitext statt Kennung: `DISTRIBUTION_LICENCE_INVALID` |
| Hash der ausgelieferten Form | `externalReferences[type=distribution].hashes[alg=SHA-512]` | `HASH_MISSING` |
| ausführbar | Property `bsi:component:executable` (`executable` / `non-executable`) | `EXECUTABLE_PROPERTY_MISSING` |
| Archiv | Property `bsi:component:archive` (`archive` / `no archive`) | `ARCHIVE_PROPERTY_MISSING` |
| strukturiert | Property `bsi:component:structured` (`structured` / `unstructured`) | `STRUCTURED_PROPERTY_MISSING` |
| Abhängigkeiten | Eintrag in `dependencies`, auch leer | `DEPENDENCIES_MISSING` |
| Vollständigkeit der Abhängigkeiten | Komponente in `compositions[]` mit `aggregate` | `COMPLETENESS_MISSING` |

Weitere Fehler: `BOM_REF_MISSING`, `BOM_REF_DUPLICATE`, `DEPENDENCY_REF_UNKNOWN` (Abhängigkeit auf eine `bom-ref`, die es nicht gibt), `COMPONENT_UNREACHABLE` (keine Kette von Abhängigkeiten führt von der Hauptkomponente zu dieser Komponente, TR Abschnitt 5.1), `BSI_PROPERTY_DUPLICATE`, `BSI_PROPERTY_INVALID` (unzulässiger Wert).

Weitere Warnungen: `EFFECTIVE_LICENCE_SPELLING` (Schreibweise `effectiveLicense` aus dem TR-PDF statt `effectiveLicence` aus der BSI-Taxonomie), `ASSOCIATED_LICENCES_DEPRECATED`, `GAP_UNKNOWN` (unbekannter Lückencode).

### 4.2 Fehlendes Feld mit und ohne Lückenvermerk

Fehlt ein Pflichtfeld und erklärt kein Lückenvermerk das Fehlen, ist das ein **Fehler**. Steht an der Komponente ein passender Lückenvermerk, meldet die Prüfung nur diesen, als Warnung oder Hinweis je nach Code (Abschnitt 6).

Lückenvermerke stehen als Property `<namespace>:gap` an der Komponente, z. B. `plcsbom:gap` = `hash-unavailable`. Jeder Namespace außer `cdx` und `bsi` zählt.

Eine logische Komponente (Property `<namespace>:logicalComponent` = `true`, etwa die Maschine selbst) braucht nach TR Abschnitt 3.2.2 weder Dateiname noch Hash noch die drei Dateieigenschaften. Die Prüfung vermerkt das als Hinweis `LOGICAL_COMPONENT`.

**Fremde SBOMs**, etwa von Komponentenherstellern oder aus Syft, kennen diese Lückenvermerke meist nicht. Fehlende Pflichtfelder erscheinen dort deshalb als Fehler. Das ist gewollt: Die Datei erfüllt die TR dann nicht.

## 5. Schweregrade und Exit-Codes

| Schwere | Bedeutung | Wirkung auf den Exit-Code |
|---|---|---|
| Fehler | Schema verletzt, Pflichtfeld fehlt ohne Lückenvermerk, Datei unlesbar | immer Exit 1 |
| Warnung | Pflichtangabe fehlt, Lücke ist vermerkt | Exit 0, mit `--release-build` Exit 1 |
| Hinweis | Lücke, die die TR zulässt oder die erst der Ist-Stand schließt | nie |

| Exit | Bedeutung | Wer ist dran |
|---|---|---|
| 0 | erfolgreich (Warnungen im Entwicklungs-Build und Hinweise ändern daran nichts) | – |
| 1 | Befund: Prüfung nicht bestanden | Entwickler, Projekt |
| 2 | Aufruf oder Konfiguration falsch: Datei fehlt, unbekannte Option, Befehl noch nicht umgesetzt | Repository |
| 3 | Umgebung, z. B. Openness-Befehl auf Linux (`OPENNESS_WINDOWS_ONLY`) | Agent-Betreuer |
| 4 | Abbruch durch unerwarteten Fehler (`UNEXPECTED`) | Agent-Betreuer |

Die Exit-Codes sind dieselben wie bei `plc-build`.

## 6. Lückencodes

Der Code im Lückenvermerk ist klein geschrieben mit Bindestrich (`hash-unavailable`), in der Konsolenmeldung groß mit Unterstrich (`[HASH_UNAVAILABLE]`).

| Code | Bedeutung | Schwere | Erklärt fehlendes Feld |
|---|---|---|---|
| `firmware-not-in-project` | Das Projekt führt keine Firmware-Version für dieses Gerät, z. B. EtherCAT-Klemmen | Hinweis | Version |
| `firmware-projected-only` | Firmware nur als projektierter Sollwert bekannt, kein Ist-Stand | Hinweis | – |
| `hash-unavailable` | Die ausgelieferte Form liegt nicht vor, kein SHA-512 bestimmbar (TR Abschnitt 3.2.1) | Hinweis | Hash |
| `filename-unavailable` | Die Komponente liegt nicht als Datei vor | Hinweis | Dateiname |
| `vendor-sbom-missing` | Für diese Firmware liegt keine Hersteller-SBOM vor | Hinweis | – |
| `device-unassigned` | Das Gerät gehört zu keiner Station | Hinweis | – |
| `program-not-read` | Die Quelle enthält keine Bausteine, geschützte Bausteine sind unbekannt | Hinweis | – |
| `version-unresolved` | Die Quelle nennt nur einen Platzhalter, z. B. Bibliotheksversion `*` | Warnung | Version |
| `runtime-version-unknown` | Die Version der Laufzeit steht nicht im Projekt (TwinCAT) | Warnung | Version |
| `libraries-not-read` | Die Quelle enthält keine Bibliotheken, und keine sind deklariert | Warnung | – |
| `license-unknown` | Keine Distributionslizenz bekannt | Warnung | Distributionslizenz |
| `supplier-unknown` | Hersteller nicht ermittelbar | Warnung | Ersteller |
| `creator-contact-unknown` | Für den Hersteller ist weder E-Mail noch URL bekannt | Warnung | Ersteller |
| `file-properties-unknown` | Unbekannt, ob ausführbar, Archiv oder strukturiert | Warnung | ausführbar, Archiv, strukturiert |
| `program-source-missing` | Für das Anwenderprogramm ist kein Quellverzeichnis angegeben, kein Hash | Warnung | Hash |
| `protected-block-unknown` | Geschützter Baustein ohne bekannten Lieferanten oder Version | Warnung | Ersteller, Version |
| `ekey-disabled` | Rockwell: elektronische Kodierung eines Moduls aus (nicht an der Steuerung), das Gerät im Feld kann abweichen | Warnung | – |
| `gsdml-not-found` | Die GSDML-Datei des Geräts liegt in keinem der angegebenen Verzeichnisse | Warnung | – |
| `devdesc-not-found` | CODESYS: Zur Gerätekennung liegt keine oder mehr als eine Gerätebeschreibung in den angegebenen Verzeichnissen | Warnung | – |
| `order-number-not-in-project` | Weder Projekt noch GSDML nennen eine Bestellnummer | Warnung | – |

Die Begründung je Code mit der TR-Stelle steht in [docs/regeln-tr03183.md](docs/regeln-tr03183.md) Abschnitt 7.

## 7. Ergebnisdatei

`validate` schreibt neben die SBOM eine Ergebnisdatei: aus `build/sbom-maschine.cdx.json` wird `build/sbom-maschine.result.json`. Die Feldnamen sind Schnittstelle.

```json
{
  "format": 1,
  "command": "validate",
  "sbom": "build/sbom-maschine.cdx.json",
  "exitCode": 0,
  "schemaValid": true,
  "errors": 0,
  "warnings": 9,
  "notes": 32,
  "components": 11,
  "stations": 2,
  "gaps": { "hash-unavailable": 9, "license-unknown": 3, "...": 0 },
  "findings": [
    { "severity": "warning", "code": "LICENSE_UNKNOWN", "component": "roboterzelle-rt",
      "fields": [ "distributionLicence" ],
      "message": "roboterzelle-rt: Keine Distributionslizenz bekannt." }
  ]
}
```

| Feld | Inhalt |
|---|---|
| `format` | Version des Dateiformats, derzeit 1 |
| `exitCode` | Exit-Code des Laufs |
| `schemaValid` | Schema CycloneDX 1.6 bestanden |
| `errors`, `warnings`, `notes` | Anzahl je Schwere |
| `components` | Komponenten einschließlich der Hauptkomponente |
| `stations` | verschiedene Stationen (Property `<namespace>:station`) |
| `gaps` | Anzahl je Lückencode |
| `findings` | alle Meldungen mit Schwere, Code, Komponente, betroffenen TR-Feldern und Text |

## 8. In der Pipeline

Mit der Ergebnisdatei kann die Pipeline einen Entwicklungs-Build mit Warnungen als instabil markieren, ohne dass der Exit-Code dafür herhalten muss. Beispiel für Jenkins, solange nur `validate` verfügbar ist (die SBOM kommt dann aus einer anderen Quelle):

```groovy
stage('SBOM prüfen') {
  steps {
    script {
      def gate = env.TAG_NAME ? '--release-build' : ''
      def rc = sh(returnStatus: true, script: "plc-sbom validate build/sbom-maschine.cdx.json ${gate}")
      def result = readJSON file: 'build/sbom-maschine.result.json'
      if (rc == 1) error "SBOM-Prüfung fehlgeschlagen: ${result.errors} Fehler, ${result.warnings} Warnungen"
      if (rc >= 2) error "SBOM-Prüfung abgebrochen (Exit ${rc}), siehe Log"
      if (result.warnings > 0) unstable "SBOM mit ${result.warnings} Warnungen"
    }
  }
}
```

Auf Windows-Agenten `bat` statt `sh`. `readJSON` stammt aus dem Plugin „Pipeline Utility Steps“. Ein vollständiges Jenkinsfile mit `build` folgt mit M13 unter `examples/Jenkinsfile`.

## 9. Häufige Meldungen

| Meldung | Ursache | Abhilfe |
|---|---|---|
| `[SBOM_NOT_FOUND]` | Die angegebene Datei gibt es nicht | Pfad prüfen; relative Pfade gelten ab dem Arbeitsverzeichnis |
| `[JSON_INVALID]` | Die Datei ist kein gültiges JSON | Datei mit einem JSON-Werkzeug prüfen, oft abgeschnitten oder falsch kodiert |
| `[NOT_CYCLONEDX]` | `bomFormat` fehlt oder ist nicht `CycloneDX` | Die Datei ist vermutlich SPDX oder ein anderes Format; SPDX prüft das Werkzeug nicht |
| `[SPEC_VERSION_UNSUPPORTED]` | CycloneDX älter als 1.6 | SBOM mit CycloneDX 1.6 neu erzeugen |
| `[NOT_IMPLEMENTED]` | Befehl folgt in einem späteren Meilenstein | Abschnitt 2 |
| `[OPENNESS_WINDOWS_ONLY]` | `export-aml` oder `doctor --adapter tia-portal` außerhalb von Windows | Schritt auf einem Windows-Agenten mit TIA Portal V21 ausführen |
| `[USAGE]` | Option fehlt oder ist unbekannt | `plc-sbom <befehl> --help` |
| `[SOURCE_NOT_FOUND]` | Die Projektdatei bzw. der Export fehlt | Pfad prüfen; Export eingecheckt? |
| `[XML_INVALID]` | Die Datei ist kein gültiges XML oder enthält eine DTD | Export neu erzeugen; DTDs werden aus Sicherheitsgründen nie gelesen |
| `[NOT_PLCOPEN]` | Die Datei ist kein PLCopen-XML-Export | In CODESYS V3.5 das ganze Projekt als PLCopen XML exportieren |
| `[CODESYS_PLC_REQUIRED]` | Der Export enthält mehrere Steuerungen | Steuerung mit `plc` wählen (ab M9 in `machine.yml`) |
| `[DEVDESC_DIR_NOT_FOUND]` | Ein Verzeichnis aus `deviceDescriptions` fehlt | Pfad in `machine.yml` prüfen (relativ zur Konfigurationsdatei) |
| `[NOT_DEVDESC]` | Eine `*.devdesc.xml` ist keine CODESYS-Gerätebeschreibung | Datei prüfen oder aus dem Verzeichnis entfernen |
| `[CODESYS_PLC_NOT_FOUND]` | Die mit `plc` genannte Steuerung steht nicht im Export | Namen aus der Meldung oder aus `inspect` übernehmen |
| `[NOT_L5X]` | Die Datei ist kein L5X-Export aus Studio 5000 | In Studio 5000 das Projekt mit „Speichern unter … L5X“ exportieren |
| `[STUDIO5000_NOT_CONTROLLER]` | Der L5X-Export enthält nur einen Bestandteil (etwa eine AOI), nicht den ganzen Controller | Das ganze Projekt als L5X speichern, nicht einzelne Bausteine exportieren |

## 10. Projekt ansehen: `inspect`

```text
plc-sbom inspect <projektdatei> --adapter <id> [--gsdml <verzeichnis>]
```

`inspect` gibt das Rohinventar eines Adapters als JSON auf der Konsole aus: was der Adapter in der Datei findet, unverändert und noch ohne Lückenregeln. Damit lässt sich prüfen, ob ein Export das Erwartete enthält, und später die `machine.yml` schreiben. In Version 0.1.0 geht das für die Adapter `codesys` und `studio5000`; für die anderen endet der Aufruf mit Exit 2 und `[NOT_IMPLEMENTED]` und nennt den Meilenstein. `--gsdml` gilt nur für die TIA-Adapter.

Beispiel (gekürzt):

```text
> plc-sbom inspect fixtures/codesys/v35/ooip-script-engine-example.xml --adapter codesys
{
  "format": 1,
  "adapter": "codesys",
  "source": "v35",
  "file": "fixtures/codesys/v35/ooip-script-engine-example.xml",
  "sha256": "ec3146e46c5ad8cc7275f376d2e557080a6f4b7cbfb72b01b75511a8d3e48de8",
  "project": {
    "productName": "CODESYS",
    "productVersion": "CODESYS V3.5 SP20 Patch 1",
    "creationDateTime": "2025-05-27T13:20:07.2176472",
    "projectName": "Script Engine Example.project",
    "devices": [
      {
        "name": "Device",
        "type": "4097",
        "id": "0000 000a",
        "version": "3.5.20.10",
        "applications": [
          {
            "name": "Application",
            "libraries": [
              {
                "reference": "#Standard",
                "namespace": "Standard",
                "placeholder": "Standard",
                "defaultResolution": "Standard, * (System)",
                "redirection": "Standard, 3.5.18.0 (System)",
                "systemLibrary": false,
                "resolution": "redirection",
                "effective": "Standard, 3.5.18.0 (System)",
                ...
```

| Feld | Inhalt |
|---|---|
| `sha256` | SHA-256 der gelesenen Datei |
| `project.devices` | Steuerungen (oberste Ebene) mit `children` (untergeordnete Geräte) und `applications` |
| `version` am Gerät | Version der **Gerätebeschreibung**, nicht die Firmware |
| `libraries[].resolution` | `fixed` (direkte Referenz), `redirection` (Platzhalter im Projekt umgeleitet), `default` (nur Standardauflösung), `none` |
| `libraries[].effective` | die Referenz, die der Export als wirksam zeigt |
| `project.projectLibraries` | Bibliotheken im POU-Pool |

Beispiel für `studio5000` (gekürzt, aus einem echten Lauf):

```text
> plc-sbom inspect fixtures/studio5000/acd-ACDTestsWithAOI.L5X --adapter studio5000
{
  "format": 1,
  "adapter": "studio5000",
  "file": "fixtures/studio5000/acd-ACDTestsWithAOI.L5X",
  "sha256": "89265a5ff85bc7bc14f7e86b0ae6ae2028b354a98cce1cd187fdaa2a35792096",
  "project": {
    "softwareRevision": "34.01",
    "targetType": "Controller",
    "exportDate": "Wed Jul 10 20:36:17 2024",
    "controller": { "name": "ACDTests", "use": "Target", "processorType": "1756-L85E", "majorRev": "34", "minorRev": "11", "redundancyEnabled": false },
    "modules": [
      { "name": "Local", "catalogNumber": "1756-L85E", "vendor": "1", ..., "major": "34", "minor": "11", "parentModule": "Local", "ekey": "Disabled", "upstreamPortType": "ICP", "slot": 0 },
      { "name": "ETH_MODULE", "catalogNumber": "1756-EN2T", "vendor": "1", ..., "major": "11", "minor": "1", "parentModule": "Local", "ekey": "CompatibleModule", "upstreamPortType": "ICP", "slot": 1 }
    ],
    "addOnInstructions": [ { "name": "AddOnInstruction", "revision": "1.7", "revisionExtension": "ExtendedText", "vendor": "ACD-TOOLS" } ]
  }
}
```

| Feld | Inhalt |
|---|---|
| `project.targetType` | `Controller` bei einem Export des ganzen Projekts; nur der ergibt eine Station |
| `modules[].major`, `minor` | projektierte Revision des Moduls, in der SBOM die Firmware-Version als Soll |
| `modules[].ekey` | elektronische Kodierung: `ExactMatch`, `CompatibleModule` oder `Disabled` |
| `modules[].slot` | Steckplatz auf der Backplane; fehlt bei Modulen hinter einem Ethernet-Port |

IP-Adressen der Ethernet-Ports liest das Werkzeug nicht, sie erscheinen auch in `inspect` nicht.

## 11. CODESYS V3.5

Eingang ist der PLCopen-XML-Export des ganzen Projekts. Die `.project`-Datei ist binär und wird nicht gelesen. Den Export erzeugt man in CODESYS V3.5 über den PLCopen-XML-Export des Projekts und checkt ihn neben dem Projekt ein. Die Station erhält im Ergebnis die Property `<namespace>:adapter:source` = `v35`.

**Steuerung.** Jede Konfiguration der obersten Ebene mit Gerätedaten ist eine Steuerung. Enthält der Export mehrere, wählt `plc` eine aus (ab M9 in `machine.yml`), sonst endet der Lauf mit Exit 2.

**Gerät.** Der Export nennt nur die Kennung der Gerätebeschreibung (`Type`, `Id`, `Version`), keinen Produktnamen, Hersteller oder Bestellnummer. In der SBOM heißt die Firmware-Komponente deshalb etwa `Gerät 4102/0000 0005, Firmware`, ohne Version und mit `firmware:origin` = `unknown`. Die Version der Gerätebeschreibung steht als `<namespace>:vendorKey:DeviceDescriptionVersion` daneben. Lücken: `firmware-not-in-project` (Hinweis), `order-number-not-in-project` und `supplier-unknown` (Warnungen).

**Gerätebeschreibungen.** Liegen die Gerätebeschreibungen (`*.devdesc.xml`) im Repository, nennt man ihre Verzeichnisse unter `deviceDescriptions` (ab M9 in `machine.yml`). Passt genau eine Datei zur Kennung des Geräts (Type, Id und Version), übernimmt das Werkzeug daraus Produktname, Hersteller und Bestellnummer; die Komponente heißt dann etwa `TCA9548, Firmware`, der Hersteller trägt `vendor:origin` = `devdesc`, Dateiname und SHA-256 der Gerätebeschreibung stehen als `vendorKey`. Passt keine oder passen mehrere Dateien, übernimmt es nichts und vermerkt `devdesc-not-found` (Warnung). Eine Bestellnummer `-` gilt als nicht angegeben. Auch mit Gerätebeschreibung bleibt die Firmware-Version unbekannt.

**Bibliotheken.** Gelesen werden die Bibliotheken jeder Anwendung der Steuerung und die des POU-Pools.

| Im Export | Version in der SBOM | Lücke |
|---|---|---|
| direkte Referenz, z. B. `Robin, 0.1.2 (INESC TEC)` | `0.1.2` | – |
| Platzhalter mit Umleitung im Projekt | Version der Umleitung | – |
| Platzhalter nur mit Standardauflösung, z. B. `#SM3_Basic` → `SM3_Basic, 4.5.1.0 (…)` | keine; `4.5.1.0` steht als `<namespace>:version:requested` | `version-unresolved` (Warnung) |
| Version `*` | keine; `*` als `<namespace>:version:requested` | `version-unresolved` (Warnung) |

Platzhalter ohne Umleitung gelten als nicht aufgelöst, weil in CODESYS die Gerätebeschreibung eine andere Version festlegen kann und das nicht im Export steht. Wer feste Versionen in der SBOM will, leitet die Platzhalter im Bibliotheksverwalter des Projekts um oder referenziert die Bibliothek direkt.

Als Hersteller einer Bibliothek gilt die Angabe in Klammern. CODESYS schreibt für Bibliotheken der eigenen Installation `System`; das ist keine Organisation, deshalb bleibt der Hersteller dort leer (`supplier-unknown`), der Wert steht in `<namespace>:library:company`.

Alle gelesenen Stellen und Regeln: [docs/adapter-codesys.md](docs/adapter-codesys.md).

## 12. Rockwell Studio 5000

Eingang ist der L5X-Export des ganzen Projekts (Studio 5000 Logix Designer: „Speichern unter … L5X“). Die `.ACD`-Datei ist binär und wird nicht gelesen. Ein Export einzelner Bestandteile, etwa einer Add-On Instruction, ergibt keine Station (`[STUDIO5000_NOT_CONTROLLER]`). Ein L5X enthält genau einen Controller; `plc` braucht dieser Adapter nicht.

**Steuerung und Module.** Jedes Modul unter `Modules` wird ein Gerät, die Steuerung zuerst (das Modul `Local`). Die Firmware-Komponente heißt nach der Bestellnummer, etwa `1756-EN2T, Firmware`, weil der Export keinen Produktnamen führt. Ihre Version ist die projektierte Revision: an der Steuerung die Projektrevision (`36.11`), an den Modulen `Major.Minor` (`11.1`). Das ist ein Sollwert (`firmware:origin` = `projected`, Hinweis `firmware-projected-only`). Ein Modul ohne Revision bekommt `firmware-not-in-project`. Die Position steht als `<Parent>/<Steckplatz>` (`Local/1`), hinter einem Ethernet-Modul als `<Parent>/<Modulname>`. IP-Adressen kommen nie in die SBOM.

**Kodierung.** Die elektronische Kodierung steht an jeder Firmware als `<namespace>:device:ekey`. Ist sie an einem Modul aus (`Disabled`), kann das Modul im Feld jede Firmware haben; das ergibt `ekey-disabled` (Warnung). An der Steuerung selbst steht in Studio-5000-Exporten immer `Disabled`, dort ist das kein Befund.

**Hersteller.** Module mit der Herstellerkennung `1` bekommen den Hersteller `Rockwell Automation` mit `vendor:origin` = `assumed`, weil der Export nur die Kennung nennt. Andere Kennungen ergeben `supplier-unknown`; die Kennung steht als `<namespace>:vendorKey:VendorId` daneben. Ab M9 lässt sich der Hersteller über `vendorOverrides` in `machine.yml` erklären.

**Add-On Instructions.** Jede AOI wird eine Komponente vom Typ `library` mit `<namespace>:software:kind` = `add-on-instruction`. Version ist die `Revision` (`1.7`), ein Revisionszusatz steht als `<namespace>:aoi:revisionExtension`. Der Hersteller ist die Angabe `Vendor` der AOI; fehlt sie, `supplier-unknown`.

Alle gelesenen Stellen und Regeln: [docs/adapter-studio5000.md](docs/adapter-studio5000.md).

## 13. Was folgt

Die nächsten Schritte nach Meilensteinplan (CLAUDE.md Abschnitt 12): M1, M2 (CODESYS V3.5) und M5 (Rockwell Studio 5000) sind umgesetzt; alle Prüfregeln mit ihrer TR-Stelle stehen in [docs/regeln-tr03183.md](docs/regeln-tr03183.md). CODESYS V2.3 und CODESYS 4 (M3, M4) warten auf Demo-Projekte. Danach kommen Beckhoff TwinCAT 3, Siemens TIA Portal über den AML-Export, zuletzt TIA Portal über Openness. Mit M9 erzeugt `build` die SBOM aus einer `machine.yml`; ab dann beschreibt diese Anleitung auch die Maschinenbeschreibung.
