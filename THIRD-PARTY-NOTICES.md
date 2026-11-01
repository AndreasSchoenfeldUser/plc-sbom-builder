# Lizenzen von Drittkomponenten

Diese Datei wird mit `dotnet run tools/check-licenses.cs -- --write` aus den NuGet-Paketen erzeugt. Nicht von Hand ändern.

Die Pakete von plc-sbom sind self-contained und enthalten die .NET-Laufzeit sowie die folgenden NuGet-Pakete.

| Komponente | Version | Lizenz | Projekt |
|---|---|---|---|
| .NET Runtime | wie im Paket | MIT | https://github.com/dotnet/runtime |
| Namotion.Reflection | 3.5.0 | MIT | https://github.com/RicoSuter/Namotion.Reflection |
| Newtonsoft.Json | 13.0.3 | MIT | https://www.newtonsoft.com/json |
| NJsonSchema | 11.6.1 | MIT | http://njsonschema.org/ |
| NJsonSchema.Annotations | 11.6.1 | MIT | http://njsonschema.org/ |
| System.CommandLine | 2.0.14 | MIT | https://github.com/dotnet/command-line-api |
| YamlDotNet | 18.1.0 | MIT | https://github.com/aaubry/YamlDotNet/wiki |

## .NET Runtime

Copyright (c) .NET Foundation and Contributors. Lizenz MIT, Text unten. Hinweise zu Drittkomponenten der Laufzeit: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT

## Namotion.Reflection 3.5.0

Copyright © Rico Suter, 2025. Lizenz MIT, Text unten.

## Newtonsoft.Json 13.0.3

Copyright © James Newton-King 2008. Lizenz MIT, Text unten.

## NJsonSchema 11.6.1

Copyright © Rico Suter, 2025. Lizenz MIT, Text unten.

## NJsonSchema.Annotations 11.6.1

Copyright © Rico Suter, 2025. Lizenz MIT, Text unten.

## System.CommandLine 2.0.14

© Microsoft Corporation. All rights reserved. Lizenz MIT, Text unten.

## YamlDotNet 18.1.0

Copyright (c) Antoine Aubry and contributors. Lizenz MIT, Text unten.

## Lizenztext MIT

```text
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```
