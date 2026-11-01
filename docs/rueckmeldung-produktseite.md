# Rückmeldung an die Produktseite

Punkte, an denen der Prototyp von comquent.de/produkte/plc-sbom-builder abweicht oder sie ergänzt. Gesammelt für die nächste Überarbeitung der Seite.

| # | Datum | Punkt | Vorschlag |
|---|---|---|---|
| 1 | 31.10.2026 | Die Seite nennt vier Adapter. Dazu kommt `tia-aml` für den AML-Export aus TIA Portal V17–V21, der ohne TIA-Installation läuft. | Fünften Adapter aufnehmen |
| 2 | 31.10.2026 | Mit `tia-aml` fehlen Bibliotheken und geschützte Bausteine. Die Seite verspricht Bibliothekstypen nur für den Openness-Weg. | Unterschied der beiden TIA-Wege benennen |
| 3 | 31.10.2026 | GSD-Geräte haben im AML keine Bestellnummer. Der SBOM Builder braucht dafür die GSDML-Dateien im Repository. | Voraussetzung nennen |
| 4 | 31.10.2026 | Die Seite nennt den Befehlsnamen nicht. Die Oberflächenentwürfe zeigen `plc-sbom build --maschine …`, das Werkzeug heißt `plc-sbom build --config …`. | Entwürfe angleichen |
| 5 | 31.10.2026 | BSI TR-03183-2 v2.1.0 erlaubt auch SPDX 3.0.1. Die Seite nennt nur CycloneDX 1.6. | SPDX als späteres Format erwähnen |
| 6 | 31.10.2026 | Die Seite nennt CODESYS V3.5. Der Adapter deckt zusätzlich CODESYS V2.3 und CODESYS 4 ab, beide nach einem Spike. | Nach M3/M4 ergänzen |
| 7 | 31.10.2026 | Die Seite verspricht für CODESYS Bibliotheken „direkt aus dem Bibliotheksverwalter“. Der PLCopen-XML-Export zeigt bei Platzhaltern nur die Standardauflösung; welche Version die Gerätebeschreibung festlegt, steht nicht darin. Der Prototyp meldet solche Bibliotheken als `version-unresolved` (E-012). Die effektive Version liefert erst das CODESYS-Scripting (nach dem Prototyp). | Bis dahin den Weg über PLCopen XML mit dieser Einschränkung beschreiben; Anwendern empfehlen, Platzhalter im Projekt umzuleiten |
| 8 | 31.10.2026 | Der PLCopen-Export nennt für CODESYS-Geräte weder Hersteller noch Bestellnummer, nur die Kennung der Gerätebeschreibung (E-014). | Gerätebeschreibungen (`*.devdesc.xml`) im Repository als Voraussetzung nennen, sobald der Leser dafür entschieden ist |
| 9 | 01.11.2026 | Mit `tia-aml` endet ein Release-Build (`--release-build`) mit Exit 1, solange für die Stationen keine Bibliotheken unter `libraries` deklariert sind (`libraries-not-read`, Warnung) und Geräte ohne Firmware-Attribut wie Stromversorgungen nicht über `passiveOrderNumbers` als passiv erklärt sind (`firmware-attribute-missing`, Warnung, E-019). | Beim AML-Weg nennen, was der Anwender in `machine.yml` ergänzt |
