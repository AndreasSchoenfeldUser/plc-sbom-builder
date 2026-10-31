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
