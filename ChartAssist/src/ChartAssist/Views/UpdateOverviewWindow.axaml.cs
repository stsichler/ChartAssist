using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ChartAssist.Core.Import;

namespace ChartAssist.Views;

/// <summary>Übersicht der in einem Abgleich aktualisierten Karten (bisher <c>dlgUpdateOverview</c>).</summary>
public partial class UpdateOverviewWindow : Window
{
    private readonly IReadOnlyList<UpdatedChart> _charts = [];

    public UpdateOverviewWindow()
    {
        InitializeComponent();
    }

    public UpdateOverviewWindow(IReadOnlyList<UpdatedChart> charts)
        : this()
    {
        _charts = charts;
        ChartList.ItemsSource = charts;
    }

    private async void OnChartDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ChartList.SelectedItem is UpdatedChart chart)
        {
            await Launch.FileAsync(this, chart.Path);
        }
    }

    private async void OnShowAll(object? sender, RoutedEventArgs e)
    {
        foreach (UpdatedChart chart in _charts)
        {
            await Launch.FileAsync(this, chart.Path);
        }
    }

    private async void OnCopyAll(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Karten kopieren nach", AllowMultiple = false });
        if (folders.Count == 0 || folders[0].TryGetLocalPath() is not string target)
        {
            return;
        }
        try
        {
            foreach (UpdatedChart chart in _charts)
            {
                File.Copy(chart.Path, Path.Combine(target, chart.Name), overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await MessageDialog.ShowAsync(this, "Kopieren fehlgeschlagen:\n" + ex.Message);
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
