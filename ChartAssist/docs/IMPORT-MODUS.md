# Konzept: Kartenabgleich ohne eigene Serverzugriffe ("Import-Modus")

Dieses Dokument beschreibt den Nachfolger von ChartButlerCS. Er stellt **selbst keine Anfragen mehr an die DFS-Server**. Der Benutzer ruft die Seiten der BasicVFR AIP selbst im Browser auf und speichert sie. ChartAssist wertet nur diese lokal gespeicherten Dateien aus und pflegt damit wie bisher das Kartenverzeichnis und das TripKit.

Weil das im Grunde ein Neuschreiben ist, entsteht der Nachfolger als **eigenständige Neuentwicklung in einem neuen Repository**, direkt auf moderner Basis (.NET 10, Avalonia, Entwicklung unter Linux mit VS Code). Diese technische Basis beschreibt [TECHNISCHE-BASIS.md](TECHNISCHE-BASIS.md). Sie kommt zuerst, der Import-Modus baut darauf auf. Der vorgeschlagene Name des Nachfolgers ist **ChartAssist** (Hintergrund in TECHNISCHE-BASIS.md).

Status: **Core umgesetzt (Phase 3), Oberfläche folgt (Phase 4).** Grundlage: ChartButlerCS 2.0.1.1 (Commit `ba93927`), aus dem die Fachlogik übernommen wird.

---

## 1. Hintergrund

Die Nutzungsbedingungen des AIS-Portals der DFS (Stand 1. August 2023) enthalten unter anderem:

- **§2:** "Eine Nutzung und Bedienung des AIS-Portal — mit Ausnahme von digitalen Datensätzen (AIXM-Datenformat) — durch elektronische und/oder automatisierte Tools ist nicht gestattet." Weiter heißt es: "Jede andere Nutzung bedarf der vorherigen schriftlichen Zustimmung durch die DFS". Zulässiger Zweck ist die Flugvorbereitung.
- **§3:** Die Nutzung der Sichtflugkarten der AIP VFR für navigatorische Zwecke ist zulässig.
- **§8:** Die Karten sind urheberrechtlich geschützt. Kommerzielle Nutzung ist unzulässig. Abgeleitete Werke bedürfen der Zustimmung.
- **§11:** "Eine Vervielfältigung der Seiten oder ihrer Inhalte bedarf der vorherigen schriftlichen Zustimmung der DFS".

Dass diese Nutzungsbedingungen des AIS-Portals auch für die BasicVFR (DFS AIP, `aip.dfs.de`) gelten, hat die DFS auf Nachfrage **ausdrücklich bestätigt**.

ChartButlerCS lädt Seiten und Karten per `HttpClient` selbst herunter und fällt damit klar unter das Verbot aus §2. Der Nachfolger soll diesen Punkt beseitigen. Das Portal wird dann nur noch durch den Menschen mit seinem Browser benutzt.

§8 und §11 betreffen dagegen das **Speichern der Karten an sich**. Das gilt auch dann, wenn jemand eine Karte manuell speichert oder ausdruckt. Technisch lässt sich das nicht lösen. Es bleibt eine Frage der Auslegung (navigatorische Nutzung nach §3, Privatkopie nach §53 UrhG). Der Import-Modus ist deshalb **keine rechtliche Freigabe**, er reduziert nur das Risiko. Eine erneute Anfrage bei der DFS entfällt (siehe Abschnitt 9).

## 2. Leitplanken

Diese Regeln sind der Kern des Konzepts. Jede Umsetzungsentscheidung muss sich daran messen lassen.

1. **ChartAssist stellt keine einzige Anfrage an DFS-Server.** Die einzige verbleibende Netzwerkverbindung ist die Prüfung auf eine neue Programmversion bei GitHub.
2. **Jeder Seitenabruf wird einzeln vom Benutzer ausgelöst.** ChartAssist darf Adressen kennen und anzeigen, so wie eine Lesezeichenliste. Ein Klick oder Enter auf **einen** Eintrag öffnet **eine** Seite im Standardbrowser. Ausgelöst wird das nur vom Klick selbst, nie von einer Änderung der Auswahl.
3. **Keine Automatisierung des Browsers:**
   - Die nächste Seite wird nie automatisch geöffnet. Nach einem Import wird der nächste offene Eintrag nur *markiert*.
   - Es gibt kein "Alle öffnen".
   - Es gibt keine Browser-Extension.
   - Es werden keine Seiten vorgeladen.
4. **Speichern macht ausschließlich der Benutzer**, mit der normalen Browserfunktion (Strg+S).
5. **Import-Dateien werden nach der Verarbeitung gelöscht.** Es bleiben nur die Karten im Kartenverzeichnis, keine gespeicherten Seiten.
6. **Keine Weitergabe:** Es gibt keine Funktion zum Teilen, Synchronisieren oder Hochladen von Karten.
7. **Der Karteninhalt bleibt unverändert.** Das TripKit skaliert und ordnet die Karten nur an. Es wird nichts zugeschnitten, übermalt oder eingeblendet.

## 3. Ablauf aus Benutzersicht

### 3.1 Einmalige Einrichtung

- Unter "Optionen" gibt es einen **Import-Ordner**. Vorschlag für den Standard: `<Kartenverzeichnis>/Import`, siehe Abschnitt 5.3.
- Beim ersten Speichern im Browser wählt der Benutzer diesen Ordner. Empfohlen wird das Format **"Webseite, nur HTML"**. "Webseite, komplett" (Standard in Firefox) wird aber ebenfalls unterstützt, siehe Abschnitt 4.
- Der Dateiname spielt keine Rolle. Alle DFS-Seiten heißen "AIP VFR Germany", der Browser schlägt also immer denselben Namen vor. ChartAssist erkennt die Seitenart am Inhalt und entfernt jede Datei gleich nach der Verarbeitung, sodass der Name beim nächsten Speichern wieder frei ist (Abschnitt 5.3).

