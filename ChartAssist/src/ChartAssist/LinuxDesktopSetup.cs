using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Platform;
using ChartAssist.Core;
using ChartAssist.Core.Platform;
using ChartAssist.Views;

namespace ChartAssist;

/// <summary>
/// Richtet unter Linux Startmenü-Eintrag und Desktop-Verknüpfung ein. Beim ersten Start wird
/// gefragt, danach werden vorhandene Einträge bei jedem Start an den Programmpfad angepasst.
/// </summary>
internal static class LinuxDesktopSetup
{
    public static async Task RunAsync(Window owner)
    {
#if DEBUG
        // Beim Entwickeln keine Einträge auf den Pfad in bin/Debug anlegen
        await Task.CompletedTask;
#else
        if (!OperatingSystem.IsLinux() || Environment.ProcessPath is not string executable)
        {
            return;
        }

        try
        {
            LinuxDesktopIntegration integration = LinuxDesktopIntegration.ForCurrentUser();
            byte[] icon = LoadIcon();
            if (integration.IsInstalled)
            {
                integration.Update(executable, icon);
                return;
            }

            Settings settings = Settings.Load(Settings.DefaultPath);
            if (settings.DesktopIntegrationOffered)
            {
                return;
            }

            bool install = await MessageDialog.AskAsync(owner, "ChartAssist einrichten",
                "Soll ChartAssist einen Eintrag im Startmenü und eine Verknüpfung auf dem Desktop bekommen?");
            settings.DesktopIntegrationOffered = true;
            settings.Save(Settings.DefaultPath);
            if (install)
            {
                integration.Install(executable, icon, desktopShortcut: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nicht kritisch: Das Programm funktioniert auch ohne Einträge
            Debug.WriteLine("Desktop-Integration fehlgeschlagen: " + e.Message);
        }
#endif
    }

    private static byte[] LoadIcon()
    {
        using Stream stream = AssetLoader.Open(new Uri("avares://ChartAssist/Assets/Icon.png"));
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
