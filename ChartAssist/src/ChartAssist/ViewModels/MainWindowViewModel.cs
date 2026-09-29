using System.Reflection;

namespace ChartAssist.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    /// <summary>Vierstellige Programmversion, z. B. "1.0.0.0".</summary>
    public string Version { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unbekannt";
}