### 3.2 Kartenabgleich

1. Der Benutzer klickt im Hauptfenster auf "Karten aktualisieren". Es öffnet sich das nicht-modale Fenster **"Kartenabgleich"**. Die Aufgabenliste enthält einen Eintrag je abonniertem Flugplatz, der auf den Permalink aus `AFCharts.Crypt` verlinkt. Der erste Eintrag ist markiert.
2. Ein Klick oder Enter auf den Eintrag öffnet die Flugplatzseite im Browser. Der Benutzer drückt Strg+S und dann Enter.
3. ChartAssist erkennt die neue Datei im Import-Ordner. **Jede** DFS-Seite enthält das Datum "Effective:". Die erste gespeicherte Seite entscheidet deshalb, ob überhaupt etwas zu tun ist:
   - **Unverändertes Datum** (gleich `AIP.LastUpdate`) bei gleicher Programmversion: Meldung "Keine Aktualisierung notwendig", fertig. Das entspricht dem Verhalten von ChartButlerCS in `DFS_CheckForNewCharts`. Ein eigener Schritt für die Startseite ist nicht nötig.
   - **Neues Datum:** Die Flugplatzseite wird ausgewertet, siehe Schritt 4.
4. ChartAssist vergleicht die Vorschaubilder der Flugplatzseite mit den lokalen Vorschaudateien.
   - **Alles aktuell:** Der Eintrag wird abgehakt, der nächste offene Flugplatz wird markiert.
   - **Geänderte oder neue Karten:** Sie erscheinen als Untereinträge "Bitte speichern: EDxx …", mit direktem Link auf die Kartenseite (Abschnitt 4, Frage 5).
   - **Entfallene Karten** werden sofort entfernt.
5. Der Benutzer öffnet eine geänderte Karte und speichert sie. Zum Öffnen doppelklickt er den Eintrag in der Liste oder klickt die Karte in der noch geöffneten Flugplatzseite an. ChartAssist ordnet die Kartenseite sicher zu (Abschnitt 4, Frage 4), übernimmt das PNG und hakt den Eintrag ab. Sobald alle Karten eines Flugplatzes da sind, wird das TripKit neu erzeugt.
6. Wenn alle Einträge erledigt sind, gilt:
   - `AIP.LastUpdate` wird auf das Effective-Datum gesetzt.
   - Die Übersicht der aktualisierten Karten wird angezeigt, wie bisher.

Typischer Aufwand pro Zyklus: je Seite ein Klick, Strg+S und Enter, also eine Seite je Flugplatz und eine je geänderter Karte. Hat sich nichts geändert, genügt eine einzige Seite.

### 3.3 Neuen Flugplatz hinzufügen

Die Suche über die gecachte Flugplatzliste (`DFS_SearchField` in ChartButlerCS) benötigt Serverzugriffe und entfällt. Stattdessen läuft es so:

1. "Neuer Flugplatz" öffnet das Fenster "Kartenabgleich" mit dem Eintrag "Flugplatzverzeichnis". Er verlinkt auf den Permalink `DFS_PermalinkAirfields`.
2. Der Benutzer navigiert im Browser zum gewünschten Flugplatz und speichert dessen Seite.
3. ChartAssist erkennt eine Flugplatzseite, die noch nicht in der Datenbank ist. ICAO-Code und Name stehen in der Überschrift der Seite (Abschnitt 4, Frage 3). ChartAssist fragt nach: "Flugplatz EDxx – Name abonnieren?".
4. Alle Karten des Platzes erscheinen als Einträge "Bitte speichern". Beim ersten Hinzufügen ist das einmalig mehr Aufwand. Bequem geht es über den "Weiter"-Pfeil der DFS-Kartenseite: ein Klick, dann Strg+S und Enter. Seiten, die keine offene Aufgabe haben, ignoriert ChartAssist mit einem Hinweis.

---

## 4. Ergebnisse der Prüfung an gespeicherten Seiten

Die folgenden Punkte wurden an **echten, manuell gespeicherten Seiten** geprüft. Grundlage: Firefox, 29.09.2026, Effective 17 SEP 2026. Gespeichert wurden die Startseite, die Flugplatzseite EDFM und die Kartenseite "EDFM Mannheim City 1", jeweils als "Webseite, nur HTML" und als "Webseite, komplett". Die Parser-Logik aus `CServerConnectionDFS.cs` wurde dafür nachgebildet und auf die Dateien angewendet.

**Diese Dateien dürfen nicht ins Repository** (§11). Sie liegen lokal in `ChartAssist/testdata/` (gespeicherte Seiten) und `ChartAssist/testcharts/` (Kartenverzeichnis), per `.gitignore` ausgeschlossen (TECHNISCHE-BASIS.md 7.4). Da alle Seiten denselben Dateinamen vorschlagen, müssen sie beim Sammeln von Testdaten von Hand umbenannt werden.

