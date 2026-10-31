# Adapter `studio5000`

Stand 31.10.2026. Liest den vollständigen L5X-Export eines Logix-Controllers aus Rockwell Studio 5000 Logix Designer (CLAUDE.md Abschnitt 5.2). Kein Studio 5000, keine Lizenz, läuft auf jedem Agenten. Entscheidungen: `docs/entscheidungen.md` E-017.

## 1. Eingang

Eingang ist der L5X-Export des ganzen Projekts (Studio 5000: „Speichern unter … L5X“). Die `.ACD`-Datei ist binär und wird nicht gelesen. XML wird ohne DTD und ohne Resolver gelesen, über lokale Elementnamen. Die echten Exporte beginnen mit UTF-8-BOM und haben CRLF-Zeilenenden; beides liest der Parser.

Nur ein Export mit `TargetType="Controller"` und `Controller@Use="Target"` ergibt eine Station. Ein Export einzelner Bestandteile (etwa einer AOI, `TargetType="AddOnInstructionDefinition"`, Controller mit `Use="Context"`) endet mit Exit 2 (`STUDIO5000_NOT_CONTROLLER`). `inspect` zeigt auch solche Exporte.

Ein L5X enthält genau einen Controller. Den Schlüssel `plc` braucht dieser Adapter deshalb nicht.

### 1.1 Fixtures

| Datei | Studio 5000 | Inhalt | Lizenz |
|---|---|---|---|
| `fixtures/studio5000/ra-ExampleForCICD_L85E.L5X` | V36.00 | Controller 1756-L85E, Revision 36.11, nur das Controller-Modul (`EKey` `Disabled`), keine AOI, `CommPath` mit IP-Adresse | MIT |
| `fixtures/studio5000/ra-DelayedSum_AOI.L5X` | V36.00 | Export einer AOI (`TargetType="AddOnInstructionDefinition"`), Revision 1.0, ohne `Vendor` | MIT |
| `fixtures/studio5000/ra-WetBulbTemperature.L5X` | V36.00 | Export einer AOI, Revision 1.0, `Vendor` `Cardoso&Filho` | MIT |
| `fixtures/studio5000/acd-ACDTestsWithAOI.L5X` | V34.01 | Controller 1756-L85E 34.11, Modul 1756-EN2T 11.1 in Steckplatz 1 (`EKey` `CompatibleModule`, Ethernet-Port mit IP-Adresse), AOI mit `Revision`, `RevisionExtension`, `Vendor` | Apache-2.0 |
| `fixtures/studio5000/acd-ACDTestsEmptyRedundant.L5X` | V34.01 | Controller mit `RedundancyInfo Enabled="true"` | Apache-2.0 |
| `fixtures/studio5000/module-baum-synthetisch.L5X` | synthetisch | entferntes Chassis hinter einem Ethernet-Modul, E/A-Modul mit `EKey` `Disabled`, Modul einer fremden (erfundenen) Herstellerkennung, Modul ohne Revision, zwei AOIs mit und ohne `Vendor` | Comquent |

Herkunft und Commits in `fixtures/FIXTURES.md`. Nicht als Beleg genommen: die L5X-Dateien aus `Colt-H/L5X-Creator` (von einem Generator erzeugt, `ExportDate="test"`) und aus `reh3376/acd-l5x-tool-lib` (generiert, gleichförmige Zeitstempel). Beide sind MIT, aber keine Exporte aus Studio 5000.

## 2. Feldtabelle

