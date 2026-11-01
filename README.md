# PLC SBOM Builder (`plc-sbom`, Prototyp)

Kommandozeilenwerkzeug, das eine CI-Pipeline in jedem Build aufruft. Es liest die SPS-Projekte einer Maschine aus und erzeugt daraus eine SBOM im Format CycloneDX 1.6 JSON, auch wenn die Maschine Steuerungen mehrerer Hersteller enthält. Die Datei wird gegen das CycloneDX-Schema und die Pflichtfelder der BSI TR-03183-2 geprüft; ein Fehler beendet den Build.

Die Bedienung (Befehle, Ausgaben, Lückencodes, Einsatz in der Pipeline) beschreibt die [ANLEITUNG.md](ANLEITUNG.md). Produktziel, Recherchestand, Architektur und Meilensteine stehen in [CLAUDE.md](CLAUDE.md). Produkt der Comquent GmbH; Schwesterwerkzeuge sind [plc-build](../plc-build-tool/) und [plc-git-bridge](../plc-git-bridge/).

## Stand

Stand 01.11.2026, Version 0.1.0: Das Gerüst (M0) steht, M1 und M2 sind abgeschlossen, M5, M6, M7 und M8 sind umgesetzt. `plc-sbom validate` ist nutzbar: Es prüft eine SBOM gegen das eingebettete Schema CycloneDX 1.6 und die Regeln der BSI TR-03183-2 und schreibt die Ergebnisdatei. Welche Regel woher kommt, steht in [docs/regeln-tr03183.md](docs/regeln-tr03183.md); in CI prüft sbomqs den Validator gegen. Mit M2 liest der Adapter `codesys` PLCopen-XML-Exporte aus CODESYS V3.5; `plc-sbom inspect <export.xml> --adapter codesys` zeigt das Rohinventar (Steuerungen, Geräte, Bibliotheken mit Auflösung). Mit M5 liest der Adapter `studio5000` L5X-Exporte aus Rockwell Studio 5000 (Steuerung, Module mit Revision und Kodierung, Add-On Instructions); `inspect --adapter studio5000` zeigt auch dafür das Rohinventar. Mit M6 liest der Adapter `twincat` Beckhoff-TwinCAT-3-Projekte (`.tsproj` mit EtherCAT-Teilnehmern, `.plcproj` mit Bibliotheken), ebenfalls mit `inspect`. Mit M7 liest der Adapter `tia-aml` den AML-Export aus Siemens TIA Portal V17 bis V21: Geräte mit Bestellnummer und projektierter Firmware, passive Module, Stationen je CPU über das PROFINET-IO-System; `inspect --adapter tia-aml` zeigt CPUs, IO-Systeme und die Zuordnung jedes Geräts. Mit M8 kommen Hersteller und Bestellnummer von GSD-Geräten aus den GSDML-Dateien im Repository (`inspect --gsdml <verzeichnis>`). Alle anderen Befehle sind mit ihren Optionen angelegt; ein Aufruf endet mit Exit 2 und `[NOT_IMPLEMENTED]` und nennt den Meilenstein, der den Befehl umsetzt. Die Beispiele unten beschreiben das Zielbild aus CLAUDE.md.

