# Entscheidungen

Kurze ADRs, neueste unten. Je Eintrag: Kontext, Entscheidung, Folgen.

## E-001: Exit 2 für Befehle, die im Gerüst noch fehlen (31.10.2026)

**Kontext.** M0 legt alle Befehle aus CLAUDE.md Abschnitt 7.1 mit ihren Optionen an, umgesetzt werden sie erst in späteren Meilensteinen. Ein Aufruf eines solchen Befehls braucht einen Exit-Code aus Abschnitt 7.2. Keiner passt genau: 1 wäre ein Befund am Projekt, 3 ein Problem des Agenten, 4 ein Absturz.

**Entscheidung.** Exit 2 mit Code `NOT_IMPLEMENTED` und dem Meilenstein in der Meldung, z. B. `[NOT_IMPLEMENTED] Der Befehl „build“ ist in plc-sbom 0.1.0 noch nicht umgesetzt. Er folgt mit Meilenstein M9.` Der Aufruf verlangt etwas, das diese Version nicht kann; behoben wird das am Aufruf oder durch eine neuere Version. Die Plattformprüfung der Openness-Befehle (`export-aml`, `doctor --adapter tia-portal`) läuft vorher: außerhalb von Windows Exit 3 mit `OPENNESS_WINDOWS_ONLY`, wie in Abschnitt 7.2 vorgesehen.

**Folgen.** Die Exit-Codes selbst bleiben unverändert. `NOT_IMPLEMENTED` verschwindet Befehl für Befehl mit den Meilensteinen. Unerwartete Ausnahmen ergeben Exit 4 mit Code `UNEXPECTED`, Stacktrace nur mit `--verbose`.

## E-002: Testlauf über Microsoft.Testing.Platform, xunit.v3 4.x (31.10.2026)

**Kontext.** plc-git-bridge nutzt xunit.v3 3.2.2 mit VSTest. Aktuell auf nuget.org ist xunit.v3 4.0.2 (09.10.2026). Diese Version bringt Microsoft.Testing.Platform 2.x mit, und das .NET-10-SDK verweigert damit den VSTest-Weg von `dotnet test`.

**Entscheidung.** xunit.v3 4.0.2, Testlauf über Microsoft.Testing.Platform (`global.json`: `"test": { "runner": "Microsoft.Testing.Platform" }`). `xunit.runner.visualstudio` und `Microsoft.NET.Test.Sdk` entfallen.

**Folgen.** Tests laufen mit `dotnet test` im Wurzelverzeichnis oder `dotnet test --solution PlcSbom.sln`; die Form `dotnet test PlcSbom.sln` geht nicht mehr. xunit.v3 zieht transitiv `Microsoft.Testing.Extensions.Telemetry` und `Microsoft.ApplicationInsights` (beide MIT) nach. Die Telemetrie ist in CI über `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` abgeschaltet; beide Pakete gehören nur zu den Tests und werden nicht ausgeliefert.

## E-003: Abhängigkeiten M0 (31.10.2026)

Geprüft auf nuget.org am 31.10.2026, Versionen in `Directory.Packages.props`.

| Paket | Version | Lizenz | Zweck | Ausgeliefert |
|---|---|---|---|---|
| System.CommandLine | 2.0.14 | MIT | Befehle, Optionen, Hilfe | ja |
| xunit.v3 | 4.0.2 | Apache-2.0 | Tests | nein |

`CycloneDX.Core` und `YamlDotNet` kommen mit M1 bzw. M9 dazu, jeweils mit Eintrag hier. (Für M1 überholt durch E-008: statt `CycloneDX.Core` ein eigener Writer und NJsonSchema.) System.CommandLine 3.0 liegt als Release Candidate vor; bis zur stabilen Version bleibt es bei 2.0.x.

## E-004: Lizenzprüfung und THIRD-PARTY-NOTICES.md über ein eigenes Skript (31.10.2026)

**Kontext.** CLAUDE.md Abschnitt 11 verlangt eine Lizenzprüfung aller Pakete in CI und eine aus den Paketen erzeugte `THIRD-PARTY-NOTICES.md`. Fertige Werkzeuge dafür wären eine weitere Abhängigkeit mit eigener Lizenz.

