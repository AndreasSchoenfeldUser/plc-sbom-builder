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

SHA-256 der echten Dateien: robin `acf12d77f7ad9ba162485e9c8d64545d97ab37e81f396507112917330af17f2d`, OSC-MQTT `db021073f01fde3538c5f752cb1b07b7f3b5280d97b2af99301aeee9fd303845`, ooip `ec3146e46c5ad8cc7275f376d2e557080a6f4b7cbfb72b01b75511a8d3e48de8`.

**Synthetische Datei und ihre echten Gegenstücke.** `zwei-steuerungen-synthetisch.xml` deckt ab, was die echten Exporte nicht zeigen: zwei Steuerungen in einem Export, ein Gerät unter einer Steuerung, zwei Anwendungen, Bibliotheken im POU-Pool neben Anwendungsbibliotheken und eine Referenz außerhalb des Musters `Titel, Version (Hersteller)`. Gegenstücke: `configuration`/`DeviceIdentification` (robin, ooip), Verschachtelung über `data[@name="configurations"]` (robin, Achspool), `PlaceholderRedirection` (ooip), Bibliotheken im POU-Pool (OSC-MQTT).

`devdesc-geraete-synthetisch.xml` verwendet als Gerätekennungen die der vier echten Gerätebeschreibungen; Gegenstücke für Elemente und Kleinschreibung der Id (`0000 000a`) sind robin und ooip.

**Nur lokal** (`fixtures/_local/codesys/`, keine Lizenz): `ec.xml` und `vb.xml` aus `github.com/arwie/controlOS_demo`, Commit `dcbae287a97ee7eda0ea114064e1b44224876762`, Pfade `codesys/controlOS_demo.project.txt/VirtualBox/EtherCAT_Master_SoftMotion/XB_EC_12.xml` und `codesys/controlOS_demo.project.txt/VirtualBox.xml`. Export je Objekt, kein Gesamtexport; nur als Beleg für die Struktur untergeordneter Geräte (docs/adapter-codesys.md 1.5), von keinem Test gelesen.
