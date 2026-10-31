# Adapter `twincat`

Stand 01.11.2026. Liest ein Beckhoff-TwinCAT-3-Projekt ohne Start der Engineering-Umgebung: die Systemmanager-Datei `.tsproj` mit dem E/A-Baum und die SPS-Projekte `.plcproj`, auf die sie verweist (CLAUDE.md Abschnitt 5.3). Kein TwinCAT, keine Lizenz, läuft auf jedem Agenten. Entscheidungen: `docs/entscheidungen.md` E-018.

## 1. Eingang

Angegeben wird die `.tsproj`-Datei. Der Adapter folgt von dort den SPS-Projekten:

- `Project/Plc/Project@PrjFilePath`: Pfad zur `.plcproj`, relativ zur `.tsproj` (AixOCAT, TcOpen).
- `Project/Plc/Project@File`: SPS-Projekt als eigene Datei unter `_Config/PLC/<File>`; deren `Project@PrjFilePath` ist relativ zu dieser Datei (SPT EtherCAT Diagnostics).

Pfade in TwinCAT-Dateien stehen mit `\`; der Adapter wandelt sie für Linux um. Fehlt eine verwiesene Datei, endet der Lauf mit Exit 2 (`TWINCAT_FILE_NOT_FOUND`). Alle XML-Dateien werden ohne DTD und ohne Resolver gelesen, über lokale Elementnamen (die `.plcproj` steht im MSBuild-Namespace).

Ein Projekt ergibt eine Station mit allen EtherCAT-Teilnehmern und den Bibliotheken aller SPS-Projekte. Den Schlüssel `plc` braucht dieser Adapter nicht.

### 1.1 Fixtures

| Ordner | TwinCAT | Inhalt | Lizenz |
|---|---|---|---|
| `fixtures/twincat/aixocat-heatpump/` | 3.1.4022.4 | EtherCAT-Master `Disabled="true"`, EK1100 mit neun Klemmen (drei baugleiche EL3204-0200), ein SPS-Projekt mit 14 Platzhaltern, Visu-Bibliotheken mit Hersteller `System` und `system` | MIT |
| `fixtures/twincat/spt-ethercat-diagnostics/` | 3.1.4026.24 | SPS-Projekt über `_Config/PLC/*.xti`, EK1100, EL6910, Endklemme EL9011 ohne `RevisionNo`, `BIC` mit Seriennummer-Feld, `TargetNetId` | 0BSD |
| `fixtures/twincat/tcopen-cognex/` | 3.1.4024.55 | PROFINET-Controller mit zwei Cognex-Geräten (kein EtherCAT), zwei SPS-Projekte mit `PlaceholderResolution` auf feste Versionen | MIT |
| `fixtures/twincat/tcopen-core/` | 3.1.4024.55 | drei SPS-Projekte, `LibraryReference` mit `newest`, `PlaceholderResolution` ohne zugehörigen Platzhalter (`Tc2_Utilities`), kein E/A-Gerät | MIT |
| `fixtures/twincat/synthetisch/` | synthetisch | EtherCAT-Antrieb einer fremden Herstellerkennung, E/A-Gerät in eigener Datei (`File=`), `LibraryReference` mit fester Version | Comquent |

Herkunft und Commits in `fixtures/FIXTURES.md`. Aus den Quell-Repositories wurden nur die `.tsproj`, die nötigen `.xti` und `.plcproj` übernommen, in derselben Ordnerstruktur.

## 2. Feldtabelle

**`.tsproj`**

| Datum | Stelle | Belegt in | Verwendung |
|---|---|---|---|
| Engineering-Werkzeug | `TcSmProject@TcVersion` | allen | `metadata.tools`: `TwinCAT XAE`, Version unverändert (`3.1.4022.4`); **nicht** die Laufzeit |
| E/A-Gerät | `Project/Io/Device`: `Name`, `@DevType`, `@Disabled` | allen mit E/A | Rahmen für die Boxen; `Disabled="true"` → `<ns>:device:disabled = true` an jedem Teilnehmer |
| ausgelagertes E/A-Gerät | `Device@File` | synthetisch | nicht gelesen, Lücke `devices-not-read` an der Station |
| Box | `Device/Box` rekursiv: `@Id`, `Name` | allen mit E/A | `<ns>:device:name` = `Name`, `<ns>:device:position` = `<Gerätename>/<Box-Id>/<Box-Id>…` |
| EtherCAT-Kennung | `Box/EtherCAT@VendorId`, `@ProductCode`, `@RevisionNo` | AixOCAT, SPT, synthetisch | `<ns>:vendorKey:VendorId`, `ProductCode`, `RevisionNo`; Hex `#x00120000` → `0x00120000` |
| Kurzbezeichnung | `Box/EtherCAT@Desc` | AixOCAT, SPT, synthetisch | Typname (`EL3204-0200, Firmware`) |
| Beschreibung | `Box/EtherCAT@Type` | AixOCAT, SPT | nur in `inspect`; Typname, wenn `Desc` fehlt |
| andere Feldbusse | `Box/Profinet` | TcOpen Cognex | nicht gelesen, Lücke `devices-not-read` mit Box- und Gerätenamen |
| SPS-Projekte | `Project/Plc/Project@Name`, `@PrjFilePath`, `@File` | allen | Verweis auf die `.plcproj` |

**Nie übernommen** (Leitplanke Netzdaten und Seriennummern): `Device@AmsNetId`, `@RemoteName`, `@AmsPort`, `Project@TargetNetId`, `EtherCAT@EtherCatAddr`, `EtherCAT@BIC` (Beckhoff Identification Code eines gescannten Geräts, enthält Seriennummern), `Profinet@IPAdd`. Der Leser liest diese Attribute nicht; auch `inspect` gibt sie nicht aus.

**`.plcproj`**

| Datum | Stelle | Belegt in | Verwendung |
|---|---|---|---|
| Programmversion | `PropertyGroup/ProgramVersion` | allen | nur in `inspect` |
| Platzhalter | `ItemGroup/PlaceholderReference@Include`, `DefaultResolution`, `Namespace`, `SystemLibrary` | allen | Bibliothek; Standardauflösung nur als `<ns>:version:requested` |
| feste Auflösung | `ItemGroup/PlaceholderResolution@Include`, `Resolution` | TcOpen | Version des Platzhalters gleichen Namens |
| direkte Referenz | `ItemGroup/LibraryReference@Include` = `Titel,Version,Hersteller` | TcOpen Core (`newest`), synthetisch (feste Version) | Bibliothek mit Version aus `Include` |

Nicht gelesen: Bausteine (`Compile`), Visualisierungen, Tasks, Variablen, Mapping, NC-Achsen (`_Config/NC/*.xti`), `.tmc`, `.tspproj` (eigenständige SPS-Projekte ohne Systemmanager).

## 3. Regeln

**EtherCAT-Teilnehmer.** Jede Box mit einem `EtherCAT`-Element und `VendorId` wird ein Gerät, Koppler wie Klemmen, in Baumreihenfolge. Eine Steuerung im Sinn des Modells gibt es nicht: Der Industrie-PC steht nicht im Projekt.

**Firmware.** Das Projekt führt für EtherCAT-Teilnehmer keine Firmware. `RevisionNo` ist die Revision aus der Gerätebeschreibung (ESI), nicht die Firmware. Jedes Gerät bekommt `firmware-not-in-project` (Hinweis), `<ns>:firmware:origin = not-in-project`. Den Ist-Stand liefert das Gerät selbst (CoE-Objekt 0x100A, ab M11 über eine Feld-Datei).

**Bestellnummer.** Das Projekt nennt keine Bestellnummer. `Desc` ist die Kurzbezeichnung, die TwinCAT anzeigt, und weicht von der Bestellnummer ab: Bei der EK1200 (TcOpen) steht `Desc="EK1200"`, `Type` beginnt mit `EK1200-5000`. Deshalb keine Bestellnummer, Lücke `order-number-not-in-project` (Warnung); bestimmt ist das Gerät über `VendorId`, `ProductCode` und `RevisionNo` (E-018).

**Hersteller.** EtherCAT-Herstellerkennung `0x00000002` ist Beckhoff; alle Klemmen und Koppler der Fixtures tragen sie. Hersteller `Beckhoff Automation GmbH & Co. KG`, `vendor:origin = assumed`. Andere Kennungen (in den Quell-Repositories `0x00000024` am IndraDrive, `0x0000001d`, `0x01000056`): kein Hersteller, `supplier-unknown`. `vendorOverrides` (M9) erklärt den Hersteller.

**Deaktivierte Geräte.** Ein E/A-Gerät mit `Disabled="true"` ist projektiert, läuft aber nicht. Seine Teilnehmer bleiben in der SBOM, damit nichts unbemerkt fehlt, und tragen `<ns>:device:disabled = true`. In Testprojekten von Bibliotheken (TcOpen, AixOCAT) sind die Master meist deaktiviert.

**Andere Feldbusse und ausgelagerte E/A.** PROFINET-Boxen und E/A-Geräte in eigenen Dateien liest der Adapter nicht. Damit sie nicht still fehlen, steht an der Station die Lücke `devices-not-read` (Warnung) mit den Namen. Ohne Anwenderprogramm und Steuerung hängt sie an der Maschine.

**Laufzeit.** Jedes TwinCAT-3-Projekt läuft auf einer TwinCAT-3-Laufzeit (XAR). Sie steht als Komponente `TwinCAT 3 Runtime` (bom-ref `<station>-rt`) in der SBOM, Hersteller Beckhoff, Name und Hersteller `assumed`, ohne Version: `runtime-version-unknown` (Warnung). `TcVersion` ist die Version der Engineering-Umgebung und wird nicht als Laufzeitversion genommen.

**Bibliotheken.** TwinCAT nutzt die Bibliotheksverwaltung von CODESYS; die Regeln folgen E-012 und E-013.

| Fall | `<ns>:library:resolution` | Version in der SBOM | Lücke |
|---|---|---|---|
| `LibraryReference` mit Version | `fixed` | Version aus `Include` | – |
| `LibraryReference` mit `newest` | `fixed` | keine; `newest` als `<ns>:version:requested` | `version-unresolved` |
| Platzhalter mit `PlaceholderResolution` | `redirection` | Version der Auflösung | – (bei `*` `version-unresolved`) |
| Platzhalter nur mit `DefaultResolution` | `default` | keine; Standardversion als `<ns>:version:requested` | `version-unresolved` |
| `PlaceholderResolution` ohne eigenen Platzhalter | `indirect` | Version der Auflösung | – |

Eine Auflösung ohne eigenen Platzhalter legt die Version einer Bibliothek fest, die eine andere Bibliothek referenziert (TcoCore: `Tc2_Utilities 3.3.47.0`). Sie kommt in die SBOM, weil sie nur so gesetzt wird. Hersteller ist die Angabe in Klammern bzw. der dritte Teil von `Include`; `System` in jeder Schreibweise ist kein Hersteller (`supplier-unknown`). Gleiche Bibliotheken aus mehreren SPS-Projekten erscheinen einmal; `newest` und `*` gelten dabei als gleich.

**Anwenderprogramm.** Kommt mit `build` (M9) aus `program` in `machine.yml` (CLAUDE.md Abschnitt 5.6).

## 4. Fehlerfälle

| Fall | Code | Exit |
|---|---|---|
| Datei fehlt | `SOURCE_NOT_FOUND` | 2 |
| kein gültiges XML oder DTD enthalten | `XML_INVALID` | 2 |
| Wurzel nicht `TcSmProject` | `NOT_TSPROJ` | 2 |
| verwiesene `.plcproj` oder `_Config/PLC/*.xti` fehlt | `TWINCAT_FILE_NOT_FOUND` | 2 |
| SPS-Projekt ohne Pfad zur `.plcproj` | `TWINCAT_PLCPROJ_MISSING` | 2 |

## 5. Offen

- **Passive Klemmen** (Endklemme EL9011, Einspeiseklemmen, EK1122): Das Projekt kennzeichnet sie nicht als passiv; sie erscheinen als Firmware-Komponente ohne Version. Abhilfe ab M9 über `passiveOrderNumbers` bzw. ein Muster auf die Kurzbezeichnung.
- **Ausgelagerte E/A-Geräte** (`Device@File`): Ablageort und Form der `.xti` sind an keinem echten Projekt belegt. Mit einer Fixture lesen statt als Lücke führen.
- **PROFINET-Geräte**: brauchen den GSDML-Leser aus M8.
- **Antriebe mit `ProductRevision`** (CLAUDE.md 5.3): in keinem Projekt der Quell-Repositories gefunden; nicht gelesen.
- **Laufzeitversion**: nur aus dem Feld oder deklariert (M9/M11).
- **Bestellnummer**: Ob sich die Bestellnummer belastbar aus Produktcode und Revision ableiten lässt (Beckhoff-ESI), ist nicht geprüft.
