# Prüfbericht Maschine XY, R2026.10

**Prüfung bestanden (Entwicklungs-Build)**

| | |
|---|---|
| Maschine | Maschine XY |
| Release | R2026.10 |
| Git-Commit | a1b2c3d4e5f60718293a4b5c6d7e8f9012345678 |
| Ersteller der SBOM | Musterbau GmbH (sbom@musterbau.example) |
| Datei | build/sbom-maschine.cdx.json |
| Format | CycloneDX 1.6, JSON |
| SBOM-Kennung | urn:uuid:1a02a05d-7584-5a0b-9e78-e462de8d242a |
| Erzeugt | 17.10.2026 11:00 UTC |
| Erzeugt mit | PLC SBOM Builder 0.1.0 |
| Prüfstatus | bestanden (Entwicklungs-Build) |
| Befunde | 0 Fehler, 116 Warnungen, 109 Hinweise |

## Kennzahlen

| | |
|---|---|
| Komponenten | 49 (mit der Maschine) |
| Stationen | 3 |
| Firmware-Komponenten | 12 |
| Hersteller-SBOMs | 1 |
| Teil-SBOMs | 1 |
| Komponenten ohne Hash | 45 |

## Quellen

| Station | Adapter | Datei | SHA-256 | Engineering-Werkzeug | Export |
|---|---|---|---|---|---|
| PLC\_1 | tia-aml | fixtures/tia-aml/plc-commissioning-sample\_device.aml | 0fade7c6844270656da6718e515d5f9fcbaa056a72891405bf1ae3c4c73bf1d6 | – | 28.12.2024 20:29 UTC |
| Roboterzelle | twincat | fixtures/twincat/aixocat-heatpump/HeatPumpTestBenchControl.tsproj | 8770c82a2c3b0138475b64ed6a3136cd1120639c144214c06dc32b0970f44cdb | – | – |
| Verpackung | codesys (v35) | fixtures/codesys/v35/robin-codesys\_project.xml | acf12d77f7ad9ba162485e9c8d64545d97ab37e81f396507112917330af17f2d | – | – |

Die SBOM ordnet die Engineering-Werkzeuge keiner Station zu; sie stehen unter „Werkzeuge“. Der Bericht aus plc-sbom build nennt sie je Station.

### Eingelesene SBOMs

| Datei | Art | SHA-256 | Zugeordnet zu |
|---|---|---|---|
| samples/maschine-xy/sbom/lieferanten/siemens-cpu1214c-v4.5-synthetisch.cdx.json | Hersteller-SBOM | d71bc02e46af86a149b53bc4d2d6f7cab5a66e34efc7d01943437b9a2dd3d1c0 | CPU 1214C DC/DC/DC, Firmware (plc-1-fw) |
| samples/maschine-xy/sbom/teil/hmi-web.cdx.json | Teil-SBOM | e8828b3d6a809b3140b77db8819e8ae05cacd3775d47bb70ffbef766273aa369 | Station PLC\_1 |

### Werkzeuge

| Werkzeug | Version | Hersteller |
|---|---|---|
| PLC SBOM Builder | 0.1.0 | Comquent GmbH |
| CODESYS | V3.5 SP15 | – |
| TIA Portal | V17 (1700.0.4302.1) | Siemens AG |
| TwinCAT XAE | 3.1.4022.4 | – |

## Maschinenbaum

