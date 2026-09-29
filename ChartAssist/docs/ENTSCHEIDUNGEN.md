# Entscheidungen aus der Planungsphase

Dieses Dokument hält fest, welche Entscheidungen vor dem Start von ChartAssist gefallen sind und welche Alternativen warum verworfen wurden. Die Planung fand am 28. und 29.09.2026 im Repository von ChartButlerCS statt. Details stehen in [IMPORT-MODUS.md](IMPORT-MODUS.md) (fachlich) und [TECHNISCHE-BASIS.md](TECHNISCHE-BASIS.md) (technisch).

---

## 1. Ausgangslage

- ChartButlerCS lädt VFR-Anflugkarten der BasicVFR AIP per `HttpClient` automatisiert von `aip.dfs.de`.
- Die Nutzungsbedingungen des AIS-Portals der DFS verbieten in §2 "eine Nutzung und Bedienung des AIS-Portal […] durch elektronische und/oder automatisierte Tools". Ausgenommen sind nur AIXM-Datensätze, und die enthalten keine Anflugkarten.
- Die DFS hat auf Nachfrage **ausdrücklich bestätigt**, dass diese Bedingungen auch für die BasicVFR gelten. Eine Zusammenarbeit hat sie abgelehnt.
- Der manuelle Abruf per Browser ist erlaubt.

Ziel: ein Werkzeug, das dem Piloten die Verwaltung der Karten abnimmt, ohne selbst auf die DFS zuzugreifen.

## 2. Verworfene Ansätze

| Ansatz | Warum verworfen |
|---|---|
| Browser-Extension, die im Hintergrund die Flugplatzseiten abruft | Ist genauso automatisiert wie der heutige `HttpClient`, nur mit anderem User-Agent. Entscheidend ist, wer den Seitenabruf auslöst, nicht wo der Code läuft. |
| Tool oder Extension lädt die jeweils nächste Adresse in den Browser, der Benutzer bestätigt nur noch mit Enter | Bei der Auslegung zählt die Funktion, nicht die Form: Das Tool entscheidet, was in welcher Reihenfolge geladen wird, und der Mensch ist nur noch Taktgeber. Das ist "Bedienung durch ein elektronisches Tool". Weil die DFS ChartButler kennt und eine Zusammenarbeit abgelehnt hat, sähe so eine Konstruktion zudem nach bewusster Umgehung aus. |
| Passive Extension, die nur vom Benutzer geöffnete Seiten ausliest | Deutlich besser, aber §2 nennt ausdrücklich auch "elektronische" Tools. Ein Content-Script, das die Seite auswertet, lässt sich als Nutzung des Portals durch ein elektronisches Tool auslegen. Das bleibt eine Grauzone. |
| "Alle öffnen" (viele Tabs mit einem Klick) | Ein Klick würde viele Abrufe auslösen. |
| Import-Modus erst im bestehenden ChartButlerCS (Windows Forms) umsetzen, danach migrieren | Der Import-Modus ist im Grunde ein Neuschreiben. Das Fenster für die Aufgabenliste wäre doppelt entstanden, und gebaut werden kann ChartButlerCS nur unter Windows. |
| Python (PyInstaller mit Tkinter oder PySide) | Für den Entwickler einfacher, für die Anwender nicht. Man braucht ohnehin eine Datei je Betriebssystem. PyInstaller-Einzeldateien starten langsam und werden auffällig oft fälschlich als Virus erkannt, was bei Piloten, die von GitHub laden, Supportaufwand erzeugt. Tkinter wirkt altbacken, PySide ist sehr groß. |
| Oberfläche als lokale Webseite (`localhost`) im Browser | Man bräuchte keine Oberflächen-Bibliothek, müsste aber bei jeder Seite den Tab wechseln. Ein natives Fenster kann dagegen "immer im Vordergrund" neben dem Browser stehen. Als Ausweichlösung notiert, falls Avalonia zu schwer wird. |
| Framework-abhängige Veröffentlichung (Runtime separat installieren) | Anwender müssten die .NET-Runtime installieren. Unter Linux und macOS startet die App sonst per Doppelklick scheinbar einfach nicht. |

## 3. Getroffene Entscheidungen