**Entscheidung.** `tools/check-licenses.cs`, eine dateibasierte .NET-10-Anwendung ohne Pakete (`dotnet run tools/check-licenses.cs`). Sie liest die Paketliste aus `dotnet list package --include-transitive --format json` und je Paket den SPDX-Ausdruck aus der `.nuspec` im NuGet-Cache. Erlaubt sind MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause und 0BSD; bei `OR` reicht eine erlaubte Seite, bei `AND` müssen alle erlaubt sein, `WITH` gilt als nicht erlaubt. Pakete ohne SPDX-Ausdruck (nur `licenseUrl` oder Lizenzdatei) sind ein Fehler, bis sie nach Rückfrage geklärt sind. Ausgeliefert sind die Pakete der Projekte unter `src/`; nur sie kommen in die Notices. CI prüft mit `--verify`, dass die eingecheckte Datei aktuell ist.

**Folgen.** Nach jeder Paketänderung `dotnet run tools/check-licenses.cs -- --write` und die Datei einchecken. Offen für M13: Die Hinweise zu Drittkomponenten der .NET-Laufzeit stehen bisher nur als Link in der Datei; für die Release-Pakete die Datei aus dem Runtime-Pack beilegen.

## E-005: Pakete self-contained als Einzeldatei, ohne Trimming (31.10.2026)

**Kontext.** CLAUDE.md Abschnitt 11: Pakete ohne installierte .NET-Laufzeit, `plc-sbom-<version>-linux-x64.tar.gz` und `plc-sbom-<version>-win-x64.zip`.

**Entscheidung.** `dotnet publish -r <rid>` erzeugt eine self-contained Einzeldatei; die Einstellungen stehen in `src/PlcSbom.Cli/PlcSbom.Cli.csproj` und greifen nur mit Runtime-Kennung, Build und Test bleiben davon unberührt. Kein Trimming, weil YamlDotNet und CycloneDX.Core mit Reflection arbeiten. Symbole sind eingebettet (`DebugType=embedded`), damit kein loses `.pdb` im Paket liegt und `--verbose` Zeilennummern zeigt. Ein Paket enthält die Exe und `THIRD-PARTY-NOTICES.md`.

**Folgen.** Die Exe ist rund 74 MB groß (linux-x64, win-x64). `plc-sbom --version` zeigt die Version aus `Directory.Build.props` und nach `+` den Commit, aus dem gebaut wurde. CI baut beide Pakete bei jedem Push, packt sie aus und ruft `--help`, `--version` und einige Exit-Codes auf. Release-Pakete auf Tag, `SHA256SUMS` und die eigene SBOM des Werkzeugs folgen mit M13.

## E-006: Solution im Format .sln (31.10.2026)

Das .NET-10-SDK legt neue Solutions als `.slnx` an. CLAUDE.md nennt `PlcSbom.sln`, die Schwesterprojekte nutzen `.sln`. Deshalb `dotnet new sln --format sln`. Ein Wechsel auf `.slnx` ist später ohne Folgen für das Werkzeug möglich.

## E-007: SBOM nur mit Software-Komponenten (Variante B) (31.10.2026)

**Kontext.** CLAUDE.md Abschnitt 4.3 lässt für M1 offen, ob Stationen und Geräte als Komponenten vom Typ `device` in die SBOM kommen (Variante A) oder nur die Software (Variante B), mit `sbomqs --bsi` als Schiedsrichter. Die SBOM-Definition des CRA (Art. 3 Nr. 39) umfasst nur Softwareelemente, die TR-03183-2 regelt Hardware nicht. Jede Komponente braucht die Pflichtfelder aus TR 5.2.2; für ein Gerät wären Dateiname, Hash, ausführbar, Archiv und strukturiert sinnlos und könnten nur mit Lückenvermerken oder als logische Komponente gefüllt werden.

**Entscheidung.** Variante B. Komponenten sind nur `firmware`, `application` und `library`. Gerät und Station stehen als Properties an der Firmware (`<ns>:device:*`, `<ns>:station`), die Struktur steht in `dependencies`: Maschine → Firmware und Anwenderprogramme, Programm → Firmware der Steuerung, Bibliotheken und Laufzeit. Die Maschine ist `metadata.component` vom Typ `device` und als logische Komponente gekennzeichnet (TR 3.2.2). Passive Module und Racks kommen nicht in die SBOM, sondern in den Hardware-Anhang des Berichts (M10).