| # | Frage | Ergebnis (Firefox) | Folge für die Umsetzung |
|---|---|---|---|
| 1 | Enthält die gespeicherte Datei das komplette HTML mit Vorschauen (`document-icon`), PNG (`imgAIP`) und `const myPermalink`? | **Ja**, in beiden Formaten. Die Vorschau-Bytes sind in "nur HTML" und "komplett" identisch, das Karten-PNG ebenso. | Das Konzept ist umsetzbar. |
| 2 | Weicht "komplett" im Markup ab? | **"Nur HTML"** ist der Originalquelltext. `DFS_GetChartLinks` (inkl. `XmlDocument`), der Datums-Regex und der `imgAIP`-Regex funktionieren **unverändert**. **"Komplett"** ist das vom Browser serialisierte DOM, inklusive Änderungen durch Erweiterungen (hier fast 200 Einträge von Dark Reader). `<img>` ist nicht geschlossen, deshalb scheitert `XmlDocument.LoadXml` für jedes `document-item`. Die Regexe für Datum und `imgAIP` funktionieren weiterhin. | Den Parser für `document-item` von `XmlDocument` auf tolerante Regexe umstellen (Abschnitt 5.2). Dann gehen beide Formate. |
| 3 | Lassen sich ICAO-Code und Flugplatzname aus der Flugplatzseite ablesen? | **Ja:** `<div class="headlineText left">…<span lang="de">Mannheim City EDFM</span>`. Das ist dasselbe Format "Name ICAO" wie in der bisherigen Flugplatzliste. | ICAO = letztes Wort, Name = Rest (wie bisher `airfield.Key.Remove(Length - 5)`), danach `Utility.GetFilenameFor`. |
| 4 | Woran lässt sich eine Kartenseite einer Karte zuordnen? | **Eindeutig über `const myURL`:** `"AD/758fd4e2…/EDFM Mannheim City 1"`. Der Hash in der Mitte ist genau der Dateiname aus dem `href` des `document-link` der Flugplatzseite (`../pages/758fd4e2….html`). Der Name steht außerdem in `<div class="headlineText float-start">`. `myPermalink` der Kartenseite (`pages/P00363.html`) taucht auf der Flugplatzseite dagegen nicht auf. | Zuordnung über den Hash. Der Name dient nur zur Anzeige und Plausibilitätsprüfung. Eine Auswahl durch den Benutzer ist nicht nötig. |
| 5 | Lässt sich die absolute URL einer Seite ermitteln? | Es gibt keinen `saved from url`-Kommentar und kein `<base>`. In "komplett" sind die `href` aber **absolut** (`https://aip.dfs.de/BasicVFR/2026SEP17/pages/<hash>.html`). In "nur HTML" sind sie relativ (`../pages/<hash>.html`). Der Ordnername `2026SEP17` entspricht dem Effective-Datum. | Kartenlink = absoluter `href`, sonst `https://aip.dfs.de/BasicVFR/<yyyyMMMdd des Effective-Datums>/pages/<hash>.html`. Das Schema ist bisher nur an **einer** Ausgabe bestätigt. Als Rückfallweg bleibt der Klick in der Flugplatzseite. Der Link gilt nur bis zur nächsten Ausgabe, wird also nicht gespeichert. |
| 6 | Enthält auch die Flugplatzseite das Effective-Datum? | **Ja**, Startseite, Flugplatzseite und Kartenseite enthalten es gleichermaßen. | Die Startseite als eigener Schritt entfällt (Abschnitt 3.2). |
| 7 | Zeichenkodierung? Umlaute? Merkt sich der Browser Ordner und Format? | UTF-8 (`<meta charset="utf-8">`). Die DFS **umschreibt Umlaute** in allen relevanten Feldern (Überschrift "Muenchen EDDM", Kartennamen, `myURL`). "ü" kommt nur in festen Oberflächentexten vor. **Firefox merkt sich** das zuletzt gewählte Format und den Zielordner. | Mit UTF-8 lesen, sonst ist nichts nötig. Nach der ersten Einrichtung sind pro Seite nur Strg+S und Enter nötig. |
| 8 | Temporäre Dateinamen beim Speichern, wann ist die Datei vollständig? | **Nicht beobachtbar**, das Speichern geht zu schnell. | `*.part` u. ä. ignorieren und prüfen, ob die Dateigröße über zwei Abfragen stabil bleibt (Abschnitt 5.3). |
| 9 | Chrome/Edge | **Offen.** | Vor dem Release mit denselben Seiten wiederholen. |

Weitere Beobachtungen:
- Kartennamen ohne ICAO-Präfix kommen vor ("AD 2-67"). Das bestehende Voranstellen des ICAO-Codes bleibt nötig.
- Die Kartenseite enthält `myPrevURL`/`myNextURL` (Hash der vorigen/nächsten Seite). Das wird nicht benötigt, erklärt aber den "Weiter"-Pfeil aus Abschnitt 3.3.
- Zweiter geprüfter Flugplatz: EDDM, 9 Karten, nur als "komplett" gespeichert. Auch hier passen Kartenseite und Flugplatzseite über den Hash zusammen.

**Abgleich gegen ein echtes Kartenverzeichnis** (`testcharts/`, von ChartButlerCS 2.0.1.1 gepflegt, letzter Abgleich mit Effective 20 AUG 2026):
- Die 7 Vorschaubilder der gespeicherten EDFM-Seite (Effective 17 SEP 2026) sind **byteweise identisch** mit den lokalen Vorschaudateien, in beiden Speicherformaten. Der Vorschauvergleich funktioniert mit gespeicherten Seiten also genauso wie bisher mit Serverantworten, und EDFM würde korrekt als "aktuell" erkannt.
- Das PNG der gespeicherten Kartenseite "EDFM Mannheim City 1" ist byteweise identisch mit der lokalen Karte, ebenfalls in beiden Formaten.
- Der Permalink in `Crypt` (`C01A45.html`) stimmt mit dem letzten Segment von `myPermalink` der Flugplatzseite überein. Bekannte Flugplätze werden also wie geplant über den Permalink zugeordnet.
- Bei allen 29 Karten gilt `Cname` = Name aus `Crypt` + `.png`. Sonderzeichen außer Leerzeichen, Punkt und Bindestrich kommen in keinem Namen vor.
- **Die BasicVFR hat einen festen 28-Tage-Zyklus**, der 14 Tage gegenüber AIRAC versetzt ist. Belegt ist das durch die gespeicherten Aktualisierungen (30.04., 28.05., 25.06., 23.07., 20.08.2026), das Effective-Datum der Testseiten (17.09.2026) und den Ordnernamen `2026MAR05`. Der Termin der nächsten Ausgabe lässt sich damit lokal berechnen (5.6).

---

## 5. Technische Umsetzung

