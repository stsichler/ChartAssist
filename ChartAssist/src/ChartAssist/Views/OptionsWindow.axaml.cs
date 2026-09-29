using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ChartAssist.Core;

namespace ChartAssist.Views;

/// <summary>Optionen: Kartenverzeichnis und Import-Ordner. Liefert beim Schließen true für "OK".</summary>
public partial class OptionsWindow : Window
{
    public OptionsWindow()
    {
        InitializeComponent();
    }

    public OptionsWindow(Settings settings)
        : this()
    {
        ChartFolderBox.Text = settings.ChartFolder;
        ImportFolderBox.Text = settings.ImportFolder;
        TripKitBox.IsChecked = settings.CreateTripKit;
    }

    public string ChartFolder => ChartFolderBox.Text?.Trim() ?? "";

    public string ImportFolder => ImportFolderBox.Text?.Trim() ?? "";

    public bool CreateTripKit => TripKitBox.IsChecked == true;

    private async void OnBrowseChartFolder(object? sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync("Kartenverzeichnis wählen", ChartFolder) is string path)
        {
            ChartFolderBox.Text = path;
        }
    }

    private async void OnBrowseImportFolder(object? sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync("Import-Ordner wählen", ImportFolder) is string path)
        {
            ImportFolderBox.Text = path;
        }
    }

    private async Task<string?> PickFolderAsync(string title, string current)
    {
        var options = new FolderPickerOpenOptions { Title = title, AllowMultiple = false };
        if (current.Length > 0 && Directory.Exists(current))
        {
            options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(current);
        }
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        if (ChartFolder.Length == 0 || !Directory.Exists(ChartFolder))
        {
            ErrorText.Text = "Bitte wählen Sie ein vorhandenes Kartenverzeichnis.";
            ErrorText.IsVisible = true;
            return;
        }
        Close(true);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
