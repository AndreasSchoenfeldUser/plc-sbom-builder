# Fixtures

Echte Beispieldateien für die Adapter (CLAUDE.md Abschnitte 2 und 13). Jedes Attribut, das ein Adapter liest, ist durch eine Datei hier und einen Test belegt.

Regeln:

- Einchecken nur mit klarer Lizenz: MIT, Apache-2.0, 0BSD, BSD oder Comquent-eigen.
- Dateien ohne Lizenz nur unter `fixtures/_local/`. Das Verzeichnis ist nicht eingecheckt (`.gitignore`); Tests, die sie brauchen, überspringen sich, wenn die Datei fehlt.
- Synthetische Dateien heißen `*-synthetisch.*` und sind nur erlaubt, wenn für dasselbe Attribut ein echtes Gegenstück existiert.
- Dateien byte-genau übernehmen (`.gitattributes`: `fixtures/** -text`).

| Datei | Umgebung | Repository | Commit | Pfad im Repository | Lizenz | Abgerufen |
|---|---|---|---|---|---|---|
| `codesys/v35/robin-codesys_project.xml` | CODESYS V3.5 SP15, PLCopen XML | `github.com/ScalABLE40/robin` | `42ba56279c4255f769538062f560b76e1a479795` | `robin_updater/cfg/codesys_project.xml` | Apache-2.0, Text in `LICENSES/ScalABLE40-robin.LICENSE` | 31.10.2026 |
| `codesys/v35/osc-mqtt-OSC_MQTT.xml` | CODESYS V3.5 SP4 Patch 1, PLCopen XML (Bibliotheksprojekt) | `github.com/kmpm/OSC-MQTT` | `ea999a108ca0c0ee7809e792b7a0b61c103b08a7` | `OSC_MQTT.xml` | Apache-2.0, Text in `LICENSES/kmpm-OSC-MQTT.LICENSE` | 31.10.2026 |
| `codesys/v35/ooip-script-engine-example.xml` | CODESYS V3.5 SP20 Patch 1, PLCopen XML | `github.com/Jake1510/ooip-tutorial-library` | `ece51b0dd4e19ced17f8aa160f2bdea1af549cc5` | `examples/scripting/Python Scripting/Script Engine Example.xml` | MIT, Text in `LICENSES/Jake1510-ooip-tutorial-library.LICENSE` | 31.10.2026 |
| `codesys/v35/zwei-steuerungen-synthetisch.xml` | CODESYS V3.5, PLCopen XML | synthetisch (Comquent) | – | – | Comquent-eigen | – |
| `codesys/v35/devdesc-geraete-synthetisch.xml` | CODESYS V3.5, PLCopen XML | synthetisch (Comquent) | – | – | Comquent-eigen | – |
| `codesys/devdesc/TCA9548.devdesc.xml`, `PCF8575IN.devdesc.xml`, `SSD1306.devdesc.xml`, `TSL2561.devdesc.xml` | CODESYS-Gerätebeschreibungen | `github.com/stefandreyer/I2C-Multiplex` | `6a9767b9005fbccf2544bdffbbdcf9c7e5cbb19c` | Wurzel des Repositorys, gleiche Dateinamen | MIT, Text in `LICENSES/stefandreyer-I2C-Multiplex.LICENSE` | 31.10.2026 |
| `studio5000/ra-ExampleForCICD_L85E.L5X`, `ra-DelayedSum_AOI.L5X`, `ra-WetBulbTemperature.L5X` | Studio 5000 V36.00, L5X | `github.com/RockwellAutomation/ra-logix-cicd` | `de14d4ac87d5295b2380cce441ed581d1930947f` | `1-production-files/L5Xs/ExampleForCICD_L85E.L5X`, `…/DelayedSum_AOI.L5X`, `…/WetBulbTemperature.L5X` | MIT, Text in `LICENSES/RockwellAutomation-ra-logix-cicd.LICENSE` | 31.10.2026 |
| `studio5000/acd-ACDTestsWithAOI.L5X`, `acd-ACDTestsEmptyRedundant.L5X` | Studio 5000 V34.01, L5X | `github.com/hutcheb/acd` | `3a2aa87314012e095a81881da321da6af046684f` | `resources/ACDTestsWithAOI.L5X`, `resources/ACDTestsEmptyRedundant.L5X` | Apache-2.0, Text in `LICENSES/hutcheb-acd.LICENSE` (keine NOTICE-Datei im Repository) | 31.10.2026 |
| `studio5000/module-baum-synthetisch.L5X` | Studio 5000, L5X | synthetisch (Comquent) | – | – | Comquent-eigen | – |
| `twincat/aixocat-heatpump/` (`.tsproj`, `.plcproj`) | TwinCAT 3.1.4022.4 | `github.com/RWTH-EBC/AixOCAT` | `580100c2eb280a9e50ce7f22ae6bc9536157900b` | `Examples/HeatPumpTestBenchControl/HeatPumpTestBenchControl/` (`HeatPumpTestBenchControl.tsproj`, `HeatPumpTestBenchControl/HeatPumpTestBenchControl.plcproj`) | MIT, Text in `LICENSES/RWTH-EBC-AixOCAT.LICENSE` | 01.11.2026 |
| `twincat/spt-ethercat-diagnostics/` (`.tsproj`, `_Config/PLC/*.xti`, `.plcproj`) | TwinCAT 3.1.4026.24 | `github.com/Beckhoff-USA-Community/SPT_V4_Samples` | `470ce0f0acc28ac16e5237a60fb8259e9c456722` | `SPT EtherCAT Diagnostics/SPT EtherCAT Diagnostics/`, gleiche Pfade | 0BSD, Text in `LICENSES/Beckhoff-USA-Community-SPT_V4_Samples.LICENSE` | 01.11.2026 |
| `twincat/tcopen-cognex/` (`.tsproj`, zwei `.plcproj`) | TwinCAT 3.1.4024.55 | `github.com/Inxton/TcOpen` | `990205791ad1984f9c13d6e1499d8f1c1c4ec20d` | `src/TcoCognexVision/src/XAE/XAE/`, gleiche Pfade | MIT, Text in `LICENSES/Inxton-TcOpen.LICENSE` | 01.11.2026 |
| `twincat/tcopen-core/` (`.tsproj`, drei `.plcproj`) | TwinCAT 3.1.4024.55 | `github.com/Inxton/TcOpen` | `990205791ad1984f9c13d6e1499d8f1c1c4ec20d` | `src/TcoCore/src/XaeTcoCore/`, gleiche Pfade | MIT, Text in `LICENSES/Inxton-TcOpen.LICENSE` | 01.11.2026 |
| `twincat/synthetisch/zelle-synthetisch.tsproj`, `Zelle/Zelle-synthetisch.plcproj` | TwinCAT 3 | synthetisch (Comquent) | – | – | Comquent-eigen | – |

