# Technische Basis des Nachfolgers: .NET 10, Avalonia, VS Code

Der Import-Modus ([IMPORT-MODUS.md](IMPORT-MODUS.md)) ist im Grunde ein Neuschreiben von ChartButlerCS. Die Anwendung stellt keine eigenen Serverzugriffe mehr, wertet stattdessen vom Benutzer gespeicherte Seiten aus und bekommt dafür einen neuen Ablauf und ein neues Fenster. Deshalb wird er **als eigenständige Neuentwicklung in einem neuen Repository** begonnen, und zwar direkt auf einer modernen Basis:

1. aktuelles .NET (**.NET 10 LTS**) statt .NET Framework 4.8 / Mono,
2. **Avalonia** statt Windows Forms,
3. **VS Code unter Linux** statt Visual Studio unter Windows,

sodass die Anwendung auf **Windows, Linux und macOS** läuft.

Dieses Dokument beschreibt diese technische Basis. **Sie kommt zuerst.** Der Import-Modus baut darauf auf (Reihenfolge siehe Abschnitt 11). Aus ChartButlerCS wird nur Fachlogik übernommen: Datenbankformat, Verzeichnisstruktur, Vorschauvergleich, TripKit, Parsing. Übernommen wird der Code, nicht die Git-Historie.

Vorgeschlagener Name des Nachfolgers und seines Repositorys: **ChartAssist**. Zur Einordnung: *ChartButler* war das ursprüngliche Tool von Jörg Pauly in C/C++. *ChartButlerCS* ist die Neuimplementierung in C#, das "CS" diente nur der Unterscheidung. Der Nachfolger arbeitet grundlegend anders: Er ist kein "Butler" mehr, der Karten selbst holt, sondern ein Assistent, der den Benutzer bei den eigenen Abrufen unterstützt. Deshalb bekommt er einen eigenen Namen (Abschnitt 13).

Grundlage der Analyse: ChartButlerCS 2.0.1.1 (Commit `ba93927`).

---

## 1. Warum eine neue Basis

| ChartButlerCS heute | Problem |
|---|---|
| .NET Framework 4.8, altes `.csproj`-Format (ToolsVersion 12) | Lässt sich mit dem .NET SDK nicht bauen (`dotnet msbuild` scheitert an den `.resx`-Dateien), unter Linux/macOS nur mit Mono |
| Windows Forms | Unter modernem .NET **nur auf Windows** lauffähig. Linux/macOS funktionieren nur über Mono, das nicht mehr weiterentwickelt wird |
| Eine `.exe` für alle Plattformen (via Mono) | Mono muss beim Anwender installiert sein |
| PDFsharp 1.32 als Quellcode im Projekt (303 Dateien), GDI+-Variante | Benötigt `System.Drawing` (GDI+), das unter .NET 6+ nur auf Windows unterstützt wird |
| Visual-Studio-Designer (`*.Designer.cs`, `*.resx`, typisiertes DataSet) | Die Designer gibt es in VS Code nicht |

Modernes .NET erzwingt den Wechsel weg von Windows Forms, weil Windows Forms dort nicht plattformübergreifend läuft. Es erzwingt auch den Wechsel weg von `System.Drawing` und damit von der GDI-Variante von PDFsharp. Weil der Import-Modus die Anwendung ohnehin weitgehend neu schreibt, gibt es keinen Grund, ihn erst noch in Windows Forms umzusetzen.

Mit dem Import-Modus entfällt außerdem ein großer Teil dessen, was bei einer Migration aufwendig gewesen wäre:
- `HttpClient`-Zugriffe auf die DFS,
- die Zertifikatsprüfung mit dem Zertifikatsspeicher,
- Worker-Thread und Statusdialog,
- der `BinaryFormatter`-Cache der Flugplatzliste,
- die GAT24-Migration.

---

## 2. Zielarchitektur

Kernidee: **Fachlogik und UI trennen.** Die Fachlogik liegt in einer UI-unabhängigen Bibliothek, die sich unter Linux bauen und mit xUnit testen lässt.

```
~/Entwicklung/ChartAssist/            (Git-Root: nur README und was Git/GitHub braucht)
├── README.md, LICENSE
├── .gitattributes, .gitignore
├── .github/workflows/                (build.yml, release.yml, siehe 9)
└── ChartAssist/                      (gesamter Quellcode; dieses Verzeichnis in VS Code öffnen)
    ├── CLAUDE.md                     (Hinweise für Claude Code)
    ├── ChartAssist.slnx
    ├── global.json, Directory.Build.props, Directory.Packages.props
    ├── .editorconfig
    ├── .vscode/
    │   ├── extensions.json
    │   ├── launch.json
    │   ├── tasks.json
    │   └── settings.json
    ├── docs/                         (TECHNISCHE-BASIS.md, IMPORT-MODUS.md, ENTSCHEIDUNGEN.md)
    ├── packaging/macos/Info.plist    (Vorlage für das .app-Bundle)
    ├── src/
    │   ├── ChartAssist.Core/         (net10.0, Class Library, keine UI-Abhängigkeit)
    │   │   ├── Data/ChartDatabase.cs       (eigenes Datenmodell, ersetzt das typisierte DataSet, siehe 4.3)
    │   │   ├── Data/ChartDatabaseXml.cs    (Lesen/Schreiben von .ChartButler.xml, formatkompatibel)
    │   │   ├── Dfs/DfsPageParser.cs        (Auswertung gespeicherter Seiten, IMPORT-MODUS 5.2)
    │   │   ├── Dfs/DfsUrls.cs              (Links zum Öffnen im Browser, ersetzt Resources.resx)
    │   │   ├── Import/ChartImport.cs       (Abgleich-Sitzung, IMPORT-MODUS 5.4)
    │   │   ├── Import/ImportFolderScanner.cs (fertige Dateien im Import-Ordner finden, IMPORT-MODUS 5.3)
    │   │   ├── TripKit/TripKitBuilder.cs   (PDF + Vorschau)
    │   │   ├── ChartFolder.cs              (Laden/Speichern/Wiederherstellen aus dem Verzeichnis)
    │   │   ├── ReleaseCheck.cs             (Abfrage des neuesten GitHub-Releases)
    │   │   ├── Settings.cs
    │   │   └── Utility.cs                  (Pfadaufbau, Dateinamen, Dateivergleich)
    │   └── ChartAssist/              (net10.0, Avalonia-App)
    │       ├── App.axaml(.cs), Program.cs
    │       ├── Views/  MainWindow, AbgleichWindow, UpdateOverviewWindow, OptionsWindow, HelpWindow,
    │       │           MessageDialog
    │       ├── ViewModels/
    │       └── Assets/ Icon.ico, Icon.png, ChartAssist.icns
    ├── tests/
    │   └── ChartAssist.Core.Tests/   (xunit v3)
    │       └── Fixtures/             (synthetische DFS-Seiten, siehe 11 – keine echten DFS-Inhalte)
    ├── testdata/                     (lokal, nicht eingecheckt: gespeicherte DFS-Seiten)
    └── testcharts/                   (lokal, nicht eingecheckt: Kopie eines echten Kartenverzeichnisses)
```

Aufbau wie bei ChartButlerCS: Das Git-Root enthält nur die README (zugleich GitHub-Pages-Seite) und was Git und GitHub brauchen. Der gesamte Quellcode liegt im Unterverzeichnis `ChartAssist/`. Daraus folgt:
- **VS Code** wird im Unterverzeichnis `ChartAssist/` geöffnet. `.vscode/` und `.editorconfig` liegen deshalb dort. Git findet das Repository im übergeordneten Verzeichnis trotzdem.
- **GitHub Actions** müssen im Git-Root unter `.github/workflows/` liegen. Die Workflows arbeiten mit `defaults: run: working-directory: ChartAssist` (Abschnitt 9).
- **`.gitignore`** liegt im Git-Root. Muster für Dateien im Unterverzeichnis brauchen deshalb `**/` oder den vollen Pfad (7.4).

