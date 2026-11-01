# Beispielmaschine „Maschine XY“

Eingang für `plc-sbom build` (Meilenstein M9) und für den Test, dass gleiche Eingabe dieselbe SBOM ergibt. Erwartete Ausgabe: [tests/PlcSbom.Tests/golden/maschine-xy.cdx.json](../../tests/PlcSbom.Tests/golden/maschine-xy.cdx.json).

```bash
SOURCE_DATE_EPOCH=1792234800 plc-sbom build --config samples/maschine-xy/machine.yml --release R2026.10 \
  --commit a1b2c3d4e5f60718293a4b5c6d7e8f9012345678
```

Mit dieser Zeit und diesem Commit entspricht `build/sbom-maschine.cdx.json` byte-genau dem Golden File (Werkzeugversion 0.1.0).

| Datei | Inhalt | Herkunft |
|---|---|---|
| `machine.yml` | drei Stationen: Verpackung (CODESYS V3.5), PLC_1 (TIA Portal V17, AML mit GSD-Gerät), Roboterzelle (TwinCAT 3) | Comquent-eigen |
| Projektdateien | über `../../fixtures/…`: robin (CODESYS), PLC.Commissioning.Lib (TIA, GSDML), AixOCAT (TwinCAT) | siehe [fixtures/FIXTURES.md](../../fixtures/FIXTURES.md) |
| `programm/` | kleine Programmquellen je Station für den Hash über die Quellen | Comquent-eigen, synthetisch |
| `hmi-web/` | `package.json` und npm-Lockfile einer Web-Oberfläche (`ms` 2.1.3, `picocolors` 1.1.1); Lockfile mit `npm install --package-lock-only` erzeugt | Comquent-eigen |
| `sbom/teil/hmi-web.cdx.json` | Teil-SBOM, erzeugt am 01.11.2026 mit Syft 1.54.1: `cd hmi-web && SYFT_FILE_METADATA_SELECTION=none syft scan dir:. --source-name hmi-web --source-version 1.4.0 -o cyclonedx-json@1.6` | Comquent-eigen, echte Syft-Ausgabe |
| `sbom/lieferanten/siemens-cpu1214c-v4.5-synthetisch.cdx.json` | Hersteller-SBOM zur CPU 1214C V4.5, vollständig nach TR-03183-2, Bestellnummer als Property, zwei Bestandteile. Hashes über erfundene Dateinamen, URLs unter `example.com`. Kein Dokument von Siemens | Comquent-eigen, synthetisch (docs/entscheidungen.md E-021) |
| `kennungen/cpe-map.yml` | CPE-Tabelle mit einer Zeile für die CPU 1214C; die CPE ist ein Beispiel | Comquent-eigen |

Alle Dateien unter `samples/` sind in `.gitattributes` als `-text` eingetragen: Der Hash über die Programmquellen und die SHA-256 der Eingangs-SBOMs hängen an den Bytes, auch an den Zeilenenden.
