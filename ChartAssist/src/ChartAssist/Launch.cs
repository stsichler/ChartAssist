using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ChartAssist;

/// <summary>
/// Öffnet Dateien, Ordner und Links mit dem Standardprogramm des Systems (TECHNISCHE-BASIS 5.4).
/// DFS-Seiten nur auf ausdrückliche Benutzeraktion und immer nur eine (IMPORT-MODUS 2, Leitplanke 2).
/// </summary>
internal static class Launch
{
    public static async Task FileAsync(Visual visual, string path)
    {
        try
        {
            if (TopLevel.GetTopLevel(visual)?.Launcher is { } launcher)
            {
                await launcher.LaunchFileInfoAsync(new FileInfo(path));
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Öffnen fehlgeschlagen ({path}): {e.Message}");
        }
    }

    public static async Task FolderAsync(Visual visual, string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            if (TopLevel.GetTopLevel(visual)?.Launcher is { } launcher)
            {
                await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Öffnen fehlgeschlagen ({path}): {e.Message}");
        }
    }

    public static async Task UriAsync(Visual visual, Uri uri)
    {
        try
        {
            if (TopLevel.GetTopLevel(visual)?.Launcher is { } launcher)
            {
                await launcher.LaunchUriAsync(uri);
            }
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Öffnen fehlgeschlagen ({uri}): {e.Message}");
        }
    }
}