---

## 3. Projektdateien (SDK-Stil)

Angelegt in Phase 1. Maßgeblich sind die Dateien selbst, hier nur der Aufbau und die Gründe:

| Datei | Inhalt |
|---|---|
| `global.json` | SDK 10.0.100 mit `rollForward: latestFeature`; `dotnet test` läuft über die **Microsoft Testing Platform** (MTP), die xunit v3 direkt unterstützt |
| `Directory.Build.props` | Für alle Projekte: `net10.0`, `Nullable`, `ImplicitUsings`, `LangVersion latest`, `InvariantGlobalization` (4.1), Produkt, Copyright und `Version` 1.0.0.0. `EnforceCodeStyleInBuild` prüft die Regeln aus `.editorconfig` beim Bauen, in CI (`CI=true`) gelten Warnungen als Fehler |
| `Directory.Packages.props` | Zentrale, feste Paketversionen (Central Package Management). Die Projekte nennen nur den Paketnamen |
| `ChartAssist.slnx` | Solution im neuen XML-Format, Standard von `dotnet new sln` ab .NET 10 |
| `src/ChartAssist.Core/ChartAssist.Core.csproj` | `IsTrimmable`. PDFsharp und SkiaSharp kommen in Phase 2 dazu, wenn sie gebraucht werden |
| `src/ChartAssist/ChartAssist.csproj` | Avalonia-App: `WinExe`, Icon, `app.manifest` (nur Windows), Compiled Bindings, Pakete Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, CommunityToolkit.Mvvm |
| `tests/ChartAssist.Core.Tests/…csproj` | xunit v3 (`OutputType Exe`). v3 kann Tests zur Laufzeit überspringen (`Assert.Skip`), das braucht es für die Tests gegen `testdata/` und `testcharts/` |
| `packaging/macos/Info.plist` | Vorlage für das `.app`-Bundle (9) |

**Versionen** (Stand 29.09.2026): Avalonia 12.1.3, CommunityToolkit.Mvvm 8.4.2, xunit.v3 4.0.1, für Phase 2 vorgemerkt PDFsharp 6.2.4 und **SkiaSharp 3.119.4**. SkiaSharp muss genau der Version entsprechen, die `Avalonia.Skia` verwendet. Auf NuGet gibt es bereits SkiaSharp 4.x, das passt aber nicht zu Avalonia 12.1.

**Abweichungen von der Planung:**
- **Avalonia 12** statt 11: Beim Start war 12.1 die aktuelle stabile Version, das Template (`Avalonia.Templates` 12.1) erzeugt sie.
- **Keine DevTools im Debug-Build.** `Avalonia.Diagnostics` gibt es für Avalonia 12 nicht mehr. Das Template bindet stattdessen `AvaloniaUI.DiagnosticsSupport` ein, das eine Verbindung zu einem externen, nicht quelloffenen DevTools-Programm von AvaloniaUI aufbaut. Das passt nicht zur Regel "keine Netzwerkverbindungen außer der Versionsprüfung" und wurde entfernt.
- Der `ViewLocator` des Templates wurde entfernt. Er arbeitet mit Reflection (Hindernis für Trimming, 8), und die Fenster werden ohnehin direkt erzeugt.

Die Versionsprüfung (`ReleaseCheck`) liest den Tag des neuesten Releases im **neuen** Repository (`v1.2.3.4`) und meldet ihn nur, wenn er **größer** ist als die eigene Version. ChartButlerCS meldete jede Abweichung, also auch bei einem neueren Vorab-Build. `<Version>` muss vierstellig bleiben.

Aus ChartButlerCS wird **nicht** übernommen:
- `app.config`, `Properties/AssemblyInfo.cs` (→ csproj-Properties),
- `ChartButlerCS022010.snk` (Strong Naming hat unter .NET keine Funktion mehr),
- `PdfSharp/` (→ NuGet PDFsharp 6, MIT-Lizenz),
- `Properties/Resources.resx` (→ `DfsUrls.cs` und Avalonia-Assets),
- `ChartButlerDataSet.xsd` samt generiertem Code und `CChart.cs` (→ eigenes Datenmodell, 4.3),
- alle Formulare samt `*.Designer.cs` und `*.resx` (→ `.axaml`),
- `CServerConnection.cs`, `dlgStatus`, `Resources/GAT24.jpg`, `Resources/DFS.png`.

---

## 4. Fachlogik: was sich bei der Übernahme ändert

Diese Punkte betreffen den Core und sind unabhängig von Avalonia.

### 4.1 Ersetzungen für .NET 10

| In ChartButlerCS (Datei) | Problem unter .NET 10 | Ersatz |
|---|---|---|
| `JavaScriptSerializer` (`System.Web.Extensions`, `frmChartDB.checkForLatestReleaseAsync`) | Existiert nicht | `System.Text.Json` (`JsonDocument.Parse(...).RootElement.GetProperty("tag_name")`) in `ReleaseCheck` |
| GitHub-Release-Check akzeptiert **jedes** Zertifikat, dazu die TLS-Einstellung per `ServicePointManager` | Sicherheitsproblem, bzw. ohne Wirkung auf `HttpClient` | Einfacher `HttpClient` mit normaler Zertifikatsprüfung und der TLS-Vorgabe des Betriebssystems. Eine eigene Zertifikatsbehandlung wird nicht mehr gebraucht, weil es keine DFS-Zugriffe mehr gibt |
| `Process.Start(path)` (`OpenFileInDefaultApp`) | Unter .NET ist `UseShellExecute` standardmäßig `false`, dann öffnet `Process.Start(path)` keine Dokumente | In der UI: Avalonia `TopLevel.Launcher.LaunchFileInfoAsync()` / `LaunchUriAsync()` (siehe 5.4). Die OS-Weiche entfällt. Darüber öffnen sich auch die DFS-Links im Standardbrowser |
| `Environment.OSVersion.Platform == PlatformID.MacOSX` | Liefert auf macOS `Unix`, der macOS-Zweig wird nie erreicht (auch heute unter Mono nicht) | Entfällt durch den Launcher. Sonst `OperatingSystem.IsMacOS()` |
| `System.Drawing` (`Bitmap`, `Graphics`, `Image.FromFile`) für TripKit und Vorschau (`DFS_UpdateTripKitCharts`) | Nur unter Windows unterstützt | **SkiaSharp** (`SKBitmap.Decode`, `SKCanvas`, `SKImage.Encode(SKEncodedImageFormat.Jpeg, …)`). Avalonia bringt SkiaSharp samt nativer Bibliotheken ohnehin mit, also entsteht keine zusätzliche Abhängigkeit. Die Version an die von Avalonia angleichen |
| PDFsharp 1.32 GDI (`XImage.FromGdiPlusImage`) | Benötigt GDI+ | NuGet **PDFsharp 6.x (Core-Build, Paket `PDFsharp`)**: `XImage.FromStream(...)` mit den PNG-Daten. Die API (`PdfDocument`, `AddPage`, `XGraphics.FromPdfPage`, `DrawImage`) bleibt fast gleich. Prüfen, ob der Core-Build die DFS-PNGs (Farbtiefe, Palette) korrekt importiert. Solange kein Text gezeichnet wird, braucht es keinen `IFontResolver` |
| `File.SetAttributes(..., FileAttributes.Hidden)` | Linux: ohne Wirkung (versteckt ist, was mit `.` beginnt); macOS: setzt `UF_HIDDEN` | Kann bleiben. Die Dateien beginnen bereits mit `.` |
| `ToShortDateString()` u. ä. (kulturabhängige Datumsanzeige) | Unter Linux braucht die Kulturunterstützung `libicu` | Datumsformat für die Anzeige explizit festlegen (`dd.MM.yyyy`). Dann kann `InvariantGlobalization` aktiviert werden, und die Abhängigkeit von `libicu` entfällt (siehe 6) |

