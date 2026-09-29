using System.Runtime.Versioning;
using ChartAssist.Core.Platform;

namespace ChartAssist.Core.Tests.Platform;

public sealed class LinuxDesktopIntegrationTests : IDisposable
{
    private static readonly byte[] Icon = [0x89, 0x50, 0x4E, 0x47];

    private readonly string _root = Directory.CreateTempSubdirectory("ChartAssistTest").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("/opt/ChartAssist", "\"/opt/ChartAssist\"")]
    [InlineData("/home/pilot/Meine Programme/ChartAssist", "\"/home/pilot/Meine Programme/ChartAssist\"")]
    [InlineData("/home/pilot/100%/ChartAssist", "\"/home/pilot/100%%/ChartAssist\"")]
    [InlineData("/home/pilot/$x/ChartAssist", "\"/home/pilot/\\\\$x/ChartAssist\"")]
    [InlineData("/home/pilot/a\"b/ChartAssist", "\"/home/pilot/a\\\\\"b/ChartAssist\"")]
    public void QuoteExec_MaskiertNachSpezifikation(string path, string expected)
    {
        Assert.Equal(expected, LinuxDesktopIntegration.QuoteExec(path));
    }

    [Theory]
    [InlineData("XDG_DESKTOP_DIR=\"$HOME/Schreibtisch\"", "/home/pilot/Schreibtisch")]
    [InlineData("XDG_DESKTOP_DIR=\"/daten/Desktop\"", "/daten/Desktop")]
    [InlineData("XDG_DESKTOP_DIR=\"$HOME/\"", null)]
    [InlineData("XDG_DOWNLOAD_DIR=\"$HOME/Downloads\"", "/home/pilot/Desktop")]
    public void ReadDesktopDir_WertetUserDirsAus(string line, string? expected)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Nur unter Linux");
        string file = Path.Combine(_root, "user-dirs.dirs");
        File.WriteAllText(file, "# Kommentar\n" + line + "\n");

        Assert.Equal(expected, LinuxDesktopIntegration.ReadDesktopDir(file, "/home/pilot"));
    }

    [Fact]
    public void ReadDesktopDir_OhneDatei_NimmtDesktop()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Nur unter Linux");
        Assert.Equal("/home/pilot/Desktop",
            LinuxDesktopIntegration.ReadDesktopDir(Path.Combine(_root, "fehlt"), "/home/pilot"));
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Install_LegtIconMenueeintragUndVerknuepfungAn()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Nur unter Linux");
        LinuxDesktopIntegration integration = CreateIntegration();

        integration.Install("/opt/ChartAssist", Icon, desktopShortcut: true);

        Assert.Equal(Icon, File.ReadAllBytes(integration.IconPath));
        string entry = File.ReadAllText(integration.MenuEntryPath);
        Assert.Contains("Exec=\"/opt/ChartAssist\"\n", entry);
        Assert.Contains("Icon=" + integration.IconPath + "\n", entry);
        Assert.Equal(entry, File.ReadAllText(integration.DesktopShortcutPath!));
        Assert.True(File.GetUnixFileMode(integration.DesktopShortcutPath!).HasFlag(UnixFileMode.UserExecute));
    }

    [Fact]
    public void Update_NachVerschieben_PasstPfadeAn()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Nur unter Linux");
        LinuxDesktopIntegration integration = CreateIntegration();
        integration.Install("/alt/ChartAssist", Icon, desktopShortcut: true);

        integration.Update("/neu/ChartAssist", Icon);

        Assert.Contains("Exec=\"/neu/ChartAssist\"\n", File.ReadAllText(integration.MenuEntryPath));
        Assert.Contains("Exec=\"/neu/ChartAssist\"\n", File.ReadAllText(integration.DesktopShortcutPath!));
    }

    [Fact]
    public void Update_LegtGeloeschteVerknuepfungNichtNeuAn()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Nur unter Linux");
        LinuxDesktopIntegration integration = CreateIntegration();
        integration.Install("/alt/ChartAssist", Icon, desktopShortcut: true);
        File.Delete(integration.DesktopShortcutPath!);

        integration.Update("/neu/ChartAssist", Icon);

        Assert.False(File.Exists(integration.DesktopShortcutPath!));
    }

    [Fact]
    public void Update_OhneInstallation_SchreibtNichts()
    {
        LinuxDesktopIntegration integration = CreateIntegration();

        integration.Update("/opt/ChartAssist", Icon);

        Assert.False(integration.IsInstalled);
        Assert.False(File.Exists(integration.IconPath));
    }

    private LinuxDesktopIntegration CreateIntegration()
    {
        string desktop = Directory.CreateDirectory(Path.Combine(_root, "Schreibtisch")).FullName;
        return new LinuxDesktopIntegration(Path.Combine(_root, "share"), desktop);
    }
}