1. **Import-Modus:** Der Benutzer ruft jede Seite selbst im Browser auf und speichert sie mit Strg+S in einen Import-Ordner. ChartAssist wertet nur diese Dateien aus. Es darf Adressen anzeigen, so wie eine Lesezeichenliste, und öffnet pro Benutzeraktion genau eine Seite. Die verbindlichen Leitplanken stehen in IMPORT-MODUS.md, Abschnitt 2.
2. **Keine rechtliche Freigabe:** §8 und §11 der Nutzungsbedingungen (Urheberrecht, Vervielfältigung) betreffen das Speichern der Karten an sich, unabhängig vom Werkzeug. Der Import-Modus verringert das Risiko, beseitigt es aber nicht. Vor dem ersten Release soll die DFS erneut mit diesem konkreten Konzept angefragt werden.
3. **Neuentwicklung in eigenem Repository**, direkt auf .NET 10 und Avalonia. Die technische Basis kommt zuerst, der Import-Modus baut darauf auf.
4. **Name ChartAssist**, Versionszählung ab 1.0. Zur Einordnung: *ChartButler* war das ursprüngliche C/C++-Tool von Jörg Pauly, *ChartButlerCS* die C#-Neuimplementierung, das "CS" diente nur der Unterscheidung. Der neue Name passt zum neuen Prinzip: kein Butler, der Karten holt, sondern ein Assistent für die eigenen Abrufe. Noch zu prüfen: ob der Name auf GitHub oder im Luftfahrtumfeld schon vergeben ist.
5. **Entwicklung unter Linux mit VS Code**, LF-Zeilenenden. ChartButlerCS nutzt CRLF, weil es nur mit Visual Studio unter Windows bearbeitet wird.
6. **Verzeichnisaufbau wie bei ChartButlerCS:** Das Git-Root enthält nur die README und was Git und GitHub brauchen, der gesamte Quellcode liegt in `ChartAssist/`.
7. **Self-contained-Veröffentlichung**, eine Datei je Plattform. Anwender installieren nichts.
8. **Bestehende Kartenverzeichnisse bleiben nutzbar.** Das Format von `.ChartButler.xml` bleibt exakt erhalten, die Datei wird **immer mit CRLF** geschrieben. Dasselbe Kartenverzeichnis soll unter Windows, Linux und macOS verwendbar sein, deshalb gelten die Dateinamensregeln von Windows auf allen Systemen.
9. **ChartButlerCS wird nicht weiterentwickelt**, auch Fehler werden dort nicht mehr behoben. Der Download bleibt verfügbar: Die Software weist auf die Nutzungsbedingungen hin, die Entscheidung über die Nutzung liegt beim Anwender. Ein letztes Release im alten Repository ist vorgemerkt (TECHNISCHE-BASIS.md, Abschnitt 11, Phase 6). Es verweist die Anwender über die vorhandene Versionsprüfung auf ChartAssist, ganz ohne Codeänderung.

10. **Lizenz: "Alle Rechte vorbehalten"**, wie bisher bei ChartButlerCS, keine Open-Source-Lizenz (`LICENSE` im Git-Root). Das Programm ist frei erhältlich, der Quellcode öffentlich einsehbar. Hintergrund: Die Idee und die Originalversion stammen von Jörg Pauly, ChartButlerCS entstand mit seiner Genehmigung unter "alle Rechte vorbehalten". Eine Open-Source-Lizenz würde vorher seine Zustimmung erfordern, soweit Code oder Texte übernommen werden. Das GitHub-Repository ist öffentlich.

## 4. Noch offen

- Ob die Datenbankdatei `.ChartButler.xml` umbenannt wird (TECHNISCHE-BASIS.md, Abschnitt 13).
- Ob das TripKit wegen §8 (abgeleitete Werke) optional wird.
- Test mit Chrome/Edge (IMPORT-MODUS.md, Abschnitt 4, Frage 9).
- Erneute Anfrage bei der DFS.

## 5. Bereits geprüft

Mit echten, von Hand in Firefox gespeicherten DFS-Seiten und einer Kopie eines echten Kartenverzeichnisses wurde bestätigt:
- Die Vorschaubilder gespeicherter Flugplatzseiten sind byteweise identisch mit den lokalen Vorschaudateien.
- Das Karten-PNG gespeicherter Kartenseiten ist identisch mit der lokalen Karte.
- Kartenseiten lassen sich eindeutig über einen Hash zuordnen.
- Beide Speicherformate ("nur HTML" und "komplett") sind auswertbar.
- Die BasicVFR hat einen festen 28-Tage-Zyklus.
- Das Format von `.ChartButler.xml` entspricht den Annahmen.

Einzelheiten: IMPORT-MODUS.md, Abschnitt 4, und TECHNISCHE-BASIS.md, Abschnitt 4.3.
