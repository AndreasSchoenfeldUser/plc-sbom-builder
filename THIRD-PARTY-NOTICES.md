# Lizenzen von Drittkomponenten

Diese Datei wird mit `dotnet run tools/check-licenses.cs -- --write` aus den NuGet-Paketen erzeugt. Nicht von Hand ändern.

Die Pakete von plc-sbom sind self-contained und enthalten die .NET-Laufzeit sowie die folgenden NuGet-Pakete.

| Komponente | Version | Lizenz | Projekt |
|---|---|---|---|
| .NET Runtime | wie im Paket | MIT | https://github.com/dotnet/runtime |
| System.CommandLine | 2.0.14 | MIT | https://github.com/dotnet/command-line-api |

## .NET Runtime

Copyright (c) .NET Foundation and Contributors. Lizenz MIT, Text unten. Hinweise zu Drittkomponenten der Laufzeit: https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT

## System.CommandLine 2.0.14

© Microsoft Corporation. All rights reserved. Lizenz MIT, Text unten.

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