Der Import-Modus wird als Neuentwicklung auf der Basis aus [TECHNISCHE-BASIS.md](TECHNISCHE-BASIS.md) umgesetzt: .NET 10, Avalonia, eine UI-unabhängige Core-Bibliothek mit xUnit-Tests. Die Klassen- und Dateinamen unten beziehen sich auf die dortige Zielarchitektur (TECHNISCHE-BASIS.md, Abschnitt 2). Die Datenbank ist das eigene Modell `ChartDatabase` (TECHNISCHE-BASIS.md 4.3), nicht mehr das typisierte DataSet.

### 5.1 Was aus ChartButlerCS übernommen wird

| ChartButlerCS 2.0.1.1 | Im Nachfolger |
|---|---|
| `DFS_GetChartLinks`, `DFS_DownloadChartFromURL` (Parsing-Teil), Effective-Parsing aus `DFS_ConnectionWorker`, `DFS_CreateDateFromString` | `Dfs/DfsPageParser.cs` (5.2), arbeitet auf gespeicherten Dateien statt auf Serverantworten |
| `DFS_UpdateCharts`, `DFS_AddNewField`, `DFS_DownloadAndCheckChart`, `DFS_RemoveOrphanCharts` | `Import/ChartImport.cs` (5.4), aufgeteilt in "prüfen" (Flugplatzseite) und "übernehmen" (Kartenseite) |
| `DFS_UpdateTripKitCharts` | `TripKit/TripKitBuilder.cs`, Logik unverändert, technisch auf SkiaSharp und PDFsharp 6 umgestellt (TECHNISCHE-BASIS.md 4.1) |
| `Utility.BuildChartPath`, `BuildChartPreviewPath`, `GetFilenameFor`, `FileEquals` | `Utility.cs`, unverändert |
| `rebuildDataBaseFromChartDir`, `readDataBase`, `updateDataBase` | `ChartFolder.cs` (TECHNISCHE-BASIS.md 4.3, 4.4) |
| `Resources.resx`: `DFS_PermalinkBaseURL`, `DFS_PermalinkMain`, `DFS_PermalinkAirfields` | `Dfs/DfsUrls.cs`, nur noch als **Links zum Öffnen im Browser**, dazu neu `DFS_BaseURL` |
| `CServerConnection.cs` (`HttpClient`, Zertifikatsprüfung, `doUpdate` mit Worker-Thread), `dlgStatus`, `Utility.GetURLText`, `DFS_GetRedirectURLText`, `DFS_SearchField` samt Flugplatzlisten-Cache, GAT24-Migration | **nicht übernommen** |

### 5.2 `DfsPageParser` (Core)

Eine statische Klasse ohne UI-Abhängigkeit. Sie nimmt HTML-Text entgegen und liefert strukturierte Daten zurück:

```csharp
public enum DfsPageKind { Unknown, OtherDfsPage, AirfieldPage, ChartPage }

public static class DfsPageParser
{
    // Erkennung anhand eindeutiger Merkmale, in dieser Reihenfolge:
    //   Kartenseite:    id="imgAIP"
    //   Flugplatzseite: class="document-item"
    //   sonstige DFS-Seite (Startseite, Verzeichnis): nur "expand-header">Effective:
    public static DfsPageKind DetectKind(string html);

    public static DateTime ParseEffectiveDate(string html);          // auf jeder DFS-Seite vorhanden
    public static DfsAirfieldPage ParseAirfieldPage(string html);    // bisher DFS_GetChartLinks
    public static DfsChartPage ParseChartPage(string html);          // bisher DFS_DownloadChartFromURL
    public static DateTime CreateDateFromString(string dateString);  // bisher DFS_CreateDateFromString (unverändert)
}

public sealed record DfsAirfieldPage(
    DateTime Effective,
    string Permalink,                    // letztes Segment von myPermalink, wie bisher in Crypt
    string Icao, string Name,            // aus <div class="headlineText left"><span lang="de">Name ICAO</span>
    IReadOnlyList<DfsChartEntry> Charts);

public sealed record DfsChartEntry(
    string Name,                         // <span class="document-name" lang="de">, noch ohne ICAO-Präfix
    string Hash,                         // Dateiname ohne .html aus dem href des document-link
    string Href,                         // absolut ("komplett") oder relativ ("nur HTML")
    byte[] PreviewPng);                  // <span class="document-icon"><img src="data:image/png;base64,…">

public sealed record DfsChartPage(
    DateTime Effective,
    string Hash,                         // mittleres Segment von const myURL = "AD/<hash>/<Name>"
    string Name,                         // <div class="headlineText float-start">
    DateTime? Date,                      // <div class="headlineText float-end">
    byte[] Png);                         // <img id="imgAIP" … src="data:image/png;base64,…">
```

Hinweise:
- **`document-item` per Regex statt `XmlDocument` auswerten.** Nur so werden beide Speicherformate unterstützt (Abschnitt 4, Frage 2). Vorgehen:
  - den Abschnitt zwischen `<li class="document-item">` und `</li>` herausschneiden, wie bisher,
  - darin einzeln suchen, jeweils unabhängig von der Attributreihenfolge und von zusätzlichen Attributen:
    - `href` des `<a>` mit `class="document-link"`,
    - den `<span>` mit `class="document-name"` und `lang="de"`,
    - das `src` des `<img>` mit dem Präfix `data:image/png;base64,`.
  - `&amp;` und andere HTML-Entities im Namen dekodieren (`WebUtility.HtmlDecode`).

  Dieser Ansatz wurde an den Testdaten bereits erprobt (Python-Nachbildung): gleiche Ergebnisse für beide Formate.
- **Kartenlink bilden** (Abschnitt 4, Frage 5):
  - Ist `Href` absolut, wird er direkt verwendet.
  - Sonst `DfsUrls.BaseUrl` (`https://aip.dfs.de/BasicVFR/`) + Effective-Datum als `yyyyMMMdd` in Großbuchstaben mit invarianter Kultur (z. B. `2026SEP17`) + `/pages/<Hash>.html`.