| Meilenstein | Inhalt | Stand |
|---|---|---|
| M0 | Gerüst: Solution, CLI-Skelett, Exit-Codes, CI-Matrix, Pakete | erledigt |
| M1 | Modell, CycloneDX-Writer, Validator (Schema und TR-Regeln) | erledigt: Modell, Writer (Variante B, E-007), Validator, `validate`, `docs/regeln-tr03183.md`, Gegenprüfung mit sbomqs ohne Abweichung (E-010) |
| M2 | Adapter `codesys`, Quelle `v35` (CODESYS V3.5, PLCopen XML) | erledigt: Adapter, `inspect --adapter codesys`, Leser für CODESYS-Gerätebeschreibungen (E-015), Feldtabelle [docs/adapter-codesys.md](docs/adapter-codesys.md), echte Fixtures, drei Golden Files; CI auf ubuntu und windows grün |
| M3, M4 | Adapter `codesys`, Quellen `v23` (CODESYS V2.3) und `c4` (CODESYS 4) | zurückgestellt, bis die Comquent-Demo-Projekte da sind (E-016) |
| M5 | Adapter `studio5000` (Rockwell) | umgesetzt: Adapter, `inspect --adapter studio5000`, Feldtabelle [docs/adapter-studio5000.md](docs/adapter-studio5000.md) (E-017), echte Fixtures aus `ra-logix-cicd` und `hutcheb/acd`, drei Golden Files, Gegenprüfung mit sbomqs ohne Abweichung; CI auf ubuntu und windows grün (Lauf 41251063889) |
| M6 | Adapter `twincat` (Beckhoff) | umgesetzt: Adapter, `inspect --adapter twincat`, Feldtabelle [docs/adapter-twincat.md](docs/adapter-twincat.md) (E-018), neuer Lückencode `devices-not-read`, echte Fixtures aus AixOCAT, SPT_V4_Samples und TcOpen, fünf Golden Files, Gegenprüfung mit sbomqs ohne Abweichung; offen ist die Bestätigung durch CI |
| M7 | Adapter `tia-aml` (TIA V17–V21, AML-Export) | umgesetzt: Leser und Adapter, Stationen über IO-Systeme, `inspect --adapter tia-aml`, Feldtabelle [docs/adapter-tia-aml.md](docs/adapter-tia-aml.md) (E-019), neuer Lückencode `firmware-attribute-missing`, V17-Fixture (MIT) und eine synthetische Datei mit zwei CPUs, drei Golden Files, V18- und V21-Exporte ohne Lizenz laufen lokal ohne Fehler, Gegenprüfung mit sbomqs ohne Abweichung; offen sind das Golden File aus dem Comquent-V21-Export und die Bestätigung durch CI |
| M8 | GSDML-Leser, GSD-Geräte in `tia-aml` | umgesetzt: GSDML-Leser in `Adapters/Gsdml/`, Hersteller und Bestellnummer aus der GSDML (E-020), `inspect --gsdml`, GSDML-Fixture des Leuze BCL248i (MIT, aus dem Repository der V17-Fixture), neues Golden File, Gegenprüfung mit sbomqs ohne Abweichung; GSD-Geräte ohne GSDML tragen jetzt auch `gsdml-not-found` |
| M9 | `build` mit `machine.yml`, Merge, Determinismus | offen |
| M10–M12 | `report`, `compare`, `ids` | offen |
| M13 | Jenkinsfile, README und ANLEITUNG.md auf Release-Stand, Release-Pakete | offen |
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

Voraussetzung: .NET 10 SDK.

```bash
dotnet build PlcSbom.sln
dotnet test                                     # Microsoft.Testing.Platform, siehe docs/entscheidungen.md E-002
dotnet format PlcSbom.sln --verify-no-changes
dotnet run tools/check-licenses.cs              # Lizenzen aller Pakete; mit -- --write neue THIRD-PARTY-NOTICES.md
dotnet run tools/crosscheck-sbomqs.cs -- --sbomqs <pfad>   # Validator gegen sbomqs v2.1.3 über alle Golden Files
```

Alle Testfälle mit Testläufen und Ergebnissen beschreibt [TESTFAELLE.md](TESTFAELLE.md) (Stand 01.11.2026: 480 Tests, alle bestanden; fünf davon brauchen Dateien ohne Lizenz aus `fixtures/_local/` und überspringen sich in CI). Die Gegenprüfung mit sbomqs und ihr Ergebnis beschreibt [docs/regeln-tr03183.md](docs/regeln-tr03183.md) Abschnitt 10.

Paket für eine Plattform (self-contained, eine Datei):

```bash
dotnet publish src/PlcSbom.Cli/PlcSbom.Cli.csproj -c Release -r linux-x64 -o out/publish   # oder win-x64, osx-arm64
out/publish/plc-sbom --help
```

CI (GitHub Actions, ubuntu und windows) baut, testet, prüft Format und Lizenzen, packt `plc-sbom-<version>-linux-x64.tar.gz` und `plc-sbom-<version>-win-x64.zip` und ruft die Exe aus dem ausgepackten Paket auf.

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
