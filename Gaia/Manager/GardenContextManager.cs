namespace Gaia.Manager;

/// <summary>
/// Single source of truth for the player's active garden context.
/// Implements IGardenContextProvider so all UI tabs and FarmingManager
/// read bed/plot state through the interface — none of them resolve
/// location logic themselves.
/// </summary>
public class GardenContextManager : IGardenContextProvider
{
    private readonly Plugin _plugin;

    // Snapshot for change detection
    private LocationContext    _lastLocation;
    private GardenPlotState?   _lastActivePlot;

    public LocationContext   CurrentLocation { get; private set; }
    public GardenPlotState?  ActivePlot      { get; private set; }
    public GardenBedState[]? ActiveBeds      { get; private set; }
    public bool              IsIndoors       => CurrentLocation != LocationContext.Outdoors;

    /// <summary>Raised whenever the resolved location or active beds change.</summary>
    public event Action? ContextChanged;

    public GardenContextManager(Plugin plugin) => _plugin = plugin;

    // ─────────────────────────────────────────────────────────
    //  IGardenContextProvider — primary API
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// Re-resolves active plot and beds from the player's current location.
    /// Call this at the start of every Draw() or on Sync button presses.
    /// </summary>
    public void Refresh(int manualSelectedPlot = -1)
    {
        var profile = _plugin.GetCurrentCharacterProfile();
        if (profile == null) return;

        var newLocation   = _plugin.LocationManager.CurrentLocation;
        int houseViewMode = _plugin.LocationManager.LastHouseViewMode;

        CurrentLocation = newLocation;

        GardenPlotState?  newPlot = null;
        GardenBedState[]? newBeds = null;

        if (CurrentLocation == LocationContext.Outdoors)
        {
            newPlot = ResolveOutdoorPlot(profile, manualSelectedPlot);
            newBeds = newPlot?.Beds;
        }
        else
        {
            newBeds = ResolveIndoorBeds(profile, houseViewMode);
        }

        bool changed = newLocation != _lastLocation || newPlot != _lastActivePlot;

        ActivePlot      = newPlot;
        ActiveBeds      = newBeds;
        _lastLocation   = newLocation;
        _lastActivePlot = newPlot;

        if (changed) ContextChanged?.Invoke();
    }

    /// <summary>Backward-compatible alias for Refresh().</summary>
    public void UpdateContext(int manualSelectedPlot = -1) => Refresh(manualSelectedPlot);

    /// <summary>Forces a specific location context (used by manual-override UI).</summary>
    public void SetManualLocation(LocationContext location)
    {
        CurrentLocation = location;
        var profile = _plugin.GetCurrentCharacterProfile();
        if (profile != null)
            ActiveBeds = ResolveIndoorBeds(profile, _plugin.LocationManager.LastHouseViewMode);
    }

    // ─────────────────────────────────────────────────────────
    //  Private resolution helpers
    // ─────────────────────────────────────────────────────────

    private GardenPlotState? ResolveOutdoorPlot(CharacterProfile profile, int manualIndex)
    {
        // GPS matching takes priority when the player has opted in
        if (profile.AutoSelectPlotByGps && Plugin.ObjectTable.LocalPlayer != null)
        {
            var plots = profile.PersonalPlots.Concat(profile.FCPlots);
            var closest = plots
                .Where(p => p.HasGps)
                .OrderBy(p => Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, p.GetGpsVector()))
                .FirstOrDefault();

            if (closest != null &&
                Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, closest.GetGpsVector()) < 20f)
                return closest;
        }

        // Fall back to manual dropdown selection
        if (manualIndex >= 0)
        {
            if (manualIndex < profile.PersonalEstateSize)
                return profile.PersonalPlots[manualIndex];

            int fcIdx = manualIndex - profile.PersonalEstateSize;
            if (fcIdx < profile.FCEstateSize)
                return profile.FCPlots[fcIdx];
        }

        return null;
    }

    private GardenBedState[]? ResolveIndoorBeds(CharacterProfile profile, int houseViewMode)
    {
        return CurrentLocation switch
        {
            LocationContext.House             => houseViewMode == 0 ? profile.PersonalPlanters : profile.FCPlanters,
            LocationContext.FCApartment       => profile.FCApartmentPlanters,
            LocationContext.PersonalApartment => profile.PersonalApartmentPlanters,
            _                                 => null,
        };
    }
}
