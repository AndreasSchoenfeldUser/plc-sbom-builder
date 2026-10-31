# Fixtures

Echte Beispieldateien für die Adapter (CLAUDE.md Abschnitte 2 und 13). Jedes Attribut, das ein Adapter liest, ist durch eine Datei hier und einen Test belegt.

Regeln:

- Einchecken nur mit klarer Lizenz: MIT, Apache-2.0, 0BSD, BSD oder Comquent-eigen.
- Dateien ohne Lizenz nur unter `fixtures/_local/`. Das Verzeichnis ist nicht eingecheckt (`.gitignore`); Tests, die sie brauchen, überspringen sich, wenn die Datei fehlt.
- Synthetische Dateien heißen `*-synthetisch.*` und sind nur erlaubt, wenn für dasselbe Attribut ein echtes Gegenstück existiert.
- Dateien byte-genau übernehmen (`.gitattributes`: `fixtures/** -text`).

| Datei | Umgebung | Repository | Commit | Pfad im Repository | Lizenz | Abgerufen |
|---|---|---|---|---|---|---|

Noch keine Fixtures. Die erste kommt mit M2 (CODESYS V3.5, PLCopen XML).