- Das Voranstellen des ICAO-Codes vor den Kartennamen (`if (!name.StartsWith(ICAO)) name = ICAO + " " + name;`) bleibt wie bisher. Es passiert in `ChartImport`, nachdem der Flugplatz zugeordnet ist.
- Ist die Kartenliste leer oder schlägt die Erkennung fehl, meldet der Parser einen Fehler (Exception oder Ergebnis mit Fehlertext). Er liefert niemals eine leere Liste als gültiges Ergebnis, weil sonst das Entfernen verwaister Karten alle Karten löschen würde.
- **Tests** (TECHNISCHE-BASIS.md 11, Phase 3):
  - Im Repository nur **synthetische Fixtures**: von Hand geschriebene Minimalseiten mit derselben HTML-Struktur, je als "nur HTML" und "komplett" (mit nicht geschlossenem `<img>` und fremden Attributen), mit kleinen generierten PNGs.
  - Zusätzlich lokale Tests gegen `testdata/` und `testcharts/`, die übersprungen werden, wenn die Ordner fehlen. Erwartet werden:
    - für EDFM 7 Karten mit in beiden Formaten identischen Vorschau-Bytes, für EDDM 9 Karten,
    - mit einer Kopie von `testcharts/`: gespeicherte EDFM-Seite → "alles aktuell", keine Dateiänderung,
    - dieselbe Kopie ohne `.EDFM Mannheim City 1.png_preview.png` → diese Karte ausstehend; danach die gespeicherte Kartenseite → PNG unverändert, Vorschau wiederhergestellt, TripKit neu erzeugt.

### 5.3 Import-Ordner (`ImportFolderScanner`, Core)

- **Neue Einstellung** `ImportFolder` in `Settings`, auswählbar im `OptionsWindow`.
  - Ist sie leer, gilt `<Kartenverzeichnis>/Import`. Der Ordner wird bei Bedarf angelegt.
  - `ChartFolder.Rebuild()` überspringt diesen Ordner. ChartButlerCS hätte ihn als Flugplatz "Impo" interpretiert. Deshalb werden allgemein alle Verzeichnisse übersprungen, die nicht dem Muster `^[A-Z0-9]{4} - ` entsprechen (TECHNISCHE-BASIS.md 4.4).
- **Erkennung neuer Dateien:** `ImportFolderScanner.Scan()` liefert die fertig geschriebenen Dateien. Die UI ruft die Methode per `DispatcherTimer` etwa alle 1–2 Sekunden auf, **nur solange das `AbgleichWindow` geöffnet ist**.
  - Polling statt `FileSystemWatcher`: einfach, zuverlässig auf allen Plattformen und ohne Ereignisse aus fremden Threads. Bei einem Ordner mit wenigen Dateien kostet es praktisch nichts.
  - Verarbeitet werden `*.html` und `*.htm`, die älteste Datei zuerst.
  - Übersprungen werden `*.part`, `*.crdownload` und `*.tmp` sowie Dateien, deren Größe sich seit der letzten Abfrage geändert hat. Wie lange Firefox beim Speichern eine temporäre Datei nutzt, war nicht beobachtbar (Abschnitt 4, Frage 8). Die Größenprüfung deckt das ab.
- **Nach erfolgreicher Verarbeitung** werden die Datei und ein zugehöriger Ressourcenordner (`<Name>_files` bei "komplett") gelöscht.
- **Bei Fehlern** wird die Datei nach `Import/Fehler/` verschoben, damit sie nicht erneut verarbeitet wird. Die Ursache erscheint in der Aufgabenliste.
- Fremde HTML-Dateien (`DetectKind` = `Unknown`) werden ebenso behandelt.
- **Gleiche Dateinamen:** Alle DFS-Seiten haben den Titel "AIP VFR Germany", der Browser schlägt also immer "AIP VFR Germany.html" vor. Das ist beabsichtigt unkritisch:
  - Die Seitenart wird ausschließlich am Inhalt erkannt (`DetectKind`), nie am Dateinamen.
  - Verarbeitete Dateien verschwinden innerhalb von 1–2 Sekunden, der Name ist beim nächsten Speichern also wieder frei.
  - Speichert der Benutzer schneller, als ChartAssist verarbeitet, fragt Firefox vor dem Überschreiben nach. Geht dabei eine noch nicht verarbeitete Seite verloren, bleibt nur deren Aufgabe offen. Die Datenbank ist davon nicht betroffen.
  - Varianten wie "AIP VFR Germany(1).html" werden genauso verarbeitet.
  - ChartAssist muss dafür nicht in den Browser eingreifen. Eine Umbenennung beim Speichern wäre ohne Extension auch gar nicht möglich.

### 5.4 `ChartImport` (Core)

Die Klasse arbeitet auf dem geladenen `ChartDatabase`-Modell und dem Kartenverzeichnis. Sie hält die Liste der in dieser Sitzung aktualisierten Karten (für das `UpdateOverviewWindow`) und den Zustand der Abgleich-Sitzung. Jeder Aufruf liefert ein Ergebnisobjekt mit Meldungstext und geänderten Aufgaben zurück. Die UI zeigt es an und speichert die Datenbank über `ChartFolder`. Die Aufrufe sind synchron und laufen in der UI per `await Task.Run(...)` (TECHNISCHE-BASIS.md 4.2).

