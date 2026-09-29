using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace ChartAssist.Core.Platform;

/// <summary>
/// Startmenü-Eintrag und Desktop-Verknüpfung unter Linux (Desktop Entry Specification von freedesktop.org).
/// Das Linux-Paket ist nur eine ausführbare Datei, ChartAssist legt die Einträge deshalb selbst an.
/// </summary>
public sealed class LinuxDesktopIntegration
{
    public const string DesktopFileName = "chartassist.desktop";

    private readonly string _dataHome;
    private readonly string? _desktopDir;

    /// <param name="dataHome">Entspricht <c>$XDG_DATA_HOME</c>, meist <c>~/.local/share</c>.</param>
    /// <param name="desktopDir">Desktop-Verzeichnis oder null, wenn es keinen Desktop gibt.</param>
    public LinuxDesktopIntegration(string dataHome, string? desktopDir)
    {
        _dataHome = dataHome;
        _desktopDir = desktopDir;
    }

    public static LinuxDesktopIntegration ForCurrentUser()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dataHome = XdgDirectory("XDG_DATA_HOME", Path.Combine(home, ".local", "share"));
        string configHome = XdgDirectory("XDG_CONFIG_HOME", Path.Combine(home, ".config"));
        string? desktopDir = ReadDesktopDir(Path.Combine(configHome, "user-dirs.dirs"), home);
        return new LinuxDesktopIntegration(dataHome, desktopDir);
    }

    public string MenuEntryPath => Path.Combine(_dataHome, "applications", DesktopFileName);

    public string IconPath => Path.Combine(_dataHome, "icons", "hicolor", "256x256", "apps", "chartassist.png");

    public string? DesktopShortcutPath =>
        _desktopDir != null && Directory.Exists(_desktopDir) ? Path.Combine(_desktopDir, DesktopFileName) : null;

    public bool IsInstalled => File.Exists(MenuEntryPath);

    /// <summary>Legt Icon, Startmenü-Eintrag und auf Wunsch die Desktop-Verknüpfung an.</summary>
    public void Install(string executablePath, byte[] iconPng, bool desktopShortcut)
    {
        string entry = CreateDesktopEntry(executablePath, IconPath);
        WriteIcon(iconPng);
        WriteFile(MenuEntryPath, entry);
        if (desktopShortcut && DesktopShortcutPath is string shortcut)
        {
            WriteShortcut(shortcut, entry);
        }
    }

    /// <summary>
    /// Passt vorhandene Einträge an, z. B. nachdem die Programmdatei verschoben wurde.
    /// Eine vom Benutzer gelöschte Desktop-Verknüpfung wird nicht neu angelegt.
    /// </summary>
    public void Update(string executablePath, byte[] iconPng)
    {
        if (!IsInstalled)
        {
            return;
        }

        string entry = CreateDesktopEntry(executablePath, IconPath);
        if (!File.Exists(IconPath) || !File.ReadAllBytes(IconPath).AsSpan().SequenceEqual(iconPng))
        {
            WriteIcon(iconPng);
        }
        if (File.ReadAllText(MenuEntryPath) != entry)
        {
            WriteFile(MenuEntryPath, entry);
        }
        if (DesktopShortcutPath is string shortcut && File.Exists(shortcut) && File.ReadAllText(shortcut) != entry)
        {
            WriteShortcut(shortcut, entry);
        }
    }

    public static string CreateDesktopEntry(string executablePath, string iconPath)
    {
        var sb = new StringBuilder();
        sb.Append("[Desktop Entry]\n");
        sb.Append("Type=Application\n");
        sb.Append("Name=ChartAssist\n");
        sb.Append("Comment=VFR-Anflugkarten der DFS verwalten\n");
        sb.Append("Exec=").Append(QuoteExec(executablePath)).Append('\n');
        sb.Append("Icon=").Append(EscapeString(iconPath)).Append('\n');
        sb.Append("Terminal=false\n");
        sb.Append("Categories=Utility;\n");
        sb.Append("StartupWMClass=ChartAssist\n");
        return sb.ToString();
    }

    /// <summary>
    /// Quotet den Programmpfad für <c>Exec</c>: in Anführungszeichen, <c>" ` $ \</c> mit Backslash maskiert,
    /// danach wie jeder Stringwert Backslashes verdoppelt und <c>%</c> als <c>%%</c>.
    /// </summary>
    public static string QuoteExec(string path)
    {
        var quoted = new StringBuilder("\"");
        foreach (char c in path)
        {
            if (c is '"' or '`' or '$' or '\\')
            {
                quoted.Append('\\');
            }
            quoted.Append(c);
        }
        quoted.Append('"');
        return EscapeString(quoted.ToString()).Replace("%", "%%");
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r");

    /// <summary>
    /// Liest <c>XDG_DESKTOP_DIR</c> aus <c>user-dirs.dirs</c> (bei deutschem System z. B. "$HOME/Schreibtisch").
    /// Ohne Eintrag gilt <c>~/Desktop</c>. Zeigt er auf das Home-Verzeichnis, ist der Desktop abgeschaltet: null.
    /// </summary>
    public static string? ReadDesktopDir(string userDirsFile, string home)
    {
        string? value = null;
        if (File.Exists(userDirsFile))
        {
            foreach (string line in File.ReadLines(userDirsFile))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("XDG_DESKTOP_DIR=", StringComparison.Ordinal))
                {
                    value = trimmed["XDG_DESKTOP_DIR=".Length..].Trim('"');
                }
            }
        }

        if (value == null)
        {
            return Path.Combine(home, "Desktop");
        }
        if (value.StartsWith("$HOME", StringComparison.Ordinal))
        {
            value = home + value["$HOME".Length..];
        }
        if (!Path.IsPathRooted(value))
        {
            return Path.Combine(home, "Desktop");
        }

        string normalized = Path.TrimEndingDirectorySeparator(value);
        return normalized == Path.TrimEndingDirectorySeparator(home) ? null : normalized;
    }

    private static string XdgDirectory(string variable, string fallback)
    {
        string? value = Environment.GetEnvironmentVariable(variable);
        return !string.IsNullOrEmpty(value) && Path.IsPathRooted(value) ? value : fallback;
    }

    private void WriteIcon(byte[] iconPng)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(IconPath)!);
        File.WriteAllBytes(IconPath, iconPng);
    }

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void WriteShortcut(string path, string content)
    {
        WriteFile(path, content);

        // KDE startet Verknüpfungen auf dem Desktop nur, wenn sie ausführbar sind,
        // GNOME zusätzlich nur, wenn sie als vertrauenswürdig markiert sind.
        if (OperatingSystem.IsLinux())
        {
            File.SetUnixFileMode(path, File.GetUnixFileMode(path)
                | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            TrustInGnome(path);
        }
    }

    private static void TrustInGnome(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo("gio") { UseShellExecute = false };
            startInfo.ArgumentList.Add("set");
            startInfo.ArgumentList.Add(path);
            startInfo.ArgumentList.Add("metadata::trusted");
            startInfo.ArgumentList.Add("true");
            using Process? process = Process.Start(startInfo);
            process?.WaitForExit(5000);
        }
        catch (Win32Exception)
        {
            // Kein gio vorhanden, also kein GNOME: nichts zu tun
        }
    }
}
