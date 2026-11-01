# Adapter `tia-aml`

Stand 01.11.2026. Liest die AutomationML-Datei aus dem CAx-Export von Siemens TIA Portal V17 bis V21 (Extras → CAx-Daten exportieren). Kein TIA, keine Lizenz, läuft auf jedem Agenten (CLAUDE.md Abschnitt 5.4). Entscheidungen: `docs/entscheidungen.md` E-019. GSD-Geräte mit GSDML-Dateien folgen mit M8.

## 1. Eingang

Angegeben wird die `.aml`-Datei. Eine Datei kann ein ganzes Projekt mit mehreren CPUs enthalten; mehrere Stationen dürfen auf dieselbe Datei zeigen, sie wird einmal gelesen (`TiaAmlAdapter.Read` nimmt alle Stationen der Datei auf einmal).

Alle XML-Dateien werden ohne DTD und ohne Resolver gelesen, über lokale Element- und Attributnamen; ein XML-Namespace (CAEX 3.0) stört nicht.

### 1.1 Fixtures

| Datei | TIA | Inhalt | Lizenz |
|---|---|---|---|
| `fixtures/tia-aml/plc-commissioning-sample_device.aml` | V17 (`WriterVersion 1700`) | S7-1200 CPU 1214C `V4.5` mit IO-System, GSD-Gerät Leuze BCL248i mit zwei Modulen, nicht mit dem IO-System verbunden | MIT |
| `fixtures/tia-aml/zwei-cpus-synthetisch.aml` | V21 | synthetisch: zwei CPUs mit je einem IO-System, zwei ET 200SP, GSD-Gerät und G120C am IO-System, ein G120C und ein Testaufbau ohne IO-System, PM ohne `FirmwareVersion`, `TypeIdentifier` mit `/V2.1`, Name `&#x9;_0`, Netzwerte aus RFC 5737 | Comquent-eigen |
| `fixtures/_local/tia-aml/openess_CIP-CIP_cs_HardwareNetwork.aml` | V18 | S7-1500 CPU 1511-1 PN, vier ET 200SP mit passiven und firmwarebehafteten Modulen, drei G120C (`4.7.10`), sieben „Link To IoSystem“, Profilschiene `6ES7 590-1***0-0AA0` | keine, nur lokal |
| `fixtures/_local/tia-aml/interoperabilitaet-TIA_Export.aml` | V21 | S7-1500 CPU 1511-1 PN `V2.8`, PM 70W ohne `FirmwareVersion`, Profilschiene mit `***`, `WriterVendor` „Siemens“ | keine, nur lokal |
| `fixtures/_local/tia-aml/interoperabilitaet-TST_Export.aml` | – | Export aus dem TIA Selection Tool (`WriterName` „TIA Selection Tool“) | keine, nur lokal |
| `fixtures/_local/tia-aml/interoperabilitaet-beispiel_export.aml` | – | AML aus einem Beckhoff-Werkzeug (`WriterVendor` Beckhoff) | keine, nur lokal |

Herkunft, Commit und SHA-256 in `fixtures/FIXTURES.md`. Die Tests der lokalen Dateien überspringen sich, wenn die Datei fehlt (in CI immer).

Befunde an den echten Dateien: `CAEXFile@SchemaVersion="2.15"` ohne Namespace, `AutomationMLVersion="2.0"`, Dokument `AR APC` in den Versionen 1.2.0 (V17), 1.3.0 (V18) und 1.4.0 (V21). Rollen stehen in allen Dateien als `SupportedRoleClass`; ein Netzknoten trägt zwei (`Node` und `NodeEthernet`). `RoleRequirements` kommt in keiner Datei vor.

## 2. Versionsprüfung (`WriterHeader`)

| Element | Beispiel | Regel |
|---|---|---|
| `WriterName` | `Totally Integrated Automation Portal` | sonst Exit 2 `[NOT_TIA_AML]` (TIA Selection Tool, Beckhoff-Export) |
| `WriterVersion` | `1700`, `1800`, `2100` | `1700` … `2100` → `V17` … `V21` in `metadata.tools`; anderer Wert: Warnung `[TIA_AML_WRITER_VERSION_UNKNOWN]`, Version wie geschrieben, Verarbeitung läuft weiter |
| `WriterRelease` | `1700.0.4302.1`, `2100.0.121.1` | als `<ns>:tool:release` an „TIA Portal“ in `metadata.tools` |
| `WriterVendor` | `Siemens AG` (V17, V18), `Siemens` (V21) | Hersteller des Werkzeugs in `metadata.tools`, wie geschrieben |
| `LastWritingDateTime` | `2026-02-03T18:18:28.2456799Z` | `<ns>:source:exportedAt` (UTC, Sekunden); ohne Zeitzone als UTC gelesen; unlesbar: Warnung `[TIA_AML_EXPORT_TIME_INVALID]` |