**Folgen.** sbomqs bewertet nur Software-Komponenten und vermisst an der Maschine Dateiname, Hash und Dateieigenschaften, weil es logische Komponenten nicht kennt (E-010). Der lesbare Baum Maschine → Station → Gerät → Firmware entsteht im Bericht. Umgesetzt in `CycloneDxWriter`, abgesichert durch Test W-05.

## E-008: Eigener CycloneDX-Writer und NJsonSchema statt CycloneDX.Core (31.10.2026)

**Kontext.** CLAUDE.md sah `CycloneDX.Core` 12.1.2 (Apache-2.0) für Modell, Serialisierung und Schemaprüfung vor. Das Paket hängt an JsonSchema.Net ab Version 9.3 (dazu JsonPointer.Net, Json.More.Net). Diese Pakete tragen auf nuget.org keinen SPDX-Ausdruck; ihre NuGet-Binärdateien stehen seit Version 8 unter der EULA „Open Source Maintenance Fee“, nach der Unternehmen ab 10.000 USD Umsatz bei kommerzieller Nutzung eine monatliche Gebühr zahlen. Der Quelltext bleibt MIT. Die Lizenzprüfung (E-004) hat die drei Pakete abgelehnt. Am 31.10.2026 standen zur Wahl: eigener Writer mit NJsonSchema, CycloneDX.Core mit Gebühr, JsonSchema.Net selbst aus dem Quelltext bauen, eigene Schemaprüfung.

**Entscheidung.** Auf Rückfrage vom Anwender entschieden: eigener Writer mit `System.Text.Json` (`Utf8JsonWriter`) und Schemaprüfung mit NJsonSchema 11.6.1 gegen das eingebettete offizielle Schema CycloneDX 1.6.2 (`bom-1.6.schema.json` mit `spdx.schema.json` und `jsf-0.82.schema.json`). Verweise löst ein eigener Resolver nur aus den eingebetteten Dateien auf; jeder andere Verweis ist ein Fehler, damit die Prüfung nie ins Netz geht.

| Paket | Version | Lizenz | Zweck | Ausgeliefert |
|---|---|---|---|---|
| NJsonSchema | 11.6.1 | MIT | Schemaprüfung CycloneDX 1.6 | ja |
| NJsonSchema.Annotations | 11.6.1 | MIT | transitiv | ja |
| Newtonsoft.Json | 13.0.3 | MIT | transitiv | ja |
| Namotion.Reflection | 3.5.0 | MIT | transitiv | ja |

**Folgen.** Der Writer bestimmt jedes Byte selbst: feste Feldreihenfolge, sortierte Komponenten und Properties, LF, kein BOM (CLAUDE.md 8.4). Er schreibt nur, was das Modell trägt; ein neues CycloneDX-Feld braucht Code im Writer. Ein SPDX-Writer (nach dem Prototyp) entsteht ebenso selbst. NJsonSchema unterstützt die Schlüsselwörter des CycloneDX-1.6-Schemas, die Tests in `SbomValidatorTests` belegen das für die genutzten Fälle; `cyclonedx-cli validate` als zweite Schemaprüfung in CI steht noch aus. Kein Trimming (E-005) bleibt richtig, NJsonSchema und Newtonsoft.Json arbeiten mit Reflection.

## E-009: Property-Namen nach der BSI-Taxonomie, effektive Lizenz optional (31.10.2026)

**Kontext.** Das TR-PDF v2.1.0 schreibt in Tabelle 12 `bsi:component:effectiveLicense`, die BSI CycloneDX Property Taxonomy v0.1.2 `bsi:component:effectiveLicence`. Die Taxonomie verlangt außerdem für `effectiveLicence` „MUST occur exactly once per component“, die TR führt die effektive Lizenz in 5.2.5 als optionales Feld („MAY“).

