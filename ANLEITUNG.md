# Anleitung: PLC SBOM Builder (`plc-sbom`)

Stand: 01.11.2026, Version 0.1.0 (Prototyp). Diese Anleitung beschreibt, was das Werkzeug in dieser Version kann und wie man es aufruft. Was erst mit späteren Meilensteinen kommt, ist als „folgt“ gekennzeichnet. Hintergrund, Architektur und Planung stehen in [CLAUDE.md](CLAUDE.md), ein Überblick in [README.md](README.md).

## 1. Wofür das Werkzeug da ist

`plc-sbom` erzeugt aus den SPS-Projekten einer Maschine eine SBOM im Format CycloneDX 1.6 JSON und prüft sie gegen das CycloneDX-Schema und die Pflichtfelder der BSI TR-03183-2 v2.1.0. Es ist für den Aufruf in einer CI-Pipeline gebaut (Jenkins, GitLab CI, Azure DevOps): ein Aufruf, ein Exit-Code, eine Ergebnisdatei für die Pipeline.

Was eine Quelle nicht hergibt, schätzt das Werkzeug nicht. Fehlt eine Angabe, steht an der Komponente ein Lückenvermerk (Abschnitt 8), und die Prüfung meldet ihn als Warnung oder Hinweis.

Eine SBOM allein erfüllt den Cyber Resilience Act nicht. Das Werkzeug bewertet keine Schwachstellen; das übernimmt ein Überwachungssystem wie Dependency-Track.

## 2. Was in Version 0.1.0 geht

| Befehl | Zweck | Stand |
|---|---|---|
| `build` | SBOM aus `machine.yml` und den SPS-Projekten erzeugen, mit Hersteller- und Teil-SBOMs zusammenführen, prüfen, Ergebnisdatei schreiben | **nutzbar** für die Adapter `codesys` (Quelle `v35`), `studio5000`, `twincat` und `tia-aml`; Prüfbericht folgt (M10) |
| `validate` | vorhandene SBOM prüfen (Schema und TR-03183-2), Ergebnisdatei schreiben | **nutzbar** |
| `inspect` | Rohinventar eines Adapters ausgeben | **nutzbar für `codesys`** (CODESYS V3.5, PLCopen XML), **`studio5000`** (Rockwell, L5X), **`twincat`** (Beckhoff TwinCAT 3) und **`tia-aml`** (Siemens TIA Portal V17–V21, AML-Export, mit `--gsdml` für GSD-Geräte); `tia-portal` folgt (M14) |
| `report` | Prüfbericht als HTML oder Markdown | folgt (M10) |
| `compare` | SBOM mit dem Ist-Stand aus dem Feld abgleichen | folgt (M11) |
| `ids` | Kennungen gegen CSAF-Advisories prüfen | folgt (M12) |
| `export-aml`, `doctor` | TIA Portal Openness (nur Windows mit TIA V21) | folgt (M14) |

Ein Befehl, der noch fehlt, endet mit Exit 2 und nennt den Meilenstein:

```text
[NOT_IMPLEMENTED] Der Befehl „report“ ist in plc-sbom 0.1.0 noch nicht umgesetzt. Er folgt mit Meilenstein M10.
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

## 4. Eine SBOM erzeugen: `build`

```text
plc-sbom build --config machine.yml --release <kennung> [--commit <sha>]
               [--out build/sbom-maschine.cdx.json] [--report html|md|none] [--release-build] [--verbose]
```

| Option | Wirkung |
|---|---|
| `--config` | Maschinenbeschreibung (Abschnitt 5) |
| `--release` | Release-Kennung, Version der Maschine und der Anwenderprogramme, z. B. `R2026.10` oder `dev-142`; ohne Leerzeichen |
| `--commit` | Git-Commit, aus dem gebaut wird. Ohne Angabe die Umgebungsvariable `GIT_COMMIT` (Jenkins), sonst `git rev-parse HEAD` im Verzeichnis der Maschinenbeschreibung |
| `--out` | SBOM-Datei, Vorgabe `build/sbom-maschine.cdx.json`; das Verzeichnis wird angelegt |
| `--report` | Prüfbericht neben der SBOM; in Version 0.1.0 noch ohne Wirkung (M10), `none` unterdrückt den Hinweis |
| `--release-build` | Warnungen zählen als Befund, Exit 1 |

Ein Aufruf erledigt alles: Jede Station wird mit ihrem Adapter gelesen, die Angaben aus `machine.yml` kommen dazu (Hersteller, Kontakte, Lizenzen, CPE, passive Geräte), je Station entsteht das Anwenderprogramm mit dem Hash über seine Quellen, Hersteller- und Teil-SBOMs werden eingehängt. Danach prüft das Werkzeug die fertige Datei wie `validate` und schreibt SBOM und Ergebnisdatei.

Beispiel mit der Beispielmaschine aus dem Repository (`samples/maschine-xy`, gekürzt):

```text
> plc-sbom build --config samples/maschine-xy/machine.yml --release R2026.10 --commit a1b2c3d4e5f60718293a4b5c6d7e8f9012345678
[adapter codesys]     Station Verpackung: 1 Gerät, 10 Bibliotheken (CODESYS V3.5 SP15)
[adapter tia-aml]     Station PLC_1: 4 Geräte, 1 Bibliothek (Export 28.12.2024, TIA Portal V17)
[adapter twincat]     Station Roboterzelle: 10 Geräte, 14 Bibliotheken (TwinCAT XAE 3.1.4022.4)
[quellen]   1 Hersteller-SBOM (1 zugeordnet), 1 Teil-SBOM (syft), 1 GSDML-Datei
[merge]     Maschine XY > 3 Stationen > 49 Komponenten
[prüfung]   Schema CycloneDX 1.6 ............ OK
[prüfung]   Pflichtfelder BSI TR-03183-2 .... OK, 117 Warnungen, 109 Hinweise
            Warnung: [PARTIAL_SBOM_NOT_TR_CONFORM] Die Teil-SBOM samples/maschine-xy/sbom/teil/hmi-web.cdx.json erfüllt BSI TR-03183-2 nicht (39 Fehler, 0 Warnungen). Ihre Komponenten sind übernommen, fehlende Pflichtfelder mit input-field-missing vermerkt. Einzelheiten zeigt plc-sbom validate samples/maschine-xy/sbom/teil/hmi-web.cdx.json.
            Warnung: [LICENSE_UNKNOWN] plc-1-fw-gsd-device-1-0-0: Keine Distributionslizenz bekannt.
            ...
            109 Hinweise ausgeblendet, mit --verbose anzeigen.
