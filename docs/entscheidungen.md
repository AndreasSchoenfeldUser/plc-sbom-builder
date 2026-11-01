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

`CycloneDX.Core` und `YamlDotNet` kommen mit M1 bzw. M9 dazu, jeweils mit Eintrag hier. (Für M1 überholt durch E-008: statt `CycloneDX.Core` ein eigener Writer und NJsonSchema. YamlDotNet: E-021.) System.CommandLine 3.0 liegt als Release Candidate vor; bis zur stabilen Version bleibt es bei 2.0.x.

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

**Folgen.** Ein Release-Build scheitert nicht an fehlenden Firmware-Hashes. Das ist eine Auslegung, kein Wortlaut: Der Absatz der TR spricht von zusammengesetzten Komponenten, nicht ausdrücklich von Geräten Dritter. **Offen:** Wird diese Auslegung nicht getragen, werden beide Lücken zur Warnung; dann scheitert jeder Release-Build einer Maschine mit Hersteller-Firmware, bis Hersteller Hashes liefern. Liefert ein Hersteller die Firmware-Datei oder eine SBOM mit Hash, entfällt die Lücke für diese Komponente (M9). Umgesetzt mit E-021: Nennt die zugeordnete Hersteller-SBOM Dateiname und SHA-512, übernimmt die Firmware beides.

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
- **GSD-Geräte** in M7 ohne GSDML: Kopfmodul als Firmware ohne Hersteller, Bestellnummer, Version; Module passiv. M8 ergänzt die GSDML-Auswertung (E-020).
- **Deklarierte Bibliotheken** (`libraries`): Herkunft steht in `<ns>:origin = declared`, nicht in `<ns>:source` (das ist die Quelldatei der Station).
- **Warnungen ohne Lücke** (unbekannte WriterVersion, Rolle, `TypeIdentifier`-Präfix, abweichende Katalogversion, IO-System ohne CPU, unbenutztes Muster) gehen nicht in die SBOM, sondern als `AdapterWarning` mit stabilem Code an den Aufrufer; `inspect` schreibt sie ins JSON und auf stderr, `build` (M9) zählt sie in der Ergebnisdatei.
- **Platzhalter** in Mustern: `*` und `?`, ganze Zeichenkette, mit Groß-/Kleinschreibung; Bestellnummern zusätzlich ohne Leerzeichen verglichen. Ein `*` im Text (`1***0`) ist ein Zeichen wie jedes andere.

**Folgen.** Eine SBOM aus einem TIA-Export ohne `libraries` trägt an jeder CPU `libraries-not-read`; ein Release-Build scheitert daran, bis die Bibliotheken deklariert sind. Jede Stromversorgung ohne Firmware-Attribut ergibt eine Warnung, bis sie über `passiveOrderNumbers` erklärt ist. **Offen:** Golden File aus dem Comquent-V21-Demo-Export (M7 laut CLAUDE.md Abschnitt 12, nachzuholen wie M3/M4), HMI-Panels, Mehr-CPU-Geräte. Die V18- und V21-Dateien laufen nur lokal; ihre Prüfung fehlt in CI.

## E-020: GSDML-Leser und GSD-Geräte in tia-aml (01.11.2026)

**Kontext.** Für GSD-Geräte nennt das AML nur GSDML-Dateiname, Art und Id im `TypeIdentifier` (E-019). Echte Fixture: `GSDML-V2.41-LEUZE-BCL248i-20211213.xml` aus demselben Repository wie die V17-AML (`vformi/PLC.Commissioning.Lib`, MIT). Befunde an der Datei: Wurzel `ISO15745Profile` mit Namespace, Kodierung ISO-8859-1, `DeviceIdentity` mit `VendorID="0x0152"`, `DeviceID="0x0015"` und `VendorName`; ein `DeviceAccessPointItem` `DAP`, dessen `ModuleInfo` `OrderNumber="501xxxxx"`, `HardwareRelease="3"` und `SoftwareRelease="V1.*.*"` nennt; 57 `ModuleItem`, keines mit Bestellnummer. Das AML schreibt den Dateinamen in Großbuchstaben (`…BCL248I-20211213.XML`), die Datei heißt `…BCL248i-20211213.xml`.