**Entscheidung.** Für Property-Namen gilt die Taxonomie, auf die die TR in Abschnitt 8.2 selbst verweist (wie in CLAUDE.md Abschnitt 9 vorgesehen). Der Writer schreibt `effectiveLicence`; der Validator warnt bei `effectiveLicense` (`EFFECTIVE_LICENCE_SPELLING`), ein doppelter Eintrag ist ein Fehler. Bei der Pflicht folgt der Validator der TR: Eine fehlende effektive Lizenz ist kein Befund.

**Folgen.** sbomqs akzeptiert beide Schreibweisen und wertet die effektive Lizenz als optional. **Offen:** Soll das Werkzeug der Taxonomie folgen und die effektive Lizenz an jeder Komponente verlangen (dann mit Lückenvermerk `license-unknown`, wenn sie fehlt)? Bis zur Antwort gilt die TR.

## E-010: Gegenprüfung mit sbomqs als feldweiser Abgleich (31.10.2026)

**Kontext.** M1 ist laut CLAUDE.md fertig, wenn der eigene Validator **und** `sbomqs --bsi` die SBOM aus dem Handmodell „akzeptieren“. sbomqs v2.1.3 (`compliance --bsi-v21` für TR v2.1.0) gibt aber kein Urteil, sondern Punkte je Feld. Es kennt weder Lückenvermerke noch logische Komponenten (TR 3.2.2) und gibt 0 Punkte für die Felder aus TR 5.2.3 und 5.2.4, die nur „falls vorhanden“ Pflicht sind. Eine SBOM, die Lücken ehrlich vermerkt statt Werte zu erfinden, kann die volle Punktzahl deshalb nie erreichen; das Handmodell erreicht 6,5 von 10.

**Optionen.** (a) Mindestpunktzahl: sagt nichts darüber, ob beide Prüfer dasselbe sehen, und die Schwelle wäre willkürlich. (b) Werte ergänzen, bis sbomqs 10 vergibt: verletzt die Leitplanke „kein erfundener Wert“. (c) Feldweiser Abgleich: Für jede Komponente und jedes Pflichtfeld aus TR 3.1, 4, 5.2.1 und 5.2.2 muss der Validator dort einen Fehler oder einen Lückenvermerk melden, wo sbomqs das Feld vermisst, und darf keinen Fehler melden, wo sbomqs es findet.

**Entscheidung.** (c), umgesetzt in `tools/crosscheck-sbomqs.cs` und als CI-Job `crosscheck` über alle Golden Files, mit sbomqs v2.1.3 (Prüfsumme im Workflow). Eine Abweichung lässt den Job scheitern und gilt als Fehler im eigenen Validator, bis das Gegenteil belegt ist. Damit auch die Seite „vorhanden“ geprüft wird, liegt die synthetische Minimal-SBOM als zweites Golden File `minimal-synthetisch.cdx.json` daneben. „Akzeptieren“ aus CLAUDE.md M1 heißt damit: Validator ohne Fehler, Abgleich ohne Abweichung.

**Folgen.** Ergebnis am 31.10.2026: keine Abweichung in beiden Golden Files. Der Abgleich hat vorher zwei Lücken im Validator gefunden, die jetzt Regeln sind: `DISTRIBUTION_LICENCE_INVALID` (TR 6.1) und `COMPONENT_UNREACHABLE` (TR 5.1). Bekannter Unterschied: sbomqs zählt auch `distribution-intake` als Ort des Hashes, der Validator bleibt bei TR-Tabelle 9 (`distribution`). Neue sbomqs-Versionen können Feldnamen oder die Kürzung der Komponentennamen ändern; der Abgleich meldet unbekannte Felder und Kennungen als Fehler. Einzelheiten in `docs/regeln-tr03183.md` Abschnitt 10. sbomqs ist ein CI-Werkzeug und wird nicht ausgeliefert.

## E-011: Hash und Dateiname bei Hersteller-Firmware als Hinweis (31.10.2026)

**Kontext.** CLAUDE.md Abschnitt 9 verlangt, im TR-PDF zu prüfen, wie TR 3.2.1 für Hersteller-Firmware gilt. Der Satz lautet: „Information that is not available due to the way a component is assembled has to be omitted even if it is specified as a required data field, e.g. the hash or filename of the component.“ Er steht im Absatz über Komponenten, die aus anderen so zusammengesetzt sind, dass sich die ursprünglichen nicht mehr bestimmen lassen.