| Datum | Stelle im Export | Belegt in | Verwendung |
|---|---|---|---|
| Engineering-Werkzeug | `RSLogix5000Content@SoftwareRevision` | allen | `metadata.tools`: Name `Studio 5000 Logix Designer`, Version unverändert (`36.00`) |
| Art des Exports | `@TargetType`, `@TargetName`, `@ContainsContext`, `Controller@Use` | allen | Prüfung, ob ein Controller-Export vorliegt |
| Exportzeitpunkt | `@ExportDate` | allen | nur in `inspect`; ohne Zeitzone, deshalb nicht als `<ns>:source:exportedAt` |
| Steuerung | `Controller@Name`, `@ProcessorType`, `@MajorRev`, `@MinorRev` | ra-L85E, acd | Firmware-Soll der Steuerung = `MajorRev.MinorRev` (`36.11`) |
| Redundanz | `Controller/RedundancyInfo@Enabled` | acd (beide Werte) | nur in `inspect` |
| Module | `Controller/Modules/Module` | allen Controller-Exporten | je Modul ein Gerät |
| Bestellnummer | `Module@CatalogNumber` | ra-L85E, acd | `<ns>:device:orderNumber` unverändert, zugleich Typname (`1756-EN2T, Firmware`) |
| Herstellerkennung | `Module@Vendor` | ra-L85E, acd (nur `1`) | `<ns>:vendorKey:VendorId`; `1` → Hersteller `Rockwell Automation` mit `vendor:origin = assumed` (E-017) |
| Produktkennung | `Module@ProductType`, `@ProductCode` | ra-L85E, acd | `<ns>:vendorKey:ProductType`, `<ns>:vendorKey:ProductCode` |
| Revision | `Module@Major`, `@Minor` | ra-L85E, acd | Firmware-Soll des Moduls = `Major.Minor` (`11.1`), `firmware:origin = projected` |
| Baum | `Module@ParentModule`, `@ParentModPortId` | acd | Steuerung = das Modul, das sein eigener Parent ist (`Local`) |
| Steckplatz | `Ports/Port` mit `Upstream="true"` und `Type="ICP"`: `@Address` | acd (`ETH_MODULE`, Steckplatz 1) | `<ns>:device:position` = `<ParentModule>/<Steckplatz>` (`Local/1`); an der Steuerung der Port mit `Id = ParentModPortId` (`Local/0`) |
| Kodierung | `Module/EKey@State` | ra-L85E, acd (`Disabled`, `CompatibleModule`) | `<ns>:device:ekey`; `Disabled` an einem Modul außer der Steuerung → `ekey-disabled` (Warnung) |
| Add-On Instructions | `Controller/AddOnInstructionDefinitions/AddOnInstructionDefinition@Name`, `@Revision`, `@Vendor` | acd, ra-AOI-Exporte | Komponente vom Typ `library`, `<ns>:software:kind = add-on-instruction`, Version = `Revision` |
| Revisionszusatz | `AddOnInstructionDefinition@RevisionExtension` | acd (`ExtendedText`) | `<ns>:aoi:revisionExtension`, nicht Teil der Version |

**Nie übernommen** (Leitplanke Netzdaten): `Port@Address` an Ethernet-Ports (IP-Adressen), `Controller@CommPath` (Kommunikationspfad mit IP-Adresse). Der Leser liest `Address` nur an Backplane-Ports (`ICP`), wo sie die Steckplatznummer ist. Auch `inspect` gibt keine IP-Adresse aus.

Nicht gelesen: Programme, Routinen, Tags, Datentypen, Tasks, `ExtendedProperties` (in acd mit Herstellername `Rockwell Automation/Allen-Bradley` am 1756-EN2T, an der Steuerung fehlt er), `Module@Inhibited`, `SafetyInfo`, `Security`, Projektseriennummer `ProjectSN`, Benutzernamen (`CreatedBy`, `EditedBy`).

## 3. Regeln

**Steuerung.** Das Modul, das sein eigener Parent ist (`Name` = `ParentModule`, in allen echten Exporten `Local`), ist die Steuerung der Station. Ihre Firmware ist die Projektrevision `Controller@MajorRev.MinorRev`; sie stimmt in allen Fixtures mit `Module@Major.Minor` des Controller-Moduls überein. Fehlt die Projektrevision, gilt die des Moduls.

**Module.** Reihenfolge: Steuerung, dann ihre Module in Tiefensuche in Dateireihenfolge, danach Module, deren Parent-Kette die Steuerung nicht erreicht. Typname ist die Bestellnummer, weil der Export keinen Produktnamen führt. Ohne `Major` keine Firmware-Version, `firmware-not-in-project`.

**Position.** Steckplatz auf einer Backplane: `<ParentModule>/<Steckplatz>`. Ein Modul hinter einem Netzwerkport (Ethernet) hat keinen Steckplatz: `<ParentModule>/<Modulname>`. Modulnamen sind je Controller eindeutig, die Position damit auch. Beispiel aus der synthetischen Datei: `Local/2` (Ethernet-Modul im lokalen Chassis), `ENET_Lokal/Remote_Rack` (Adapter des entfernten Chassis), `Remote_Rack/3` (E/A-Modul darin).

