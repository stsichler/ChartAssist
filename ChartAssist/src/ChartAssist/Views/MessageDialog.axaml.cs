using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChartAssist.Views;

/// <summary>
/// Einfache Rückfrage mit Ja/Nein, Ersatz für <c>MessageBox.Show</c> (TECHNISCHE-BASIS 5.2).
/// Wird in Phase 4 um weitere Button-Varianten ergänzt.
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    /// <summary>Zeigt die Frage modal an. Schließen über das Fenster gilt als "Nein".</summary>
    public static async Task<bool> AskAsync(Window owner, string title, string message)
    {
        var dialog = new MessageDialog { Title = title };
        dialog.MessageText.Text = message;
        return await dialog.ShowDialog<bool>(owner);
    }

    private void OnYes(object? sender, RoutedEventArgs e) => Close(true);

    private void OnNo(object? sender, RoutedEventArgs e) => Close(false);
}