**Entscheidung.**
- **Suche:** Dateiname ohne Groß-/Kleinschreibung, rekursiv in allen `gsdml`-Verzeichnissen; gelesen wird nur die gesuchte Datei. Gleicher Name mit gleichem Inhalt mehrfach: die erste nach Pfad. Gleicher Name mit verschiedenem Inhalt: keine wird genommen, Lücke `gsdml-not-found` mit allen Fundstellen (wie E-015, welche Datei TIA verwendet hat, ist offen). Fehlendes Verzeichnis: Exit 2 `[GSDML_DIR_NOT_FOUND]`; eine Datei mit dem gesuchten Namen, die keine GSDML ist: Exit 2 `[NOT_GSDML]`.
- **Ohne `gsdml`-Verzeichnis** trägt das Kopfmodul ebenfalls `gsdml-not-found`; die Meldung sagt, dass kein Verzeichnis angegeben ist. So zeigt die Lücke, wie sie sich schließen lässt. Die M7-Golden-Files bekommen dadurch je GSD-Gerät diese eine Lücke dazu.
- **Hersteller** aus `DeviceIdentity/VendorName@Value` mit `vendor:origin = gsdml`. `vendorOverrides` gilt davor, wenn die Bestellnummer aus der GSDML auf ein Muster passt (`declared`). Ohne GSDML gibt es keine Bestellnummer, auf die ein Muster passen könnte.
- **Bestellnummer** unverändert aus `ModuleInfo/OrderNumber@Value` des Zugangspunkts bzw. Moduls mit der Id aus dem `TypeIdentifier`. `orderNumber:pattern = true`, wenn sie `*`, `?` oder mindestens zwei kleine `x` hintereinander enthält (`501xxxxx`: eine GSDML für mehrere Artikel). Ein einzelnes großes `X` gehört zu echten Bestellnummern (`6ES7 214-1AG40-0XB0`) und zählt nicht. Die Regel gilt nur für Bestellnummern aus GSDML-Dateien.
- **Releases:** `SoftwareRelease` → `<ns>:gsdml:softwareRelease`, `HardwareRelease` → `<ns>:gsdml:hardwareRelease`. Beide beschreiben den Stand, für den die GSDML geschrieben ist, nicht das Gerät; die Firmware bleibt `firmware-not-in-project`.
- **Herstellerschlüssel:** `VendorId`, `DeviceId` wie geschrieben (`0x0152`), `gsdmlFile` (Pfad der gefundenen Datei), `gsdmlSha256`; `GSDML`, `GsdType`, `GsdId` bleiben die Werte aus dem AML.
- **Id fehlt in der GSDML:** Warnung `[TIA_AML_GSDML_ITEM_NOT_FOUND]` (die Datei passt vermutlich nicht zum Projekt). Der Hersteller aus der Datei bleibt, die Bestellnummer fehlt (`order-number-not-in-project`).
- **Module** bleiben passiv (Hardware-Anhang), bekommen aber Hersteller und, falls vorhanden, Bestellnummer aus der GSDML; sie tragen keine Lücken.
- **`inspect --gsdml <verzeichnis>`** zeigt je GSD-Teil die Angaben aus der GSDML (`gsdml`: Datei, Hersteller, Bestellnummer, Releases). Fehlt die Datei oder ist sie mehrdeutig, steht das als Warnung `[GSDML_NOT_FOUND]` bzw. `[GSDML_AMBIGUOUS]` im JSON und auf stderr; in einer Station ist es die Lücke `gsdml-not-found`.
- **Fixture:** Die GSDML-Datei hat Leuze geschrieben; sie trägt keinen eigenen Lizenzhinweis und liegt im MIT-lizenzierten Repository neben der AML-Datei. Eingecheckt wie die AML-Datei aus demselben Repository. Das Comquent-Demo-Projekt (CLAUDE.md Abschnitt 13) bringt eine weitergabefähige GSDML mit und kann sie ersetzen.

