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

## E-016: M3 und M4 zurückgestellt, weiter mit M5 (31.10.2026)

**Kontext.** M3 (CODESYS V2.3) und M4 (CODESYS 4) beginnen je mit einem Spike an einem Comquent-Demo-Projekt (CLAUDE.md Abschnitte 5.1.2, 5.1.3 und 13). Beide Demo-Projekte liegen noch nicht vor. Öffentliche Beispielprojekte mit Lizenz gibt es für V2.3 nicht, für CODESYS 4 (seit 24.09.2026) ebenfalls nicht.

**Entscheidung.** Wie in CLAUDE.md Abschnitt 12 vorgesehen: Die Arbeit geht mit M5 (`studio5000`) weiter, M3 und M4 werden nachgeholt, sobald die Demo-Projekte da sind.

**Folgen.** Die Quellen `v23` und `c4` des Adapters `codesys` bleiben bis dahin nicht umgesetzt. Die Reihenfolge der übrigen Adapter bleibt.

## E-017: Regeln des Adapters studio5000 (31.10.2026)

**Kontext.** Der L5X-Export nennt je Modul Bestellnummer (`CatalogNumber`), CIP-Herstellerkennung (`Vendor`), Produktkennung und die projektierte Revision (`Major`, `Minor`), aber keinen Herstellernamen und keinen Produktnamen. Echte Fixtures: drei Dateien aus `RockwellAutomation/ra-logix-cicd` (MIT), zwei aus `hutcheb/acd` (Apache-2.0). Alle Module darin haben `Vendor="1"`; das Controller-Modul hat überall `EKey State="Disabled"`. Ports von Ethernet-Modulen tragen IP-Adressen, der Controller einen `CommPath` mit IP-Adresse.

**Entscheidung.**
- Herstellerkennung `1` → `Rockwell Automation`, `vendor:origin = assumed`. Beleg: CLAUDE.md 5.2 („`Vendor="1"` = Rockwell“) und `ExtendedProperties` des 1756-EN2T in acd (`Rockwell Automation/Allen-Bradley`). Der Name wird nicht aus `ExtendedProperties` gelesen, weil er dort nur an manchen Modulen steht und dieselbe Kennung sonst zwei Schreibweisen bekäme. Andere Kennungen: kein Hersteller, `supplier-unknown`, keine eingebettete Kennungstabelle.
- Firmware-Soll: Steuerung `Controller@MajorRev.MinorRev`, Module `Major.Minor`, unverändert ohne Auffüllen der Nebenversion. `firmware:origin = projected`.
- Kodierung als neue Property `<ns>:device:ekey`. Dafür hat `Device` im Modell ein allgemeines Feld `SourceProperties` wie `SoftwareItem` (kein Sonderfall im Kern, H2); bestehende Golden Files ändern sich nicht. `ekey-disabled` (Warnung) nur an Modulen außer der Steuerung: Die Hauptversion der Steuerung bindet ohnehin das Projekt (Rockwell 1756-UM900, Wortlaut nicht abrufbar, als Annahme in `docs/adapter-studio5000.md` vermerkt), und an der Steuerung steht `Disabled` in jedem echten Export.
- Position aus Parent und Steckplatz der Backplane (`Local/1`), hinter Netzwerkports aus Parent und Modulname. IP-Adressen liest der Leser gar nicht erst.
- Nur Controller-Exporte ergeben eine Station; ein Export einzelner Bestandteile endet mit Exit 2 (`STUDIO5000_NOT_CONTROLLER`), der Wurzeltest mit `NOT_L5X`.
- AOIs als Komponenten vom Typ `library` (`SoftwareKind.AddOnInstruction`), Version = `Revision`, `RevisionExtension` als `<ns>:aoi:revisionExtension`.
- Werkzeugname `Studio 5000 Logix Designer` mit der `SoftwareRevision` des Exports. Der Export nennt den Produktnamen nicht; der Name folgt aus der Eingangsdefinition des Adapters (CLAUDE.md 1.1).

**Folgen.** Jede Rockwell-Firmware trägt `creator-contact-unknown`, bis `machine.yml` einen Kontakt für den Hersteller nennt (M9). Ein Modul mit ausgeschalteter Kodierung ergibt eine Warnung, ein Release-Build mit `--release-build` scheitert daran; Abhilfe ist die Kodierung im Projekt einzuschalten. Fremdgeräte sind nur synthetisch belegt.

## E-018: Regeln des Adapters twincat (01.11.2026)