### 4.2 Threading

In ChartButlerCS laufen lange Netzwerkzugriffe in einem Worker-Thread mit modalem Statusdialog (`doUpdate`, `sts.Invoke(...)`). Das entfällt.

Die Verarbeitung einer gespeicherten Seite ist kurz: Parsen, PNG schreiben, bei Bedarf das TripKit erzeugen. Daraus folgt:
- Die Core-API (`ChartImport`, `TripKitBuilder`) ist **synchron und UI-frei**. Sie liefert Ergebnisobjekte statt `MessageBox`-Aufrufen.
- Die UI ruft sie mit `await Task.Run(...)` auf, damit das Fenster während der TripKit-Erzeugung bedienbar bleibt. Es läuft immer nur ein Import zur Zeit.
- Nur `ReleaseCheck` ist asynchron (`HttpClient`).
- Die `Thread.Sleep(3000)` aus ChartButlerCS entfallen ersatzlos.

### 4.3 Datenbank: eigenes Datenmodell statt typisiertem DataSet

Hinter dem DataSet steckt keine Datenbank, sondern nur eine XML-Datei (`.ChartButler.xml`) mit fünf kleinen "Tabellen". Der Nachfolger bekommt ein eigenes, spezialisiertes Modell. Das bestehende Dateiformat muss dabei aber exakt erhalten bleiben, damit vorhandene Kartenverzeichnisse weiter nutzbar sind (Abschnitt 10). Vorteile:
- Kein Visual-Studio-Generator (`MSDataSetGenerator`) nötig, denn den gibt es in VS Code nicht.
- Keine Reflection. Trimming ist damit möglich (siehe 8).
- Typsichere, lesbare Zugriffe statt `IsCryptNull()`, `GetAFChartsRows()`, `BindingSource.Find(...)` und kulturabhängiger Filter-Strings (z. B. `"LastUpdate = '" + updrow.Date + "'"` in `updateTreeView`).
- Leicht testbar.

**Modell:** umgesetzt in `Data/ChartDatabase.cs`, mit drei Abweichungen vom ursprünglichen Vorschlag unten:
- **Karten als flache Liste** `ChartDatabase.Charts` in Dateireihenfolge statt `Airfield.Charts`. In der echten Datei wechseln sich die Flugplätze ab (neue Karten stehen am Ende), und nur so bleibt der Round-Trip byteweise identisch. `ChartsOf(airfield)` liefert die Karten eines Platzes.
- **`Updates` als Liste** in Dateireihenfolge statt `SortedSet`, aus demselben Grund. `AddUpdate` behält wie ChartButlerCS die letzten 5.
- **Datumswerte als `DateOnly`** (siehe unten, Datumswerte).

Ursprünglicher Vorschlag:

```csharp
public sealed class ChartDatabase
{
    public List<Airfield> Airfields { get; } = new();
    public DateTime? AipLastUpdate { get; set; }        // Tabelle AIP (0..1 Zeile): Effective-Datum des letzten vollständigen Abgleichs
    public SortedSet<DateTime> Updates { get; } = new(); // Tabelle Updates, max. 5 Einträge
    public string? Version { get; set; }                 // Tabelle ChartButler
    public string? DataSource { get; set; }              // Tabelle ChartButler ("DFS"; null/"GAT24" = Altbestand)
}

public sealed class Airfield
{
    public string Icao { get; set; } = "";
    public string Name { get; set; } = "";               // AFname
    public DateTime? LastUpdate { get; set; }
    public List<Chart> Charts { get; } = new();          // ersetzt Relation Airfields_AFCharts
}

public sealed class Chart
{
    public string Name { get; set; } = "";               // Cname, global eindeutig (heute PK)
    public DateTime CreationDate { get; set; }
    public DateTime? LastUpdate { get; set; }
    public string? AirfieldPermalink { get; set; }       // Crypt, Teil vor '#'
    public string? ServerName { get; set; }              // Crypt, Teil nach '#'
    public bool IsTripKit => AirfieldPermalink == null;  // TripKit hat kein Crypt
}
```

`Crypt` im Modell in seine zwei Bestandteile zu zerlegen beseitigt die verstreuten `Split('#')`-Aufrufe. In der Datei bleibt `Crypt` unverändert als `"<Permalink>#<Name>"` stehen.

**Dateiformat, das exakt erhalten bleiben muss** (abgeleitet aus `ChartButlerDataSet.xsd` und dem Verhalten von `DataSet.WriteXml`; an einer echten Datei aus `testcharts/` bestätigt, siehe unten):

```xml
<?xml version="1.0" standalone="yes"?>
<ChartButlerDataSet>
  <AFCharts>
    <ICAO>EDxx</ICAO>
    <Cname>EDxx AD 2 … .png</Cname>
    <CreationDate>2025-03-20T00:00:00+01:00</CreationDate>
    <Crypt>C0xxxx.html#EDxx …</Crypt>          <!-- optional -->
    <LastUpdate>2025-03-20T00:00:00+01:00</LastUpdate> <!-- optional -->
  </AFCharts>
  …
  <Airfields><ICAO/><AFname/><LastUpdate/>(optional)</Airfields>
  <AIP><LastUpdate/></AIP>
  <Updates><Date/></Updates>
  <ChartButler><Version/><DataSource/></ChartButler>
</ChartButlerDataSet>
```

- Kein Namespace. Die "Zeilen" sind direkte Kinder des Wurzelelements, gruppiert in der Reihenfolge AFCharts, Airfields, AIP, Updates, ChartButler.
- Fehlende optionale Werte (`DBNull`) werden als fehlendes Element geschrieben, nicht als leeres.
- **Zeilenenden immer CRLF**, auf allen Plattformen (`XmlWriterSettings { Indent = true, NewLineChars = "\r\n", NewLineHandling = NewLineHandling.Replace }`). So ist die Datei unter Windows, Linux und macOS byteweise identisch, und dasselbe Kartenverzeichnis lässt sich auf allen Systemen verwenden (Abschnitt 10). Beim Lesen auch LF akzeptieren: ChartButlerCS hat die Datei unter Mono mit den Zeilenenden des jeweiligen Systems geschrieben.
- **An einer echten Datei bestätigt** (`testcharts/.ChartButler.xml`, geschrieben von ChartButlerCS 2.0.1.1 unter Windows, 8 Flugplätze, 37 Karten):
  - Kopfzeile genau `<?xml version="1.0" standalone="yes"?>`, **ohne** `encoding`-Angabe, UTF-8 **ohne BOM**,
  - CRLF in allen Zeilen, Einrückung 2 Leerzeichen,
  - Reihenfolge AFCharts, Airfields, AIP, Updates, ChartButler,
  - keine leeren Elemente; fehlende Werte fehlen als Element (bei TripKit-Zeilen fehlt `Crypt`),
  - `Airfields` enthält `LastUpdate`,
  - der Offset der Datumswerte wechselt mit der Sommerzeit (`+01:00` bzw. `+02:00`), er gehört also zum jeweiligen Datum und nicht zum Zeitpunkt des Schreibens.

  Die Datei ist die Grundlage des Round-Trip-Tests: lesen, schreiben, **byteweise** vergleichen.
- **Datumswerte:** Alle Werte sind reine Kalendertage (in ChartButlerCS immer `.Date`), geschrieben als Mitternacht mit Offset. Das DataSet verwendet dafür die Zeitzone des Rechners. Damit hinge die Datei von der Zeitzone ab, und der Round-Trip-Test würde in CI (UTC) scheitern. ChartAssist verwendet deshalb **fest die deutsche Zeitzone**:
  - Lesen: `xs:dateTime` in deutsche Zeit umrechnen, Kalendertag nehmen. Das liefert auch dann das richtige Datum, wenn ChartButlerCS die Datei auf einem Rechner mit anderer Zeitzone umgeschrieben hat.
  - Schreiben: Mitternacht mit dem deutschen Offset dieses Tages, z. B. `2026-08-20T00:00:00+02:00`. Das ist genau das, was ChartButlerCS auf einem deutschen Rechner schreibt.
  - Zeitzone `Europe/Berlin`, unter Windows ohne ICU `W. Europe Standard Time` (`Data/GermanTime.cs`).