**Entscheidung.** Das Werkzeug liest den Satz so: Die Maschine wird aus Geräten zusammengesetzt, deren Firmware der Maschinenbauer nie als Datei in der Hand hat. Hash und Dateiname sind damit durch die Art der Zusammensetzung nicht verfügbar. Lücken `hash-unavailable` und `filename-unavailable` an jeder Firmware, Schwere **Hinweis**. Gleiches gilt für das Anwenderprogramm, das als Download in die Steuerung und nicht als stabile Datei ausgeliefert wird (CLAUDE.md 5.6).

**Folgen.** Ein Release-Build scheitert nicht an fehlenden Firmware-Hashes. Das ist eine Auslegung, kein Wortlaut: Der Absatz der TR spricht von zusammengesetzten Komponenten, nicht ausdrücklich von Geräten Dritter. **Offen:** Wird diese Auslegung nicht getragen, werden beide Lücken zur Warnung; dann scheitert jeder Release-Build einer Maschine mit Hersteller-Firmware, bis Hersteller Hashes liefern. Liefert ein Hersteller die Firmware-Datei oder eine SBOM mit Hash, entfällt die Lücke für diese Komponente (M9).

## E-012: CODESYS-Platzhalter ohne Umleitung gelten als nicht aufgelöst (31.10.2026)

**Kontext.** Ein PLCopen-XML-Export aus CODESYS V3.5 führt Bibliotheken oft als Platzhalter (`Library Name="#SM3_Basic"`) mit einer Standardauflösung (`DefaultResolution="SM3_Basic, 4.5.1.0 (…)"`). In CODESYS löst zuerst eine Umleitung im Anwendungskontext auf, danach die Gerätebeschreibung, danach Library Support Package und Bibliotheksprofil (CODESYS-Hilfe „Placeholder“, `content.helpme-codesys.com/en/LibDevSummary/placeholder.html`, abgerufen 31.10.2026). Umleitungen stehen im Export (`PlaceholderRedirection`, Fixture `ooip-script-engine-example.xml`), die Auflösung durch die Gerätebeschreibung nicht.

**Entscheidung.** Feste Version nur für direkte Referenzen (`Titel, Version (Hersteller)`) und für Platzhalter mit Umleitung im selben Bibliotheksverwalter. Ein Platzhalter nur mit Standardauflösung bekommt keine Version; die Standardversion steht als `<ns>:version:requested`, dazu `version-unresolved` (Warnung) mit einer Meldung, die den Grund nennt. Gleiches gilt für `*`.

**Folgen.** Kein geratener Versionsstand in der SBOM (Leitplanke „kein erfundener Wert“). Ein typisches Projekt hat dadurch mehrere Warnungen, ein Release-Build mit `--release-build` scheitert daran. Abhilfe für Anwender: Platzhalter im Projekt umleiten (Bibliotheksverwalter → Platzhalter) oder direkt referenzieren; nach dem Prototyp das CODESYS-Scripting (`effective_resolution`). Alternative wäre gewesen, die Standardauflösung als Version mit Hinweis zu schreiben; verworfen, weil die Gerätebeschreibung gerade bei Systembibliotheken (`IoStandard`, `3SLicense`) eine andere Version festlegen kann.

## E-013: Hersteller „System“ bei CODESYS-Bibliotheken ist kein Hersteller (31.10.2026)

**Kontext.** CODESYS schreibt in Bibliotheksreferenzen für Bibliotheken der eigenen Installation `System` als Hersteller (`IoStandard, 3.5.15.0 (System)`, alle drei echten Fixtures). Das ist keine Organisation, und eine Zuordnung zu „CODESYS GmbH“ ist aus der Quelle nicht belegt.

**Entscheidung.** Bei `System` kein `supplier`/`manufacturer`, Lücke `supplier-unknown` (Warnung) mit eigener Meldung, Wert in `<ns>:library:company`. Andere Angaben (`3S - Smart Software Solutions GmbH`, `CAA Technical Workgroup`, `INESC TEC`) werden unverändert Hersteller mit `vendor:origin = source`.

**Folgen.** Eine Zuordnung `System` → Hersteller kann später der Anwender erklären (`vendorOverrides` oder ein eigener Schlüssel in `machine.yml`, M9), dann mit `origin = declared`.

