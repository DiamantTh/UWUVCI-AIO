namespace UWUVCI.App.Uno.ViewModels;

/// <summary>
/// Represents one entry in the navigation rail / sidebar.
/// </summary>
public sealed class NavigationItem : ObservableObject
{
    private bool _isSelected;

    public required string Label     { get; init; }
    /// <summary>Segoe Fluent / Material icon glyph or unicode char.</summary>
    public required string Glyph     { get; init; }
    public required Type   PageType  { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
}