- Maschine XY: R2026.10
  - Station PLC\_1: tia-aml
    - Steuerung
      - CPU 1214C DC/DC/DC, Firmware: V4.5 · 6ES7 214-1AG40-0XB0 · S7-1200 station\_1/0/1
        - Beispiel-Echtzeitkern: 2.1.0 (Hersteller-SBOM)
        - Beispiel-TCP/IP-Stack: 1.4.2 (Hersteller-SBOM)
    - Anwenderprogramm
      - PLC\_1, Anwenderprogramm: R2026.10 (git a1b2c3d)
    - E/A-Module und Feldgeräte (1)
      - BCL248i, Firmware: Firmware unbekannt · 501xxxxx · GSD device\_1/0/0
    - Bibliotheken (1)
      - LGF: 7.0.1
    - Teil-SBOMs
      - hmi-web: 1.4.0 · samples/maschine-xy/sbom/teil/hmi-web.cdx.json
        - maschine-xy-hmi-web: 1.4.0
          - ms: 2.1.3
          - picocolors: 1.1.1
  - Station Roboterzelle: twincat
    - Anwenderprogramm
      - Roboterzelle, Anwenderprogramm: R2026.10 (git a1b2c3d)
    - E/A-Module und Feldgeräte (10)
      - EK1100, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1
      - EL3214, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/10
      - EL3064, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/2
      - EL3204-0200, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/3
      - EL3204-0200, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/4
      - EL3204-0200, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/5
      - EL3403-0010, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/6
      - EL4004, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/7
      - EL2652, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/8
      - EL2622, Firmware: Firmware unbekannt · Device 1 (EtherCAT)/1/9
    - Laufzeit
      - TwinCAT 3 Runtime: ohne Version
    - Bibliotheken (14)
      - Tc2\_Standard: angefragt \*
      - Tc2\_System: angefragt \*
      - Tc2\_Utilities: angefragt \*
      - Tc3\_Module: angefragt \*
      - VisuDialogs: angefragt \*
      - VisuElem3DPath: angefragt 3.5.6.0
      - VisuElemMeter: angefragt 3.5.6.0
      - VisuElems: angefragt 3.5.6.10
      - VisuElemsDateTime: angefragt 3.5.6.0
      - VisuElemsSpecialControls: angefragt 3.5.6.0
      - VisuElemsWinControls: angefragt 3.5.6.0
      - VisuElemTextEditor: angefragt 3.5.6.0
      - visuinputs: angefragt 3.5.6.0
      - VisuNativeControl: angefragt 3.5.6.0
  - Station Verpackung: codesys
    - Steuerung
      - Gerät 4102/0000 0005, Firmware: Firmware unbekannt
    - Anwenderprogramm
      - Verpackung, Anwenderprogramm: R2026.10 (git a1b2c3d)
    - Bibliotheken (10)
      - 3SLicense: angefragt 0.0.0.0
      - Breakpoint Logging Functions: angefragt 3.5.5.0
      - CAA Device Diagnosis: angefragt 3.5.15.0
      - IoStandard: angefragt 3.5.15.0
      - Robin: 0.1.2
      - SM3\_Basic: angefragt 4.5.1.0
      - SM3\_CNC: angefragt 4.5.1.0
      - SM3\_Robotics: angefragt 4.5.1.0
      - SM3\_Robotics\_Visu: angefragt 4.5.1.0
      - SM3\_Transformation: angefragt 4.5.1.0

## Komponenten je Station

### Station PLC\_1

| Typ | Name | Version | Kennung | Pflichtfelder | Herkunft |
|---|---|---|---|---|---|
| Teil-SBOM | hmi-web | 1.4.0 | partial-hmi-web/af63bd4c8601b7f1 | Warnung: input-field-missing | aus samples/maschine-xy/sbom/teil/hmi-web.cdx.json |
| Anwenderprogramm | PLC\_1, Anwenderprogramm | R2026.10 (git a1b2c3d) | plc-1-app | Hinweis: filename-unavailable, hash-unavailable, program-not-read | Hersteller declared, Angabe declared |
| Firmware | CPU 1214C DC/DC/DC, Firmware | V4.5 | cpe:2.3:o:siemens:simatic\_s7-1200\_cpu\_firmware:4.5:\*:\*:\*:\*:\*:\*:\* | Hinweis: firmware-projected-only | Hersteller assumed, Firmware projected |
| Firmware | BCL248i, Firmware | – | plc-1-fw-gsd-device-1-0-0 | Warnung: license-unknown; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller gsdml, Firmware not-in-project |
| Bibliothek | LGF | 7.0.1 | plc-1-lib-lgf | Warnung: file-properties-unknown; Hinweis: filename-unavailable, hash-unavailable | Hersteller declared, Angabe declared |

### Station Roboterzelle