[bericht]   Der Prüfbericht folgt mit Meilenstein M10; in dieser Version wird keiner geschrieben.
[ausgabe]   build/sbom-maschine.cdx.json
[ausgabe]   build/sbom-maschine.result.json
Fertig in 0,3 s
```

Die vielen Warnungen sind gewollt: Sie zeigen, was die Projekte nicht hergeben (Bibliotheksversionen als Platzhalter, Klemmen ohne Bestellnummer, Lizenzen nicht erklärt). Ein Entwicklungs-Build endet trotzdem mit Exit 0, ein Release-Build mit `--release-build` scheitert, bis die Warnungen behoben oder in `machine.yml` erklärt sind.

### 4.1 Was in der SBOM steht

| Komponente | Woher | Abhängigkeiten |
|---|---|---|
| Maschine (`metadata.component`, Typ `device`) | `machine` in `machine.yml`, Version = `--release` | alle Firmware-Komponenten, Anwenderprogramme und Teil-SBOMs |
| Firmware je Gerät mit eigener Firmware | Adapter | Bestandteile aus der Hersteller-SBOM, sonst keine (`unknown`) |
| Anwenderprogramm je Station (`<Station>, Anwenderprogramm`) | `machine.yml` und Git | Firmware der Steuerung, Bibliotheken, Laufzeit |
| Bibliotheken, Add-On Instructions, Laufzeit | Adapter oder `libraries` | – |
| Komponenten aus Hersteller- und Teil-SBOMs | die eingelesene Datei, unverändert | wie in der Datei |

**Anwenderprogramm.** Version ist das Release mit dem kurzen Commit, etwa `R2026.10 (git a1b2c3d)`. Hersteller ist der Maschinenbauer aus `machine`, Lizenz die aus `licenses` für ihn. Das Programm gilt als ausführbar, kein Archiv, unstrukturiert, weil es als übersetzter Code in die Steuerung geladen wird. Mit `program` rechnet das Werkzeug einen SHA-512 über die Quellen (Abschnitt 5.3); einen Hash der ausgelieferten Form gibt es nicht (`hash-unavailable`, Hinweis). Ohne `program` steht `program-source-missing` (Warnung).

**Zeitstempel und Seriennummer.** `metadata.timestamp` ist `SOURCE_DATE_EPOCH`, sonst die Commit-Zeit, sonst die aktuelle Uhrzeit mit Warnung `[TIMESTAMP_FROM_CLOCK]`. Die `serialNumber` ist eine UUID aus Maschine, Release und Commit. Gleiche Eingabe ergibt so dieselbe Datei, Byte für Byte, auf Linux und Windows.

**Pfade.** Alle Pfade in der SBOM sind relativ zum Git-Repository, in dem die Maschinenbeschreibung liegt (das Verzeichnis mit `.git`), mit `/`. Außerhalb eines Repositorys gelten sie relativ zur Maschinenbeschreibung.

### 4.2 Hersteller-SBOMs

Hersteller-SBOMs (`vendorSboms`) beschreiben die Firmware eines Geräts. Eine Datei gehört zu einer Firmware, wenn ihre `metadata.component`

- dieselbe CPE hat wie die Firmware (aus der CPE-Tabelle, Abschnitt 5.4), oder
- denselben Hersteller (`supplier.name`, sonst `manufacturer.name`), dieselbe Version (ein führendes `V` zählt nicht) und dieselbe Bestellnummer (ohne Leerzeichen) nennt. Die Bestellnummer steht dort als Property `<namespace>:device:orderNumber`, so wie `plc-sbom` sie selbst schreibt.

Dann hängen die Bestandteile der Hersteller-SBOM unter der Firmware, die Firmware bekommt deren Vollständigkeitsangabe, und `vendor-sbom-missing` entfällt. Nennt die Hersteller-SBOM Dateiname (`bsi:component:filename`) und SHA-512 der Firmware-Datei, übernimmt die Firmware beides; `hash-unavailable` und `filename-unavailable` entfallen. Die Wurzel der Hersteller-SBOM steht nicht noch einmal in der SBOM, sie ist die Firmware selbst. Passt keine Firmware, bleibt die Datei draußen (Warnung `[VENDOR_SBOM_UNUSED]`). Ohne Firmware-Version im Projekt gibt es keine Zuordnung, es sei denn über die CPE.

### 4.3 Teil-SBOMs

Teil-SBOMs (`partialSboms`) kommen aus Syft, cdxgen oder ähnlichen Werkzeugen, etwa für eine Web-Oberfläche. Ihre Hauptkomponente wird eine Komponente der Maschine; mit `station` trägt sie `<namespace>:station`. Die Hauptkomponente hängt von allen Komponenten der Teil-SBOM ab, auf die sonst nichts zeigt.

### 4.4 Was mit eingelesenen SBOMs geschieht

- Jede `bom-ref` bekommt einen Präfix aus dem Dateinamen (`vendor-<datei>/…`, `partial-<datei>/…`), damit sie in der Maschine eindeutig ist. Komponenten ohne `bom-ref` bekommen eine.
- Die effektive Lizenz (`bsi:component:effectiveLicence`) wird entfernt. Sie gibt an, unter welcher Lizenz der Ersteller der jeweiligen SBOM eine Komponente nutzt, und muss beim Zusammenführen neu gesetzt werden (TR-03183-2 Abschnitt 3.2.8).
- Jede Datei wird für sich gegen die TR geprüft. Erfüllt sie sie nicht, gibt es eine Warnung (`[VENDOR_SBOM_NOT_TR_CONFORM]`, `[PARTIAL_SBOM_NOT_TR_CONFORM]`), und jede Komponente, der Pflichtfelder fehlen, bekommt den Lückenvermerk `input-field-missing` (Warnung). Werte, die vorhanden, aber ungültig sind (etwa eine Distributionslizenz als Freitext), bleiben Fehler.
- Jede Komponente trägt `<namespace>:inputSbom` mit der Quelldatei; die Hauptkomponente einer Teil-SBOM und die Firmware zu einer Hersteller-SBOM zusätzlich `<namespace>:inputSbom:sha256`.
- Eine Datei, die kein JSON ist, keine CycloneDX-SBOM, keine `metadata.component` hat oder eine `bom-ref` doppelt führt, ist ein Fehler (`[INPUT_SBOM_INVALID]`, Exit 1); sie wird nicht übernommen.

## 5. Die Maschinenbeschreibung `machine.yml`

Eine Datei je Maschine, im Repository der Maschine. Pfade gelten relativ zu dieser Datei. Unbekannte Schlüssel, falsche Typen und Schlüssel, die für den Adapter einer Station nicht gelten, beenden den Lauf mit Exit 2 und `[CONFIG_INVALID]` samt Zeilennummer, damit ein Tippfehler nicht still übergangen wird. Werte werden als Text gelesen, wie sie dastehen (`version: 1.10` bleibt `1.10`).

Die Beispielmaschine `samples/maschine-xy/machine.yml`:

```yaml
machine:
  name: Maschine XY
  supplier: Musterbau GmbH
  contact: sbom@musterbau.example
namespace: firma

gsdml: [../../fixtures/gsdml]
vendorContacts:
  "Siemens AG": https://www.siemens.com
  "Beckhoff Automation GmbH & Co. KG": https://www.beckhoff.com
  "Leuze electronic GmbH + Co. KG": https://www.leuze.com
licenses:
  "Musterbau GmbH": LicenseRef-musterbau-proprietary
  "Siemens AG": LicenseRef-siemens-proprietary
  "Beckhoff Automation GmbH & Co. KG": LicenseRef-beckhoff-proprietary
sourceUrl: "https://git.musterbau.example/maschine-xy/-/tree/{commit}/{path}"

stations:
  - name: Verpackung
    adapter: codesys
    source: v35
    path: ../../fixtures/codesys/v35/robin-codesys_project.xml
    program: programm/Verpackung

  - name: PLC_1
    adapter: tia-aml
    path: ../../fixtures/tia-aml/plc-commissioning-sample_device.aml
    plc: PLC_1
    devices: ["GSD device_1"]
    libraries:
      - { name: LGF, vendor: Siemens AG, version: "7.0.1" }
    program: programm/PLC_1

  - name: Roboterzelle
    adapter: twincat
    path: ../../fixtures/twincat/aixocat-heatpump/HeatPumpTestBenchControl.tsproj
    program: programm/Roboterzelle