- Charts gehören über `ICAO` zu ihrem Flugplatz. Verwaiste Charts (ICAO ohne Flugplatz) werden beim Lesen ignoriert. Doppelte Schlüssel (ICAO, Cname, Date) gelten wie beim DataSet als Fehler, dann wird aus dem Verzeichnis wiederhergestellt.

**Umsetzung:**
- Lesen und Schreiben mit `System.Xml.Linq` (`XDocument`) oder `XmlReader`/`XmlWriter`, beides trimming-sicher. **Nicht** mit `XmlSerializer`: Der nutzt Reflection und bildet die flache Tabellenstruktur nur umständlich ab.
- Die Logik "nur schreiben, wenn sich der Inhalt geändert hat" (Vergleich mit Temp-Datei in `updateDataBase`) und "Datei löschen, wenn keine Charts vorhanden" beibehalten.
- Beim Schreiben über eine Temp-Datei im selben Verzeichnis und `File.Move(..., overwrite: true)` arbeiten (ChartButlerCS: `Delete` + `Move`, dazwischen kann die Datei fehlen).
- **Kompatibilitätstests:** eine echte `.ChartButler.xml` aus Version 2.0.x lesen und wieder schreiben, das Ergebnis muss inhaltlich identisch sein. Zusätzlich eine Datei, die mit der neuen Version geschrieben wurde, von ChartButlerCS 2.0.x einlesen lassen (einmal manuell), damit ein Zurückwechseln möglich bleibt.

### 4.4 Dateien und Pfade

- `Settings` und die Einstellungsdatei liegen unter `Environment.SpecialFolder.ApplicationData`. Unter .NET ist das: Windows `%APPDATA%`, Linux `$XDG_CONFIG_HOME` bzw. `~/.config`, macOS ebenfalls `~/.config`. Das entspricht dem Mono-Verhalten von ChartButlerCS, dessen Einstellungen also gefunden werden können (Abschnitt 10). Auf einem Mac einmal verifizieren.
- Den Kartenordner explizit an den Core übergeben und Pfade mit `Path.Combine(chartFolder, …)` bilden, statt wie ChartButlerCS `Directory.SetCurrentDirectory(ChartFolder)` zu verwenden. Das aktuelle Verzeichnis ist prozessweiter Zustand und macht Tests schwierig.
- `ChartFolder.Rebuild()` (bisher `rebuildDataBaseFromChartDir()`) überspringt alle Verzeichnisse, die nicht dem Muster `^[A-Z0-9]{4} - ` entsprechen, insbesondere den Import-Ordner (IMPORT-MODUS 5.3). Statt `Console.WriteLine` wird ein Logger oder `System.Diagnostics.Debug` verwendet.

### 4.5 Ressourcen

- `Properties.Resources.DFS_*` → `static class DfsUrls` mit Konstanten. Die URLs dienen nur noch als Links zum Öffnen im Browser, neu kommt `DFS_BaseURL` für die Kartenlinks hinzu (IMPORT-MODUS 5.2).
- Hilfetexte → direkt ins `HelpWindow.axaml` oder als eingebettete Textdatei. Sie werden für den neuen Ablauf ohnehin neu geschrieben.
- Icons → `Assets/` als `AvaloniaResource`.

---

## 5. UI mit Avalonia

### 5.1 Grundsatzentscheidungen

- **Theme:** `FluentTheme`. Es sieht auf allen drei Plattformen gleich aus; ein nativer Look ist nicht das Ziel.
- **Muster:** MVVM mit `CommunityToolkit.Mvvm` für das Hauptfenster (Baum, Vorschau, Hinweisbanner, Buttons) und das Abgleichfenster (Aufgabenliste). Für die kleinen Dialoge reicht Code-Behind.
- **Compiled Bindings** (`x:DataType`) standardmäßig aktivieren: Fehler zur Compile-Zeit statt zur Laufzeit. Das ist außerdem Voraussetzung für Trimming.
- **Dialoge sind in Avalonia asynchron:** `ShowDialog` liefert einen `Task`, Event-Handler werden `async`.

### 5.2 Fenster

| Fenster | Entspricht in ChartButlerCS | Hinweise |
|---|---|---|
| `MainWindow` + `MainWindowViewModel` | `frmChartDB` | Laden/Speichern der DB und Wiederherstellen liegen in `ChartFolder` (Core). Rechtlicher Hinweis und Willkommenshinweis im `Opened`-Event; bei "Abbrechen" `Close()`. "Karten aktualisieren" und "Neuer Flugplatz" öffnen das `AbgleichWindow` |
| `AbgleichWindow` + ViewModel | – (neu) | Aufgabenliste des Import-Modus, `Topmost` umschaltbar, Import-Timer über `DispatcherTimer`. Details: IMPORT-MODUS 5.5 |
| `UpdateOverviewWindow` | `dlgUpdateOverview` | `ListView` → `ListBox`. "Alle kopieren" nutzt `StorageProvider.OpenFolderPickerAsync`. "Sofort drucken" öffnet die Datei, also Launcher |
| `OptionsWindow` | `frmOptions` | Kartenverzeichnis und neu der Import-Ordner über `StorageProvider.OpenFolderPickerAsync()` + `IStorageFolder.TryGetLocalPath()` |
| `HelpWindow` | `frmHelp` | `TabControl` bleibt. Links als `HyperlinkButton`. Die Programminformationen enthalten den Hinweis auf die Entwicklung mit KI-Unterstützung (ENTSCHEIDUNGEN.md, 3.11) |
| `MessageDialog` | `MessageBox.Show` | Avalonia hat keine eingebaute MessageBox. Ein kleines eigenes Fenster (OK, OK/Abbrechen, Ja/Nein, Standard-Button) oder NuGet `MessageBox.Avalonia` |

Entfallen: `dlgStatus` (kein Worker mehr) und `InputBox` (neue Flugplätze werden über die gespeicherte Seite erkannt, IMPORT-MODUS 3.3).

### 5.3 Controls

| Windows Forms | Avalonia |
|---|---|
| `TreeView` + `TreeNode.Tag` | `TreeView` mit `ItemsSource` + `TreeDataTemplate` über ViewModels (`AirfieldNode`, `ChartNode`, `UpdateNode`). Doppelklick über `DoubleTapped`, Enter über `KeyDown`. Rechtsklick wählt in Avalonia den Knoten nicht automatisch aus, daher `ContextRequested` behandeln. Fetter Gruppenknoten per Style |
| `PictureBox` | `Image` mit `Stretch="Uniform"`. Die Bitmap über einen Stream laden (`new Bitmap(stream)`), damit die Datei nicht gesperrt bleibt. Wichtig, weil der Import eine gerade angezeigte Vorschau überschreiben kann |
| `SplitContainer` | `Grid` mit `GridSplitter` |
| `Panel` (Hinweisbanner) | `Border` mit `Background`-Binding und `PointerPressed`/`Button` |
| `ContextMenuStrip` / `ToolStripMenuItem` | `ContextMenu` / `MenuItem` |
| `ToolTip` | `ToolTip.Tip`-Attached-Property |
| `ListView` (Aufgabenliste) | `TreeView` oder `ListBox` mit Einrückung für Karten unter ihrem Flugplatz |

### 5.4 Dateien und Links öffnen

