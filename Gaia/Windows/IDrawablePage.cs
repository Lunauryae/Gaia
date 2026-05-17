namespace Gaia.Windows;

/// <summary>
/// Contract for every tab panel in Gaia's main window.
/// Implementing this interface is what lets MainWindow iterate a clean
/// List&lt;IDrawablePage&gt; instead of knowing about every concrete tab type.
/// </summary>
public interface IDrawablePage
{
    /// <summary>Text shown on the tab button.</summary>
    string TabLabel { get; }

    /// <summary>Called every frame while this tab is active.</summary>
    void Draw();
}
