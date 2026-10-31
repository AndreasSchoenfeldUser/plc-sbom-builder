# PLC SBOM Builder (`plc-sbom`, Prototyp)

Kommandozeilenwerkzeug, das eine CI-Pipeline in jedem Build aufruft. Es liest die SPS-Projekte einer Maschine aus und erzeugt daraus eine SBOM im Format CycloneDX 1.6 JSON, auch wenn die Maschine Steuerungen mehrerer Hersteller enthält. Die Datei wird gegen das CycloneDX-Schema und die Pflichtfelder der BSI TR-03183-2 geprüft; ein Fehler beendet den Build.

Produktziel, Recherchestand, Architektur und Meilensteine stehen in [CLAUDE.md](CLAUDE.md). Produkt der Comquent GmbH; Schwesterwerkzeuge sind [plc-build](../plc-build-tool/) und [plc-git-bridge](../plc-git-bridge/).

## Stand

Stand 31.10.2026: Es gibt noch keinen Code. Die Befehle und Beispiele unten beschreiben das Zielbild aus CLAUDE.md und laufen erst nach den genannten Meilensteinen.

| Meilenstein | Inhalt | Stand |
|---|---|---|
| M0 | Gerüst: Solution, CLI-Skelett, Exit-Codes, CI-Matrix, Pakete | offen |
| M1 | Modell, CycloneDX-Writer, Validator (Schema und TR-Regeln) | offen |
| M2–M4 | Adapter `codesys` (V3.5, V2.3, CODESYS 4) | offen, V2.3 und CODESYS 4 brauchen Comquent-Demo-Projekte |
| M5 | Adapter `studio5000` (Rockwell) | offen |
| M6 | Adapter `twincat` (Beckhoff) | offen |
| M7, M8 | Adapter `tia-aml` (TIA V17–V21, AML-Export), GSDML-Leser | offen |
| M9 | `build` mit `machine.yml`, Merge, Determinismus | offen |
| M10–M12 | `report`, `compare`, `ids` | offen |
| M13 | Jenkinsfile, Anwender-README, Release-Pakete | offen |
| M14 | Adapter `tia-portal` über TIA Openness V21 (Windows) | offen |

## Was das Werkzeug liest

| Adapter | Umgebung | Eingang | Läuft auf |
|---|---|---|---|
| `codesys` | CODESYS V3.5, V2.3, CODESYS 4 | V3.5: PLCopen-XML-Export; V2.3 und CODESYS 4 nach Spike | überall |
| `studio5000` | Rockwell Studio 5000 | L5X-Export | überall |
| `twincat` | Beckhoff TwinCAT 3 | `.tsproj`/`.xti` und `.plcproj` | überall |
| `tia-aml` | Siemens TIA Portal V17–V21 | AML aus dem CAx-Export | überall |
| `tia-portal` | Siemens TIA Portal V21 | `.ap21`/`.zap21` über Openness | Windows mit TIA V21 |

Je Station liefert der Adapter Geräte mit Bestellnummer, projektierte Firmware-Stände, Bibliotheken und das Anwenderprogramm. Das Werkzeug führt das mit Hersteller-SBOMs und mit Teil-SBOMs aus Syft oder cdxgen zusammen (Maschine → Station → Komponente) und schreibt neben der JSON-Datei einen Prüfbericht in HTML oder Markdown.

Was eine Quelle nicht hergibt, wird nicht geschätzt. Fehlt etwa die Firmware einer EtherCAT-Klemme im Projekt, steht an der Komponente ein Lückenvermerk (`firmware-not-in-project`) statt einer geratenen Version. Netzdaten wie IP-Adressen oder PROFINET-Gerätenamen kommen nie in die SBOM.

## Aufruf (Zielbild)

