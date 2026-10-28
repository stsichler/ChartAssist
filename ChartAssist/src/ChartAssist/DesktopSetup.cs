using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;
using ChartAssist.Core;
using ChartAssist.Core.Platform;
using ChartAssist.Views;

namespace ChartAssist;

/// <summary>
/// Startmenü-Eintrag und Desktop-Verknüpfung unter Linux (<c>.desktop</c>) und Windows (<c>.lnk</c>). Beim ersten
/// Start wird gefragt, danach werden vorhandene Einträge bei jedem Start an den Programmpfad angepasst. Über die
/// Optionen lassen sie sich jederzeit neu anlegen. macOS braucht das nicht: Die .app kommt in den Ordner "Programme".
/// </summary>
internal static class DesktopSetup
{
    public static bool IsSupported => OperatingSystem.IsLinux() || OperatingSystem.IsWindows();

    /// <summary>Beim Start: einmal nachfragen bzw. vorhandene Einträge an den Programmpfad anpassen.</summary>
    /// <param name="save">Speichert die Einstellungen, nachdem sich <see cref="Settings.DesktopIntegrationOffered"/> geändert hat.</param>
    public static async Task RunAsync(Window owner, Settings settings, Action save)
    {
#if DEBUG
        // Beim Entwickeln keine Einträge auf den Pfad in bin/Debug anlegen
        await Task.CompletedTask;
#else
        if (!IsSupported || Environment.ProcessPath is not string executable)
        {
            return;
        }

        try
        {
            if (IsInstalled())
            {
                Update(executable);
                return;
            }

            if (settings.DesktopIntegrationOffered)
            {
                return;
            }

            bool install = await MessageDialog.AskAsync(owner, "ChartAssist einrichten",
                "Soll ChartAssist einen Eintrag im Startmenü und eine Verknüpfung auf dem Desktop bekommen?");
            settings.DesktopIntegrationOffered = true;
            save();
            if (install)
            {
                Install(executable);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or COMException)
        {
            // Nicht kritisch: Das Programm funktioniert auch ohne Einträge
            Debug.WriteLine("Desktop-Integration fehlgeschlagen: " + e.Message);
        }
#endif
    }

    /// <summary>Knopf in den Optionen: legt Startmenü-Eintrag und Desktop-Verknüpfung an, auch nach einem früheren "Nein".</summary>
    /// <returns>Meldung für den Benutzer.</returns>
    public static string InstallNow()
    {
        if (!IsSupported || Environment.ProcessPath is not string executable)
        {
            return "Auf diesem System nicht verfügbar.";
        }
        try
        {
            Install(executable);
            return "Startmenü-Eintrag und Desktop-Verknüpfung wurden angelegt.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or COMException)
        {
            return "Konnte nicht angelegt werden: " + e.Message;
        }
    }

    private static bool IsInstalled() => OperatingSystem.IsWindows()
        ? WindowsShortcuts.ForCurrentUser().IsInstalled
        : LinuxDesktopIntegration.ForCurrentUser().IsInstalled;

    private static void Install(string executable)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsShortcuts.ForCurrentUser().Install(executable, desktopShortcut: true);
        }
        else
        {
            LinuxDesktopIntegration.ForCurrentUser().Install(executable, LoadIcon(), desktopShortcut: true);
        }
    }

    private static void Update(string executable)
    {
        if (OperatingSystem.IsWindows())
        {
            WindowsShortcuts.ForCurrentUser().Update(executable);
        }
        else
        {
            LinuxDesktopIntegration.ForCurrentUser().Update(executable, LoadIcon());
        }
    }

    private static byte[] LoadIcon()
    {
        using Stream stream = AssetLoader.Open(new Uri("avares://ChartAssist/Assets/Icon.png"));
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
