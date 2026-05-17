namespace Gaia.Helpers;

/// <summary>
/// A centralized data repository for Gaia. 
/// Holds all hardcoded game data, crop names, seed mappings, and growth math.
/// By keeping this here, the UI tabs don't have to constantly redefine what a "Thavnairian Onion" is!
/// </summary>
public static class GardenData
{
    // ==========================================
    // --- CORE IDENTIFIERS ---
    // ==========================================

    /// <summary>
    /// The Data IDs for the physical garden patch objects in the game world.
    /// 2003757 = Deluxe (8 beds), 2003756 = Standard (6 beds), 2003755 = Round (4 beds)
    /// </summary>
    public static readonly uint[] ValidPatches = { 2003757, 2003756, 2003755 };

    // ==========================================
    // --- ITEM TRACKING ARRAYS ---
    // ==========================================

    public static readonly string[] CropsToLoad = {
        "Thavnairian Onion", "Apricot", "Shroud Tea", "Mimett Gourd", "Midland Cabbage",
        "Krakka Root", "Tantalplant", "Wizard Eggplant", "Royal Kukuru Bean",
        "Blood Currant", "Mirror Apple", "Pixie Plum", "Sun Lemon", "Rolanberry", "Old World Fig",
        "Almonds", "Coerthan Tea Leaves", "Mandrake", "Pearl Roselle", "Nymeia Lily",
        "Curiel Root", "Sylkis Bud", "Pahsana Fruit", "Glazenut", "La Noscean Leek",
        "Prickly Pineapple", "Cieldalaes Pineapple", "Gysahl Greens"
    };

    public static readonly string[] TrackedSeeds = {
        "Thavnairian Onion Seeds", "Apricot Kernels", "Shroud Tea Seeds",
        "Mimett Gourd Seeds", "Midland Cabbage Seeds", "Krakka Root Seeds", "Tantalplant Seeds", "Wizard Eggplant Seeds",
        "Royal Kukuru Seeds", "Blood Currant Seeds", "Mirror Apple Seeds", "Pixie Plum Seeds", "Sun Lemon Seeds", "Rolanberry Seeds", "Old World Fig Seeds",
        "Almond Seeds", "Coerthan Tea Seeds", "Mandrake Seeds", "Pearl Roselle Seeds", "Nymeia Lily Seeds",
        "Curiel Root Seeds", "Sylkis Bud Seeds", "Pahsana Fruit Seeds", "Glazenut Seeds", "La Noscean Leek Seeds",
        "Prickly Pineapple Seeds", "Cieldalaes Pineapple Seeds", "Gysahl Greens Seeds"
    };