**Folgen.** Ein GSD-Gerät mit gefundener GSDML hat Hersteller und Bestellnummer; offen bleiben `firmware-not-in-project` und, weil die GSDML keinen Kontakt nennt, `creator-contact-unknown`. Die Bestellnummer `501xxxxx` trägt für einen CSAF-Abgleich nicht; das macht `orderNumber:pattern` sichtbar. **Offen:** Module mit eigener Bestellnummer sind nur über eine abgewandelte Kopie der echten Datei im Test belegt, GSDML-Dateien mit mehreren Zugangspunkten gar nicht. `VendorName` im `ModuleInfo` des Zugangspunkts wird nicht gelesen (in der Fixture gleich dem aus `DeviceIdentity`).

## E-021: `build` mit `machine.yml`, Programm-Hash und Zusammenführen (M9, 01.11.2026)

**Kontext.** M9 setzt `plc-sbom build` um (CLAUDE.md Abschnitte 5.6, 5.7, 6, 7.1 bis 7.4, 8.4). CLAUDE.md beschreibt Ziel und Schlüssel, lässt aber offen, wie Kontakte der Hersteller, die Quellcode-URI, die Dateieigenschaften des Anwenderprogramms und die Zuordnung von Hersteller-SBOMs zu Firmware aussehen. Eine echte Hersteller-SBOM für ein Steuerungsgerät gibt es nicht (CLAUDE.md 1.3). TR-03183-2 v2.1.0 nachgelesen am 01.11.2026: 3.2.5 (bei Abweichungen gilt die referenzierte BOM), 3.2.8 (die effektive Lizenz „has to be set anew when merging SBOMs“), 5.1 (Verweise nur auf konforme SBOMs, mehrere Instanzen mit verschiedenen Metadaten getrennt).

**Entscheidung.**

- **YAML.** YamlDotNet 18.1.0 (MIT, auf nuget.org veröffentlicht am 26.06.2026, gleiche Version wie in plc-git-bridge) über das `RepresentationModel`, nicht über Deserialisierung in Klassen. So sind unbekannte Schlüssel, doppelte Schlüssel und falsche Typen Fehler mit Zeilennummer (`CONFIG_INVALID`), Werte bleiben Text (`1.10` wird nicht zu `1.1`), und die Reihenfolge von `vendorOverrides` bleibt erhalten. Schlüssel einer Station, die für ihren Adapter nicht gelten (`plc` an `twincat`), sind Fehler. `source` ist für `codesys` Pflicht.

  | Paket | Version | Lizenz | Zweck | Ausgeliefert |
  |---|---|---|---|---|
  | YamlDotNet | 18.1.0 | MIT | `machine.yml`, CPE-Tabelle | ja |