**Sitzungszustand, nur im Speicher:**
- das Effective-Datum der ersten in der Sitzung gespeicherten Seite. Es dient auch als Ersatzdatum, wenn eine Karte kein Datum liefert (bisher die Variable `Update`). Weicht eine spätere Seite davon ab, hat die DFS während der Sitzung eine neue Ausgabe veröffentlicht. Dann bricht die Sitzung mit einem Hinweis ab und muss neu gestartet werden,
- je Flugplatz der Status "offen", "wartet auf Karten", "erledigt" oder "Fehler",
- je ausstehender Karte, **nach Hash geordnet**: Flugplatz, Kartenname, Permalink, Vorschau-Bytes von der Flugplatzseite und der Kartenlink.

**`CheckEffectiveDate(DateTime)`**, für jede importierte Seite zuerst aufgerufen:
- Bei der ersten Seite der Sitzung wird das Effective-Datum mit `AipLastUpdate` und der Programmversion verglichen, wie bisher in `DFS_CheckForNewCharts`. Sind beide gleich, heißt das "nichts zu tun", und die Sitzung endet.
- Bei jeder weiteren Seite wird es mit dem Datum der Sitzung verglichen (siehe oben).
- Eine sonstige DFS-Seite (Startseite, Verzeichnis) wird danach ohne weitere Verarbeitung gelöscht.
- Beim Hinzufügen eines neuen Flugplatzes entfällt der Vergleich mit `AipLastUpdate`.

**`ApplyAirfieldPage(DfsAirfieldPage)`**, abgeleitet aus `DFS_UpdateCharts` und `DFS_AddNewField`:
1. **Flugplatz zuordnen:**
   - Zuerst über den Permalink: ein Flugplatz, dessen Karten diesen `AirfieldPermalink` haben.
   - Sonst über den ICAO-Code. Dann gilt, wie bisher, die Warnung "Permalink hat sich verändert".
   - Sonst ist es ein neuer Flugplatz. Nach Rückfrage in der UI wird er angelegt. Dabei werden keine Einträge in `Updates` erzeugt, entsprechend `init = true` in ChartButlerCS.
   - Umgesetzt: Verzeichnis und Datenbankeintrag entstehen erst mit der ersten übernommenen Karte. Bricht der Benutzer vorher ab, bleibt kein leerer Flugplatz zurück.
2. **Jede Karte** wird mit dem Algorithmus aus `DFS_DownloadAndCheckChart` geprüft: Karte über Permalink und Namen bzw. über den Dateinamen (`Cname`) suchen und die Vorschau-Bytes mit der lokalen Vorschaudatei vergleichen. Bei einer Abweichung wird die Karte **als ausstehend gemerkt statt heruntergeladen**.
3. **Verwaiste Karten entfernen** (bisher `DFS_RemoveOrphanCharts`), allerdings nur bei nicht-leerer Kartenliste (5.2).
4. **Abschluss:**
   - Ohne ausstehende Karten wird der Flugplatz als erledigt markiert. Wurden Karten entfernt, wird vorher das TripKit neu erzeugt.
   - Mit ausstehenden Karten wechselt der Status auf "wartet auf Karten".

**`ApplyChartPage(DfsChartPage)`**
1. Die ausstehende Karte **über den Hash** zuordnen (Abschnitt 4, Frage 4). Weicht der Name der Kartenseite vom erwarteten Namen ab, wird das nur protokolliert. Gibt es zum Hash keine ausstehende Karte, erscheint der Hinweis "Karte nicht angefordert", und nichts wird geändert. Das passiert z. B. beim Durchblättern mit dem "Weiter"-Pfeil.
2. Übernehmen, wie im zweiten Teil von `DFS_DownloadAndCheckChart`:
   - PNG und die gemerkte Vorschau schreiben (die Vorschau als versteckte Datei),
   - `CreationDate` und `LastUpdate` setzen,
   - `Updates` pflegen (höchstens 5 Einträge),
   - neue Karte ins Modell aufnehmen,
   - die Karte in die Liste der aktualisierten Karten eintragen.
3. Wenn die letzte ausstehende Karte des Flugplatzes da ist, erzeugt `TripKitBuilder` das TripKit neu. Danach ist der Flugplatz erledigt.

Wichtig: **Die Vorschau wird erst geschrieben, wenn die Karte selbst übernommen ist.** Bricht der Benutzer ab, gilt die Karte beim nächsten Abgleich wieder als geändert.

**Sitzungsende**
- Sind alle Flugplätze erledigt, wird `AipLastUpdate` auf das Effective-Datum gesetzt, wie bisher am Ende von `DFS_CheckForNewCharts`.
- Ein unvollständiger Abgleich ändert `AipLastUpdate` nicht. Beim nächsten Mal erscheinen also wieder alle Flugplätze. Bereits aktualisierte Plätze sind dann sofort "aktuell" und kosten nur einmal Speichern.

### 5.5 `AbgleichWindow` (Avalonia, nicht-modal)

Inhalt:
- Kurze Anleitung: "Eintrag doppelklicken → Seite im Browser mit Strg+S in den Import-Ordner speichern (empfohlen: 'Webseite, nur HTML')".
- Wird eine Seite als "komplett" gespeichert (erkennbar am Ordner `<Name>_files`, `ImportFolderScanner.IsSavedComplete`), erscheint ein kurzer Tipp: "Speichern Sie als 'Webseite, nur HTML'." Grund: "komplett" ist das vom Browser dargestellte DOM, das Erweiterungen wie Dark Reader verändern; "nur HTML" ist der Originalquelltext. "Komplett" bleibt trotzdem unterstützt, weil es in Firefox der Standard ist.
- Aufgabenliste (`TreeView` oder `ListBox`) mit Aufgabe und Status. Karten erscheinen eingerückt unter ihrem Flugplatz. Regel für alle Einträge: **abgehakt, sobald die Seite, auf die der Link zeigt, übernommen ist**. Ein Flugplatz also mit seiner Seite (fehlende Karten stehen eingerückt darunter), eine Karte mit ihrer Kartenseite, Erledigtes bleibt abgehakt stehen, Karten in der Reihenfolge der Flugplatzseite. Der Eintrag "→ Flugplatzverzeichnis öffnen" ist keine Aufgabe, sondern nur der Einstieg zum Navigieren; er muss nicht gespeichert werden und wird nie abgehakt. Er steht beim Hinzufügen und beim Abgleich, wenn Flugplätze ohne bekannten Permalink (z. B. nach Wiederherstellung der Datenbank) keinen eigenen Link haben.
- Buttons "Im Browser öffnen" (öffnet genau den markierten Eintrag über `Launcher.LaunchUriAsync`, TECHNISCHE-BASIS.md 5.4), "Import-Ordner öffnen" und "Schließen".
- Umschalter "Immer im Vordergrund" (`Topmost`), damit das Fenster neben dem Browser sichtbar bleibt.
- Das Fenster hat keinen Besitzer. Sonst käme beim Klick hinein auch das Hauptfenster vor den Browser. Es schließt sich mit dem Hauptfenster.
- Statuszeile mit der letzten Meldung.