```csharp
var launcher = TopLevel.GetTopLevel(this)!.Launcher;
await launcher.LaunchFileInfoAsync(new FileInfo(path));   // Karte / PDF / Import-Ordner öffnen
await launcher.LaunchUriAsync(new Uri(url));              // DFS-Seite im Standardbrowser, Projektseite
```

Das ersetzt `OpenFileInDefaultApp` mit seiner OS-Weiche und funktioniert auf allen drei Plattformen.

---

## 6. Plattformspezifika

| Thema | Windows | Linux | macOS |
|---|---|---|---|
| Mindestversion | Windows 10 / 11 (Windows 7/8.1 werden von .NET 10 **nicht** unterstützt) | Aktuelle glibc-Distributionen (x64, optional arm64); musl/Alpine bräuchte eine eigene RID | Laut .NET-10-Supportmatrix (nur aktuelle macOS-Versionen); x64 und arm64 |
| Paket | `.zip` mit einer `.exe` (Single-File) | `.tar.gz` mit ausführbarer Datei; optional AppImage | **`.app`-Bundle** in `.zip`/`.dmg`: `Contents/MacOS/ChartAssist`, `Contents/Info.plist`, `Contents/Resources/ChartAssist.icns` |
| Icon | `ApplicationIcon` (`.ico`) | Fenster-Icon über Avalonia. Startmenü-Eintrag und Desktop-Verknüpfung legt die App selbst an: beim ersten Start nach Rückfrage, danach passt sie die Pfade bei jedem Start an (`LinuxDesktopIntegration`). Nur im Release-Build | `.icns` aus dem vorhandenen Icon erzeugen |
| Signatur | Optional (ohne Signatur warnt SmartScreen) | – | Unsignierte Apps blockiert Gatekeeper ("App ist beschädigt" nach Download). Ohne Apple-Developer-ID mindestens eine Ad-hoc-Signatur (**auf Apple Silicon Pflicht**) und eine Anleitung für Anwender (`xattr -dr com.apple.quarantine ChartAssist.app` bzw. Rechtsklick → Öffnen). Mit Developer-ID: `codesign` + `notarytool`. Deshalb den macOS-Build auf einem macOS-Runner erstellen |
| Abhängigkeiten | keine (self-contained, siehe 8) | `fontconfig`, X11 bzw. XWayland; `libicu` nur ohne `InvariantGlobalization` (4.1) | keine |

---

## 7. VS Code als Entwicklungsumgebung

### 7.1 Extensions (`.vscode/extensions.json`)

```json
{
  "recommendations": [
    "ms-dotnettools.csharp",
    "ms-dotnettools.csdevkit",
    "avaloniateam.vscode-avalonia",
    "editorconfig.editorconfig"
  ]
}
```

- **C# Dev Kit** bringt den Solution Explorer und den Test Explorer mit. Es steht unter einer Visual-Studio-Lizenz (für Einzelpersonen und Open Source kostenlos). Wer es nicht nutzen möchte, arbeitet nur mit der C#-Extension und setzt `"dotnet.preferCSharpExtension": true`.
- **Avalonia for VS Code** bietet XAML-Vervollständigung und eine Vorschau für `.axaml`.

### 7.2 `.vscode/tasks.json`

Aufgaben `build`, `test` und `format`: jeweils `dotnet <befehl>` mit `cwd` = `${workspaceFolder}`, ohne Angabe der Solution-Datei. Dadurch funktionieren sie unabhängig davon, ob die Solution als `.sln` oder als `.slnx` (Standard von `dotnet new sln` ab .NET 10) angelegt wird. Das wird in Phase 1 entschieden.

### 7.3 `.vscode/launch.json`

```json
{
  "version": "0.2.0",
  "configurations": [
    { "name": "ChartAssist", "type": "coreclr", "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/ChartAssist/bin/Debug/net10.0/ChartAssist.dll",
      "cwd": "${workspaceFolder}", "console": "internalConsole", "stopAtEntry": false }
  ]
}
```

### 7.4 Git und Formatierung

Im neuen Repository lässt sich alles von Anfang an sauber festlegen:
- **Zeilenenden: LF** für alle Textdateien (`.gitattributes`: `* text=auto eol=lf`). ChartButlerCS verwendet CRLF, weil es ausschließlich mit Visual Studio unter Windows bearbeitet wird. Der Nachfolger wird unter Linux mit VS Code entwickelt und bekommt deshalb die dort nativen Zeilenenden. Es gibt keinen Altbestand, der umgestellt werden müsste.
  - Das gilt für den Quellcode. Für die Datenbankdatei im Kartenverzeichnis gilt unabhängig davon immer CRLF (4.3).
  - Eingecheckte XML-Fixtures für die Round-Trip-Tests müssen deshalb von der LF-Regel ausgenommen werden, sonst verfälscht Git sie beim Auschecken: in `.gitattributes` `ChartAssist/tests/**/Fixtures/*.xml -text` (bereits angelegt).
- `.editorconfig` (in `ChartAssist/`) mit Einrückung, `charset = utf-8` und `end_of_line = lf`, damit VS Code und `dotnet format` dieselben Regeln verwenden.
- `.gitignore` (im Git-Root, bereits angelegt):
  - ignorieren: `bin/`, `obj/`, `publish/`, `TestResults/`, `.vs/` in jeder Tiefe,
  - von `**/.vscode/*` nur `launch.json`, `tasks.json`, `extensions.json` und `settings.json` einchecken (`!`-Ausnahmen). Das `**/` ist nötig, weil `.vscode/` im Unterverzeichnis liegt: Ein Muster mit `/` in der Mitte gilt sonst nur relativ zum Git-Root,
  - **`/ChartAssist/testdata/`** und **`/ChartAssist/testcharts/`** ignorieren: echte, gespeicherte DFS-Seiten und ein echtes Kartenverzeichnis mit DFS-Karten dürfen wegen §11 der DFS-Nutzungsbedingungen nicht ins Repository (IMPORT-MODUS 4).
- `dotnet format` als Ersatz für die automatische Formatierung von Visual Studio.

---

## 8. Build und Veröffentlichung

Lokal (in VS Code oder im Terminal, jeweils im Verzeichnis `ChartAssist/`):

```bash
dotnet build
dotnet test
dotnet run --project src/ChartAssist
```

### Veröffentlichungsmodell: self-contained, eine Datei pro Plattform

Ziel ist, dass Anwender **nichts installieren müssen**. ChartButlerCS lief unter Windows mit dem vorinstallierten .NET Framework, für Linux und macOS brauchte es Mono. Der Nachfolger bringt die .NET-Runtime deshalb selbst mit.

```bash
dotnet publish src/ChartAssist -c Release -r win-x64   --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
dotnet publish src/ChartAssist -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
dotnet publish src/ChartAssist -c Release -r osx-arm64 --self-contained true
dotnet publish src/ChartAssist -c Release -r osx-x64   --self-contained true
```

- **macOS ohne Single-File:** Dort ist die eine Datei das `.app`-Bundle. Die Publish-Ausgabe kommt vollständig nach `Contents/MacOS/`. So liegen die nativen Bibliotheken signiert im Bundle, statt beim Start aus einer Single-File-Datei in ein Temp-Verzeichnis entpackt zu werden, wo Gatekeeper und die Signaturprüfung auf Apple Silicon Probleme machen können.
- Die Release-Pipeline setzt zusätzlich `-p:DebugType=none` (keine `.pdb` neben der Datei) und `-p:Version=<aus dem Tag>`.