- **Neue Schlüssel** gegenüber CLAUDE.md 7.4: `vendorContacts` (Hersteller → E-Mail oder URL) für den Ersteller nach TR 5.2.2, weil `machine.yml` sonst keinen Ort für Herstellerkontakte hatte (E-017 erwartete ihn). `sourceUrl` mit `{commit}` und `{path}` für die Quellcode-URI (TR 5.2.4); ohne Vorlage entsteht keine URI, statt eine aus dem Git-Remote zu raten. `vendorSboms` nimmt ein Muster oder eine Liste.
- **Pfade in der SBOM** sind relativ zum Git-Repository über `machine.yml` (Verzeichnis mit `.git`, auch als Datei bei Worktrees), sonst relativ zu `machine.yml`, immer mit `/`.
- **Commit und Zeitstempel** wie CLAUDE.md 7.1 und 8.4. Ohne Commit: Warnung `COMMIT_UNKNOWN`, Programmversion nur das Release. Zeitstempel aus der Uhr: Warnung `TIMESTAMP_FROM_CLOCK`. Beides sind Warnungen, ein Release-Build scheitert daran. Ungültige Werte (`COMMIT_INVALID`, `SOURCE_DATE_EPOCH_INVALID`) sind Exit 2.
- **Anwenderprogramm** an jeder Station: Hersteller ist der Maschinenbauer (`declared`), Lizenz aus `licenses` für ihn. Dateieigenschaften fest `executable`, `no archive`, `unstructured`, weil das Programm als übersetzter Code in die Steuerung geladen wird; Begründung wie bei der Firmware (TR 8.1.6, 8.1.7). Die Alternative `file-properties-unknown` hätte jeden Release-Build scheitern lassen, ohne dass eine Quelle die Angabe je liefern könnte.
- **Programm-Hash** über die Bytes, wie sie ausgecheckt sind; keine Normalisierung von Zeilenenden, weil das Werkzeug dafür Text- und Binärdateien unterscheiden müsste. Empfehlung `.gitattributes` mit festen Zeilenenden; `samples/**` ist `-text`. Mit `sourceUrl` steht der Hash unter `externalReferences[type=source-distribution]`, sonst als Property `<ns>:sourceCode:sha512` (CycloneDX verlangt an einer externen Referenz eine URL). Dazu `<ns>:sourceCode:path` und `:files`. Der Writer schreibt den Hash damit auch ohne URI; nur der Hash der ausgelieferten Form ohne URI bleibt ein Modellfehler.
- **Erklärungen aus `machine.yml` im Kern, für alle Adapter gleich** (H2): `vendorOverrides` und `passiveOrderNumbers` treffen die Bestellnummer, bei Geräten ohne Bestellnummer den Typnamen (EtherCAT-Kurzbezeichnung). Als passiv erklärt werden nur Geräte ohne Firmware-Version, nie die Steuerung. Ein Override entfernt `supplier-unknown`. Der Adapter `tia-aml` wendet beide Listen weiter selbst an (E-019, E-020); die zweite Anwendung im Kern ändert dann nichts. Kontakte und Lizenzen werden über den Herstellernamen genau zugeordnet. Eine effektive Lizenz setzt das Werkzeug nie selbst.
- **CPE** nur aus `cpeMap`, Zeile mit Hersteller, Bestellnummer-Muster und CPE mit `{version}` (Pflicht, sonst hätte jede Firmware-Version dieselbe CPE). Ohne Firmware-Version keine CPE. Sonderzeichen der Version werden nach CPE 2.3 mit `\` maskiert.
- **Hersteller-SBOM gehört zur Firmware**, wenn CPE gleich ist, oder Hersteller, Bestellnummer (ohne Leerzeichen) und Version (ohne führendes `V`). Die Bestellnummer liest das Werkzeug aus einer Property `<namespace>:device:orderNumber` an `metadata.component`, so wie es sie selbst schreibt. Die Wurzel der Hersteller-SBOM wird nicht als eigene Komponente geschrieben: Sie beschreibt dieselbe Firmware, zwei Komponenten mit gleichem Namen, gleicher Version und ggf. gleicher CPE hätten Dependency-Track doppelte Treffer gegeben und den sbomqs-Abgleich mehrdeutig gemacht (erster Entwurf, verworfen). Stattdessen hängen ihre Bestandteile unter der Firmware, die Firmware übernimmt die Vollständigkeitsangabe und, wenn genannt, Dateiname und SHA-512 (TR 3.2.5). `vendor-sbom-missing` entfällt. Passt keine Firmware: Warnung `VENDOR_SBOM_UNUSED`, nicht übernommen; ohne CPE und Bestellnummer: `VENDOR_SBOM_NO_KEY`.
- **Teil-SBOM**: Ihre Wurzel wird Komponente unter der Maschine, mit `station` trägt sie `<ns>:station`. Die Wurzel hängt von allen Komponenten ab, auf die in der Datei nichts zeigt (Syft schreibt für die Wurzel keine Abhängigkeiten).
- **Übernahme eingelesener SBOMs** (TR 5.1, 3.2.8): Präfix `vendor-<datei>/` bzw. `partial-<datei>/` vor jeder `bom-ref`; effektive Lizenz entfernt; jede Datei für sich geprüft (Warnung `…_NOT_TR_CONFORM`); fehlende Pflichtfelder einer Komponente ergeben den **neuen Lückencode `input-field-missing`** (Warnung, erklärt alle Felder aus TR 5.2.2). Ungültige Werte bleiben Fehler. Unlesbare Dateien, Dateien ohne `metadata.component` oder mit doppelter `bom-ref`: Fehler `INPUT_SBOM_INVALID` (Exit 1, CLAUDE.md 4.4: „Eingabe-SBOM unlesbar“), nicht übernommen. Eine fehlende Teil-SBOM ist dagegen Exit 2 (`PARTIAL_SBOM_NOT_FOUND`), weil ein Schritt der Pipeline fehlt.
- **Warnungen des Laufs** (Adapter-Warnungen, eingelesene SBOMs, Commit, Zeitstempel) stehen mit den Befunden des Validators in Konsole und Ergebnisdatei und zählen als Warnungen.
- **Ergebnisdatei auch bei Exit 2, 3, 4**, mit dem Code der Ursache als einzigem Befund, damit `readJSON` in der Pipeline immer eine Datei findet.
- **`--report`** bleibt bis M10 ohne Wirkung; die Konsole sagt das in einer Zeile `[bericht]`.
- **Beispielmaschine** `samples/maschine-xy` mit den Fixtures für `codesys`, `tia-aml` (mit GSDML) und `twincat`, eigenen Programmquellen, einer Teil-SBOM und einer synthetischen Hersteller-SBOM. Die Teil-SBOM hat Comquent mit Syft 1.54.1 (Prüfsumme gegen das Release geprüft) aus einer eigenen kleinen Web-Oberfläche erzeugt (`package.json`, npm-Lockfile mit `ms` 2.1.3 und `picocolors` 1.1.1), mit `SYFT_FILE_METADATA_SELECTION=none`, weil Syft sonst den absoluten Pfad des Lockfiles in die Datei schreibt. Die Hersteller-SBOM ist synthetisch (`*-synthetisch.cdx.json`), weil kein Hersteller eine veröffentlicht; ihr Gegenstück für die Struktur ist die echte Syft-Datei, für die Bestellnummer-Property die Ausgabe von `plc-sbom` selbst.

**Folgen.** Die Regel `build/` in `.gitignore` steht jetzt als `/build/`: Git auf macOS vergleicht ohne Groß-/Kleinschreibung (`core.ignorecase`), sonst wären die neuen Quellordner `src/PlcSbom.Core/Build/` und `tests/PlcSbom.Tests/Build/` nie eingecheckt worden. H3 ist für M9 belegt: Golden File `maschine-xy.cdx.json` auf ubuntu und windows, zwei Läufe byte-gleich (Test und CI-Schritt mit dem ausgelieferten Paket). sbomqs-Abgleich ohne Abweichung. Die Beispielmaschine hat im Entwicklungs-Build 117 Warnungen; ein Release-Build scheitert, wie gewollt, bis die Lücken geschlossen oder erklärt sind. **Offen:** Wie echte Hersteller-SBOMs die Bestellnummer nennen (Comquent-Demo, erste echte Lieferanten-SBOM); `vendorOverrides` für Bibliotheken mit Hersteller `System` (E-013); ob eine Übersicht der Warnungen je Code statt einer Zeile je Warnung in der Konsole besser lesbar ist (eine Maschine mit 100 Klemmen erzeugt Hunderte Zeilen).