## E-014: CODESYS-Geräte ohne Produktname, Gerätebeschreibungen später (31.10.2026)

**Kontext.** Der PLCopen-Export nennt je Gerät nur `DeviceIdentification` (Type, Id, Version der Gerätebeschreibung), keinen Produktnamen, keinen Hersteller, keine Bestellnummer. Dieselbe Kennung `4102/0000 0005` erscheint in Exporten verschiedener Geräte.

**Entscheidung.** Typname `Gerät <Type>/<Id>`, Kennung und Version der Gerätebeschreibung als `<ns>:vendorKey:DeviceType`, `DeviceId`, `DeviceDescriptionVersion`. Lücken `firmware-not-in-project`, `order-number-not-in-project`, `supplier-unknown`; `firmware:origin = unknown`. Der Writer schreibt `firmware:origin = unknown` jetzt auch ohne Firmware-Version (vorher immer `not-in-project`); Golden File `hand-model.cdx.json` bleibt unverändert. Die Quellvariante steht als `<ns>:adapter:source` (`v35`) an jeder Komponente der Station.

**Folgen.** Der Leser für Gerätebeschreibungen ist mit E-015 umgesetzt. Ursprünglicher Vorschlag: ein Leser für CODESYS-Gerätebeschreibungen (`*.devdesc.xml`) analog zum GSDML-Leser, mit Verzeichnissen in `machine.yml`. Er würde Hersteller, Produktname und Bestellnummer aus der Gerätebeschreibung holen (`vendor:origin = gsdml` entsprechend als eigener Wert). Fixture: `stefandreyer/I2C-Multiplex` (MIT). Bis dahin trägt jedes CODESYS-Gerät zwei Warnungen.

## E-015: Leser für CODESYS-Gerätebeschreibungen (31.10.2026)

**Kontext.** E-014: Der PLCopen-Export nennt zu Geräten nur die Kennung der Gerätebeschreibung. Die Gerätebeschreibung selbst (`*.devdesc.xml`) nennt Produktname, Hersteller und Bestellnummer. Vom Anwender freigegeben am 31.10.2026. Fixtures: vier Dateien aus `stefandreyer/I2C-Multiplex` (MIT). Befunde an allen 13 Dateien dieses Repositorys: Bestellnummer überall `-`; zwei Kennungen kommen doppelt vor (`500/FFFF 0007/1.0.1.1` bei `PCF8575IN` und `SSD1306`, `500/FFFF 0010/1.0.1.1` bei `PCA9685` und `4channelDimmer`); das Feld `Description` ist in einer Datei falsch.

**Entscheidung.**
- Neuer Schlüssel `deviceDescriptions` in `machine.yml` (Liste von Verzeichnissen, wie `gsdml`), ausgewertet ab M9. Gelesen werden alle `*.devdesc.xml` darunter, sortiert.
- Zuordnung exakt über Type, Id (ohne Groß-/Kleinschreibung, die Fixtures schreiben Hex teils klein) und Version. Andere Versionen derselben Kennung werden nicht genommen, weil Name, Hersteller oder Bestellnummer sich zwischen Versionen ändern können.
- Nur bei genau einem Treffer: Produktname, Hersteller (`vendor:origin = devdesc`, neuer Wert von `ValueOrigin`), Bestellnummer. Dateiname und SHA-256 der Gerätebeschreibung als `vendorKey`. `-` als Bestellnummer gilt als fehlend.
- Kein oder mehrere Treffer: neuer Lückencode `devdesc-not-found` (Warnung, erklärt kein TR-Feld), Gerät wie ohne Gerätebeschreibung.
- Die Version der Gerätebeschreibung bleibt die der Beschreibung, nie die Firmware.

**Folgen.** Ist `deviceDescriptions` nicht gesetzt, ändert sich nichts gegenüber E-014. Mit vollständigen Gerätebeschreibungen entfällt `supplier-unknown` am Gerät, `order-number-not-in-project` nur, wenn die Beschreibung eine Bestellnummer hat. Offen: Prüfung an Gerätebeschreibungen echter Industriegeräte (Comquent-Demo-Projekt).