- **Warum pro Plattform:** Runtime und Avalonias native Bibliotheken (Skia, HarfBuzz) sind je Betriebssystem und Architektur verschieden. Außerdem erzeugt nur ein plattformspezifischer Build eine direkt startbare Datei (`ChartAssist.exe`, `ChartAssist`, `.app`).
- **Größe:** Das ist der Preis von self-contained. Die Datei enthält Runtime, Avalonia und Skia. Gemessen in Phase 1 (leeres Hauptfenster, ohne Trimming), Download-Archive: `win-x64` 42 MB, `linux-x64` 40 MB, `macos-arm64` 43 MB, `macos-x64` 45 MB. Entpackt ist die Linux-Datei 49 MB groß.
- **Trimming** (`-p:PublishTrimmed=true`) verkleinert die Datei deutlich. Voraussetzungen:
  - Compiled Bindings in Avalonia (5.1),
  - kein reflection-basierter Code im Core (DataSet und `XmlSerializer` werden deshalb vermieden, 4.3),
  - `System.Text.Json` mit Source-Generator.

  Trimming erst aktivieren, wenn die App läuft, und danach alle Fenster einmal testen, denn Trimming-Fehler zeigen sich oft erst zur Laufzeit.
- **`InvariantGlobalization`** (4.1) spart unter Linux die `libicu`-Abhängigkeit und etwas Größe.
- **Runtime-Updates:** Eine mitgelieferte Runtime wird nicht über Windows Update o. ä. aktualisiert. Sicherheitsrelevante .NET-Patches erreichen die Anwender nur über ein neues Release. Bei einer Anwendung, deren einziger Netzzugriff die GitHub-Versionsprüfung ist, ist das Risiko gering. Die Release-Pipeline (9) macht ein Neu-Release aber billig.

---

## 9. CI und Release-Automatisierung (GitHub Actions)

- Beide Workflows liegen im Git-Root und setzen `defaults: run: working-directory: ChartAssist`, weil der Quellcode im Unterverzeichnis liegt (Abschnitt 2).
- **`.github/workflows/build.yml`**, bei jedem Push und Pull Request: Matrix `windows-latest`, `ubuntu-latest`, `macos-latest` → `dotnet build` + `dotnet test`, unter Linux zusätzlich `dotnet format --verify-no-changes`. So fallen plattformspezifische Probleme früh auf.
- **`.github/workflows/release.yml`**, ausgelöst durch einen Tag `v*`:
  - Matrix: `windows-latest` → `win-x64`, `ubuntu-latest` → `linux-x64`, `macos-latest` → `osx-arm64` und `osx-x64`.
  - Schritte: `actions/setup-dotnet` (10.0.x) → `dotnet test` → `dotnet publish` (siehe 8) → auf macOS `.app`-Bundle zusammenstellen und signieren (ad-hoc oder Developer-ID + Notarisierung) → Archiv erstellen → an das GitHub-Release anhängen.
  - Asset-Namen `ChartAssist-win-x64.zip`, `ChartAssist-linux-x64.tar.gz`, `ChartAssist-macos-arm64.zip`, `ChartAssist-macos-x64.zip`.
  - Der Tag muss eine vierstellige Version haben (`v1.0.0.0`), sonst bricht der Lauf ab. Die Version aus dem Tag wird in die Programmversion und ins `Info.plist` übernommen.
  - `osx-x64` wird auf dem Apple-Silicon-Runner (`macos-latest`) quer gebaut.
  - Die `.icns`-Datei entsteht im Lauf mit `sips` und `iconutil` aus `Assets/Icon.png` (256 × 256).
  - Das Release wird als **Entwurf** angelegt, mit Standardtext (Downloads, Gatekeeper-Hinweis, KI-Hinweis). Veröffentlicht wird es von Hand, bei Tests als Pre-release. Erst ein veröffentlichtes, reguläres Release ist für `releases/latest` und die Versionsprüfung sichtbar.
- **`README.md`** des neuen Repositorys: Download-Links pro Plattform, Systemvoraussetzungen (Windows 10+, keine Runtime-Installation), Beschreibung des Ablaufs mit Browser und Import-Ordner, Hinweis auf die DFS-Nutzungsbedingungen. Die README kann auch hier als GitHub-Pages-Seite dienen.

---

## 10. Kompatibilität mit bestehenden Anwenderdaten

Ein Umstieg von ChartButlerCS soll ohne Datenverlust möglich sein: Nach Auswahl des bisherigen Kartenverzeichnisses übernimmt der Nachfolger alle Flugplätze und Karten.

| Datei | Ort | Maßnahme |
|---|---|---|
| `.ChartButler.xml` | im Kartenverzeichnis | Eigener Reader/Writer, der exakt das bisherige DataSet-Format liest und schreibt (4.3). Round-Trip-Test mit einer echten Datei einer 2.0.x-Version; einmal manuell prüfen, dass 2.0.x die neu geschriebene Datei liest |
| Verzeichnis- und Dateinamen, versteckte Vorschaudateien | im Kartenverzeichnis | `Utility.BuildChartPath`/`BuildChartPreviewPath` und `GetFilenameFor` unverändert übernehmen. Die Vorschaudateien sind für den Import-Modus wesentlich, denn über sie wird Aktualität festgestellt |
| `ChartButlerCS.config` | `ApplicationData` | Der Nachfolger hat eine eigene Einstellungsdatei (`ChartAssist/Settings.json`, JSON mit Source-Generator). Fehlt sie beim ersten Start, übernimmt er `ChartFolder` aus `ChartButlerCS.config`, falls vorhanden. Den rechtlichen Hinweis zeigt er trotzdem, weil der Text neu ist |
| `ChartButlerCS.DFS.AFcache` | `ApplicationData` | Wird nicht mehr gebraucht und bleibt unberührt, denn sie gehört ChartButlerCS |

### Gemeinsames Kartenverzeichnis für Windows, Linux und macOS

Dasselbe Kartenverzeichnis soll abwechselnd unter Windows, Linux und macOS verwendet werden können, z. B. auf einem Netzlaufwerk, einem USB-Stick oder per Cloud-Synchronisation. Dafür gilt:
- **`.ChartButler.xml` immer mit CRLF** schreiben, egal auf welchem System (4.3). Andernfalls würde jeder Systemwechsel die Datei ändern, obwohl sich inhaltlich nichts geändert hat. Außerdem würde der Vergleich "nur schreiben, wenn sich der Inhalt geändert hat" bei jedem Wechsel anschlagen.
- **Dateinamen nach den Regeln von Windows bilden, auch unter Linux und macOS.** `GetFilenameFor` ersetzt heute nur `/ \ < >` und entfernt Anführungszeichen. Unter Windows sind außerdem `: * ? |` und Steuerzeichen verboten, ebenso Namen, die mit Punkt oder Leerzeichen enden, und reservierte Namen wie `CON`. Die Funktion wird entsprechend erweitert. Bei DFS-Namen, die ChartButlerCS bisher unverändert übernommen hat, ändert sich das Ergebnis dadurch nicht, denn solche Zeichen hätten schon unter Windows nicht funktioniert.
- **Groß-/Kleinschreibung:** Linux unterscheidet sie, Windows und macOS standardmäßig nicht. Pfade immer exakt so bilden, wie sie in der Datenbank stehen (ICAO in Großbuchstaben), und keine Namen erzeugen, die sich nur in der Schreibweise unterscheiden.
- **Versteckte Dateien:** Die Vorschaudateien beginnen mit einem Punkt, was unter Linux und macOS genügt. Unter Windows zusätzlich das Attribut `Hidden` setzen. Wurde eine Datei unter Linux angelegt, fehlt ihr das Attribut unter Windows. Das ist nur kosmetisch.
- **Dateirechte:** Alle Dateien im Kartenverzeichnis bekommen die Standardrechte des Benutzers (gemäß `umask`). ChartButlerCS hat dazu einen **Bug**: Im Test-Kartenverzeichnis haben 24 von 75 Dateien nur Rechte für den Besitzer (`0600`). Ursache ist `Path.GetTempFileName()`, das unter Unix Dateien mit `0600` anlegt. Nach `File.Move` ins Kartenverzeichnis bleibt das so (`CServerConnectionDFS.cs` in `DFS_DownloadAndCheckChart` und `DFS_UpdateTripKitCharts`, `frmChartDB.updateDataBase`). Der Nachfolger vermeidet das, indem er Temp-Dateien im Zielverzeichnis selbst anlegt und danach umbenennt. Das ist ohnehin nötig, damit das Umbenennen atomar ist (4.3). Ein Test prüft, dass neu geschriebene Karten, Vorschauen, TripKit und Datenbank unter Linux nicht `0600` haben. Bereits vorhandene Dateien mit `0600` aus ChartButlerCS korrigiert der Nachfolger nicht automatisch.
- Die Datenbank enthält keine absoluten Pfade, nur Datei- und Flugplatznamen. Daran ändert sich nichts.

