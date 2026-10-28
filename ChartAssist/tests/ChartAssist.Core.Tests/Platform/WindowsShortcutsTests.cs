using System.Runtime.Versioning;
using ChartAssist.Core.Platform;

namespace ChartAssist.Core.Tests.Platform;

/// <summary>Legt echte Verknüpfungen an und liest sie zurück; läuft nur unter Windows (in CI auf dem Windows-Runner).</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsShortcutsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ChartAssistTest").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Install_LegtStartmenueUndDesktopAn()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Nur unter Windows");
        (WindowsShortcuts shortcuts, string executable) = Create("Programm");

        shortcuts.Install(executable, desktopShortcut: true);

        Assert.True(shortcuts.IsInstalled);
        Assert.Equal(executable, WindowsShortcuts.ReadTarget(shortcuts.StartMenuPath), ignoreCase: true);
        Assert.Equal(executable, WindowsShortcuts.ReadTarget(shortcuts.DesktopPath), ignoreCase: true);
    }

    [Fact]
    public void Update_NachVerschieben_PasstZielAn()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Nur unter Windows");
        (WindowsShortcuts shortcuts, string oldExecutable) = Create("Alt");
        shortcuts.Install(oldExecutable, desktopShortcut: true);
        string newExecutable = CreateExecutable("Neu");

        shortcuts.Update(newExecutable);

        Assert.Equal(newExecutable, WindowsShortcuts.ReadTarget(shortcuts.StartMenuPath), ignoreCase: true);
        Assert.Equal(newExecutable, WindowsShortcuts.ReadTarget(shortcuts.DesktopPath), ignoreCase: true);
    }

    [Fact]
    public void Update_LegtGeloeschteVerknuepfungNichtNeuAn()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Nur unter Windows");
        (WindowsShortcuts shortcuts, string executable) = Create("Programm");
        shortcuts.Install(executable, desktopShortcut: true);
        File.Delete(shortcuts.DesktopPath);

        shortcuts.Update(CreateExecutable("Neu"));

        Assert.False(File.Exists(shortcuts.DesktopPath));
    }

    [Fact]
    public void OhneInstallation_NichtInstalliert()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Nur unter Windows");
        (WindowsShortcuts shortcuts, _) = Create("Programm");

        Assert.False(shortcuts.IsInstalled);
    }

    private (WindowsShortcuts, string Executable) Create(string folder)
    {
        string desktop = Directory.CreateDirectory(Path.Combine(_root, "Desktop")).FullName;
        return (new WindowsShortcuts(Path.Combine(_root, "Startmenü", "Programme"), desktop), CreateExecutable(folder));
    }

    private string CreateExecutable(string folder)
    {
        string path = Path.Combine(Directory.CreateDirectory(Path.Combine(_root, folder)).FullName, "ChartAssist.exe");
        File.WriteAllBytes(path, [0x4D, 0x5A]);
        return path;
    }
}
