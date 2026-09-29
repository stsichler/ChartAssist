namespace ChartAssist.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ChartAssistTest").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void SpeichernUndLaden_ErhaeltWerte()
    {
        string path = Path.Combine(_root, "ChartAssist", "Settings.json");

        new Settings { DesktopIntegrationOffered = true }.Save(path);

        Assert.True(Settings.Load(path).DesktopIntegrationOffered);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"));
    }

    [Fact]
    public void Laden_OhneDatei_LiefertStandardwerte()
    {
        Assert.False(Settings.Load(Path.Combine(_root, "fehlt.json")).DesktopIntegrationOffered);
    }

    [Fact]
    public void Laden_DefekteDatei_LiefertStandardwerte()
    {
        string path = Path.Combine(_root, "Settings.json");
        File.WriteAllText(path, "{ kaputt");

        Assert.False(Settings.Load(path).DesktopIntegrationOffered);
    }

    [Fact]
    public void LoadOrImport_UebernimmtKartenverzeichnisAusChartButlerCS()
    {
        string legacy = Path.Combine(_root, "ChartButlerCS.config");
        File.WriteAllText(legacy, "\uFEFF<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n"
            + "  <setting name=\"ChartFolder\">\r\n    <value>/home/pilot/Karten</value>\r\n  </setting>\r\n"
            + "  <setting name=\"EulaRead\">\r\n    <value>v2</value>\r\n  </setting>\r\n</configuration>");

        Settings settings = Settings.LoadOrImport(Path.Combine(_root, "Settings.json"), legacy);

        Assert.Equal("/home/pilot/Karten", settings.ChartFolder);
        Assert.Equal("", settings.LegalNoticeAccepted); // der rechtliche Hinweis ist neu
    }

    [Fact]
    public void LoadOrImport_VorhandeneEinstellungenHabenVorrang()
    {
        string path = Path.Combine(_root, "Settings.json");
        new Settings { ChartFolder = "/neu" }.Save(path);

        Assert.Equal("/neu", Settings.LoadOrImport(path, Path.Combine(_root, "fehlt.config")).ChartFolder);
    }

    [Fact]
    public void ReadChartButlerCSChartFolder_LeererWertMitLeerraum_IstNull()
    {
        string legacy = Path.Combine(_root, "ChartButlerCS.config");
        File.WriteAllText(legacy, "<configuration>\n  <setting name=\"ChartFolder\">\n    <value>\n    </value>\n  </setting>\n"
            + "  <setting name=\"ServerUsername\">\n    <value>\n    </value>\n  </setting>\n</configuration>");

        Assert.Null(Settings.ReadChartButlerCSChartFolder(legacy));
    }

    [Theory]
    [InlineData("ChartButlerCS.config", null)]
    [InlineData("ChartButlerCS_Kartenverzeichnis.config", "testcharts")]
    public void ReadChartButlerCSChartFolder_EchteDateien(string file, string? expectedFolderName)
    {
        string path = Path.Combine(TestData.RequireLocal("testdata"), file);

        string? folder = Settings.ReadChartButlerCSChartFolder(path);

        Assert.Equal(expectedFolderName, folder == null ? null : Path.GetFileName(folder));
    }

    [Fact]
    public void EffectiveImportFolder_StandardImKartenverzeichnis()
    {
        Assert.Equal(Path.Combine("/karten", "Import"), new Settings { ChartFolder = "/karten" }.EffectiveImportFolder);
        Assert.Equal("/anders", new Settings { ChartFolder = "/karten", ImportFolder = "/anders" }.EffectiveImportFolder);
    }

    [Fact]
    public void CreateTripKit_IstOhneEintragEingeschaltet()
    {
        string path = Path.Combine(_root, "Settings.json");
        File.WriteAllText(path, "{ \"ChartFolder\": \"/karten\" }");

        Assert.True(Settings.Load(path).CreateTripKit);
    }
}