**Firmware ist Soll.** `Major.Minor` ist die projektierte Revision, nicht der Stand im Feld. Wie weit das Feld abweichen darf, sagt die Kodierung: `ExactMatch` verlangt die Revision, `CompatibleModule` lässt eine kompatible zu, `Disabled` lässt jede zu. Die Kodierung steht deshalb an jeder Firmware als `<ns>:device:ekey`. Alle Firmware-Stände tragen `firmware-projected-only` (Hinweis), bis ein Ist-Stand vorliegt (M11).

**Kodierung an der Steuerung.** Am Controller-Modul steht in allen vier echten Controller-Exporten `EKey State="Disabled"`. Das ist dort kein Projektierungsentscheid: Laut Rockwell müssen Firmware der Steuerung und Logix Designer dieselbe Hauptversion haben (ControlLogix 5590 User Manual 1756-UM900, Abschnitt „Controller Firmware and Logix Designer Application Compatibility“, über die Suche am 31.10.2026 gefunden; die Seite blockt automatische Abrufe, Wortlaut nicht geprüft). Die Steuerung bekommt deshalb kein `ekey-disabled`. **Annahme**, an weiteren Exporten prüfen.

**Hersteller.** Herstellerkennung `1` ist Rockwell Automation (CIP-Herstellerkennung; in acd nennt `ExtendedProperties` am 1756-EN2T mit `Vendor="1"` den Hersteller `Rockwell Automation/Allen-Bradley`). Name `Rockwell Automation`, `vendor:origin = assumed`. Andere Kennungen: kein Hersteller, `supplier-unknown` mit der Kennung in der Meldung, Kennung als `<ns>:vendorKey:VendorId`. Eine Tabelle CIP-Kennung → Hersteller ist nicht eingebettet; `vendorOverrides` in `machine.yml` (M9) erklärt den Hersteller mit `origin = declared`.

**Add-On Instructions.** Je AOI eine Komponente vom Typ `library` mit bom-ref `<station>-aoi-<name>`. Version = `Revision` unverändert (in allen Fixtures `Major.Minor`, z. B. `1.7`), `RevisionExtension` als eigene Property. Hersteller = `Vendor` unverändert, `vendor:origin = source`; ohne `Vendor` `supplier-unknown`. Für AOIs ohne Hersteller oder Kontakt gelten die allgemeinen Lückenregeln (`supplier-unknown`, `creator-contact-unknown`, `file-properties-unknown`, `license-unknown`).

**Anwenderprogramm.** Kommt mit `build` (M9) aus `program` in `machine.yml` (CLAUDE.md Abschnitt 5.6), nicht aus dem Export.

## 4. Fehlerfälle

| Fall | Code | Exit |
|---|---|---|
| Datei fehlt | `SOURCE_NOT_FOUND` | 2 |
| kein gültiges XML oder DTD enthalten | `XML_INVALID` | 2 |
| Wurzel nicht `RSLogix5000Content` | `NOT_L5X` | 2 |
| kein Controller-Export (`TargetType`, `Controller@Use`) | `STUDIO5000_NOT_CONTROLLER` | 2 |

## 5. Offen

- **Fremdgeräte**: Kein echter Export enthält ein Modul mit einer anderen Herstellerkennung als `1`; die Regel ist nur synthetisch belegt.
- **Geschützte AOIs**: Quellgeschützte AOIs (`EncodedData`) kommen in keiner Fixture vor und werden nicht gelesen. Sie sind der Fall „geschützter Baustein“ aus CLAUDE.md 4.1.
- **Redundanz**: Ein redundantes Controller-Paar hat zwei Steuerungen mit gleichem Firmware-Soll, das Projekt beschreibt eine. Ob das in die SBOM gehört (zweite Firmware-Komponente oder Property), ist nicht entschieden; `inspect` zeigt `redundancyEnabled`.
- **Safety-Controller** (`SafetyInfo` mit Inhalt, GuardLogix): keine Fixture.
- **Andere Backplanes** (CompactLogix, Point I/O, 5094): Nur `ICP` (ControlLogix) ist belegt. Bei anderen Port-Typen steht die Position als `<Parent>/<Modulname>`.
- **Kodierung der Steuerung**: Annahme aus Abschnitt 3 an weiteren Exporten und der Rockwell-Doku prüfen.