Verhalten:
- Klick oder Enter öffnet den Link des Eintrags. Nach einem erfolgreichen Import wird der nächste offene Eintrag **markiert, aber nicht geöffnet** (Leitplanke 3). Deshalb reagiert das Öffnen auf den Klick (`Tapped`), nicht auf die Auswahl. Pfeiltasten verschieben nur die Markierung; das Fenster verarbeitet sie selbst, weil der Neuaufbau der Liste nach jedem Import den Fokus des Eintrags löscht.
- Der Pfad des Import-Ordners ist selektierbar und hat einen Knopf "Kopieren", z. B. für den Speichern-Dialog des Browsers.
- Rechtsklick auf einen Eintrag: Kontextmenü "Link kopieren", um die Adresse selbst in den Browser einzufügen. Ein Rechtsklick öffnet nie eine Seite.
- Der Import-Timer läuft nur, solange das Fenster offen ist. Während eine Datei verarbeitet wird, pausiert er.
- Nach jedem Import wird die Datenbank über `ChartFolder` gespeichert, und das Hauptfenster aktualisiert seinen Baum. Die Datenbank ist damit auch bei einem Abbruch konsistent.
- Solange das Fenster offen ist, sind im Hauptfenster "Flugplatz löschen" und "Optionen" deaktiviert. So kann das Kartenverzeichnis nicht mitten in der Sitzung wechseln.
- Ist alles erledigt (Abgleich vollständig, keine Aktualisierung nötig, beim Hinzufügen alle Karten des Platzes übernommen), schließt sich das Fenster nach kurzer Pause selbst. Nach einem Abbruch bleibt es offen.
- Beim Schließen kommt das Hauptfenster in den Vordergrund (Activate und kurz `Topmost`, weil GNOME Activate ablehnt, solange ChartAssist nicht aktiv ist). Es erscheint das `UpdateOverviewWindow` mit den aktualisierten Karten, falls es welche gibt, sonst nach automatischem Schließen eine kurze Meldung mit dem Ergebnis.

### 5.6 Weitere Punkte

- **`MainWindow`:** "Karten aktualisieren" und "Neuer Flugplatz" öffnen das `AbgleichWindow` im jeweiligen Modus. Eine ICAO-Eingabe gibt es nicht mehr. Das Hinweisbanner spricht von "Kartenabgleich" statt "Abgleich mit dem Server".
- **Erinnerung an neue Ausgaben:** ChartButlerCS mahnt einen Abgleich an, wenn der letzte älter als 28 Tage ist. Weil die BasicVFR einen festen 28-Tage-Zyklus hat (Abschnitt 4), kann der Nachfolger genauer sein: Nächste erwartete Ausgabe = `AipLastUpdate` + 28 Tage. Ab diesem Tag erscheint das Banner, z. B. "Seit 17.09.2026 ist eine neue Ausgabe der BasicVFR zu erwarten". Das ist nur eine Erinnerung. Ob sich tatsächlich etwas geändert hat, entscheidet weiterhin das Effective-Datum der ersten gespeicherten Seite. Die Statusleiste des Hauptfensters zeigt dauerhaft die Ausgabe des Kartensatzes und die nächste planmäßige Ausgabe. Weicht die DFS einmal vom Zyklus ab, geht dadurch nichts verloren.
- **Rechtlicher Hinweis beim ersten Start**, neu formuliert:
  - ChartAssist greift selbst nicht auf die DFS zu,
  - der Benutzer ruft die Seiten selbst im Browser auf und speichert sie,
  - weiterhin der Hinweis auf die Nutzungsbedingungen (§8, §11) und auf die eigene Verantwortung für die Aktualität.
- **Hilfe:** den Ablauf beschreiben, am besten mit einem Screenshot des Speichern-Dialogs.
- **Datenbank:** Das Dateiformat bleibt unverändert (TECHNISCHE-BASIS.md 4.3). `DataSource` bleibt `"DFS"`, denn die Daten sind identisch, nur der Weg ist anders. Datenbanken mit `DataSource` `null` oder `"GAT24"` werden nicht mehr migriert. Ein Hinweis bittet, die Plätze neu zu abonnieren.
- **Übernahme bestehender Kartenverzeichnisse** aus ChartButlerCS: siehe TECHNISCHE-BASIS.md, Abschnitt 10.

---

## 6. Umsetzungsreihenfolge

Die Gesamtreihenfolge steht in TECHNISCHE-BASIS.md, Abschnitt 11. **Zuerst entsteht die technische Basis** (Phasen 0–2: Repository, Grundgerüst mit Release-Pipeline, Datenmodell, `ChartFolder`, TripKit). Der Import-Modus folgt in den Phasen 3 und 4:

