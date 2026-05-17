namespace Gaia.Manager;

/// <summary>
/// Single source of truth for the player's active garden context.
/// All UI tabs and FarmingManager read active bed/plot state through this
/// interface — they never perform location resolution themselves.
/// </summary>
public interface IGardenContextProvider
{
    /// <summary>Where the player currently is (Outdoors / House / FCApartment / PersonalApartment).</summary>
    LocationContext CurrentLocation { get; }

    /// <summary>The resolved outdoor plot, or null when indoors or GPS unmatched.</summary>
    GardenPlotState? ActivePlot { get; }

    /// <summary>
    /// The active bed array for the current context.
    /// Outdoor: ActivePlot.Beds. Indoor: the resolved pot array.
    /// </summary>
    GardenBedState[]? ActiveBeds { get; }

    /// <summary>True whenever CurrentLocation is not Outdoors.</summary>
    bool IsIndoors { get; }

    /// <summary>
    /// Re-resolves the active plot and beds from the player's current location.
    /// Call this at the start of every Draw() or on Sync button presses.
    /// </summary>
    void Refresh(int manualSelectedPlot = -1);

    /// <summary>Forces a specific location context (used by manual override UI).</summary>
    void SetManualLocation(LocationContext location);

    /// <summary>Raised whenever the resolved location or active beds change.</summary>
    event Action? ContextChanged;
}