**Kontext.** Ein TwinCAT-3-Projekt nennt je EtherCAT-Teilnehmer `VendorId`, `ProductCode`, `RevisionNo`, eine Kurzbezeichnung (`Desc`) und eine Beschreibung (`Type`), aber weder Herstellernamen noch Bestellnummer noch Firmware. Echte Fixtures: AixOCAT `HeatPumpTestBenchControl` (MIT), Beckhoff USA `SPT EtherCAT Diagnostics` (0BSD), TcOpen `TcoCognexVision` und `TcoCore` (MIT). Von 1.339 EtherCAT-Elementen mit `VendorId` in den durchsuchten Repositories tragen 1.290 `#x00000002`, die übrigen `#x00000024`, `#x0000001d` und `#x01000056`; die EK1200 hat `Desc="EK1200"`, ihre `Type` beginnt mit `EK1200-5000`. Viele E/A-Master sind deaktiviert (`Device@Disabled="true"`). Daneben stehen PROFINET-Geräte (`DevType` 113 und 119) ohne EtherCAT-Kennung.

**Entscheidung.**
- **Bestellnummer:** keine. `Desc` ist die Kurzbezeichnung und nicht verlässlich die Bestellnummer (EK1200 ↔ EK1200-5000), `Type` ist Beschreibungstext. Jeder Teilnehmer bekommt `order-number-not-in-project` (Warnung); bestimmt ist er über `VendorId`, `ProductCode`, `RevisionNo` als `vendorKey`, Hex als `0x…` in Kleinschreibung. Typname ist `Desc`.
- **Hersteller:** `VendorId 0x00000002` → `Beckhoff Automation GmbH & Co. KG`, `vendor:origin = assumed` (EtherCAT-Herstellerkennung von Beckhoff; alle Beckhoff-Klemmen der Fixtures tragen sie). Andere Kennungen: `supplier-unknown`.
- **Firmware:** keine, `firmware-not-in-project` (Hinweis), Meldung nennt die `RevisionNo` als ESI-Revision und CoE 0x100A als Quelle des Ist-Stands.
- **Deaktivierte Geräte:** bleiben in der SBOM, Property `<ns>:device:disabled = true`. Weglassen hätte projektierte Hardware still entfernt.
- **Nicht gelesene Geräte:** PROFINET-Boxen und E/A-Geräte in eigener Datei (`Device@File`, ohne echte Fixture) ergeben den neuen Lückencode `devices-not-read` (Warnung, kein TR-Feld) an der Station, mit Namen. E/A-Geräte ohne Boxen (z. B. BACnet-Treiber) sind keine Feldgeräte und werden übergangen.
- **Laufzeit:** Komponente `TwinCAT 3 Runtime` mit Hersteller Beckhoff, beides `assumed`, ohne Version (`runtime-version-unknown`). `TcVersion` ist die XAE-Version und geht nur in `metadata.tools`.
- **Bibliotheken:** wie E-012 und E-013, da TwinCAT die Bibliotheksverwaltung von CODESYS nutzt. Feste Version nur bei `PlaceholderResolution` oder `LibraryReference` mit Version; `*`, `newest` und eine Standardauflösung allein ergeben `version-unresolved`. Eine `PlaceholderResolution` ohne eigenen Platzhalter kommt als Bibliothek mit `library:resolution = indirect` in die SBOM. `System` ist in jeder Schreibweise kein Hersteller.
- **Writer:** Stationslücken einer Station ohne Anwenderprogramm und ohne Steuerungs-Firmware hängen an der Maschine. Vorher gingen sie verloren, sobald die Station überhaupt Komponenten hatte; das trat erst mit TwinCAT auf, weil dort keine Steuerung im Projekt steht. Bestehende Golden Files ändern sich dadurch nicht.

**Folgen.** Ein TwinCAT-Projekt mit vielen Klemmen erzeugt viele Warnungen `order-number-not-in-project`; ein Release-Build scheitert daran, bis eine Quelle die Bestellnummer liefert. **Offen:** Soll die Kurzbezeichnung bei Beckhoff-Kennung als Bestellnummer gelten (mit Herkunft `assumed`), um die Warnungen zu vermeiden? Dagegen spricht die EK1200. Ebenfalls offen: passive Klemmen (EL9011) erkennen, ausgelagerte E/A-Geräte lesen, PROFINET über GSDML (M8).

## E-019: Regeln des Adapters tia-aml (01.11.2026)