### Versionswechsel

Das Feld `ChartButler.Version` in der DB ändert sich mit dem Nachfolger. Dadurch gilt der erste Abgleich als vollständig, was erwünscht ist. Wechselt ein Anwender zurück zu ChartButlerCS, erzwingt dieses aus demselben Grund einen vollständigen Abgleich mit dem Server. Das funktioniert, ist aber genau der Serverzugriff, den der Nachfolger vermeiden soll. Paralleler Betrieb beider Programme auf demselben Kartenverzeichnis wird deshalb nicht empfohlen.

---

## 11. Reihenfolge der Entwicklung

Jede Phase endet mit einem baubaren, getesteten Stand. Die Phasen 3 und 4 setzen den Import-Modus um, die Details stehen in IMPORT-MODUS.md.

0. **Repository anlegen** (klein)
   - **Erledigt am 29.09.2026:**
     - lokales Repository `~/Entwicklung/ChartAssist` (Branch `main`),
     - `.gitignore` und `.gitattributes` im Git-Root, README-Platzhalter,
     - `ChartAssist/CLAUDE.md`,
     - die Konzeptdokumente in `ChartAssist/docs/`,
     - `ChartAssist/testdata/` und `ChartAssist/testcharts/` aus dem alten Repository kopiert (ignoriert),
     - `LICENSE` im Git-Root: "Alle Rechte vorbehalten", keine Open-Source-Lizenz (ENTSCHEIDUNGEN.md, 3.10),
     - `.editorconfig` und `.vscode/` in `ChartAssist/` (7),
     - zwei `ChartButlerCS.config` als Testdateien in `testdata/` (siehe unten),
     - erster Commit.
   - **Offen:**
     - keiner mehr (GitHub-Repository `stsichler/ChartAssist`, öffentlich, verbunden am 29.09.2026).
   - **Testdateien `ChartButlerCS.config`** für die Übernahme der Einstellungen (10, Phase 2):
     - `testdata/ChartButlerCS.config`: echte Datei aus `~/.config`, unverändert. UTF-8 **mit BOM**, LF, ohne Zeilenumbruch am Ende. `ChartFolder` ist leer, enthält aber Leerraum (`<value>` + Zeilenumbruch + Einrückung + `</value>`), also beim Lesen trimmen. Enthält noch den veralteten Schlüssel `ServerUsername`, der ignoriert werden muss.
     - `testdata/ChartButlerCS_Kartenverzeichnis.config`: im selben Format, `ChartFolder` zeigt auf das lokale `testcharts/`. Für CI in Phase 2 eine synthetische Kopie mit einem Pfad in einem Temp-Verzeichnis anlegen, die Datei enthält keine DFS-Inhalte.

1. **Grundgerüst und Pipeline** (klein bis mittel)
   - Solution mit `ChartAssist.Core`, `ChartAssist.Core.Tests` und der Avalonia-App per Template anlegen. Die App zeigt zunächst nur ein leeres Hauptfenster.
   - `build.yml` und `release.yml` einrichten und ein erstes Test-Release auf allen drei Plattformen erzeugen: Startet die Datei per Doppelklick? Wie groß ist sie? Wie verhält sich Gatekeeper?
   - So früh, weil Paketierung und Signatur die größten Unbekannten sind und sich nicht am Ende stauen sollen.
   - **Stand 29.09.2026:**
     - erledigt: Solution (`ChartAssist.slnx`), zentrale Build-Dateien, Core mit `DfsUrls` und ersten Tests, Avalonia-App mit leerem Hauptfenster (Icon, Versionsnummer), `build.yml`, `release.yml`. Lokal unter Linux gebaut, getestet, gestartet und als Single-File veröffentlicht (3, 8),
     - beide Workflows laufen auf GitHub grün, Release-Entwurf `v0.1.0.0` mit allen vier Paketen erzeugt,
     - Linux auf echtem Rechner geprüft, dazu Startmenü-Eintrag und Desktop-Verknüpfung (6). **Phase 1 abgeschlossen.**
     - Windows auf echtem Rechner geprüft. Offen: macOS (Gatekeeper), sobald ein Mac verfügbar ist.

2. **Core-Basis** (mittel)
   - Datenmodell mit XML-Reader/-Writer (4.3) und Round-Trip-Tests gegen die echte `.ChartButler.xml`. Die Tests mit echten Daten laufen nur lokal, die Datei liegt in `testdata/`. Für CI eine synthetische, anonymisierte Datei gleichen Aufbaus einchecken.
   - `ChartFolder` (Laden, Speichern, Wiederherstellen), `Settings` mit Übernahme aus ChartButlerCS (10), `Utility`.
   - `TripKitBuilder` mit SkiaSharp und PDFsharp 6. Test: Ein PDF aus generierten Beispielbildern im Hoch- und Querformat hat die erwartete Seitenzahl. Einmal lokal mit echten DFS-PNGs prüfen (Risiko in 13).
   - `ReleaseCheck`.
   - **Erledigt am 29.09.2026.** Ergebnisse:
     - Round-Trip gegen die echte `.ChartButler.xml` byteweise identisch, Wiederherstellung aus `testcharts/` ergibt dieselben Flugplätze und Karten,
     - TripKit aus den echten Karten: für alle 8 Flugplätze dieselbe Seitenzahl wie bei ChartButlerCS, Farben und Anordnung per Sichtprüfung korrekt,
     - `Settings` übernimmt das Kartenverzeichnis aus `ChartButlerCS.config` (beide Testdateien).

3. **Import-Modus im Core** (mittel), siehe IMPORT-MODUS 5.2–5.4
   - `DfsPageParser` mit Tests gegen **synthetische Fixtures**: von Hand geschriebene Minimalseiten mit derselben HTML-Struktur wie die DFS-Seiten, jeweils als "nur HTML" und "komplett", mit kleinen generierten PNGs statt echter Karten. Zusätzlich Tests gegen `testdata/`, die übersprungen werden, wenn der Ordner fehlt.
   - `ChartImport` und `ImportFolderScanner` mit Tests in einem temporären Kartenverzeichnis: neuer Flugplatz, unverändert, geänderte Karte, entfallene Karte, leere Kartenliste.
   - **Erledigt am 29.09.2026.** Synthetische Seiten erzeugt `tests/…/Dfs/DfsTestPages.cs` in beiden Formaten ("komplett" mit Dark-Reader-Attributen). Gegen die echten Daten bestätigt: beide Formate liefern dieselben Karten und Vorschauen; EDFM ist "alles aktuell" ohne Dateiänderung; ohne Vorschau wird die Karte angefordert, danach sind PNG und Vorschau identisch mit dem Original und das TripKit ist neu erzeugt.

4. **Oberfläche** (groß)
   - `MessageDialog`, dann `MainWindow` (Baum, Vorschau, Banner, Buttons), `AbgleichWindow`, `OptionsWindow`, `UpdateOverviewWindow`, `HelpWindow`.
   - Auf allen drei Plattformen manuell testen (Checklisten in Abschnitt 12 und IMPORT-MODUS 7).