```text
plc-sbom build      --config machine.yml --release <kennung> [--commit <sha>]
                    [--out build/sbom-maschine.cdx.json] [--report html|md|none] [--release-build]
plc-sbom validate   <sbom> [--release-build]
plc-sbom report     <sbom> --format html|md --out build/sbom-bericht.html
plc-sbom compare    <sbom> --field feld/1042.json [--as-built build/sbom-1042.cdx.json] [--csv <datei>]
plc-sbom ids        <sbom> --csaf <verzeichnis> [--cpe-map kennungen/cpe-map.yml]
plc-sbom inspect    <projektdatei> --adapter <id> [--gsdml <verzeichnis>]
plc-sbom export-aml --project <.ap21|.zap21> --out <datei.aml> [--allow-upgrade]   (Windows, TIA V21)
plc-sbom doctor     [--adapter tia-portal] [--deep]                                  (Windows, TIA V21)
```

Neben der SBOM schreibt `build` eine Ergebnisdatei `<out>.result.json` mit Exit-Code und der Zahl der Fehler, Warnungen und Hinweise. Damit kann eine Jenkins-Pipeline einen Build mit Warnungen als instabil markieren.

Eine Maschine wird in `machine.yml` beschrieben, gekürzt:

```yaml
machine:
  name: Maschine XY
  supplier: Musterbau GmbH
  contact: sbom@musterbau.example
namespace: firma

stations:
  - name: PLC_1
    adapter: tia-aml
    path: export/Linie1.aml
    plc: PLC_1
    program: tia/PLC_1

  - name: Roboterzelle
    adapter: twincat
    path: twincat/Zelle/Zelle.tsproj
    program: twincat/Zelle

vendorSboms: sbom/lieferanten/*.cdx.json
```

Alle Schlüssel stehen in CLAUDE.md, Abschnitt 7.4.

## Exit-Codes

Gleich wie bei `plc-build`.

| Code | Bedeutung |
|---|---|
| 0 | erfolgreich; Warnungen im Entwicklungs-Build und Hinweise ändern daran nichts |
| 1 | Befund: Schemafehler, Pflichtfeld fehlt, mit `--release-build` auch Warnungen; `compare` findet Abweichungen |
| 2 | Aufruf oder Konfiguration falsch (`machine.yml`, Quelldatei, Adapter-ID) |
| 3 | Umgebung (TIA fehlt, Openness-Gruppe, Firewall, Lizenz, Openness-Befehl auf Linux) |
| 4 | Abbruch (Zeitlimit, Worker abgestürzt oder ohne gültige Antwort) |

## Bauen und testen

Voraussetzung: .NET 10 SDK. Ab M0:

```bash
dotnet build PlcSbom.sln
dotnet test PlcSbom.sln
```

Der Openness-Worker unter `workers/tia-openness/` ist eine eigene Solution (.NET Framework 4.8) und baut nur auf Windows mit TIA Portal V21. Live-Tests gegen TIA laufen nur mit `PLC_SBOM_LIVE=1`.

## Aufbau (geplant)

| Pfad | Inhalt |
|---|---|
| `src/PlcSbom.Core` | Modell, Adapter, Merge, Kennungen, Feldabgleich, Validator, Ausgabe |
| `src/PlcSbom.Cli` | Einstieg `plc-sbom`, Befehle, Exit-Codes |
| `workers/tia-openness` | Openness-Worker für `tia-portal` und `export-aml` (ab M14) |
| `tests/PlcSbom.Tests` | xUnit, Golden Files unter `golden/` |
| `fixtures/` | echte Beispieldateien, Herkunft und Lizenz in `FIXTURES.md`; Dateien ohne Lizenz nur unter `_local/` (nicht eingecheckt) |
| `samples/maschine-xy` | Beispielmaschine mit `machine.yml` und erwarteter Ausgabe |
| `docs/` | Entscheidungen, TR-Regeln, Adapter-Feldtabellen, Messungen |

## Abgrenzung

Das Werkzeug bewertet keine Schwachstellen und schreibt keine CVEs oder VEX-Dokumente in die SBOM; das übernimmt ein Überwachungssystem wie Dependency-Track. Es liest keine Anlagen im Netz aus, analysiert keine Firmware-Binärdateien und schreibt nie in Engineering-Projekte. Eine SBOM allein erfüllt den Cyber Resilience Act nicht.

## Lizenz

Proprietär, Comquent GmbH.