| Typ | Name | Version | Kennung | Pflichtfelder | Herkunft |
|---|---|---|---|---|---|
| Anwenderprogramm | Roboterzelle, Anwenderprogramm | R2026.10 (git a1b2c3d) | roboterzelle-app | Hinweis: filename-unavailable, hash-unavailable | Hersteller declared, Angabe declared |
| Firmware | EK1100, Firmware | – | roboterzelle-fw-device-1-ethercat-1 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL3214, Firmware | – | roboterzelle-fw-device-1-ethercat-1-10 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL3064, Firmware | – | roboterzelle-fw-device-1-ethercat-1-2 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL3204-0200, Firmware | – | roboterzelle-fw-device-1-ethercat-1-3 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL3204-0200, Firmware | – | roboterzelle-fw-device-1-ethercat-1-4 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL3204-0200, Firmware | – | roboterzelle-fw-device-1-ethercat-1-5 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL3403-0010, Firmware | – | roboterzelle-fw-device-1-ethercat-1-6 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL4004, Firmware | – | roboterzelle-fw-device-1-ethercat-1-7 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL2652, Firmware | – | roboterzelle-fw-device-1-ethercat-1-8 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Firmware | EL2622, Firmware | – | roboterzelle-fw-device-1-ethercat-1-9 | Warnung: order-number-not-in-project; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Hersteller assumed, Firmware not-in-project |
| Bibliothek | Tc2\_Standard | – (angefragt \*) | roboterzelle-lib-tc2-standard | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | Tc2\_System | – (angefragt \*) | roboterzelle-lib-tc2-system | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | Tc2\_Utilities | – (angefragt \*) | roboterzelle-lib-tc2-utilities | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | Tc3\_Module | – (angefragt \*) | roboterzelle-lib-tc3-module | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | VisuDialogs | – (angefragt \*) | roboterzelle-lib-visudialogs | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | VisuElem3DPath | – (angefragt 3.5.6.0) | roboterzelle-lib-visuelem3dpath | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | VisuElemMeter | – (angefragt 3.5.6.0) | roboterzelle-lib-visuelemmeter | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | VisuElems | – (angefragt 3.5.6.10) | roboterzelle-lib-visuelems | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | VisuElemsDateTime | – (angefragt 3.5.6.0) | roboterzelle-lib-visuelemsdatetime | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | VisuElemsSpecialControls | – (angefragt 3.5.6.0) | roboterzelle-lib-visuelemsspecialcontrols | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | VisuElemsWinControls | – (angefragt 3.5.6.0) | roboterzelle-lib-visuelemswincontrols | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | VisuElemTextEditor | – (angefragt 3.5.6.0) | roboterzelle-lib-visuelemtexteditor | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | visuinputs | – (angefragt 3.5.6.0) | roboterzelle-lib-visuinputs | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | VisuNativeControl | – (angefragt 3.5.6.0) | roboterzelle-lib-visunativecontrol | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Laufzeit | TwinCAT 3 Runtime | – | roboterzelle-rt | Warnung: file-properties-unknown, runtime-version-unknown; Hinweis: filename-unavailable, hash-unavailable | Hersteller assumed, Angabe assumed |

### Station Verpackung