SHA-256 der echten Dateien: robin `acf12d77f7ad9ba162485e9c8d64545d97ab37e81f396507112917330af17f2d`, OSC-MQTT `db021073f01fde3538c5f752cb1b07b7f3b5280d97b2af99301aeee9fd303845`, ooip `ec3146e46c5ad8cc7275f376d2e557080a6f4b7cbfb72b01b75511a8d3e48de8`.

SHA-256 der L5X-Dateien: ra-ExampleForCICD_L85E `d39622b04aa02c0c152faf02c076cea0f06aff51a1019e75b2e1e2813245555e`, ra-DelayedSum_AOI `8dbccf0fd736218ccfe5e62988884ac87f40b5172f92e369ba12d50a3b637256`, ra-WetBulbTemperature `6a022dbb28b46d830565a3fe26e4b6d2f1f281e92c1ecd52c3511aa18c64c496`, acd-ACDTestsWithAOI `89265a5ff85bc7bc14f7e86b0ae6ae2028b354a98cce1cd187fdaa2a35792096`, acd-ACDTestsEmptyRedundant `7f9643025ad40e9fa423dbd836bac3d11c550f4ae2178683ec176f3885b268d4`.

**Synthetische Datei und ihre echten Gegenstücke.** `zwei-steuerungen-synthetisch.xml` deckt ab, was die echten Exporte nicht zeigen: zwei Steuerungen in einem Export, ein Gerät unter einer Steuerung, zwei Anwendungen, Bibliotheken im POU-Pool neben Anwendungsbibliotheken und eine Referenz außerhalb des Musters `Titel, Version (Hersteller)`. Gegenstücke: `configuration`/`DeviceIdentification` (robin, ooip), Verschachtelung über `data[@name="configurations"]` (robin, Achspool), `PlaceholderRedirection` (ooip), Bibliotheken im POU-Pool (OSC-MQTT).

