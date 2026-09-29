using System.Collections.ObjectModel;
using ChartAssist.Core.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ChartAssist.ViewModels;

/// <summary>Knoten im Baum des Hauptfensters: Gruppe, Flugplatz, Karte oder Aktualisierung.</summary>
public partial class TreeNodeViewModel(string header) : ViewModelBase
{
    public string Header { get; } = header;

    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    /// <summary>Gruppenknoten "Flugplätze" und "Aktualisierungen" werden fett dargestellt.</summary>
    public bool IsGroup { get; init; }

    public Airfield? Airfield { get; init; }

    public Chart? Chart { get; init; }

    /// <summary>Unter "Flugplätze" lässt sich über das Kontextmenü der Flugplatz löschen.</summary>
    public bool CanDeleteAirfield { get; init; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }
}
