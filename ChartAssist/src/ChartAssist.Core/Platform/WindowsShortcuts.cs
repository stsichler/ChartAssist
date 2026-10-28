using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace ChartAssist.Core.Platform;

/// <summary>
/// Verknüpfungen (<c>.lnk</c>) im Startmenü und auf dem Desktop unter Windows. Das Windows-Paket ist nur eine
/// <c>.exe</c>, ChartAssist legt die Verknüpfungen deshalb selbst an, wie unter Linux die <c>.desktop</c>-Dateien.
/// Erzeugt über die Windows-Schnittstelle IShellLink; die COM-Anbindung erzeugt der Compiler, sie verträgt Trimming.
/// </summary>
/// <param name="programsDir">Startmenü-Ordner "Programme" des Benutzers.</param>
/// <param name="desktopDir">Desktop des Benutzers.</param>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsShortcuts(string programsDir, string desktopDir)
{
    public const string FileName = "ChartAssist.lnk";

    private const string Description = "VFR-Anflugkarten der DFS verwalten";

    public static WindowsShortcuts ForCurrentUser() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));

    public string StartMenuPath => Path.Combine(programsDir, FileName);

    public string DesktopPath => Path.Combine(desktopDir, FileName);

    public bool IsInstalled => File.Exists(StartMenuPath);

    /// <summary>Legt die Verknüpfung im Startmenü an und auf Wunsch auf dem Desktop.</summary>
    public void Install(string executablePath, bool desktopShortcut)
    {
        Directory.CreateDirectory(programsDir);
        Write(StartMenuPath, executablePath);
        if (desktopShortcut && Directory.Exists(desktopDir))
        {
            Write(DesktopPath, executablePath);
        }
    }

    /// <summary>
    /// Passt vorhandene Verknüpfungen an, z. B. nachdem die Programmdatei verschoben wurde.
    /// Eine vom Benutzer gelöschte Verknüpfung wird nicht neu angelegt.
    /// </summary>
    public void Update(string executablePath)
    {
        foreach (string shortcut in new[] { StartMenuPath, DesktopPath })
        {
            if (File.Exists(shortcut) && !string.Equals(ReadTarget(shortcut), executablePath, StringComparison.OrdinalIgnoreCase))
            {
                Write(shortcut, executablePath);
            }
        }
    }

    /// <summary>Ziel einer Verknüpfung, z. B. "C:\Programme\ChartAssist.exe".</summary>
    public static string? ReadTarget(string shortcutPath) => WithShellLink(link =>
    {
        ((IPersistFile)link).Load(shortcutPath, 0 /* STGM_READ */);
        nint buffer = Marshal.AllocHGlobal(MaxPath * sizeof(char));
        try
        {
            link.GetPath(buffer, MaxPath, nint.Zero, 4 /* SLGP_RAWPATH */);
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    });

    private static void Write(string shortcutPath, string executablePath) => WithShellLink(link =>
    {
        link.SetPath(executablePath);
        link.SetWorkingDirectory(Path.GetDirectoryName(executablePath)!);
        link.SetDescription(Description);
        link.SetIconLocation(executablePath, 0); // das in die .exe eingebettete Programm-Icon
        ((IPersistFile)link).Save(shortcutPath, true);
        return true;
    });

    private const int MaxPath = 1024;

    private static readonly Guid ClsidShellLink = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IidShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    private static readonly StrategyBasedComWrappers ComWrappers = new();

    private static T WithShellLink<T>(Func<IShellLinkW, T> action)
    {
        // Im UI-Thread ist COM schon initialisiert (STAThread); dann meldet CoInitializeEx S_FALSE oder einen
        // anderen Modus, beides ist in Ordnung. Nur ein erfolgreicher Aufruf wird wieder abgemeldet.
        int init = CoInitializeEx(nint.Zero, 2 /* COINIT_APARTMENTTHREADED */);
        try
        {
            Marshal.ThrowExceptionForHR(CoCreateInstance(ClsidShellLink, nint.Zero, 1 /* CLSCTX_INPROC_SERVER */, IidShellLinkW, out nint pointer));
            var link = (IShellLinkW)ComWrappers.GetOrCreateObjectForComInstance(pointer, CreateObjectFlags.UniqueInstance);
            Marshal.Release(pointer);
            try
            {
                return action(link);
            }
            finally
            {
                ((ComObject)(object)link).FinalRelease();
            }
        }
        finally
        {
            if (init >= 0)
            {
                CoUninitialize();
            }
        }
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();
}

// Die Reihenfolge der Methoden entspricht der Vtable der Windows-Schnittstellen und darf nicht geändert werden.

[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal partial interface IShellLinkW
{
    void GetPath(nint pszFile, int cch, nint pfd, uint fFlags);

    void GetIDList(out nint ppidl);

    void SetIDList(nint pidl);

    void GetDescription(nint pszName, int cch);

    void SetDescription(string pszName);

    void GetWorkingDirectory(nint pszDir, int cch);

    void SetWorkingDirectory(string pszDir);

    void GetArguments(nint pszArgs, int cch);

    void SetArguments(string pszArgs);

    void GetHotkey(out ushort pwHotkey);

    void SetHotkey(ushort wHotkey);

    void GetShowCmd(out int piShowCmd);

    void SetShowCmd(int iShowCmd);

    void GetIconLocation(nint pszIconPath, int cch, out int piIcon);

    void SetIconLocation(string pszIconPath, int iIcon);

    void SetRelativePath(string pszPathRel, uint dwReserved);

    void Resolve(nint hwnd, uint fFlags);

    void SetPath(string pszFile);
}

/// <summary>IPersistFile einschließlich der geerbten Methode von IPersist (GetClassID).</summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0000010b-0000-0000-C000-000000000046")]
internal partial interface IPersistFile
{
    void GetClassID(out Guid pClassID);

    [PreserveSig]
    int IsDirty();

    void Load(string pszFileName, uint dwMode);

    void Save(string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);

    void SaveCompleted(string pszFileName);

    void GetCurFile(out nint ppszFileName);
}