vendorSboms: sbom/lieferanten/*.cdx.json
partialSboms:
  - path: sbom/teil/hmi-web.cdx.json
    station: PLC_1
cpeMap: kennungen/cpe-map.yml
```

### 5.1 Schlüssel der obersten Ebene

| Schlüssel | Pflicht | Inhalt |
|---|---|---|
| `machine.name`, `machine.supplier` | ja | Name der Maschine und Maschinenbauer; der Maschinenbauer ist Ersteller der SBOM und Hersteller der Anwenderprogramme |
| `machine.contact` | nein, aber TR-Pflicht | E-Mail-Adresse (auch `mailto:…`) oder URL (`https://…`) des Maschinenbauers. Fehlt sie, meldet die Prüfung `SBOM_CREATOR_MISSING` (Fehler) |
| `namespace` | nein | Namespace der eigenen Properties, Vorgabe `plcsbom`; `cdx` und `bsi` sind vergeben |
| `stations` | ja | Liste der Stationen (Abschnitt 5.2) |
| `gsdml` | nein | Verzeichnisse mit GSDML-Dateien (TIA, Abschnitt 16) |
| `deviceDescriptions` | nein | Verzeichnisse mit CODESYS-Gerätebeschreibungen `*.devdesc.xml` (Abschnitt 13) |
| `vendorOverrides` | nein | Muster → Hersteller eines Geräts, der erste Treffer in Dateireihenfolge gilt; Herkunft `declared`, `supplier-unknown` entfällt |
| `passiveOrderNumbers` | nein | Muster für Geräte ohne eigene Firmware; sie gehen in den Hardware-Anhang statt in die SBOM |
| `vendorContacts` | nein | Hersteller → E-Mail-Adresse oder URL. Schließt `creator-contact-unknown` für die Firmware und Software dieses Herstellers; Komponenten aus eingelesenen SBOMs bleiben, wie sie sind |
| `licenses` | nein | Hersteller → Distributionslizenz als SPDX-Kennung, SPDX-Ausdruck oder `LicenseRef-…`. Gilt für Firmware und Software dieses Herstellers, für die Maschine und die Anwenderprogramme über den Maschinenbauer. Schließt `license-unknown` |
| `sourceUrl` | nein | URL der Programmquellen mit `{commit}` und `{path}` (Abschnitt 5.3) |
| `vendorSboms` | nein | Muster (eines oder eine Liste) für Hersteller-SBOMs, `*` und `?` je Pfadteil, `**` über Verzeichnisse |
| `partialSboms` | nein | Liste von `path` und optional `station` |
| `cpeMap` | nein | CPE-Tabelle (Abschnitt 5.4) |
| `protectedBlockVendors` | nein | für den Adapter `tia-portal` (M14), wird bis dahin nur gelesen |

**Muster** in `vendorOverrides` und `passiveOrderNumbers`: `*` für beliebig viele Zeichen, `?` für genau eines, mit Groß-/Kleinschreibung. Sie treffen die Bestellnummer, mit und ohne Leerzeichen (`"6ES7511*"` trifft `6ES7 511-1AK02-0AB0`). Ein Gerät ohne Bestellnummer, etwa eine EtherCAT-Klemme, treffen sie über die Typbezeichnung (`"EL9011"`). Als passiv erklären lässt sich nur ein Gerät ohne Firmware-Version im Projekt, und nie die Steuerung selbst; es trägt dann `<namespace>:device:passive` = `declared`.

**Hersteller** werden über den Namen genau so verglichen, wie er in der SBOM steht. Beckhoff-Klemmen heißen `Beckhoff Automation GmbH & Co. KG`, Beckhoff-Bibliotheken nennen `Beckhoff Automation GmbH`; für beide braucht es je einen Eintrag.

### 5.2 Stationen

| Schlüssel | Adapter | Inhalt |
|---|---|---|
| `name` | alle | Name der Station, eindeutig in der Maschine |
| `adapter` | alle | `codesys`, `studio5000`, `twincat`, `tia-aml`, `tia-portal` |
| `path` | alle | Projektdatei oder Export |
| `program` | alle | Quellverzeichnis (oder eine Datei) des Anwenderprogramms |
| `source` | `codesys`, Pflicht | `v35`; `v23` und `c4` folgen mit M3 und M4 |
| `plc` | `codesys`, `tia-aml`, `tia-portal` | Steuerung bzw. CPU in der Quelle; Pflicht, wenn sie mehrere enthält |
| `devices`, `ignoreDevices` | `tia-aml`, `tia-portal` | weitere bzw. ausgeschlossene Geräte (Abschnitt 16) |
| `libraries` | `tia-aml` | deklarierte Bibliotheken mit `name`, optional `vendor` und `version` |
| `allowUpgrade`, `globalLibraries` | `tia-portal` | folgt mit M14 |

Der Adapter `tia-portal` endet in Version 0.1.0 mit `[NOT_IMPLEMENTED]` (M14), die Quellen `v23` und `c4` des Adapters `codesys` ebenso (M3, M4). Mehrere `tia-aml`-Stationen dürfen dieselbe AML-Datei lesen; sie wird einmal gelesen.

### 5.3 Hash über die Programmquellen

Mit `program` rechnet das Werkzeug einen SHA-512 über das Verzeichnis: alle Dateien darunter, Pfad relativ mit `/`, ordinal sortiert, je Datei eine Zeile `<pfad>\0<sha512 des inhalts>\n`, darüber SHA-512. Ist `program` eine Datei, ist es der SHA-512 ihres Inhalts. In der SBOM stehen Pfad und Anzahl der Dateien als `<namespace>:sourceCode:path` und `<namespace>:sourceCode:files`.

Mit `sourceUrl` wird daraus die Quellcode-URI (TR-03183-2 Abschnitt 5.2.4): `{commit}` ist der volle Commit, `{path}` der Pfad des Programms im Repository. Der Hash steht dann unter `externalReferences` vom Typ `source-distribution`. Ohne `sourceUrl` oder ohne bekannten Commit steht er als Property `<namespace>:sourceCode:sha512`.

Der Hash gilt für die Bytes, wie Git sie auscheckt. Mit `core.autocrlf=true` auf einem Windows-Agenten ändern sich Zeilenenden und damit der Hash. Für die Programmverzeichnisse deshalb feste Zeilenenden in `.gitattributes` setzen, etwa `programm/** -text`.

### 5.4 CPE-Tabelle

Die Tabelle aus `cpeMap` ordnet Hersteller und Bestellnummer eine CPE zu. Das Werkzeug setzt eine CPE nur, wenn eine Zeile passt und die Firmware-Version bekannt ist; aus dem Typnamen leitet es nie eine ab. `{version}` ist die Firmware ohne führendes `V`.

```yaml
- vendor: Siemens AG
  orderNumber: "6ES7 214-1AG4?-0XB0"
  cpe: "cpe:2.3:o:siemens:simatic_s7-1200_cpu_firmware:{version}:*:*:*:*:*:*:*"
```

Jede Zeile braucht `vendor`, `orderNumber` (Muster wie oben) und `cpe` mit `{version}`; sonst Exit 2 mit `[CPE_MAP_INVALID]`.

## 6. Eine SBOM prüfen: `validate`

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

### 6.1 Was geprüft wird

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

### 6.2 Fehlendes Feld mit und ohne Lückenvermerk

Fehlt ein Pflichtfeld und erklärt kein Lückenvermerk das Fehlen, ist das ein **Fehler**. Steht an der Komponente ein passender Lückenvermerk, meldet die Prüfung nur diesen, als Warnung oder Hinweis je nach Code (Abschnitt 8).

Lückenvermerke stehen als Property `<namespace>:gap` an der Komponente, z. B. `plcsbom:gap` = `hash-unavailable`. Jeder Namespace außer `cdx` und `bsi` zählt.

Eine logische Komponente (Property `<namespace>:logicalComponent` = `true`, etwa die Maschine selbst) braucht nach TR Abschnitt 3.2.2 weder Dateiname noch Hash noch die drei Dateieigenschaften. Die Prüfung vermerkt das als Hinweis `LOGICAL_COMPONENT`.

**Fremde SBOMs**, etwa von Komponentenherstellern oder aus Syft, kennen diese Lückenvermerke meist nicht. Prüft man sie mit `validate`, erscheinen fehlende Pflichtfelder deshalb als Fehler. Das ist gewollt: Die Datei erfüllt die TR dann nicht. Führt `build` sie mit der Maschine zusammen, vermerkt es die Lücken mit `input-field-missing` (Abschnitt 4.4).

## 7. Schweregrade und Exit-Codes

| Schwere | Bedeutung | Wirkung auf den Exit-Code |
|---|---|---|
| Fehler | Schema verletzt, Pflichtfeld fehlt ohne Lückenvermerk, Datei unlesbar | immer Exit 1 |
| Warnung | Pflichtangabe fehlt, Lücke ist vermerkt | Exit 0, mit `--release-build` Exit 1 |
| Hinweis | Lücke, die die TR zulässt oder die erst der Ist-Stand schließt | nie |

| Exit | Bedeutung | Wer ist dran |
|---|---|---|
| 0 | erfolgreich (Warnungen im Entwicklungs-Build und Hinweise ändern daran nichts) | – |
| 1 | Befund: Prüfung nicht bestanden | Entwickler, Projekt |
| 2 | Aufruf oder Konfiguration falsch: `machine.yml` ungültig, Datei fehlt, unbekannte Option, Befehl noch nicht umgesetzt | Repository |
| 3 | Umgebung, z. B. Openness-Befehl auf Linux (`OPENNESS_WINDOWS_ONLY`) | Agent-Betreuer |
| 4 | Abbruch durch unerwarteten Fehler (`UNEXPECTED`) | Agent-Betreuer |

Die Exit-Codes sind dieselben wie bei `plc-build`.

## 8. Lückencodes

Der Code im Lückenvermerk ist klein geschrieben mit Bindestrich (`hash-unavailable`), in der Konsolenmeldung groß mit Unterstrich (`[HASH_UNAVAILABLE]`).

| Code | Bedeutung | Schwere | Erklärt fehlendes Feld |
|---|---|---|---|
| `firmware-not-in-project` | Das Projekt führt keine Firmware-Version für dieses Gerät, z. B. EtherCAT-Klemmen | Hinweis | Version |
| `firmware-projected-only` | Firmware nur als projektierter Sollwert bekannt, kein Ist-Stand | Hinweis | – |
| `firmware-attribute-missing` | TIA: Das Gerät hat im AML kein Attribut `FirmwareVersion` (etwa eine Stromversorgung); ob es eigene Firmware hat, ist offen. Passive Geräte mit `passiveOrderNumbers` erklären | Warnung | – |
| `hash-unavailable` | Die ausgelieferte Form liegt nicht vor, kein SHA-512 bestimmbar (TR Abschnitt 3.2.1) | Hinweis | Hash |
| `filename-unavailable` | Die Komponente liegt nicht als Datei vor | Hinweis | Dateiname |
| `vendor-sbom-missing` | Für diese Firmware liegt keine Hersteller-SBOM vor | Hinweis | – |
| `device-unassigned` | Das Gerät gehört zu keiner Station | Hinweis | – |
| `devices-not-read` | Die Quelle enthält Geräte, die der Adapter nicht ausliest (TwinCAT: PROFINET-Geräte, E/A in eigener Datei); sie fehlen in der SBOM | Warnung | – |
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
| `gsdml-not-found` | Die GSDML-Datei des Geräts liegt in keinem der angegebenen Verzeichnisse, liegt dort mehrfach mit verschiedenem Inhalt, oder es ist kein Verzeichnis angegeben | Warnung | – |
| `devdesc-not-found` | CODESYS: Zur Gerätekennung liegt keine oder mehr als eine Gerätebeschreibung in den angegebenen Verzeichnissen | Warnung | – |
| `order-number-not-in-project` | Weder Projekt noch GSDML nennen eine Bestellnummer | Warnung | – |
| `input-field-missing` | Die Komponente stammt aus einer eingelesenen Hersteller- oder Teil-SBOM, der Pflichtfelder fehlen | Warnung | Ersteller, Version, Dateiname, Distributionslizenz, Hash, ausführbar, Archiv, strukturiert |

Die Begründung je Code mit der TR-Stelle steht in [docs/regeln-tr03183.md](docs/regeln-tr03183.md) Abschnitt 7.

## 9. Ergebnisdatei

`build` und `validate` schreiben neben die SBOM eine Ergebnisdatei: aus `build/sbom-maschine.cdx.json` wird `build/sbom-maschine.result.json`. Die Feldnamen sind Schnittstelle.

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
| `findings` | alle Meldungen mit Schwere, Code, Komponente, betroffenen TR-Feldern und Text; bei `build` auch die Warnungen der Adapter und zu eingelesenen SBOMs, Commit und Zeitstempel |

`command` ist `build` oder `validate`. Endet `build` mit Exit 2, 3 oder 4, schreibt es die Ergebnisdatei trotzdem: `exitCode` nennt den Exit, `errors` ist 1, und der einzige Eintrag in `findings` trägt den Code der Ursache, etwa `CONFIG_NOT_FOUND`. Eine SBOM entsteht dann nicht. So kann die Pipeline die Datei immer lesen.

## 10. In der Pipeline

Mit der Ergebnisdatei kann die Pipeline einen Entwicklungs-Build mit Warnungen als instabil markieren, ohne dass der Exit-Code dafür herhalten muss. Beispiel für Jenkins:

```groovy
stage('SBOM') {
  steps {
    script {
      def release = env.TAG_NAME ?: "dev-${env.BUILD_NUMBER}"
      def gate = env.TAG_NAME ? '--release-build' : ''
      def rc = sh(returnStatus: true, script: "plc-sbom build --config machine.yml --release ${release} ${gate}")
      def result = readJSON file: 'build/sbom-maschine.result.json'
      if (rc == 1) error "SBOM-Prüfung fehlgeschlagen: ${result.errors} Fehler, ${result.warnings} Warnungen"
      if (rc >= 2) error "SBOM-Lauf abgebrochen (Exit ${rc}), siehe Log"
      if (result.warnings > 0) unstable "SBOM mit ${result.warnings} Warnungen"
    }
  }
}
stage('Archiv') {
  steps { archiveArtifacts artifacts: 'build/sbom-*.cdx.json, build/*.result.json' }
}
```

Jenkins setzt `GIT_COMMIT`; `--commit` ist dann nicht nötig. Für reproduzierbare Dateien bei gleichem Commit reicht die Commit-Zeit, `SOURCE_DATE_EPOCH` muss nicht gesetzt werden. Auf Windows-Agenten `bat` statt `sh`. `readJSON` stammt aus dem Plugin „Pipeline Utility Steps“. Ein vollständiges Jenkinsfile folgt mit M13 unter `examples/Jenkinsfile`.

## 11. Häufige Meldungen

| Meldung | Ursache | Abhilfe |
|---|---|---|
| `[CONFIG_NOT_FOUND]`, `[CONFIG_NOT_READABLE]` | Die Maschinenbeschreibung aus `--config` fehlt oder ist nicht lesbar | Pfad prüfen; relativ zum Arbeitsverzeichnis |
| `[CONFIG_INVALID]` | `machine.yml` ist kein gültiges YAML, enthält einen unbekannten Schlüssel, einen Schlüssel, der für den Adapter nicht gilt, oder einen ungültigen Wert | Datei und Zeile stehen in der Meldung; Abschnitt 5 |
| `[RELEASE_INVALID]` | `--release` ist leer oder enthält Leerzeichen | z. B. `R2026.10` oder `dev-142` |
| `[COMMIT_INVALID]` | `--commit` oder `GIT_COMMIT` ist kein Git-Commit | 7 bis 64 Hexadezimalzeichen |
| `[SOURCE_DATE_EPOCH_INVALID]` | `SOURCE_DATE_EPOCH` ist keine Zahl von Sekunden | Variable korrigieren oder entfernen |
| `[PROGRAM_NOT_FOUND]` | Das Verzeichnis aus `program` fehlt | Pfad prüfen (relativ zu `machine.yml`) |
| `[PARTIAL_SBOM_NOT_FOUND]` | Eine Teil-SBOM aus `partialSboms` fehlt | Schritt, der sie erzeugt (Syft, cdxgen), vor `build` ausführen |
| `[CPE_MAP_NOT_FOUND]`, `[CPE_MAP_INVALID]` | Die CPE-Tabelle fehlt oder ist fehlerhaft | Abschnitt 5.4 |
| `[OUTPUT_NOT_WRITABLE]` | SBOM oder Ergebnisdatei lassen sich nicht schreiben | Pfad und Rechte von `--out` prüfen |
| `[INPUT_SBOM_INVALID]` (Fehler) | Eine Hersteller- oder Teil-SBOM ist kein JSON, keine CycloneDX-SBOM, ohne `metadata.component` oder mit doppelter `bom-ref` | Datei vom Lieferanten bzw. Werkzeug neu beziehen; sie wird nicht übernommen |
| `[COMMIT_UNKNOWN]` (Warnung) | Kein Commit aus `--commit`, `GIT_COMMIT` oder git | Im Git-Repository bauen oder `--commit` angeben |
| `[TIMESTAMP_FROM_CLOCK]` (Warnung) | Zeitstempel ist die aktuelle Uhrzeit; zwei Läufe ergeben verschiedene Dateien | Im Git-Repository bauen oder `SOURCE_DATE_EPOCH` setzen |
| `[VENDOR_SBOMS_NOT_FOUND]` (Warnung) | Ein Muster unter `vendorSboms` trifft keine Datei | Muster und Verzeichnis prüfen |
| `[VENDOR_SBOM_UNUSED]`, `[VENDOR_SBOM_NO_KEY]` (Warnung) | Eine Hersteller-SBOM passt zu keiner Firmware, oder sie nennt weder CPE noch Bestellnummer | Abschnitt 4.2; Hersteller, Bestellnummer und Version mit dem Projekt vergleichen |
| `[VENDOR_SBOM_NOT_TR_CONFORM]`, `[PARTIAL_SBOM_NOT_TR_CONFORM]` (Warnung) | Die eingelesene SBOM erfüllt die TR nicht | `plc-sbom validate <datei>` zeigt die Einzelheiten; Abschnitt 4.4 |
| `[INPUT_SBOM_REF_UNKNOWN]` (Warnung) | Eine eingelesene SBOM verweist in `dependencies` auf `bom-ref`s, die es in ihr nicht gibt | Die Verweise sind nicht übernommen; Datei beim Lieferanten melden |
| `[SBOM_NOT_FOUND]` | Die angegebene Datei gibt es nicht | Pfad prüfen; relative Pfade gelten ab dem Arbeitsverzeichnis |
| `[JSON_INVALID]` | Die Datei ist kein gültiges JSON | Datei mit einem JSON-Werkzeug prüfen, oft abgeschnitten oder falsch kodiert |
| `[NOT_CYCLONEDX]` | `bomFormat` fehlt oder ist nicht `CycloneDX` | Die Datei ist vermutlich SPDX oder ein anderes Format; SPDX prüft das Werkzeug nicht |
| `[SPEC_VERSION_UNSUPPORTED]` | CycloneDX älter als 1.6 | SBOM mit CycloneDX 1.6 neu erzeugen |
| `[NOT_IMPLEMENTED]` | Befehl, Adapter (`tia-portal`) oder Quelle (`codesys` mit `v23`, `c4`) folgt in einem späteren Meilenstein | Abschnitt 2 |
| `[OPENNESS_WINDOWS_ONLY]` | `export-aml` oder `doctor --adapter tia-portal` außerhalb von Windows | Schritt auf einem Windows-Agenten mit TIA Portal V21 ausführen |
| `[USAGE]` | Option fehlt oder ist unbekannt | `plc-sbom <befehl> --help` |
| `[SOURCE_NOT_FOUND]` | Die Projektdatei bzw. der Export fehlt | Pfad prüfen; Export eingecheckt? |
| `[XML_INVALID]` | Die Datei ist kein gültiges XML oder enthält eine DTD | Export neu erzeugen; DTDs werden aus Sicherheitsgründen nie gelesen |
| `[NOT_PLCOPEN]` | Die Datei ist kein PLCopen-XML-Export | In CODESYS V3.5 das ganze Projekt als PLCopen XML exportieren |
| `[CODESYS_PLC_REQUIRED]` | Der Export enthält mehrere Steuerungen | Steuerung mit `plc` in `machine.yml` wählen |
| `[DEVDESC_DIR_NOT_FOUND]` | Ein Verzeichnis aus `deviceDescriptions` fehlt | Pfad in `machine.yml` prüfen (relativ zur Konfigurationsdatei) |
| `[NOT_DEVDESC]` | Eine `*.devdesc.xml` ist keine CODESYS-Gerätebeschreibung | Datei prüfen oder aus dem Verzeichnis entfernen |
| `[CODESYS_PLC_NOT_FOUND]` | Die mit `plc` genannte Steuerung steht nicht im Export | Namen aus der Meldung oder aus `inspect` übernehmen |
| `[NOT_L5X]` | Die Datei ist kein L5X-Export aus Studio 5000 | In Studio 5000 das Projekt mit „Speichern unter … L5X“ exportieren |
| `[NOT_TSPROJ]` | Die Datei ist keine TwinCAT-3-Projektdatei | Die `.tsproj`-Datei des Projekts angeben, nicht die `.sln` oder `.plcproj` |
| `[TWINCAT_FILE_NOT_FOUND]` | Eine `.plcproj` oder `_Config/PLC/*.xti`, auf die das Projekt verweist, fehlt | Datei einchecken; der Pfad steht in der Meldung |
| `[TWINCAT_PLCPROJ_MISSING]` | Ein SPS-Projekt im `.tsproj` nennt keine `.plcproj` | Projekt in TwinCAT öffnen und speichern |
| `[STUDIO5000_NOT_CONTROLLER]` | Der L5X-Export enthält nur einen Bestandteil (etwa eine AOI), nicht den ganzen Controller | Das ganze Projekt als L5X speichern, nicht einzelne Bausteine exportieren |
| `[NOT_AML]` | Die Datei ist keine AutomationML-Datei (Wurzel nicht `CAEXFile`) | Die `.aml`-Datei aus dem CAx-Export angeben |
| `[NOT_TIA_AML]` | Die AML-Datei stammt nicht aus TIA Portal (etwa aus dem TIA Selection Tool) | In TIA Portal über Extras → CAx-Daten exportieren |
| `[TIA_AML_PLC_REQUIRED]` | Die AML-Datei enthält mehrere CPUs, oder mehrere Stationen lesen sie, und `plc` fehlt | CPU mit `plc` wählen; die Namen stehen in der Meldung und in `inspect` |
| `[TIA_AML_PLC_NOT_FOUND]` | Die mit `plc` genannte CPU steht nicht in der AML-Datei | Namen aus der Meldung oder aus `inspect` übernehmen (genaue Schreibweise) |
| `[TIA_AML_DEVICE_IN_TWO_STATIONS]` | Ein Gerät gehört zu zwei Stationen (gleiche CPU zweimal, oder `devices` greift ein Gerät am IO-System einer anderen Station) | `plc` und `devices` der Stationen prüfen |
| `[TIA_AML_…]` als Warnung | `WRITER_VERSION_UNKNOWN`, `ROLE_UNKNOWN`, `TYPE_IDENTIFIER_UNKNOWN`, `CATALOG_VERSION_DIFFERS`, `IOSYSTEM_WITHOUT_CPU`, `PATTERN_UNUSED`, `EXPORT_TIME_INVALID`, `GSDML_ITEM_NOT_FOUND`: etwas in der Datei war nicht vollständig auswertbar; der Lauf geht weiter | Meldung lesen; bei `PATTERN_UNUSED` Tippfehler in `devices` oder `ignoreDevices` prüfen; bei `GSDML_ITEM_NOT_FOUND` prüfen, ob die GSDML-Datei zur Version im Projekt passt |
| `[GSDML_DIR_NOT_FOUND]` | Ein Verzeichnis aus `gsdml` bzw. `--gsdml` fehlt | Pfad prüfen (in `machine.yml` relativ zur Konfigurationsdatei) |
| `[NOT_GSDML]` | Die Datei mit dem gesuchten GSDML-Namen ist keine GSDML-Datei | Datei prüfen oder ersetzen |
| `[GSDML_NOT_FOUND]`, `[GSDML_AMBIGUOUS]` als Warnung von `inspect` | Die GSDML-Datei eines GSD-Geräts liegt nicht im Verzeichnis, oder mehrfach mit verschiedenem Inhalt | GSDML-Datei einchecken bzw. die überzählige Kopie entfernen |

## 12. Projekt ansehen: `inspect`

```text
plc-sbom inspect <projektdatei> --adapter <id> [--gsdml <verzeichnis>]
```

`inspect` gibt das Rohinventar eines Adapters als JSON auf der Konsole aus: was der Adapter in der Datei findet, unverändert und noch ohne Lückenregeln. Damit lässt sich prüfen, ob ein Export das Erwartete enthält, und später die `machine.yml` schreiben. In Version 0.1.0 geht das für die Adapter `codesys`, `studio5000`, `twincat` und `tia-aml`; für `tia-portal` endet der Aufruf mit Exit 2 und `[NOT_IMPLEMENTED]` und nennt den Meilenstein. `--gsdml` gilt nur für `tia-aml` und nennt ein Verzeichnis mit GSDML-Dateien (Abschnitt 16).

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

Beispiel für `twincat` (gekürzt, aus einem echten Lauf):

```text
> plc-sbom inspect "fixtures/twincat/spt-ethercat-diagnostics/SPT EtherCAT Diagnostics.tsproj" --adapter twincat
{
  "format": 1,
  "adapter": "twincat",
  "file": "fixtures/twincat/spt-ethercat-diagnostics/SPT EtherCAT Diagnostics.tsproj",
  "sha256": "225630ab0ee3d008e41fc7923e922dfdf8b8e5b652a36a5f0582e518d9984813",
  "project": {
    "tcVersion": "3.1.4026.24",
    "ioDevices": [
      {
        "name": "Device 1 (EtherCAT)",
        "devType": "111",
        "disabled": false,
        "boxes": [
          {
            "id": "1",
            "name": "Term 1 (EK1100)",
            "fieldbus": "EtherCAT",
            "vendorId": "#x00000002",
            "productCode": "#x044c2c52",
            "revisionNo": "#x00120000",
            "desc": "EK1100",
            "type": "EK1100 EtherCAT Coupler (2A E-Bus)",
            "children": [ { "id": "2", "name": "Term 2 (EL6910)", ... }, { "id": "9", "name": "Term 9 (EL9011)", ... } ]
          }
        ]
      }
    ],
    "plcProjects": [
      {
        "name": "EtherCatDiagnosticsSample",
        "file": "EtherCatDiagnosticsSample/EtherCatDiagnosticsSample.plcproj",
        "programVersion": "3.1.4026.24",
        "libraries": [ { "kind": "placeholder", ... }, ... ]
      }
    ]
  }
}
```

| Feld | Inhalt |
|---|---|
| `tcVersion` | Version der Engineering-Umgebung (XAE), nicht der Laufzeit |
| `ioDevices[].disabled` | E/A-Gerät im Projekt deaktiviert |
| `boxes[].fieldbus` | `EtherCAT` oder `Profinet`; nur EtherCAT-Teilnehmer kommen in die SBOM |
| `boxes[].revisionNo` | Revision aus der Gerätebeschreibung (ESI), nicht die Firmware |
| `libraries[].kind` | `placeholder` (`PlaceholderReference`), `direct` (`LibraryReference`), `resolutionOnly` (`PlaceholderResolution` ohne eigenen Platzhalter) |

AMS-Net-IDs, IP-Adressen und der Beckhoff Identification Code (BIC) erscheinen auch in `inspect` nicht.

Für `tia-aml` zeigt `inspect` die CPUs mit ihren IO-Systemen und je Gerät, zu welcher CPU es als Station gehören würde. Damit schreibt man `plc` und `devices` in die `machine.yml`. Beispiel aus der synthetischen Datei mit zwei CPUs (gekürzt):

```text
> plc-sbom inspect Linie1.aml --adapter tia-aml
[TIA_AML_CATALOG_VERSION_DIFFERS] Linie1.aml: S71500/ET200MP station_1/DI 16x24VDC HF_1 hat im TypeIdentifier die Version V2.1, als FirmwareVersion aber V2.2. In die SBOM geht die FirmwareVersion.
{
  "format": 1,
  "adapter": "tia-aml",
  "file": "Linie1.aml",
  "inspection": {
    "tool": { "name": "TIA Portal", "version": "V21", "release": "2100.0.121.1", "vendor": "Siemens AG" },
    "cpus": [
      { "name": "PLC_1", "device": "S71500/ET200MP station_1", "typeName": "CPU 1511-1 PN", "firmwareVersion": "V2.8", "ioSystems": [ "PROFINET IO-System" ] },
      { "name": "PLC_2", "device": "S71500/ET200MP station_2", "typeName": "CPU 1515-2 PN", "firmwareVersion": "V2.9", "ioSystems": [ "PROFINET IO-System" ] }
    ],
    "devices": [
      {
        "name": "ET 200SP station_1",
        "folder": "Ungrouped devices",
        "typeIdentifier": "System:Device.ET200SP",
        "assignedTo": [ { "cpu": "PLC_1", "via": "io-system" } ],
        "items": [
          { "name": "IO_Device_1", "position": "0/0", "typeIdentifier": "OrderNumber:6ES7 155-6AU01-0CN0", "typeName": "IM 155-6 PN HF", "firmwareVersion": "V4.2", "deviceItemType": "HeadModule", "treatment": "firmware" },
          { "name": "BA 2xRJ45", "position": "0/0/127", "typeIdentifier": "OrderNumber:6ES7 193-6AR00-0AA0", "typeName": "BA 2xRJ45", "firmwareVersion": "V0.0", "treatment": "passive" },
          ...
        ]
      },
      ...
    ],
    "warnings": [ { "code": "TIA_AML_CATALOG_VERSION_DIFFERS", "message": "..." } ]
  }
}
```

| Feld | Inhalt |
|---|---|
| `cpus[].ioSystems` | PROFINET-IO-Systeme, die der CPU gehören |
| `devices[].assignedTo` | CPU, zu deren Station das Gerät gehört: `via` = `cpu` (enthält die CPU) oder `io-system` (an ihrem IO-System); leer: an keinem IO-System, kommt nur über `devices` in eine Station |
| `items[].treatment` | `firmware`, `firmwareAttributeMissing` (kein Attribut `FirmwareVersion`), `passive` (`V0.0`), `rack`, `gsdDevice`, `gsdModule`, `structure`, `unknown` |

Warnungen stehen im JSON und zusätzlich auf stderr. IP-Adressen, Subnetzmasken und Variablentabellen erscheinen nicht.

Mit `--gsdml <verzeichnis>` steht bei jedem GSD-Teil zusätzlich, was die GSDML-Datei sagt. Beispiel aus der V17-Fixture (gekürzt):

```text
> plc-sbom inspect fixtures/tia-aml/plc-commissioning-sample_device.aml --adapter tia-aml --gsdml fixtures/gsdml/
...
  {
    "name": "BCL248i",
    "position": "0/0",
    "typeIdentifier": "GSD:GSDML-V2.41-LEUZE-BCL248I-20211213.XML/DAP/DAP",
    "typeName": "BCL248i",
    "deviceItemType": "HeadModule",
    "treatment": "gsdDevice",
    "gsdml": {
      "file": "fixtures/gsdml/GSDML-V2.41-LEUZE-BCL248i-20211213.xml",
      "vendor": "Leuze electronic GmbH + Co. KG",
      "orderNumber": "501xxxxx",
      "softwareRelease": "V1.*.*",
      "hardwareRelease": "3"
    }
  },
...
```

Liegt die Datei nicht im Verzeichnis, fehlt `gsdml`, und auf stderr steht etwa:

```text
[GSDML_NOT_FOUND] Die GSDML-Datei GSDML-V2.41-LEUZE-BCL248I-20211213.XML liegt in keinem der Verzeichnisse aus „gsdml“.
```

## 13. CODESYS V3.5

Eingang ist der PLCopen-XML-Export des ganzen Projekts. Die `.project`-Datei ist binär und wird nicht gelesen. Den Export erzeugt man in CODESYS V3.5 über den PLCopen-XML-Export des Projekts und checkt ihn neben dem Projekt ein. Die Station erhält im Ergebnis die Property `<namespace>:adapter:source` = `v35`.

**Steuerung.** Jede Konfiguration der obersten Ebene mit Gerätedaten ist eine Steuerung. Enthält der Export mehrere, wählt `plc` in `machine.yml` eine aus, sonst endet der Lauf mit Exit 2.

**Gerät.** Der Export nennt nur die Kennung der Gerätebeschreibung (`Type`, `Id`, `Version`), keinen Produktnamen, Hersteller oder Bestellnummer. In der SBOM heißt die Firmware-Komponente deshalb etwa `Gerät 4102/0000 0005, Firmware`, ohne Version und mit `firmware:origin` = `unknown`. Die Version der Gerätebeschreibung steht als `<namespace>:vendorKey:DeviceDescriptionVersion` daneben. Lücken: `firmware-not-in-project` (Hinweis), `order-number-not-in-project` und `supplier-unknown` (Warnungen).

**Gerätebeschreibungen.** Liegen die Gerätebeschreibungen (`*.devdesc.xml`) im Repository, nennt man ihre Verzeichnisse unter `deviceDescriptions` in `machine.yml`. Passt genau eine Datei zur Kennung des Geräts (Type, Id und Version), übernimmt das Werkzeug daraus Produktname, Hersteller und Bestellnummer; die Komponente heißt dann etwa `TCA9548, Firmware`, der Hersteller trägt `vendor:origin` = `devdesc`, Dateiname und SHA-256 der Gerätebeschreibung stehen als `vendorKey`. Passt keine oder passen mehrere Dateien, übernimmt es nichts und vermerkt `devdesc-not-found` (Warnung). Eine Bestellnummer `-` gilt als nicht angegeben. Auch mit Gerätebeschreibung bleibt die Firmware-Version unbekannt.

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

## 14. Rockwell Studio 5000

Eingang ist der L5X-Export des ganzen Projekts (Studio 5000 Logix Designer: „Speichern unter … L5X“). Die `.ACD`-Datei ist binär und wird nicht gelesen. Ein Export einzelner Bestandteile, etwa einer Add-On Instruction, ergibt keine Station (`[STUDIO5000_NOT_CONTROLLER]`). Ein L5X enthält genau einen Controller; `plc` braucht dieser Adapter nicht.

**Steuerung und Module.** Jedes Modul unter `Modules` wird ein Gerät, die Steuerung zuerst (das Modul `Local`). Die Firmware-Komponente heißt nach der Bestellnummer, etwa `1756-EN2T, Firmware`, weil der Export keinen Produktnamen führt. Ihre Version ist die projektierte Revision: an der Steuerung die Projektrevision (`36.11`), an den Modulen `Major.Minor` (`11.1`). Das ist ein Sollwert (`firmware:origin` = `projected`, Hinweis `firmware-projected-only`). Ein Modul ohne Revision bekommt `firmware-not-in-project`. Die Position steht als `<Parent>/<Steckplatz>` (`Local/1`), hinter einem Ethernet-Modul als `<Parent>/<Modulname>`. IP-Adressen kommen nie in die SBOM.

**Kodierung.** Die elektronische Kodierung steht an jeder Firmware als `<namespace>:device:ekey`. Ist sie an einem Modul aus (`Disabled`), kann das Modul im Feld jede Firmware haben; das ergibt `ekey-disabled` (Warnung). An der Steuerung selbst steht in Studio-5000-Exporten immer `Disabled`, dort ist das kein Befund.

**Hersteller.** Module mit der Herstellerkennung `1` bekommen den Hersteller `Rockwell Automation` mit `vendor:origin` = `assumed`, weil der Export nur die Kennung nennt. Andere Kennungen ergeben `supplier-unknown`; die Kennung steht als `<namespace>:vendorKey:VendorId` daneben. Über `vendorOverrides` in `machine.yml` lässt sich der Hersteller erklären (Muster auf die Bestellnummer).

**Add-On Instructions.** Jede AOI wird eine Komponente vom Typ `library` mit `<namespace>:software:kind` = `add-on-instruction`. Version ist die `Revision` (`1.7`), ein Revisionszusatz steht als `<namespace>:aoi:revisionExtension`. Der Hersteller ist die Angabe `Vendor` der AOI; fehlt sie, `supplier-unknown`.

Alle gelesenen Stellen und Regeln: [docs/adapter-studio5000.md](docs/adapter-studio5000.md).

## 15. Beckhoff TwinCAT 3

Eingang ist die `.tsproj`-Datei des Projekts. Das Werkzeug folgt von dort den SPS-Projekten (`.plcproj`), auch wenn sie als eigene Datei unter `_Config/PLC/` abgelegt sind. Beides muss im Repository liegen; fehlt eine verwiesene Datei, endet der Lauf mit `[TWINCAT_FILE_NOT_FOUND]`. Ein Projekt ist eine Station; `plc` braucht dieser Adapter nicht.

**EtherCAT-Teilnehmer.** Jeder Koppler und jede Klemme wird eine Firmware-Komponente, benannt nach der Kurzbezeichnung, etwa `EL3204-0200, Firmware`. Das Projekt führt weder Firmware noch Bestellnummer: Jede Komponente trägt `firmware-not-in-project` (Hinweis) und `order-number-not-in-project` (Warnung). Bestimmt ist das Gerät über `<namespace>:vendorKey:VendorId`, `ProductCode` und `RevisionNo` (Hex als `0x…`). `RevisionNo` ist die Revision aus der Gerätebeschreibung, nicht die Firmware. Die Position steht als `<E/A-Gerät>/<Box-Id>/…`, etwa `Device 1 (EtherCAT)/1/3`.

**Hersteller.** Teilnehmer mit der EtherCAT-Herstellerkennung `0x00000002` bekommen `Beckhoff Automation GmbH & Co. KG` mit `vendor:origin` = `assumed`. Andere Kennungen ergeben `supplier-unknown`; `vendorOverrides` erklärt den Hersteller, bei Klemmen ohne Bestellnummer über die Kurzbezeichnung (`"EL6692": …`). Passive Klemmen wie die Endklemme EL9011 lassen sich mit `passiveOrderNumbers` ebenso über die Kurzbezeichnung erklären.

**Deaktivierte Geräte.** Ist ein E/A-Gerät im Projekt deaktiviert, bleiben seine Teilnehmer in der SBOM und tragen `<namespace>:device:disabled` = `true`.

**Nicht gelesene Geräte.** PROFINET-Geräte und E/A-Geräte, deren Konfiguration in einer eigenen Datei liegt, liest diese Version nicht. Das Werkzeug nennt sie in der Lücke `devices-not-read` (Warnung), damit sie nicht unbemerkt fehlen.

**Laufzeit.** Die SBOM enthält die Komponente `TwinCAT 3 Runtime` von Beckhoff, ohne Version (`runtime-version-unknown`, Warnung). Im Projekt steht nur die Version der Engineering-Umgebung; sie erscheint in `metadata.tools` als `TwinCAT XAE`.

**Bibliotheken.** Gelesen werden alle Bibliotheken aller SPS-Projekte, gleiche einmal.

| In der `.plcproj` | Version in der SBOM | Lücke |
|---|---|---|
| Platzhalter mit fester Auflösung (`PlaceholderResolution`), z. B. `TcoCore, 0.6.0.913 (Vortex.Library)` | `0.6.0.913` | – |
| direkte Referenz (`LibraryReference`) mit Version | diese Version | – |
| Version `*` oder `newest` | keine; `*` bzw. `newest` als `<namespace>:version:requested` | `version-unresolved` (Warnung) |
| Platzhalter nur mit Standardauflösung, z. B. `VisuElems, 3.5.6.10 (System)` | keine; `3.5.6.10` als `<namespace>:version:requested` | `version-unresolved` (Warnung) |

Wer feste Versionen in der SBOM will, setzt im Bibliotheksverwalter des SPS-Projekts für die Platzhalter eine feste Auflösung. Hersteller `System` (in jeder Schreibweise) ist keine Organisation, dort bleibt der Hersteller leer (`supplier-unknown`).

Alle gelesenen Stellen und Regeln: [docs/adapter-twincat.md](docs/adapter-twincat.md).

## 16. Siemens TIA Portal V17–V21 (AML-Export)

Eingang ist die AML-Datei aus dem CAx-Export von TIA Portal (Extras → CAx-Daten exportieren), eingecheckt im Repository. Gelesen werden Dateien aus TIA Portal V17 bis V21; eine andere `WriterVersion` ergibt eine Warnung, die Datei wird trotzdem gelesen. Dateien aus anderen Werkzeugen, etwa dem TIA Selection Tool, enden mit `[NOT_TIA_AML]`. Die TIA-Version steht in `metadata.tools` als `TIA Portal` (`V21`, Release `2100.0.121.1`), der Exportzeitpunkt an jeder Komponente als `<namespace>:source:exportedAt`, damit ein veralteter Export auffällt.

**Stationen.** Eine Station ist eine CPU und die Geräte ihres PROFINET-IO-Systems. Enthält die Datei mehrere CPUs, wählt `plc` in `machine.yml` je Station eine aus; mehrere Stationen dürfen dieselbe Datei lesen. Zur Station gehören das Gerät der CPU (Profilschiene, Stromversorgung, Module), alle Geräte an ihrem IO-System und alle, die ein Muster unter `devices` trifft (etwa ein Panel, das nur am Subnetz hängt). Geräte, die keine Station beansprucht, kommen unter die Maschine mit `device-unassigned` (Hinweis); was dort nicht hingehört, schließt `ignoreDevices` aus. Ohne `plc` ist die ganze Datei eine Station, wenn sie höchstens eine CPU enthält.

**Firmware.** Jedes Gerät mit Bestellnummer und Firmware-Stand wird eine Firmware-Komponente, benannt nach dem Typnamen, etwa `CPU 1511-1 PN, Firmware` mit Version `V2.8` (`firmware:origin` = `projected`, Hinweis `firmware-projected-only`). Antriebe wie der G120C stehen mit ihrer Firmware ohne „V“ (`4.7.10`) wie im Projekt. Die Position steht als `<Gerät>/<Steckplatzpfad>`, etwa `ET 200SP station_1/0/0`.

| Im Export | In der SBOM |
|---|---|
| `FirmwareVersion` = `V0.0` (Busadapter, einfache DI/DQ) | nicht; passives Modul, später im Hardware-Anhang des Berichts |
| Profilschiene, Rack (Typname `Rack`) | nicht; Hardware-Anhang, Bestellnummer mit `*` als Muster gekennzeichnet |
| integrierte Teile (`BuiltIn`: E/A der CPU, Schnittstellen, OPC UA) | nicht; Teil des übergeordneten Geräts |
| kein Attribut `FirmwareVersion` (etwa PM 70W) | Firmware ohne Version, `firmware-attribute-missing` (Warnung) und `firmware-not-in-project` (Hinweis); mit `passiveOrderNumbers` in `machine.yml` (z. B. `"6EP1*"`) als passiv erklärt |

**Hersteller.** Geräte aus dem Siemens-Katalog (`TypeIdentifier` `OrderNumber:…`) bekommen `Siemens AG` mit `vendor:origin` = `assumed`. Mit `vendorOverrides` (Muster auf Bestellnummer) lässt sich ein anderer Hersteller erklären (`declared`).

**GSD-Geräte.** Für Geräte anderer Hersteller über GSDML nennt das AML nur den Dateinamen der GSDML und den Typnamen. Hersteller und Bestellnummer liest das Werkzeug aus der GSDML-Datei. Dazu checkt man die GSDML-Dateien ein und nennt ihre Verzeichnisse unter `gsdml` in `machine.yml` (bei `inspect` mit `--gsdml`). Gesucht wird der Dateiname aus dem AML ohne Groß-/Kleinschreibung, auch in Unterverzeichnissen.

| Fall | In der SBOM |
|---|---|
| GSDML gefunden | Hersteller aus der GSDML (`vendor:origin` = `gsdml`), Bestellnummer aus dem Zugangspunkt bzw. Modul; `SoftwareRelease` und `HardwareRelease` als `<namespace>:gsdml:softwareRelease` und `:hardwareRelease`; `VendorId`, `DeviceId`, Pfad und SHA-256 der Datei als `<namespace>:vendorKey:…`. Lücke `firmware-not-in-project` (Hinweis) |
| GSDML nicht gefunden, mehrfach mit verschiedenem Inhalt, oder kein Verzeichnis angegeben | `gsdml-not-found`, `order-number-not-in-project`, `supplier-unknown` (Warnungen), `firmware-not-in-project` (Hinweis) |
| GSDML gefunden, aber ohne den Zugangspunkt bzw. das Modul aus dem AML | Hersteller aus der GSDML, `order-number-not-in-project`; Warnung `[TIA_AML_GSDML_ITEM_NOT_FOUND]` |

Die `SoftwareRelease` der GSDML ist nicht die Firmware des Geräts, sondern der Stand, für den die Datei geschrieben ist; die Firmware bleibt offen, bis ein Ist-Stand vorliegt. Manche Hersteller schreiben eine Bestellnummer mit Platzhaltern in die GSDML (Leuze: `501xxxxx`); sie steht unverändert in der SBOM, dazu `<namespace>:device:orderNumber:pattern` = `true`. Ein Muster aus `vendorOverrides` auf diese Bestellnummer geht vor der GSDML. Module des GSD-Geräts gelten als passiv und bekommen den Hersteller aus der GSDML. Der Dateiname aus dem AML steht immer als `<namespace>:vendorKey:GSDML`.

**Bibliotheken und Programm.** Der CAx-Export enthält weder Bibliotheken noch Bausteine. Jede Station mit CPU trägt `libraries-not-read` (Warnung) und `program-not-read` (Hinweis). Bibliotheken lassen sich je Station unter `libraries` in `machine.yml` deklarieren; sie kommen mit `<namespace>:origin` = `declared` in die SBOM, und `libraries-not-read` entfällt. Bibliothekstypen und geschützte Bausteine liefert erst der Adapter `tia-portal` über Openness (M14).

IP-Adressen, Subnetzmasken, PROFINET-Gerätenamen und Variablentabellen werden nicht gelesen.

Alle gelesenen Stellen und Regeln: [docs/adapter-tia-aml.md](docs/adapter-tia-aml.md).

## 17. Was folgt

Die nächsten Schritte nach Meilensteinplan (CLAUDE.md Abschnitt 12): M1, M2 (CODESYS V3.5), M5 (Rockwell Studio 5000), M6 (Beckhoff TwinCAT 3), M7 (Siemens TIA Portal, AML-Export), M8 (GSD-Geräte mit GSDML-Dateien) und M9 (`build` mit `machine.yml`, Hersteller- und Teil-SBOMs) sind umgesetzt; alle Prüfregeln mit ihrer TR-Stelle stehen in [docs/regeln-tr03183.md](docs/regeln-tr03183.md). CODESYS V2.3 und CODESYS 4 (M3, M4) warten auf Demo-Projekte. Als Nächstes kommt der Prüfbericht in HTML und Markdown (M10), danach der Abgleich mit dem Ist-Stand aus dem Feld (M11) und die Kennungsprüfung gegen CSAF (M12). TIA Portal über Openness kommt zuletzt (M14).
