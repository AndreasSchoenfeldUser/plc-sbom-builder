# Adapter `codesys`

Stand 31.10.2026. Ein Adapter für drei CODESYS-Generationen, unterschieden über `source` (CLAUDE.md Abschnitt 5.1). Umgesetzt ist die Quelle `v35`; `v23` und `c4` folgen nach den Spikes in M3 und M4.

## 1. Quelle `v35`: CODESYS V3.5, PLCopen-XML-Export

Eingang ist der PLCopen-XML-Export des ganzen Projekts (Namespace `http://www.plcopen.org/xml/tc6_0200`). Die `.project`-Datei ist binär und wird nicht gelesen. Der Parser arbeitet über lokale Elementnamen, weil CODESYS die Gerätedaten ohne Namespace (`xmlns=""`) in das PLCopen-Dokument einbettet. XML wird ohne DTD und ohne Resolver gelesen.

### 1.1 Fixtures

| Datei | CODESYS | Inhalt | Lizenz |
|---|---|---|---|
| `fixtures/codesys/v35/robin-codesys_project.xml` | V3.5 SP15 | ein Gerät, eine Anwendung, neun Platzhalter mit `DefaultResolution`, eine direkte Referenz, eine untergeordnete Konfiguration ohne Gerätedaten (Achspool) | Apache-2.0 |
| `fixtures/codesys/v35/ooip-script-engine-example.xml` | V3.5 SP20 Patch 1 | ein Gerät, Platzhalter mit `PlaceholderRedirection`, Standardauflösung mit `*` | MIT |
| `fixtures/codesys/v35/osc-mqtt-OSC_MQTT.xml` | V3.5 SP4 Patch 1 | Bibliotheksprojekt ohne Gerät, Bibliotheken im POU-Pool, zwei Referenzen mit `*` | Apache-2.0 |
| `fixtures/codesys/v35/zwei-steuerungen-synthetisch.xml` | synthetisch | zwei Steuerungen, untergeordnetes Gerät, zwei Anwendungen je Gerät, POU-Pool, Referenz außerhalb des Musters | Comquent |

Herkunft und Commits in `fixtures/FIXTURES.md`.

### 1.2 Feldtabelle

| Datum | Stelle im Export | Belegt in | Verwendung |
|---|---|---|---|
| Engineering-Werkzeug | `fileHeader@productName`, `@productVersion` | allen | `metadata.tools`: Name `CODESYS`, Version ohne vorangestellten Produktnamen (`V3.5 SP15`) |
| Hersteller des Werkzeugs | `fileHeader@companyName` | allen (immer leer) | nur übernommen, wenn gesetzt |
| Exportzeitpunkt | `fileHeader@creationDateTime` | allen | nur in `inspect`; ohne Zeitzone, deshalb nicht als `<ns>:source:exportedAt` (eine Zeitzone müsste erfunden werden) |
| Projektname, -version | `contentHeader@name`, `@version` | allen | nur in `inspect` |
| Steuerung | `instances/configurations/configuration` mit `addData/data[@name="Device"]/Device/DeviceType/DeviceIdentification` | robin, ooip | ein Gerät je Konfiguration der obersten Ebene, `IsController` |
| Gerätename | `configuration@name` | robin, ooip | `<ns>:device:name` |
| Gerätekennung | `DeviceIdentification/Type`, `/Id` | robin, ooip | `<ns>:vendorKey:DeviceType`, `<ns>:vendorKey:DeviceId`; Typname `Gerät <Type>/<Id>` |
| Version der Gerätebeschreibung | `DeviceIdentification/Version` | robin, ooip | `<ns>:vendorKey:DeviceDescriptionVersion`, **nie** als Firmware |
| Untergeordnete Geräte | `configuration/addData/data[@name="configurations"]/configurations/configuration` mit Gerätedaten | Verschachtelung: robin (Achspool ohne Gerätedaten); Gerät darin: synthetisch | weitere Geräte der Station, `<ns>:device:position` = Pfad der Konfigurationsnamen unterhalb der Steuerung |
| Anwendungen | `configuration/resource@name` | robin, ooip, synthetisch | nur für `inspect` und die Zuordnung der Bibliotheken |
| Bibliotheken einer Anwendung | `resource/addData/data[@name="http://www.3s-software.com/plcopenxml/libraries"]/Libraries/Library` | robin, ooip | `library`-Komponenten der Station |
| Bibliotheken im POU-Pool | `project/addData/data[@name=".../libraries"]/Libraries/Library` | osc-mqtt | `library`-Komponenten jeder Station aus dieser Datei |
| Referenz | `Library@Name`: `#<Platzhalter>` oder `Titel, Version (Hersteller)` | allen | `<ns>:library:reference` |
| Standardauflösung | `Library@DefaultResolution` | robin, ooip | Version nur als `<ns>:version:requested` |
| Umleitung | `Libraries/PlaceholderRedirections/PlaceholderRedirection@Placeholder`, `@Redirection` | ooip | feste Version |
| Namensraum, Systembibliothek | `Library@Namespace`, `@SystemLibrary` | allen | nur in `inspect` |

Nicht gelesen: Bausteine, Variablen, Tasks, Geräteparameter (`HostParameterSet`, `DeviceParameterSet`), `ObjectId`, `ProjectStructure`.

### 1.3 Regeln

**Gerät.** Der Export nennt weder Produktname noch Hersteller noch Bestellnummer, nur die Kennung der Gerätebeschreibung. Daraus folgt:

