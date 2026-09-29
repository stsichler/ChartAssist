# CLAUDE.md

Hinweise für Claude Code zur Arbeit an diesem Projekt.

## Projekt

ChartAssist ist der Nachfolger von ChartButlerCS: eine Desktop-Anwendung für Windows, Linux und macOS, die VFR-Anflugkarten der **BasicVFR AIP der DFS** in einem lokalen Kartenverzeichnis verwaltet und pro Flugplatz ein TripKit-PDF erzeugt.

Der grundlegende Unterschied zu ChartButlerCS: **ChartAssist greift selbst nie auf die DFS zu.** Der Benutzer ruft die Seiten im Browser auf und speichert sie mit Strg+S in einen Import-Ordner. ChartAssist wertet diese Dateien aus ("Import-Modus"). Grund sind die Nutzungsbedingungen der DFS, §2, siehe `docs/ENTSCHEIDUNGEN.md`.

- UI-Texte, Kommentare, Dokumentation und Commit-Messages sind **deutsch**.
- **Status:** Die Planung ist abgeschlossen. Das Projekt wird zusammen mit dem Benutzer von Grund auf aufgebaut. Phasen 0 bis 3 sind erledigt, als Nächstes kommt Phase 4 (Oberfläche) (`docs/TECHNISCHE-BASIS.md`, Abschnitt 11).

## Vor der Arbeit lesen

| Dokument | Inhalt |
|---|---|
| `docs/ENTSCHEIDUNGEN.md` | Entscheidungen und verworfene Alternativen aus der Planungsphase. Nicht ohne neuen Grund neu aufrollen |
| `docs/IMPORT-MODUS.md` | Fachliches Konzept: Leitplanken, Ablauf, Prüfergebnisse an echten DFS-Seiten, Parser, Abgleich-Sitzung, Test-Checkliste |
| `docs/TECHNISCHE-BASIS.md` | Technische Basis: Architektur, Projektdateien, .NET 10, Avalonia, Datenbankformat, Build, Paketierung, CI, Reihenfolge (Abschnitt 11), Risiken |

## Verbindliche Regeln

- **Keine Netzwerkzugriffe auf DFS-Server**, in keiner Form. Einzige erlaubte Netzwerkverbindung ist die Versionsprüfung bei GitHub.
- **Keine Automatisierung des Browsers.** DFS-Seiten nur einzeln auf ausdrückliche Benutzeraktion öffnen (eine Aktion = eine Seite). Kein automatisches Weiterschalten, kein "Alle öffnen", keine Extension. Details: Leitplanken in `docs/IMPORT-MODUS.md`, Abschnitt 2.
- **Echte DFS-Inhalte nie einchecken** (§11 der Nutzungsbedingungen). Sie liegen nur lokal:
  - `testdata/`: von Hand gespeicherte DFS-Seiten (Firefox, "nur HTML" und "komplett"),
  - `testcharts/`: Kopie eines echten Kartenverzeichnisses, geschrieben von ChartButlerCS 2.0.1.1.

  Beide sind per `.gitignore` ausgeschlossen. Tests im Repository und in CI verwenden nur synthetische Fixtures. Tests gegen die echten Daten werden übersprungen, wenn die Ordner fehlen.
- **KI-Kennzeichnung:** Das Projekt entsteht mit KI-Unterstützung und weist das offen aus (`docs/ENTSCHEIDUNGEN.md`, 3.11). Jeder Commit mit KI-Beteiligung trägt die `Co-Authored-By`-Zeile. Die Hinweise in README und LICENSE nicht entfernen oder abschwächen. Hilfe-Fenster und Release-Notes bekommen denselben Hinweis. Keine Kopfzeilen pro Quelldatei.
- **Kompatibilität mit bestehenden Kartenverzeichnissen:** Das Format von `.ChartButler.xml` exakt erhalten (`docs/TECHNISCHE-BASIS.md`, Abschnitt 4.3). Die Datei immer mit **CRLF** schreiben, auf allen Plattformen. Dateinamen nach den Regeln von Windows bilden, auch unter Linux.

## Struktur und Konventionen

- Git-Root `~/Entwicklung/ChartAssist/` enthält nur README und was Git/GitHub brauchen (`.gitignore`, `.gitattributes`, später `LICENSE`, `.github/`). Der gesamte Quellcode liegt in `ChartAssist/`. VS Code wird in `ChartAssist/` geöffnet.
- Zeilenenden **LF** (`.gitattributes`). Ausnahme: XML-Fixtures der Datenbanktests (`-text`).
- Projekte: `src/ChartAssist.Core` (UI-frei, testbar), `src/ChartAssist` (Avalonia 12), `tests/ChartAssist.Core.Tests` (xunit v3). Gemeinsame Einstellungen in `Directory.Build.props`, Paketversionen nur in `Directory.Packages.props`. `Nullable` und `ImplicitUsings` aktiv.
- Umgebung: Linux, .NET SDK 10.0.112 unter `/usr/lib/dotnet`, Avalonia-Templates installiert. Bauen, testen, formatieren, starten im Verzeichnis `ChartAssist/`: `dotnet build`, `dotnet test`, `dotnet format`, `dotnet run --project src/ChartAssist`.

## Referenz: ChartButlerCS

Die Fachlogik (Parsing, Vorschauvergleich, TripKit, Datenbank) wird aus ChartButlerCS 2.0.1.1 übernommen (Commit `ba93927`):
- lokal: `~/Entwicklung/ChartButler (Joerg Pauly)/ChartButlerCS/`, vor allem `CServerConnectionDFS.cs`, `Utility.cs`, `frmChartDB.cs` (Datenbank laden/speichern/wiederherstellen), `ChartButlerDataSet.xsd`,
- auf GitHub: `stsichler/ChartButlerCS`.

ChartButlerCS wird nicht weiterentwickelt. **Keine Änderungen am alten Repository vorschlagen oder vornehmen.** Einzige Ausnahme ist das vorgemerkte letzte Release, das der Benutzer selbst anstößt (`docs/TECHNISCHE-BASIS.md`, Abschnitt 11, Phase 6). Bekannte Fehler von ChartButlerCS vermeidet ChartAssist, z. B. Dateirechte `0600` durch `Path.GetTempFileName()`.