Ein anderes Wurzelelement als `CAEXFile` ergibt Exit 2 `[NOT_AML]`.

## 3. Elemente

Der Leser durchläuft alle `InternalElement` rekursiv und erkennt sie am letzten Teil von `SupportedRoleClass@RefRoleClassPath`:

| Rolle | Verwendung |
|---|---|
| `AutomationProject` | Projektname (für `inspect`) |
| `DeviceUserFolder` | durchlaufen, Ordnername in `inspect` |
| `Device` | ein Gerät: Station, ET 200SP, Antrieb, GSD-Gerät |
| `DeviceItem`, `CommunicationInterface`, `CommunicationPort` | Teile eines Geräts; `BuiltIn` = `true` → kein eigener Eintrag, aber Kinder werden weiter durchlaufen |
| `IoSystem` | PROFINET-IO-System; gehört der CPU, unter der es hängt |
| `Node`, `NodeEthernet`, `Subnet`, `TagTable`, `TagUserFolder` | übersprungen samt Inhalt (Netzdaten, Variablentabellen) |
| unbekannte oder keine Rolle | durchlaufen; unbekannte Rollen ergeben eine Warnung `[TIA_AML_ROLE_UNKNOWN]` |

`InternalLink` werden überall gesammelt. Ein „Link To IoSystem“ hat in der V18-Fixture auf Seite A die Schnittstelle des Geräts (`<ID>:LogicalEndPoint_Interface`) und auf Seite B das IO-System (`<ID>:LogicalEndPoint_IoSystem`). Der Leser prüft beide Seiten: Die Seite, deren ID ein IO-System ist, gilt als IO-System, die andere als Element eines Geräts. In der V18-Fixture liegt die Schnittstelle der ET 200SP unter dem passiven Busadapter `BA 2xRJ45`, die der G120C direkt unter dem Kopfmodul; gezählt wird immer das ganze Gerät.

Element-Namen werden ohne Steuerzeichen übernommen (`&#x9;_0` → `_0`).

## 4. Attribute

Attribute stehen als `Attribute@Name` mit Kind-Element `Value`.

| Attribut | Beispiele aus Fixtures | Regel |
|---|---|---|
| Element-`Name` | `PLC_1`, `IO_Device_Main`, `Pump_Water` | `<ns>:device:name` |
| `TypeIdentifier` | `OrderNumber:6ES7 511-1AK02-0AB0`, `System:Rack.S71500`, `GSD:GSDML-V2.41-LEUZE-BCL248I-20211213.XML/DAP/DAP` | Präfix entscheidet, Abschnitt 5 |
| `TypeName` | `CPU 1511-1 PN`, `IM 155-6 PN HF`, `G120C PN`, `Rack` | Komponentenname `<TypeName>, Firmware`, `<ns>:device:type`; `Rack` → Rack |
| `FirmwareVersion` | `V2.8`, `V3.3`, `V0.0`, `4.7.10` | unverändert; nur zum Vergleich ohne führendes `V` |
| `DeviceItemType` | `CPU`, `HeadModule` | `<ns>:device:classification`; `CPU` markiert Stationswurzeln |
| `PositionNumber` | `0`, `1`, `127`, `32768` | Pfad der nicht integrierten Teile; Position `<Gerätename>/<Pfad>`, etwa `S7-1200 station_1/0/1` |
| `BuiltIn` | `true`, `false` | `true` (ohne Groß-/Kleinschreibung) → kein eigener Eintrag |
| `NetworkAddress`, `SubnetMask`, `IpProtocolSelection` | `192.168.0.1`, `255.255.255.0`, `Project` | **nie gelesen** (Leitplanke Netzdaten) |
| `InstallationDate`, `Label`, `IoType`, `StartAddress`, `Length`, `Address`, `Comment`, `Type`, `Number` | | nicht gelesen |

Fehlt `PositionNumber`, steht der Name im Pfad.

## 5. Regeln

### 5.1 `TypeIdentifier`