    // Seeds that the plugin de-emphasises for outdoor 8-bed plots. Inventory panel
    // still counts them and indoor pot dropdown still offers them, but the outdoor
    // per-slot seed picker hides them — outdoor plots are typically used for cross
    // parents (Royal Kukuru × X), not for planting finished exotic seeds directly.
    // Soft preference, not a game-engine rule.
    public static readonly HashSet<string> ExoticSeeds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Thavnairian Onion Seeds",
        "Prickly Pineapple Seeds",
        "Cieldalaes Pineapple Seeds",
    };

    public static readonly string[] TrackedSoils = {
        "Grade 3 Thanalan Topsoil", "Grade 3 Shroud Topsoil", "Grade 3 La Noscean Topsoil",
        "Grade 2 Thanalan Topsoil", "Grade 2 Shroud Topsoil", "Grade 2 La Noscean Topsoil",
        "Grade 1 Thanalan Topsoil", "Grade 1 Shroud Topsoil", "Grade 1 La Noscean Topsoil",
        "Potting Soil"
    };

    // ==========================================
    // --- AUTO-FILL PRIORITY LOGIC ---
    // ==========================================

    public static readonly string[] CrossSoilPriority = {
        "Grade 3 Thanalan Topsoil", "Potting Soil",
        "Grade 1 Thanalan Topsoil", "Grade 1 Shroud Topsoil", "Grade 1 La Noscean Topsoil",
        "Grade 2 Thanalan Topsoil", "Grade 2 Shroud Topsoil", "Grade 2 La Noscean Topsoil",
        "Grade 3 Shroud Topsoil", "Grade 3 La Noscean Topsoil"
    };

    public static readonly string[] TempSoilPriority = {
        "Potting Soil",
        "Grade 1 Thanalan Topsoil", "Grade 1 Shroud Topsoil", "Grade 1 La Noscean Topsoil",
        "Grade 2 Thanalan Topsoil", "Grade 2 Shroud Topsoil", "Grade 2 La Noscean Topsoil",
        "Grade 3 Shroud Topsoil", "Grade 3 La Noscean Topsoil", "Grade 3 Thanalan Topsoil"
    };

    // ==========================================
    // --- TRANSLATION MAPS ---
    // ==========================================

    public static readonly Dictionary<string, string> SeedToCropMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Thavnairian Onion Seeds", "Thavnairian Onion" }, { "Apricot Kernels", "Apricot" }, { "Shroud Tea Seeds", "Shroud Tea" },
        { "Mimett Gourd Seeds", "Mimett Gourd" }, { "Midland Cabbage Seeds", "Midland Cabbage" },
        { "Krakka Root Seeds", "Krakka Root" }, { "Tantalplant Seeds", "Tantalplant" }, { "Wizard Eggplant Seeds", "Wizard Eggplant" },
        { "Royal Kukuru Seeds", "Royal Kukuru Bean" },
        { "Blood Currant Seeds", "Blood Currant" }, { "Mirror Apple Seeds", "Mirror Apple" },
        { "Pixie Plum Seeds", "Pixie Plum" }, { "Sun Lemon Seeds", "Sun Lemon" }, { "Rolanberry Seeds", "Rolanberry" }, { "Old World Fig Seeds", "Old World Fig" },
        { "Almond Seeds", "Almonds" }, { "Coerthan Tea Seeds", "Coerthan Tea Leaves" }, { "Mandrake Seeds", "Mandrake" },
        { "Pearl Roselle Seeds", "Pearl Roselle" }, { "Nymeia Lily Seeds", "Nymeia Lily" },
        { "Curiel Root Seeds", "Curiel Root" }, { "Sylkis Bud Seeds", "Sylkis Bud" }, { "Pahsana Fruit Seeds", "Pahsana Fruit" },
        { "Glazenut Seeds", "Glazenut" }, { "La Noscean Leek Seeds", "La Noscean Leek" },
        { "Prickly Pineapple Seeds", "Prickly Pineapple" }, { "Cieldalaes Pineapple Seeds", "Cieldalaes Pineapple" },
        { "Gysahl Greens Seeds", "Gysahl Greens" }
    };

    // ==========================================
    // --- DICTIONARIES & EXCEL DATA ---
    // ==========================================

    public static Dictionary<string, uint> CropIcons { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, uint> ItemIds { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public static void Initialize(Dalamud.Plugin.Services.IDataManager dataManager)
    {
        if (CropIcons.Count > 0) return;

        var itemSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        if (itemSheet == null) return;

        var cropsToFind = new HashSet<string>(CropsToLoad, StringComparer.OrdinalIgnoreCase);
        var itemsToFind = new HashSet<string>(TrackedSeeds, StringComparer.OrdinalIgnoreCase);
        foreach (var soil in TrackedSoils) itemsToFind.Add(soil);

        foreach (var item in itemSheet)
        {
            string itemName = item.Name.ToString();

            if (cropsToFind.Contains(itemName))
            {
                CropIcons[itemName] = item.Icon;
                cropsToFind.Remove(itemName);
            }
            if (itemsToFind.Contains(itemName))
            {
                ItemIds[itemName] = item.RowId;
                itemsToFind.Remove(itemName);
            }

            if (cropsToFind.Count == 0 && itemsToFind.Count == 0) break;
        }
    }

    // ==========================================
    // --- HELPER MATH & LOGIC ---
    // ==========================================

    public static string NormalizeCropNameForIcon(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        string iconSearch = name;

        if (iconSearch.Contains("Thav", StringComparison.OrdinalIgnoreCase)) return "Thavnairian Onion";
        if (iconSearch.Contains("Royal Kukuru", StringComparison.OrdinalIgnoreCase)) return "Royal Kukuru Bean";
        if (iconSearch.Contains("Wiz. Eggplant", StringComparison.OrdinalIgnoreCase)) return "Wizard Eggplant";
        if (iconSearch.Contains("Mid. Cabbage", StringComparison.OrdinalIgnoreCase)) return "Midland Cabbage";
        if (iconSearch.Contains("LaNo Leek", StringComparison.OrdinalIgnoreCase)) return "La Noscean Leek";

        return iconSearch;
    }

    public static uint GetIconIdForName(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        string iconSearch = NormalizeCropNameForIcon(name);
        return CropIcons.TryGetValue(iconSearch, out uint iconId) ? iconId : 0;
    }

    public static int GetYieldScore(string yieldName)
    {
        if (string.IsNullOrEmpty(yieldName)) return 0;

        if (yieldName.Contains("Thav", StringComparison.OrdinalIgnoreCase)) return 100;

        if (yieldName.Contains("Glazenut") || yieldName.Contains("Blood Currant") || yieldName.Contains("Krakka") ||
            yieldName.Contains("Tantalplant") || yieldName.Contains("Curiel") || yieldName.Contains("Sylkis") ||
            yieldName.Contains("Pahsana") || yieldName.Contains("Mimett") || yieldName.Contains("Apricot") ||
            yieldName.Contains("Royal Kukuru") || yieldName.Contains("LaNo Leek")) return 50;

        if (yieldName.Contains("Old World Fig") || yieldName.Contains("Mirror Apple") || yieldName.Contains("Rolanberry") ||
            yieldName.Contains("Sun Lemon") || yieldName.Contains("Pixie Plum")) return 25;

        return 10;
    }

    public static float GetCropGrowTime(string seedName)
    {
        if (string.IsNullOrEmpty(seedName)) return 120f;
        string lower = seedName.ToLower();

        if (lower.Contains("thavnairian onion")) return 240f;

        if (lower.Contains("glazenut") || lower.Contains("blood currant") || lower.Contains("nymeia lily") ||
            lower.Contains("pearl roselle") || lower.Contains("royal kukuru") || lower.Contains("broombush") ||
            lower.Contains("jute") || lower.Contains("chives")) return 168f;

        if (lower.Contains("krakka")) return 72f;

        return 120f;
    }
}