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
        Assert.False(File.Exists(path + ".tmp"));
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
}