| Form | Behandlung |
|---|---|
| `OrderNumber:<MLFB>` | Siemens-Katalogeintrag. Bestellnummer bis zum ersten `/`, mit Leerzeichen unverändert. Enthält sie `*` (Profilschiene `6ES7 590-1***0-0AA0`), `<ns>:device:orderNumber:pattern` = `true` |
| `OrderNumber:<MLFB>/<Version>[//<Zusatz>]` | Version als `<ns>:vendorKey:catalogVersion`; weicht sie von `FirmwareVersion` ab (ohne `V` verglichen), Warnung `[TIA_AML_CATALOG_VERSION_DIFFERS]`; in die SBOM geht `FirmwareVersion`. Nur synthetisch belegt (Abschnitt 1.1) |
| `System:Device.<Familie>`, `System:Rack.<Familie>` | Struktur, keine Komponente, nicht im Hardware-Anhang |
| `GSD:<Datei>/D`, `GSD:<Datei>/R/<Id>` | Struktur |
| `GSD:<Datei>/DAP/<Id>` | Kopfmodul eines GSD-Geräts (5.4) |
| `GSD:<Datei>/M/<Id>` | Modul eines GSD-Geräts (5.4) |
| anderes Präfix oder andere GSD-Art | nicht ausgewertet, Warnung `[TIA_AML_TYPE_IDENTIFIER_UNKNOWN]`; das Gerät fehlt in der SBOM |
| kein `TypeIdentifier` | Struktur |

### 5.2 Firmware und passive Module (`OrderNumber:`)

| Fall | Ergebnis | Belegt durch |
|---|---|---|
| `TypeName` = `Rack` | Rack: passiv, Hardware-Anhang, Bestellnummer bleibt | V18 `Rail_0`, V21 `Profilschiene_0` |
| `FirmwareVersion` vorhanden, nicht `V0.0` | Firmware-Komponente, `firmware:origin` = `projected`, Hinweis `firmware-projected-only` | alle |
| `FirmwareVersion` = `V0.0` | passiv, Hardware-Anhang | V18 `BA 2xRJ45`, DI/DQ-Module |
| `FirmwareVersion` fehlt | Firmware-Komponente ohne Version: `firmware-attribute-missing` (Warnung) und `firmware-not-in-project` (Hinweis) | V21 PM 70W `6EP1332-4BA00` |
| `FirmwareVersion` fehlt, Bestellnummer passt auf `passiveOrderNumbers` | passiv, `<ns>:device:passive` = `declared` | synthetisch |

`passiveOrderNumbers` gilt nur für Geräte ohne Firmware-Attribut; ein Gerät mit `FirmwareVersion` bleibt Firmware. Muster mit `*` und `?`; Bestellnummern passen mit und ohne Leerzeichen (`6ES7511*` und `6ES7 511*`).

### 5.3 Hersteller

`OrderNumber:`-Geräte bekommen `Siemens AG` mit `vendor:origin` = `assumed`. Begründung: `OrderNumber:`-Kennungen stammen aus dem Siemens-Hardwarekatalog bzw. aus Siemens-HSPs; Geräte Dritter kommen über GSD. Alle `OrderNumber:`-Geräte der drei echten Fixtures sind Siemens-Geräte (SIMATIC, SINAMICS). An jeder neuen Fixture prüfen. `vendorOverrides` (Muster auf Bestellnummer → Hersteller, erster Treffer gilt) überschreibt mit `vendor:origin` = `declared`.

### 5.4 GSD-Geräte (bis M8)

Das AML nennt für GSD-Geräte nur Dateiname, Art und Id im `TypeIdentifier` und den `TypeName` (V17: Leuze BCL248i).

- Kopfmodul (`DAP`): Firmware-Komponente `<TypeName>, Firmware` ohne Hersteller, Bestellnummer und Version. Lücken `order-number-not-in-project`, `supplier-unknown` (Warnungen) und `firmware-not-in-project` (Hinweis). `<ns>:vendorKey:GSDML` = Dateiname, `GsdType`, `GsdId` = Art und Id.
- Module (`M`) ohne `FirmwareVersion`: passiv, Hardware-Anhang.
- Die GSDML-Datei selbst liest erst M8 (Hersteller, Bestellnummer, `SoftwareRelease` als `<ns>:gsdml:softwareRelease`, nie als Firmware). `--gsdml` bei `inspect` endet bis dahin mit `[NOT_IMPLEMENTED]`.

### 5.5 Bibliotheken und Programm

Der CAx-Export enthält weder Bibliotheken noch Bausteine.

- Station mit CPU ohne deklarierte Bibliotheken: `libraries-not-read` (Warnung) und `program-not-read` (Hinweis) an der Station. Ohne Anwenderprogramm (vor M9) hängen sie an der Firmware der CPU.
- Mit `libraries` in der Station: je Eintrag eine `library`-Komponente mit `<ns>:origin` = `declared` (Hersteller `vendor:origin` = `declared`); `libraries-not-read` entfällt. Ohne Version: `version-unresolved`.

## 6. Stationen

Eine Station ist eine CPU und die Geräte ihrer IO-Systeme (CLAUDE.md 5.4.4).