| Typ | Name | Version | Kennung | Pflichtfelder | Herkunft |
|---|---|---|---|---|---|
| Anwenderprogramm | Verpackung, Anwenderprogramm | R2026.10 (git a1b2c3d) | verpackung-app | Hinweis: filename-unavailable, hash-unavailable | Hersteller declared, Angabe declared |
| Firmware | Gerät 4102/0000 0005, Firmware | – | verpackung-fw | Warnung: license-unknown, order-number-not-in-project, supplier-unknown; Hinweis: filename-unavailable, firmware-not-in-project, hash-unavailable, vendor-sbom-missing | Firmware unknown |
| Bibliothek | 3SLicense | – (angefragt 0.0.0.0) | verpackung-lib-3slicense | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | Breakpoint Logging Functions | – (angefragt 3.5.5.0) | verpackung-lib-breakpoint-logging-functions | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | CAA Device Diagnosis | – (angefragt 3.5.15.0) | verpackung-lib-caa-device-diagnosis | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | IoStandard | – (angefragt 3.5.15.0) | verpackung-lib-iostandard | Warnung: file-properties-unknown, license-unknown, supplier-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Angabe source |
| Bibliothek | Robin | 0.1.2 | verpackung-lib-robin | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | SM3\_Basic | – (angefragt 4.5.1.0) | verpackung-lib-sm3-basic | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | SM3\_CNC | – (angefragt 4.5.1.0) | verpackung-lib-sm3-cnc | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | SM3\_Robotics | – (angefragt 4.5.1.0) | verpackung-lib-sm3-robotics | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | SM3\_Robotics\_Visu | – (angefragt 4.5.1.0) | verpackung-lib-sm3-robotics-visu | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |
| Bibliothek | SM3\_Transformation | – (angefragt 4.5.1.0) | verpackung-lib-sm3-transformation | Warnung: creator-contact-unknown, file-properties-unknown, license-unknown, version-unresolved; Hinweis: filename-unavailable, hash-unavailable | Hersteller source, Angabe source |

### Aus samples/maschine-xy/sbom/teil/hmi-web.cdx.json

| Typ | Name | Version | Kennung | Pflichtfelder | Herkunft |
|---|---|---|---|---|---|
| Bibliothek | maschine-xy-hmi-web | 1.4.0 | cpe:2.3:a:maschine-xy-hmi-web:maschine-xy-hmi-web:1.4.0:\*:\*:\*:\*:\*:\*:\* | Warnung: input-field-missing | aus samples/maschine-xy/sbom/teil/hmi-web.cdx.json |
| Bibliothek | ms | 2.1.3 | cpe:2.3:a:vercel:ms:2.1.3:\*:\*:\*:\*:node.js:\*:\* | Warnung: input-field-missing | aus samples/maschine-xy/sbom/teil/hmi-web.cdx.json |
| Bibliothek | picocolors | 1.1.1 | cpe:2.3:a:picocolors:picocolors:1.1.1:\*:\*:\*:\*:\*:\*:\* | Warnung: input-field-missing | aus samples/maschine-xy/sbom/teil/hmi-web.cdx.json |

### Aus samples/maschine-xy/sbom/lieferanten/siemens-cpu1214c-v4.5-synthetisch.cdx.json

| Typ | Name | Version | Kennung | Pflichtfelder | Herkunft |
|---|---|---|---|---|---|
| Bibliothek | Beispiel-Echtzeitkern | 2.1.0 | vendor-siemens-cpu1214c-v4-5-synthetisch/rtos | vollständig | aus samples/maschine-xy/sbom/lieferanten/siemens-cpu1214c-v4.5-synthetisch.cdx.json |
| Bibliothek | Beispiel-TCP/IP-Stack | 1.4.2 | vendor-siemens-cpu1214c-v4-5-synthetisch/tcpip | vollständig | aus samples/maschine-xy/sbom/lieferanten/siemens-cpu1214c-v4.5-synthetisch.cdx.json |

## Lücken