`module-baum-synthetisch.L5X` (Studio 5000) deckt ab, was die echten L5X-Exporte nicht zeigen: ein entferntes Chassis hinter einem Ethernet-Modul, ein E/A-Modul mit `EKey State="Disabled"`, ein Modul mit fremder Herstellerkennung (`65000`, erfunden, ohne Bezug zu einem echten Hersteller), ein Modul ohne `Major`/`Minor` und AOIs mit und ohne `Vendor` und `RevisionExtension`. IP-Adressen aus dem Dokumentationsbereich (RFC 5737). Gegenstücke: `Module`-Attribute, `Port@Upstream`/`@Type="ICP"`, Ethernet-Port mit Adresse, `EKey` und AOI-Attribute in `acd-ACDTestsWithAOI.L5X`, `Vendor`-loses AOI in `ra-DelayedSum_AOI.L5X`.

Die TwinCAT-Projekte sind nur teilweise übernommen: `.tsproj`, die verwiesenen `.xti` unter `_Config/PLC/` und die `.plcproj`, ohne Bausteine, `.tmc` und Visualisierungen. SHA-256: AixOCAT `.tsproj` `8770c82a2c3b0138475b64ed6a3136cd1120639c144214c06dc32b0970f44cdb`, SPT `.tsproj` `225630ab0ee3d008e41fc7923e922dfdf8b8e5b652a36a5f0582e518d9984813`, TcoCognexVision `.tsproj` `c487bb8d5189bc8f423fa7702b5d69735f33cc328667966f8a66763c64018eda`, TcoCore `.tsproj` `ddef9a7dd030a7d48ff174e3dc759e1f0ee9afe030687e05203cb09330e88136`.

`twincat/synthetisch/` deckt ab, was die echten TwinCAT-Projekte nicht zeigen: einen EtherCAT-Antrieb mit fremder Herstellerkennung (`#x00000024`, wie der IndraDrive MPB20 in TcOpen `TcoDrivesBeckhoff`, nicht eingecheckt), ein E/A-Gerät in eigener Datei (`Device@File`) und eine `LibraryReference` mit fester Version. Netzwerte aus dem Dokumentationsbereich (RFC 5737). Gegenstücke: Box- und EtherCAT-Attribute (AixOCAT, SPT), `PlaceholderResolution` (TcOpen), `LibraryReference@Include` (TcoCore).

`devdesc-geraete-synthetisch.xml` verwendet als Gerätekennungen die der vier echten Gerätebeschreibungen; Gegenstücke für Elemente und Kleinschreibung der Id (`0000 000a`) sind robin und ooip.

**Nur lokal** (`fixtures/_local/codesys/`, keine Lizenz): `ec.xml` und `vb.xml` aus `github.com/arwie/controlOS_demo`, Commit `dcbae287a97ee7eda0ea114064e1b44224876762`, Pfade `codesys/controlOS_demo.project.txt/VirtualBox/EtherCAT_Master_SoftMotion/XB_EC_12.xml` und `codesys/controlOS_demo.project.txt/VirtualBox.xml`. Export je Objekt, kein Gesamtexport; nur als Beleg für die Struktur untergeordneter Geräte (docs/adapter-codesys.md 1.5), von keinem Test gelesen.