1. CPUs sind `DeviceItem`s mit `DeviceItemType` = `CPU`. `plc` wählt eine über den Element-Namen (genau, mit Groß-/Kleinschreibung).
2. Ohne `plc` ist die ganze Datei eine Station, aber nur, wenn genau eine Station die Datei liest und sie höchstens eine CPU enthält; sonst Exit 2 `[TIA_AML_PLC_REQUIRED]` mit der Liste der CPUs. `ignoreDevices` nimmt auch hier Geräte heraus.
3. Mit `plc` gehören zur Station: das `Device` der CPU mit allem darin (Rack, Stromversorgung, Module), jedes Gerät, dessen Schnittstelle mit einem IO-System dieser CPU verbunden ist, und jedes Gerät, das ein Muster unter `devices` trifft (Gerätename oder Name eines seiner Teile).
4. Ein IO-System gehört der CPU, unter der es hängt. Hängt es unter einem anderen Teil (etwa einem Kommunikationsmodul), gehört es der einzigen CPU desselben Geräts; gibt es keine oder mehrere, Warnung `[TIA_AML_IOSYSTEM_WITHOUT_CPU]`.
5. Übrige Geräte: Trifft sie ein Muster aus `ignoreDevices` (aller Stationen dieser Datei), kommen sie in die Liste der ausgeschlossenen Geräte (für den Bericht, M10). Sonst gehen sie unter die Maschine mit `device-unassigned` (Hinweis) und den Properties `<ns>:adapter`, `<ns>:source`, `<ns>:source:sha256`, weil die Station fehlt, die sie sonst trägt.
6. Ein Gerät, das zwei Stationen beanspruchen (zwei Stationen mit derselben CPU, `devices` auf ein Gerät am IO-System einer anderen Station), ergibt Exit 2 `[TIA_AML_DEVICE_IN_TWO_STATIONS]`.
7. `plc` nicht in der Datei: Exit 2 `[TIA_AML_PLC_NOT_FOUND]` mit der Liste der CPUs.
8. Ein Muster unter `devices` oder `ignoreDevices`, das kein Gerät trifft: Warnung `[TIA_AML_PATTERN_UNUSED]`.

Die CPU der Station ist die Steuerung (`bom-ref` `<station>-fw`), auf ihrer Firmware läuft das Anwenderprogramm.

## 7. `inspect`

`plc-sbom inspect <datei.aml> --adapter tia-aml` gibt aus: Projektname, Werkzeug (TIA-Version), Exportzeit, CPUs mit IO-Systemen, und je Gerät die Zuordnung, die jede CPU als Station mit `plc` und ohne `devices` bekäme (`via`: `cpu` oder `io-system`), dazu jedes nicht integrierte Teil mit Position, `TypeIdentifier`, Firmware und Behandlung (`firmware`, `firmwareAttributeMissing`, `passive`, `rack`, `gsdDevice`, `gsdModule`, `structure`, `unknown`). Warnungen stehen im JSON und zusätzlich als `[CODE] Text` auf stderr. Netzdaten und Element-IDs erscheinen nicht.

## 8. Fehlerfälle

| Fall | Ergebnis |
|---|---|
| Datei fehlt, kein gültiges XML, DTD | Exit 2 `[SOURCE_NOT_FOUND]`, `[XML_INVALID]` |
| Wurzel nicht `CAEXFile` | Exit 2 `[NOT_AML]` |
| nicht von TIA Portal geschrieben | Exit 2 `[NOT_TIA_AML]` |
| `plc` nicht gefunden, mehrere CPUs ohne `plc`, Gerät in zwei Stationen | Exit 2 `[TIA_AML_PLC_NOT_FOUND]`, `[TIA_AML_PLC_REQUIRED]`, `[TIA_AML_DEVICE_IN_TWO_STATIONS]` |
| unbekannte `WriterVersion`, Rolle, `TypeIdentifier`-Präfix, IO-System ohne CPU, abweichende Katalogversion, unbenutztes Muster | Warnung, weiter |

## 9. Offen

- Comquent-V21-Demo-Export (CLAUDE.md Abschnitt 13) mit ET 200SP, GSD-Gerät, Antrieb und HMI-Panel: Golden File folgt, sobald der Export vorliegt. Bis dahin decken V17 (eingecheckt) und V18/V21 (lokal) die echten Fälle ab.
- HMI-Panels: keine Fixture. Ein Panel ohne IO-System lässt sich über `devices` aufnehmen; wie seine Firmware im AML steht, ist offen.
- Mehrere CPUs in einem `Device` (S7-400H) und IO-Systeme an Kommunikationsmodulen: ohne Fixture, nur die Regel aus Abschnitt 6 Nr. 4.
- `RoleRequirements` statt `SupportedRoleClass`: in keiner Fixture; solche Elemente werden als Elemente ohne Rolle durchlaufen.
