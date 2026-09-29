using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChartAssist.Views;

/// <summary>
/// Einfache Meldung oder Rückfrage, Ersatz für <c>MessageBox.Show</c> (TECHNISCHE-BASIS 5.2).
/// Schließen über das Fenster gilt als "Nein" bzw. "Abbrechen".
/// </summary>
public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    /// <summary>Meldung mit "OK".</summary>
    public static async Task ShowAsync(Window owner, string message, string title = "ChartAssist")
    {
        await Create(title, message, "OK", reject: null, acceptIsDefault: true).ShowDialog<bool>(owner);
    }

    /// <summary>Rückfrage mit "Ja" und "Nein".</summary>
    /// <param name="yesIsDefault">false bei Fragen mit Folgen, z. B. Löschen: Enter wählt dann "Nein".</param>
    public static Task<bool> AskAsync(Window owner, string title, string message, bool yesIsDefault = true) =>
        Create(title, message, "Ja", "Nein", yesIsDefault).ShowDialog<bool>(owner);

    /// <summary>Hinweis mit "OK" und "Abbrechen".</summary>
    public static Task<bool> ConfirmAsync(Window owner, string title, string message) =>
        Create(title, message, "OK", "Abbrechen", acceptIsDefault: true).ShowDialog<bool>(owner);

    private static MessageDialog Create(string title, string message, string accept, string? reject, bool acceptIsDefault)
    {
        var dialog = new MessageDialog { Title = title };
        dialog.MessageText.Text = message;
        dialog.AcceptButton.Content = accept;
        dialog.AcceptButton.IsDefault = acceptIsDefault;
        dialog.RejectButton.Content = reject;
        dialog.RejectButton.IsVisible = reject != null;
        dialog.RejectButton.IsDefault = !acceptIsDefault;
        if (reject == null)
        {
            dialog.AcceptButton.IsCancel = true;
        }
        return dialog;
    }

    private void OnAccept(object? sender, RoutedEventArgs e) => Close(true);

    private void OnReject(object? sender, RoutedEventArgs e) => Close(false);
}