- Typname `Gerät <Type>/<Id>`, Komponentenname `Gerät 4102/0000 0005, Firmware`. Eine Zuordnung der Kennung zu einem Produkt ist nicht belegt. Dieselbe Kennung `4102/0000 0005` steht in zwei Exporten mit verschiedenen Gerätebeschreibungen (V3.5 SP15 und ein Raspberry-Pi-Gerät, `fixtures/_local/`).
- Lücken je Gerät: `firmware-not-in-project` (Hinweis, Meldung nennt die Version der Gerätebeschreibung), `order-number-not-in-project` (Warnung), `supplier-unknown` (Warnung).
- `<ns>:firmware:origin = unknown`, keine Firmware-Version.

**Steuerung wählen.** Jede Konfiguration der obersten Ebene mit Gerätedaten ist eine Steuerung. Eine Station ist eine Steuerung mit ihren untergeordneten Geräten. `plc` wählt die Steuerung über den Konfigurationsnamen. Enthält der Export genau eine Steuerung, darf `plc` fehlen; bei mehreren ohne `plc` endet der Lauf mit Exit 2 (`CODESYS_PLC_REQUIRED`), ein unbekannter Name ebenso (`CODESYS_PLC_NOT_FOUND`). Ein Export ohne Gerät (Bibliotheksprojekt) ergibt eine Station ohne Geräte.

**Konfiguration ohne Gerätedaten** (z. B. „SoftMotion General Axis Pool“) ist kein Gerät. Untergeordnete Geräte darin rücken eine Ebene nach oben.

**Bibliotheken.** Die Station erhält die Bibliotheken aus dem POU-Pool und aus allen Anwendungen ihrer Steuerung. Gleiche Bibliotheken (Titel, Version, Hersteller und ob die Version fest ist) erscheinen einmal.

| Fall | Version in der SBOM | Lücke |
|---|---|---|
| direkte Referenz `Titel, Version (Hersteller)` | Version aus der Referenz | – |
| Platzhalter mit `PlaceholderRedirection` | Version aus der Umleitung | – |
| Platzhalter nur mit `DefaultResolution` | keine; Standardversion als `<ns>:version:requested` | `version-unresolved` (Warnung) |
| Version `*` (direkt, umgeleitet oder Standard) | keine; `*` als `<ns>:version:requested` | `version-unresolved` (Warnung) |
| Referenz außerhalb des Musters | keine | `version-unresolved` und `supplier-unknown` |

Begründung für Platzhalter ohne Umleitung: In CODESYS löst zuerst eine Umleitung im Anwendungskontext auf, danach die Gerätebeschreibung, danach Library Support Package und Bibliotheksprofil (CODESYS-Hilfe „Placeholder“, `content.helpme-codesys.com/en/LibDevSummary/placeholder.html`, abgerufen 31.10.2026). Die Auflösung durch die Gerätebeschreibung steht nicht im Export. Die Standardauflösung ist deshalb nicht sicher die übersetzte Version (docs/entscheidungen.md E-012).

**Hersteller einer Bibliothek** ist der Teil in Klammern der aufgelösten Referenz, mit `vendor:origin = source` und ohne Kontakt (`creator-contact-unknown`). CODESYS schreibt für Bibliotheken der eigenen Installation `System`. Das ist keine Organisation: kein Hersteller, Lücke `supplier-unknown`, Wert in `<ns>:library:company` (E-013).

**Eigene Properties** der Quelle `v35`: `<ns>:adapter:source = v35`, an Bibliotheken `<ns>:library:reference`, `<ns>:library:resolution` (`fixed`, `redirection`, `default`, `none`), `<ns>:library:company`.

**Anwenderprogramm.** Kommt mit `build` (M9) aus `program` in `machine.yml` (CLAUDE.md Abschnitt 5.6), nicht aus dem Export.

### 1.4 Fehlerfälle

| Fall | Code | Exit |
|---|---|---|
| Datei fehlt | `SOURCE_NOT_FOUND` | 2 |
| kein gültiges XML oder DTD enthalten | `XML_INVALID` | 2 |
| Wurzel nicht `project` im PLCopen-Namespace | `NOT_PLCOPEN` | 2 |
| mehrere Steuerungen ohne `plc` | `CODESYS_PLC_REQUIRED` | 2 |
| `plc` nicht im Export | `CODESYS_PLC_NOT_FOUND` | 2 |

### 1.5 Offen

- **Untergeordnete Geräte im Gesamtexport** (Feldbus, E/A) sind nur synthetisch belegt. Die Verschachtelung folgt dem Achspool in robin und einem Export je Objekt aus `arwie/controlOS_demo` (ohne Lizenz, nur in `fixtures/_local/`). Mit dem Comquent-Demo-Projekt prüfen.
- **Gerätebeschreibungen** (`*.devdesc.xml`) nennen Name, Hersteller und Bestellnummer eines Geräts zur Kennung aus `DeviceIdentification`. Ein Leser analog zu GSDML (Verzeichnisse in `machine.yml`) würde `supplier-unknown` und `order-number-not-in-project` schließen. Fixture dafür: `stefandreyer/I2C-Multiplex` (MIT). Nicht Teil von M2, Vorschlag in E-014.
- **Effektive Bibliotheksversion**: nur über CODESYS-Scripting (`effective_resolution`), nach dem Prototyp.