| Code | Schwere | Anzahl | Komponenten | Erklärung | Was sie schließt |
|---|---|---|---|---|---|
| creator-contact-unknown | Warnung | 13 | roboterzelle-lib-tc2-standard, roboterzelle-lib-tc2-system, roboterzelle-lib-tc2-utilities, roboterzelle-lib-tc3-module, verpackung-lib-3slicense, verpackung-lib-breakpoint-logging-functions, verpackung-lib-caa-device-diagnosis, verpackung-lib-robin, verpackung-lib-sm3-basic, verpackung-lib-sm3-cnc, verpackung-lib-sm3-robotics, verpackung-lib-sm3-robotics-visu, verpackung-lib-sm3-transformation | Für den Hersteller ist weder E-Mail-Adresse noch URL bekannt. | E-Mail-Adresse oder URL des Herstellers unter „vendorContacts“ in machine.yml eintragen. |
| file-properties-unknown | Warnung | 26 | plc-1-lib-lgf, roboterzelle-lib-tc2-standard, roboterzelle-lib-tc2-system, roboterzelle-lib-tc2-utilities, roboterzelle-lib-tc3-module, roboterzelle-lib-visudialogs, roboterzelle-lib-visuelem3dpath, roboterzelle-lib-visuelemmeter, roboterzelle-lib-visuelems, roboterzelle-lib-visuelemsdatetime, roboterzelle-lib-visuelemsspecialcontrols, roboterzelle-lib-visuelemswincontrols, roboterzelle-lib-visuelemtexteditor, roboterzelle-lib-visuinputs, roboterzelle-lib-visunativecontrol, roboterzelle-rt, verpackung-lib-3slicense, verpackung-lib-breakpoint-logging-functions, verpackung-lib-caa-device-diagnosis, verpackung-lib-iostandard, verpackung-lib-robin, verpackung-lib-sm3-basic, verpackung-lib-sm3-cnc, verpackung-lib-sm3-robotics, verpackung-lib-sm3-robotics-visu, verpackung-lib-sm3-transformation | Ob die Komponente ausführbar, ein Archiv oder strukturiert ist, ist nicht bekannt. | Bibliothek als Datei mit bekannten Eigenschaften einbinden; die Quelle sagt nichts dazu. |
| input-field-missing | Warnung | 4 | partial-hmi-web/af63bd4c8601b7f1, partial-hmi-web/pkg:npm/maschine-xy-hmi-web@1.4.0?package-id=2ca70c872f5ed6a5, partial-hmi-web/pkg:npm/ms@2.1.3?package-id=bb1077662b3cb7e0, partial-hmi-web/pkg:npm/picocolors@1.1.1?package-id=9b208867cdc8b323 | Die Komponente stammt aus einer eingelesenen SBOM, der Pflichtfelder fehlen; sie wurde unverändert übernommen. | Vollständige SBOM beim Lieferanten bzw. vom erzeugenden Werkzeug anfordern. |
| license-unknown | Warnung | 26 | plc-1-fw-gsd-device-1-0-0, roboterzelle-lib-tc2-standard, roboterzelle-lib-tc2-system, roboterzelle-lib-tc2-utilities, roboterzelle-lib-tc3-module, roboterzelle-lib-visudialogs, roboterzelle-lib-visuelem3dpath, roboterzelle-lib-visuelemmeter, roboterzelle-lib-visuelems, roboterzelle-lib-visuelemsdatetime, roboterzelle-lib-visuelemsspecialcontrols, roboterzelle-lib-visuelemswincontrols, roboterzelle-lib-visuelemtexteditor, roboterzelle-lib-visuinputs, roboterzelle-lib-visunativecontrol, verpackung-fw, verpackung-lib-3slicense, verpackung-lib-breakpoint-logging-functions, verpackung-lib-caa-device-diagnosis, verpackung-lib-iostandard, verpackung-lib-robin, verpackung-lib-sm3-basic, verpackung-lib-sm3-cnc, verpackung-lib-sm3-robotics, verpackung-lib-sm3-robotics-visu, verpackung-lib-sm3-transformation | Keine Distributionslizenz bekannt. | Distributionslizenz des Herstellers unter „licenses“ in machine.yml eintragen. |
| order-number-not-in-project | Warnung | 11 | roboterzelle-fw-device-1-ethercat-1, roboterzelle-fw-device-1-ethercat-1-10, roboterzelle-fw-device-1-ethercat-1-2, roboterzelle-fw-device-1-ethercat-1-3, roboterzelle-fw-device-1-ethercat-1-4, roboterzelle-fw-device-1-ethercat-1-5, roboterzelle-fw-device-1-ethercat-1-6, roboterzelle-fw-device-1-ethercat-1-7, roboterzelle-fw-device-1-ethercat-1-8, roboterzelle-fw-device-1-ethercat-1-9, verpackung-fw | Weder Projekt noch GSDML nennen eine Bestellnummer. | Bestellnummer über eine Gerätebeschreibung oder den Ist-Stand ermitteln. |
| runtime-version-unknown | Warnung | 1 | roboterzelle-rt | Die Version der Laufzeit steht nicht im Projekt. | Version der Laufzeit aus dem Feld einlesen oder deklarieren. |
| supplier-unknown | Warnung | 12 | roboterzelle-lib-visudialogs, roboterzelle-lib-visuelem3dpath, roboterzelle-lib-visuelemmeter, roboterzelle-lib-visuelems, roboterzelle-lib-visuelemsdatetime, roboterzelle-lib-visuelemsspecialcontrols, roboterzelle-lib-visuelemswincontrols, roboterzelle-lib-visuelemtexteditor, roboterzelle-lib-visuinputs, roboterzelle-lib-visunativecontrol, verpackung-fw, verpackung-lib-iostandard | Der Hersteller ist nicht ermittelbar. | Hersteller mit „vendorOverrides“ in machine.yml erklären. |
| version-unresolved | Warnung | 23 | roboterzelle-lib-tc2-standard, roboterzelle-lib-tc2-system, roboterzelle-lib-tc2-utilities, roboterzelle-lib-tc3-module, roboterzelle-lib-visudialogs, roboterzelle-lib-visuelem3dpath, roboterzelle-lib-visuelemmeter, roboterzelle-lib-visuelems, roboterzelle-lib-visuelemsdatetime, roboterzelle-lib-visuelemsspecialcontrols, roboterzelle-lib-visuelemswincontrols, roboterzelle-lib-visuelemtexteditor, roboterzelle-lib-visuinputs, roboterzelle-lib-visunativecontrol, verpackung-lib-3slicense, verpackung-lib-breakpoint-logging-functions, verpackung-lib-caa-device-diagnosis, verpackung-lib-iostandard, verpackung-lib-sm3-basic, verpackung-lib-sm3-cnc, verpackung-lib-sm3-robotics, verpackung-lib-sm3-robotics-visu, verpackung-lib-sm3-transformation | Die Quelle nennt keine feste Version, sondern einen Platzhalter. | Bibliothek im Projekt auf eine feste Version setzen. |
| filename-unavailable | Hinweis | 41 | plc-1-app, plc-1-fw-gsd-device-1-0-0, plc-1-lib-lgf, roboterzelle-app, roboterzelle-fw-device-1-ethercat-1, roboterzelle-fw-device-1-ethercat-1-10, roboterzelle-fw-device-1-ethercat-1-2, roboterzelle-fw-device-1-ethercat-1-3, roboterzelle-fw-device-1-ethercat-1-4, roboterzelle-fw-device-1-ethercat-1-5, roboterzelle-fw-device-1-ethercat-1-6, roboterzelle-fw-device-1-ethercat-1-7, roboterzelle-fw-device-1-ethercat-1-8, roboterzelle-fw-device-1-ethercat-1-9, roboterzelle-lib-tc2-standard, roboterzelle-lib-tc2-system, roboterzelle-lib-tc2-utilities, roboterzelle-lib-tc3-module, roboterzelle-lib-visudialogs, roboterzelle-lib-visuelem3dpath, roboterzelle-lib-visuelemmeter, roboterzelle-lib-visuelems, roboterzelle-lib-visuelemsdatetime, roboterzelle-lib-visuelemsspecialcontrols, roboterzelle-lib-visuelemswincontrols, roboterzelle-lib-visuelemtexteditor, roboterzelle-lib-visuinputs, roboterzelle-lib-visunativecontrol, roboterzelle-rt, verpackung-app, verpackung-fw, verpackung-lib-3slicense, verpackung-lib-breakpoint-logging-functions, verpackung-lib-caa-device-diagnosis, verpackung-lib-iostandard, verpackung-lib-robin, verpackung-lib-sm3-basic, verpackung-lib-sm3-cnc, verpackung-lib-sm3-robotics, verpackung-lib-sm3-robotics-visu, verpackung-lib-sm3-transformation | Die Komponente liegt nicht als Datei vor, ein Dateiname ist nicht bestimmbar. | Hersteller-SBOM mit Dateinamen unter „vendorSboms“ einbinden; sonst zulässig (TR 3.2.1). |
| firmware-not-in-project | Hinweis | 12 | plc-1-fw-gsd-device-1-0-0, roboterzelle-fw-device-1-ethercat-1, roboterzelle-fw-device-1-ethercat-1-10, roboterzelle-fw-device-1-ethercat-1-2, roboterzelle-fw-device-1-ethercat-1-3, roboterzelle-fw-device-1-ethercat-1-4, roboterzelle-fw-device-1-ethercat-1-5, roboterzelle-fw-device-1-ethercat-1-6, roboterzelle-fw-device-1-ethercat-1-7, roboterzelle-fw-device-1-ethercat-1-8, roboterzelle-fw-device-1-ethercat-1-9, verpackung-fw | Das Projekt führt keine Firmware-Version für dieses Gerät. | Ist-Stand aus dem Feld einlesen (plc-sbom compare --as-built). |
| firmware-projected-only | Hinweis | 1 | plc-1-fw | Die Firmware ist nur als projektierter Sollwert bekannt, ein Ist-Stand liegt nicht vor. | Ist-Stand aus dem Feld einlesen (plc-sbom compare --as-built). |
| hash-unavailable | Hinweis | 41 | plc-1-app, plc-1-fw-gsd-device-1-0-0, plc-1-lib-lgf, roboterzelle-app, roboterzelle-fw-device-1-ethercat-1, roboterzelle-fw-device-1-ethercat-1-10, roboterzelle-fw-device-1-ethercat-1-2, roboterzelle-fw-device-1-ethercat-1-3, roboterzelle-fw-device-1-ethercat-1-4, roboterzelle-fw-device-1-ethercat-1-5, roboterzelle-fw-device-1-ethercat-1-6, roboterzelle-fw-device-1-ethercat-1-7, roboterzelle-fw-device-1-ethercat-1-8, roboterzelle-fw-device-1-ethercat-1-9, roboterzelle-lib-tc2-standard, roboterzelle-lib-tc2-system, roboterzelle-lib-tc2-utilities, roboterzelle-lib-tc3-module, roboterzelle-lib-visudialogs, roboterzelle-lib-visuelem3dpath, roboterzelle-lib-visuelemmeter, roboterzelle-lib-visuelems, roboterzelle-lib-visuelemsdatetime, roboterzelle-lib-visuelemsspecialcontrols, roboterzelle-lib-visuelemswincontrols, roboterzelle-lib-visuelemtexteditor, roboterzelle-lib-visuinputs, roboterzelle-lib-visunativecontrol, roboterzelle-rt, verpackung-app, verpackung-fw, verpackung-lib-3slicense, verpackung-lib-breakpoint-logging-functions, verpackung-lib-caa-device-diagnosis, verpackung-lib-iostandard, verpackung-lib-robin, verpackung-lib-sm3-basic, verpackung-lib-sm3-cnc, verpackung-lib-sm3-robotics, verpackung-lib-sm3-robotics-visu, verpackung-lib-sm3-transformation | Die ausgelieferte Form liegt dem Werkzeug nicht vor, ein SHA-512 ist nicht bestimmbar. | Hersteller-SBOM mit SHA-512 der Firmware-Datei unter „vendorSboms“ einbinden; bei Firmware und Anwenderprogramm sonst zulässig (TR 3.2.1). |
| program-not-read | Hinweis | 1 | plc-1-app | Die Quelle enthält keine Bausteine, geschützte Bausteine sind unbekannt. | Erst der Adapter tia-portal liest Bausteine (M14). |
| vendor-sbom-missing | Hinweis | 12 | plc-1-fw-gsd-device-1-0-0, roboterzelle-fw-device-1-ethercat-1, roboterzelle-fw-device-1-ethercat-1-10, roboterzelle-fw-device-1-ethercat-1-2, roboterzelle-fw-device-1-ethercat-1-3, roboterzelle-fw-device-1-ethercat-1-4, roboterzelle-fw-device-1-ethercat-1-5, roboterzelle-fw-device-1-ethercat-1-6, roboterzelle-fw-device-1-ethercat-1-7, roboterzelle-fw-device-1-ethercat-1-8, roboterzelle-fw-device-1-ethercat-1-9, verpackung-fw | Für diese Firmware liegt keine SBOM des Herstellers vor. | SBOM vom Hersteller anfordern und unter „vendorSboms“ einbinden. |