**Kontext.** Der CAx-Export von TIA Portal nennt je Gerät `TypeIdentifier`, `TypeName`, `FirmwareVersion`, `DeviceItemType` und die Lage im Baum, dazu IO-Systeme und „Link To IoSystem“, aber keinen Herstellernamen, keine Bibliotheken und keine Bausteine. Echte Fixtures: V17 aus `vformi/PLC.Commissioning.Lib` (MIT, eingecheckt), V18 aus `frattam-eng/openess_CIP` und V21 aus `JBuschkind/Interoperabilit-tsprojekt` (beide ohne Lizenz, nur `fixtures/_local`). Das Comquent-V21-Demo-Projekt liegt noch nicht vor. Die V21-Fixture enthält eine Stromversorgung PM 70W ohne Attribut `FirmwareVersion`; CLAUDE.md 4.4 verlangt dafür eine Warnung, `firmware-not-in-project` ist aber ein Hinweis, und der Validator liest die Schwere nur aus dem Code.

**Entscheidung.**
- **Neuer Lückencode `firmware-attribute-missing`** (Warnung, erklärt kein TR-Feld): Die Quelle führt für das Gerät kein Firmware-Attribut, ob es eine eigene Firmware hat, ist offen. Er steht neben `firmware-not-in-project` (Hinweis, erklärt die Version). Die Warnung verschwindet, wenn der Anwender das Gerät über `passiveOrderNumbers` als passiv erklärt. Eine zweite Schwere für denselben Code hätte den Validator von der Quelle abhängig gemacht (H2).
- **Hersteller** von `OrderNumber:`-Geräten: `Siemens AG`, `vendor:origin = assumed` (CLAUDE.md 5.4.3); alle `OrderNumber:`-Geräte der Fixtures sind Siemens-Geräte. `vendorOverrides` gilt vor der Annahme, erster Treffer in der Reihenfolge aus `machine.yml`.
- **Rollen** nur aus `SupportedRoleClass`; `RoleRequirements` kommt in keiner Fixture vor und wird nicht gelesen (Elemente ohne bekannte Rolle werden durchlaufen). Netzknoten, Subnetze und Variablentabellen werden samt Inhalt übersprungen, Netzattribute nie gelesen.
- **Stationen** nach CLAUDE.md 5.4.4. Die Link-Seite ist an der V18-Fixture belegt (A = Schnittstelle des Geräts, B = IO-System); der Leser prüft trotzdem beide Seiten. Zur Station gehört immer das ganze `Device`, an dessen Schnittstelle der Link hängt. Ohne `plc` ist die Datei eine Station nur, wenn genau eine Station sie liest und sie höchstens eine CPU enthält. `ignoreDevices` wirkt auf die Geräte, die keine Station beansprucht (bei einer Station ohne `plc` auf die ganze Datei); Muster aller Stationen derselben Datei gelten zusammen. Nicht zugeordnete Geräte tragen `<ns>:adapter`, `<ns>:source` und `<ns>:source:sha256` selbst, weil der Writer für sie keine Station hat.
- **Position** = `<Gerätename>/<PositionNumber-Pfad>` der nicht integrierten Teile, damit sie über alle Geräte einer Station eindeutig ist (`ET 200SP station_1/0/0`).
- **GSD-Geräte** in M7 ohne GSDML: Kopfmodul als Firmware ohne Hersteller, Bestellnummer, Version; Module passiv. M8 ergänzt die GSDML-Auswertung.
- **Deklarierte Bibliotheken** (`libraries`): Herkunft steht in `<ns>:origin = declared`, nicht in `<ns>:source` (das ist die Quelldatei der Station).
- **Warnungen ohne Lücke** (unbekannte WriterVersion, Rolle, `TypeIdentifier`-Präfix, abweichende Katalogversion, IO-System ohne CPU, unbenutztes Muster) gehen nicht in die SBOM, sondern als `AdapterWarning` mit stabilem Code an den Aufrufer; `inspect` schreibt sie ins JSON und auf stderr, `build` (M9) zählt sie in der Ergebnisdatei.
- **Platzhalter** in Mustern: `*` und `?`, ganze Zeichenkette, mit Groß-/Kleinschreibung; Bestellnummern zusätzlich ohne Leerzeichen verglichen. Ein `*` im Text (`1***0`) ist ein Zeichen wie jedes andere.

**Folgen.** Eine SBOM aus einem TIA-Export ohne `libraries` trägt an jeder CPU `libraries-not-read`; ein Release-Build scheitert daran, bis die Bibliotheken deklariert sind. Jede Stromversorgung ohne Firmware-Attribut ergibt eine Warnung, bis sie über `passiveOrderNumbers` erklärt ist. **Offen:** Golden File aus dem Comquent-V21-Demo-Export (M7 laut CLAUDE.md Abschnitt 12, nachzuholen wie M3/M4), HMI-Panels, Mehr-CPU-Geräte. Die V18- und V21-Dateien laufen nur lokal; ihre Prüfung fehlt in CI.