1. **Abschnitt 4 abschließen:** Für Firefox ist das erledigt. Offen ist nur Chrome/Edge (Frage 9). Das kann parallel zu den Phasen 0–2 geschehen.
2. **Phase 3, Core:**
   - `DfsPageParser` mit synthetischen Fixtures und lokalen Tests gegen `testdata/` (5.2),
   - `ImportFolderScanner` (5.3),
   - `ChartImport` mit Tests in einem temporären Kartenverzeichnis (5.4).
3. **Phase 4, Oberfläche:** `AbgleichWindow` (5.5) zusammen mit `MainWindow` und `OptionsWindow` (Import-Ordner).
4. **Vor dem Release:** Checkliste in Abschnitt 7 durchgehen, Hilfe und README mit dem Ablauf füllen.

## 7. Test-Checkliste

Die Logik (Parser, Import, Datenbank) wird mit xUnit getestet (TECHNISCHE-BASIS.md 11). Diese Checkliste gilt für den manuellen Test der fertigen Anwendung auf Windows, Linux und macOS, jeweils mit Firefox und Chrome/Edge. Allgemeine Punkte stehen in TECHNISCHE-BASIS.md, Abschnitt 12.

- [ ] Erste Flugplatzseite mit unverändertem Effective-Datum → "Keine Aktualisierung notwendig"
- [ ] Erste Flugplatzseite mit neuem Datum → wird ausgewertet, übrige Flugplätze bleiben als Aufgaben offen
- [ ] Startseite statt Flugplatzseite gespeichert → Effective-Datum wird übernommen, Datei gelöscht
- [ ] Seiten mit abweichendem Effective-Datum innerhalb einer Sitzung → Abbruch mit Hinweis
- [ ] Flugplatzseite, alles aktuell → Eintrag abgehakt, keine Dateiänderung, Datenbank unverändert
- [ ] Flugplatzseite mit geänderter Karte → ausstehende Karte; nach Speichern der Kartenseite PNG, Vorschau und TripKit aktualisiert, Eintrag unter "Aktualisierungen"
- [ ] Auf dem Server entfallene Karte → lokal gelöscht, TripKit neu erzeugt
- [ ] Neuer Flugplatz über das Flugplatzverzeichnis → Rückfrage, Verzeichnis angelegt, alle Karten ausstehend
- [ ] Kartenseite ohne passende offene Aufgabe → Hinweis, keine Änderung
- [ ] Mehrere ausstehende Karten, Zuordnung der Kartenseite über den Hash korrekt, auch in beliebiger Reihenfolge
- [ ] Direkter Kartenlink aus der Aufgabenliste öffnet die richtige Kartenseite, für "nur HTML" (abgeleitete URL) und "komplett" (absoluter `href`)
- [ ] Geänderter Permalink eines Flugplatzes → Warnung, Zuordnung über ICAO-Code
- [ ] "Webseite, komplett" gespeichert → korrekt verarbeitet, Ressourcenordner `<Name>_files` entfernt
- [ ] Fremde HTML-Datei, abgeschnittene Datei, `.part`-Datei → nicht verarbeitet bzw. nach `Fehler/` verschoben
- [ ] Leere oder nicht erkannte Kartenliste → **keine** Karten gelöscht
- [ ] Fenster mitten im Abgleich geschlossen → Datenbank konsistent; beim nächsten Abgleich erneut angeboten
- [ ] Import-Ordner im Kartenverzeichnis → wird beim Wiederherstellen der Datenbank ignoriert
- [ ] Kartenverzeichnis von ChartButlerCS übernommen → erster Abgleich gilt als vollständig (Versionswechsel)
- [ ] Netzwerkmitschnitt bzw. Code-Suche: keine Verbindung von ChartAssist zu `aip.dfs.de`

## 8. Verhältnis zu `TECHNISCHE-BASIS.md`

- **TECHNISCHE-BASIS.md** beschreibt die technische Basis des Nachfolgers: Projektstruktur, .NET 10, Avalonia, Build, Paketierung, CI, Kompatibilität mit bestehenden Daten. Sie kommt zuerst.
- **Dieses Dokument** beschreibt, was der Nachfolger fachlich tut: Leitplanken, Ablauf, Auswertung der gespeicherten Seiten, Abgleich-Sitzung.
- **ENTSCHEIDUNGEN.md** hält fest, welche Entscheidungen in der Planungsphase gefallen sind und welche Alternativen warum verworfen wurden.

## 9. Offene Entscheidungen

| Thema | Vorschlag |
|---|---|
| Ort des Import-Ordners | Standard `<Kartenverzeichnis>/Import`, konfigurierbar. Alternative: ein eigener Ordner außerhalb, falls das Kartenverzeichnis z. B. auf ein Tablet synchronisiert wird. |
| TripKit optional machen (§8, abgeleitete Werke) | Einstellung im `OptionsWindow`; Standard offen lassen, bis die Rechtslage klarer ist |
| Fortschritt über Programmneustarts hinweg merken | Zunächst nicht. Eine Sitzung dauert nur Minuten. Später optional, z. B. über ein zusätzliches Element `LastChecked` je Flugplatz. Vorher prüfen, ob ChartButlerCS 2.0.x eine Datei mit unbekannten Elementen noch liest (TECHNISCHE-BASIS.md 4.3, Rückwärtskompatibilität). |
| GAT24-Altbestände | Keine Migration mehr. Nutzer alter GAT24-Datenbanken abonnieren ihre Plätze neu. |
| Kartenaufgaben mit direktem Link | Umsetzen (Abschnitt 4, Frage 5). Das URL-Schema für "nur HTML" beruht auf einer einzigen Ausgabe. Schlägt der Link fehl, klickt der Benutzer die Karte in der Flugplatzseite an. |
| Name, Repository, Umgang mit ChartButlerCS | Siehe TECHNISCHE-BASIS.md, Abschnitt 13. |
| Erneute Anfrage bei der DFS | **Entschieden:** entfällt. Nach der bisherigen Absage ist keine Zustimmung zu erwarten (ENTSCHEIDUNGEN.md, 3.2). |
