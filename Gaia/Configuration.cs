using Gaia.Core.Data;

namespace Gaia;

/// <summary>Visual theme for the Dashboard panel.</summary>
public enum GaiaTheme
{
    GardenCodex = 0,  // Dark amber/brown — default
    FieldGuide = 1,  // Parchment/sepia
    Kawaii = 2,  // Soft pink/pastel with animation
}

[Serializable]
public class GardenBedState
{
    public string SeedName { get; set; } = "";
    public DateTime LastWateredTime { get; set; } = DateTime.Now;
    public DateTime PlantedTime { get; set; } = DateTime.MinValue;
    public DateTime LastFertilizedTime { get; set; } = DateTime.MinValue;
    public bool IsMature { get; set; } = false;
    public bool IsEmpty { get; set; } = true;
    public bool SkipHarvest { get; set; } = false;
    public bool IsWilted { get; set; } = false;
    public string InteractionError { get; set; } = "";
    public long InteractionErrorTimeout { get; set; } = 0;
    public float GpsX { get; set; } = 0f;
    public float GpsY { get; set; } = 0f;
    public float GpsZ { get; set; } = 0f;
    public uint DataId { get; set; } = 0;
    public bool HasGps => GpsX != 0 || GpsY != 0 || GpsZ != 0;
    public Vector3 GetGpsVector() => new Vector3(GpsX, GpsY, GpsZ);
}

[Serializable]
public class GardenPlotState
{
    // Individual Plot Stats
    public int TotalWaterings { get; set; } = 0;
    public int TotalFertilizings { get; set; } = 0;
    public int TotalHarvests { get; set; } = 0;

    // The Graph Data! Stores the hours elapsed between each watering.
    public List<float> WateringIntervalHistory { get; set; } = new List<float>();

    // Helper to keep the graph from getting infinitely large
    public void AddWateringDataPoint(float hoursSinceLastWater)
    {
        WateringIntervalHistory.Add(hoursSinceLastWater);
        if (WateringIntervalHistory.Count > 50)
        {
            WateringIntervalHistory.RemoveAt(0); // Keep only the last 50 data points
        }
    }

    public string TopExpectedYield { get; set; } = "";
    public GardenBedState[] Beds { get; set; } = new GardenBedState[8];
    public float GpsX { get; set; } = 0f;
    public float GpsY { get; set; } = 0f;
    public float GpsZ { get; set; } = 0f;
    public uint PatchId { get; set; } = 2003757;
    public int BedCount { get; set; } = 8;
    public bool HasGps => GpsX != 0;
    public Vector3 GetGpsVector() => new Vector3(GpsX, GpsY, GpsZ);
    public GardenPlotState() { for (int i = 0; i < 8; i++) Beds[i] = new GardenBedState(); }
}

[Serializable]
public class CharacterProfile
{
    public bool HasCompletedOnboarding { get; set; } = false;
    public bool AutoSelectPlotByGps { get; set; } = true;
    public int PersonalHouseSize { get; set; } = 0;
    public int FCHouseSize { get; set; } = 0;
    public int PersonalPlanterCount { get; set; } = 0;
    public GardenPlotState[] PersonalPlots { get; set; } = new GardenPlotState[3] { new(), new(), new() };
    public GardenBedState[] PersonalPlanters { get; set; } = new GardenBedState[4] { new(), new(), new(), new() };
    public GardenBedState[] PersonalApartmentPlanters { get; set; } = new GardenBedState[2] { new(), new() };
    public int PersonalEstateSize { get; set; } = 3;
    public int PersonalApartmentPlanterCount { get; set; } = 0;
    public int FCPlanterCount { get; set; } = 0;
    public GardenPlotState[] FCPlots { get; set; } = new GardenPlotState[3] { new(), new(), new() };
    public GardenBedState[] FCPlanters { get; set; } = new GardenBedState[4] { new(), new(), new(), new() };
    public GardenBedState[] FCApartmentPlanters { get; set; } = new GardenBedState[2] { new(), new() };
    public int FCEstateSize { get; set; } = 3;
    public int FCApartmentPlanterCount { get; set; } = 0;
}

[Serializable]
public class Configuration : BaseConfiguration
{
    // Version is inherited from BaseConfiguration

    public bool ShowDebugMessages = false;
    public int FertilizerChoice = 0;

    // --- TIMING VARIABLES ---
    public int WaterMenuDelay = 300;
    public int HarvestAnimDelay = 1500;
    public int GeneralDelay = 300;
    public int BetweenPlantsDelay = 1000;
    public int InteractionTimeout = 4000;
    public int ShortTickDelay = 100;
    public int PostTimeoutDelay = 200;
    public int MenuSelectionDelay = 500;

    // --- Item & UI Settings ---
    public uint FertilizerId { get; set; } = 7767; // Fish Meal
    public string InventoryAddonName { get; set; } = "InventoryGrid3E";

    // --- Magic Timers (in milliseconds) ---
    public int StateTimeout { get; set; } = 15000;
    public int InventoryWaitTimeout { get; set; } = 3500;
    public int FertilizeVerifyTimeout { get; set; } = 3000;
    public int MenuInteractionTimeout { get; set; } = 3000;

    public int FertilizePollDelay { get; set; } = 50;
    public int NumpadPollDelay { get; set; } = 150;
    public int SkipPlantDelay { get; set; } = 200;
    public int MenuSuccessDelay { get; set; } = 100;
    public int FertilizeSuccessDelay { get; set; } = 1000;

    public int AdvanceIndoorDelay { get; set; } = 800;
    public int AdvanceOutdoorDelay { get; set; } = 300;

    // --- Lifetime Analytics ---
    public int TotalBedsWatered { get; set; } = 0;
    public int TotalBedsFertilized { get; set; } = 0;
    public int TotalPlantsPlanted { get; set; } = 0;
    public int TotalBedsHarvested { get; set; } = 0;
    public Dictionary<string, int> LifetimeHarvestYields { get; set; } = new Dictionary<string, int>();

    // --- Dashboard & UI ---
    public GaiaTheme SelectedTheme { get; set; } = GaiaTheme.GardenCodex;

    // --- Per-character data ---
    public Dictionary<ulong, CharacterProfile> Characters { get; set; } = new Dictionary<ulong, CharacterProfile>();

    // NOTE: Save() is inherited from BaseConfiguration — no longer needs
    // a static reference to Plugin.PluginInterface.
}