## Weitere Befunde

| Schwere | Code | Komponente | Meldung |
|---|---|---|---|
| Hinweis | LOGICAL\_COMPONENT | maschine-xy | maschine-xy ist eine logische Komponente. Dateiname, Hash und die Eigenschaften ausführbar, Archiv und strukturiert entfallen (TR-03183-2 Abschnitt 3.2.2). |

## Abgleich mit dem Feld

| | |
|---|---|
| Feld-Datei | samples/maschine-xy/feld/1042.json |
| SHA-256 | d7a267dff9cae416397c172c13f32509bb18d09e0a84ab70b4dd5fe2308ba34f |
| Seriennummer der Maschine | 1042 |
| Ausgelesen am | 22.10.2026 |
| Anlass | inbetriebnahme |
| Geprüft | 13 Geräte der SBOM |
| Ergebnis | 2 gleich, 1 abweichend, 10 nur im Projekt, 1 nur im Feld, 1 passiv |

| Station | Position | Gerät | Bestellnummer SBOM | Bestellnummer Feld | Firmware SBOM | Firmware Feld | Ergebnis | Hinweis |
|---|---|---|---|---|---|---|---|---|
| PLC\_1 | S7-1200 station\_1/0/1 | CPU 1214C DC/DC/DC | 6ES7 214-1AG40-0XB0 | 6ES7 214-1AG40-0XB0 | V4.5 | V4.6.1 | abweichend | Firmware im Feld V4.6.1, in der SBOM V4.5. |
| PLC\_1 | GSD device\_1/0/0 | BCL248i | 501xxxxx | 50132148 | – | V1.4.0 | gleich | Firmware V1.4.0 nur im Feld bekannt, die SBOM führt keine. |
| Roboterzelle | Device 1 (EtherCAT)/1 | EK1100 | – | – | – | V2.3 | gleich | Firmware V2.3 nur im Feld bekannt, die SBOM führt keine. |
| Roboterzelle | Device 1 (EtherCAT)/1/10 | EL3214 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Roboterzelle | Device 1 (EtherCAT)/1/2 | EL3064 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Roboterzelle | Device 1 (EtherCAT)/1/3 | EL3204-0200 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Roboterzelle | Device 1 (EtherCAT)/1/4 | EL3204-0200 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Roboterzelle | Device 1 (EtherCAT)/1/5 | EL3204-0200 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Roboterzelle | Device 1 (EtherCAT)/1/6 | EL3403-0010 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Roboterzelle | Device 1 (EtherCAT)/1/7 | EL4004 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Roboterzelle | Device 1 (EtherCAT)/1/8 | EL2652 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Roboterzelle | Device 1 (EtherCAT)/1/9 | EL2622 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| Verpackung | – | Gerät 4102/0000 0005 | – | – | – | – | nur-im-projekt | Im Ist-Stand nicht gefunden. |
| PLC\_1 | – | 6ES7 193-6BP00-0DA0 | – | 6ES7 193-6BP00-0DA0 | – | V0.0 | passiv | Firmware 0.0: Gerät ohne eigene Firmware, steht nicht in der SBOM. |
| PLC\_1 | – | 6GK5 208-0BA00-2AC2 | – | 6GK5 208-0BA00-2AC2 | – | V4.2 | nur-im-feld | Kein Gerät der SBOM passt (Station und Position, Bestellnummer). |

## Anhang: Hardware ohne eigene Firmware

Passive Module und Racks stehen nicht in der SBOM, weil sie keine Software enthalten. Diesen Anhang schreibt nur plc-sbom build, das die Projekte selbst liest.

Bericht von PLC SBOM Builder zu build/sbom-maschine.cdx.json. Eine SBOM allein erfüllt den Cyber Resilience Act nicht; Schwachstellen bewertet das Werkzeug nicht.