5. **Release** (klein)
   - `README.md` fertigstellen, Vorab-Release (Pre-release) erstellen und auf echten Rechnern testen, auch den Umstieg von ChartButlerCS mit einem bestehenden Kartenverzeichnis.
   - Trimming aktivieren und erneut testen (8).

6. **ChartButlerCS abkündigen: letztes Release** (klein)

   ChartButlerCS wird nicht weiterentwickelt, auch Fehler werden dort nicht mehr behoben. Der Download bleibt verfügbar: Die Software weist auf die Nutzungsbedingungen der DFS hin, die Entscheidung über die Nutzung liegt beim Anwender. Die Anwender lassen sich **ohne Codeänderung** auf ChartAssist hinweisen:
   - Die Versionsprüfung von ChartButlerCS fragt das neueste Release **im alten Repository** ab. Sie zeigt das blaue Banner "Eine neue Version von ChartButler steht zur Verfügung", sobald dessen Tag von `"v2.0.1.1"` abweicht. Releases im neuen Repository sieht ChartButlerCS nicht.
   - Ein Klick auf das Banner öffnet `https://stsichler.github.io/ChartButlerCS/`, also die README im Git-Root des alten Repositorys.

   **Status: vorgemerkt.** Der Benutzer stößt diesen Schritt selbst an, frühestens wenn es ein erstes Release von ChartAssist gibt, auf das verwiesen werden kann. Checkliste:
   - [ ] **README.md im Git-Root** des alten Repositorys (zugleich GitHub-Pages-Seite) aktualisieren:
     - Hinweis auf ChartAssist mit Link, gut sichtbar am Anfang.
     - Den Hinweis "**eventuell** eine schriftliche Zustimmung durch die DFS" eindeutig formulieren. Die DFS hat inzwischen ausdrücklich bestätigt, dass die Nutzungsbedingungen des AIS-Portals auch für die BasicVFR gelten. Die Nutzung von ChartButlerCS verstößt damit gegen §2. Die Entscheidung über die Nutzung liegt beim Anwender.
     - Optional die veralteten GAT24-Passagen entfernen.
   - [ ] **Release-Notes** für das letzte Release: dieselben beiden Aussagen (Verweis auf ChartAssist, eindeutiger Hinweis auf die Nutzungsbedingungen), dazu: ChartButlerCS wird nicht weiterentwickelt.
   - [ ] **Letztes Release** im alten Repository mit neuem Tag, z. B. `v2.0.1.2`. Es muss **wieder `ChartButlerCS.zip` enthalten** (dieselbe Datei wie in v2.0.1.1), denn der Download-Link der README zeigt auf `releases/latest/download/ChartButlerCS.zip` und liefe sonst ins Leere. Kein Pre-Release, denn `releases/latest` berücksichtigt nur reguläre Releases.
   - Nicht geändert werden die Texte in der Anwendung selbst (Startmeldung in `frmChartDB.cs`, Hilfe in `frmHelp.resx`). Sie sagen weiterhin "eventuell", weil keine neue Programmversion gebaut wird.

---

## 12. Test-Checkliste (pro Plattform)

Ergänzend zur Checkliste des Import-Modus (IMPORT-MODUS 7):

- [ ] Download entpacken und per Doppelklick starten, ohne dass eine .NET-Runtime installiert ist
- [ ] Erststart ohne Einstellungen: rechtlicher Hinweis, Aufforderung zur Ordnerwahl, Optionen-Dialog
- [ ] Erststart mit vorhandener `ChartButlerCS.config`: Kartenverzeichnis wird übernommen, Datenbank gelesen, Baum vollständig, Hinweis "Abgleich erforderlich" (Versionswechsel)
- [ ] TripKit-PDF öffnet sich im Standard-Viewer, Hoch- und Querformat sind korrekt
- [ ] Vorschau im Hauptfenster, Doppelklick bzw. Enter öffnet die Karte
- [ ] Flugplatz über das Kontextmenü löschen (Verzeichnis wird entfernt)
- [ ] "Alle kopieren" und "Alle anzeigen" in der Update-Übersicht
- [ ] Defekte bzw. fehlende `.ChartButler.xml` → Wiederherstellung aus dem Verzeichnis
- [ ] Hinweis auf eine neue Version (Release-Build, mit älterer Versionsnummer testen); offline → kein Fehler, kein Hinweis
- [ ] Links im Hilfe-Fenster und DFS-Links öffnen den Standardbrowser
- [ ] Hohe DPI / Skalierung, dunkles Systemdesign (Fluent-Theme)

---

## 13. Risiken und offene Entscheidungen

| Thema | Einschätzung |
|---|---|
| **Name und Repository** | Vorschlag **ChartAssist**, Versionszählung neu ab 1.0. Weil der Name neu ist, muss das alte Repository deutlich auf den Nachfolger verweisen (11, Phase 6). Vor der Festlegung prüfen, ob der Name auf GitHub oder als Produktname im Luftfahrtumfeld schon vergeben ist |
| **Name der Datenbankdatei `.ChartButler.xml`** | Offen. **Beibehalten:** Ein Kartenverzeichnis funktioniert ohne Umweg mit beiden Programmen, und ein Zurückwechseln zu ChartButlerCS bleibt möglich. **Umbenennen** (z. B. `.ChartAssist.xml`, beim ersten Start aus der alten Datei übernommen): passt zum neuen Namen und trennt die Programme sauber. ChartButlerCS fände dann aber keine aktuelle Datenbank mehr und würde seine alte Datei weiterverwenden bzw. die Datenbank aus dem Verzeichnis neu aufbauen. Das Format selbst (4.3) ist davon unabhängig |
| **Umgang mit ChartButlerCS** | Entschieden: wird wegen der DFS-Nutzungsbedingungen nicht weiterentwickelt, auch Fehler werden nicht mehr behoben. Nach dem ersten Release von ChartAssist abkündigen (11, Phase 6). Der Download von ChartButlerCS bleibt weiter verfügbar. Die Software weist die Anwender auf die Nutzungsbedingungen der DFS hin (Startmeldung, Hilfe, README), die Entscheidung über die Nutzung liegt beim Anwender |
| **Wegfall von Windows 7/8.1 und alten macOS-Versionen** | Anwender auf solchen Systemen müssen bei ChartButlerCS 2.0.x bleiben. Im README deutlich kommunizieren |
| **Download-Größe** | Durch self-contained deutlich größer als ChartButlerCS. Für eine Desktop-Anwendung trotzdem unkritisch, Trimming verringert sie (8) |
| **macOS-Signatur** | Ohne Apple-Developer-Account (kostenpflichtig) bleiben die Gatekeeper-Warnungen. Entscheiden, ob ein Account angeschafft wird oder ob eine Anleitung genügt |
| **PNG-Import in PDFsharp 6 Core** | **Erledigt:** Die echten DFS-Karten (8-Bit RGB) werden direkt übernommen, ohne Umwandlung (Phase 2) |
| **SkiaSharp-Version** | Muss zu der von Avalonia verwendeten passen, sonst gibt es Konflikte bei den nativen Bibliotheken |
| **Layoutänderungen der DFS-Seiten** | Bleiben das größte Risiko im Betrieb, jetzt für den Parser der gespeicherten Seiten. Die Parser-Tests machen Anpassungen einfacher |
| **Testdaten und Urheberrecht** | Echte DFS-Seiten nur lokal (`testdata/`). Im Repository und in CI nur synthetische Fixtures (11, Phase 3) |
| **Formatkompatibilität von `.ChartButler.xml`** | Das Format ist an einer echten Datei bestätigt (4.3). Der eigene Reader/Writer muss es trotzdem genau treffen: Datums-Offsets, fehlende optionale Elemente, Elementreihenfolge. Der byteweise Round-Trip-Test gegen `testcharts/` ist Pflicht. Ideal wäre zusätzlich eine unter Linux/Mono geschriebene Datei (LF), um das tolerante Lesen zu prüfen |
