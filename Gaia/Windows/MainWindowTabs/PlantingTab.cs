using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using Gaia.Helpers;
using Gaia.Manager;

namespace Gaia.Windows.MainWindowTabs;

public enum PatternType { None, FourByFour, RichMan, PoorMan }

public class PlantingTab : IDrawablePage
{
    private readonly Plugin plugin;

    public string TabLabel => "  Planting  ";
    private bool manualOverride = false;
    private int manualContextSelection = 0;

    private int selectedTargetPlot = 0;

    private string[] plannedSeeds = new string[8] { "", "", "", "", "", "", "", "" };
    private string[] plannedSoils = new string[8] { "", "", "", "", "", "", "", "" };

    private int selectedPath = 0;
    private string[] pathOptions = { "The Royal Road", "The Twin Flower Power" };

    private int rightBranchLayout = 0;
    private int premiumLayout = 0;
    private string[] layoutOptions = { "4x4 Setup", "5x3 Rich Man's Setup", "5x3 Poor Man's Setup" };

    private int selectedGoal = 0;
    private string[] goalOptions = { "Family Offspring", "Thavnairian Offspring" };
    private int thavLayout = 0;

    public bool UseTemporarySeed { get; private set; } = true;

    private string seedSearchFilter = "";
    private string soilSearchFilter = "";

    private readonly Dictionary<string, int> ownedSeeds = new();
    private readonly Dictionary<string, int> ownedSoils = new();

    private int _plantingMode = 0; // 0 = Easy, 1 = Advanced
    private int _easyGoalSelection = 0; // For the Easy Mode dropdown

    public PlantingTab(Plugin plugin)
    {
        this.plugin = plugin;
    }

    private void ScanInventory()
    {
        ownedSeeds.Clear();
        foreach (var seed in GardenData.TrackedSeeds)
        {
            int count = InventoryHelper.GetItemCount(seed);
            if (count > 0) ownedSeeds[seed] = count;
        }

        ownedSoils.Clear();
        foreach (var soil in GardenData.TrackedSoils)
        {
            int count = InventoryHelper.GetItemCount(soil);
            if (count > 0) ownedSoils[soil] = count;
        }
    }

    private (bool canPlant, int reqA, int reqB, int reqSoil, int reqTemp, int ownedA, int ownedB, int ownedSoil, int ownedTemp, string seedA, string seedB, string[] bestBlueprint) CalculateEasyModeRequirements(GardenBedState[] realBeds, int maxBeds)
    {
        var pair = GetBestThavCross();
        string seedA = string.IsNullOrEmpty(pair.seedA) ? "Royal Kukuru Seeds" : pair.seedA;
        string seedB = string.IsNullOrEmpty(pair.seedB) ? "Tantalplant Seeds" : pair.seedB;

        // 1. Generate the possible Blueprints
        string[] blueprintNormal = new string[8];
        string[] blueprintInverse = new string[8];

        for (int i = 0; i < 8; i++)
        {
            blueprintNormal[i] = (i % 2 == 0) ? seedA : seedB;
            blueprintInverse[i] = (i % 2 == 0) ? seedB : seedA;
        }

        // 2. Score them against the real garden (Lower score = fewer uproots needed)
        int CalculateErrors(string[] blueprint)
        {
            int errors = 0;
            for (int i = 0; i < 8; i++)
            {
                if (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty)
                {
                    if (!realBeds[i].SeedName.Contains(blueprint[i].Replace(" Seeds", ""), StringComparison.OrdinalIgnoreCase))
                        errors++;
                }
            }
            return errors;
        }

        // 3. Adopt the best fitting blueprint!
        string[] chosenBlueprint = (CalculateErrors(blueprintInverse) < CalculateErrors(blueprintNormal)) ? blueprintInverse : blueprintNormal;

        // 4. Calculate exactly what we need to buy based on the winning blueprint
        int neededA = 0;
        int neededB = 0;
        int neededSoil = 0;
        int neededTemp = 0;
        bool isCompletelyEmpty = true;

        for (int i = 0; i < 8; i++)
        {
            string idealSeed = chosenBlueprint[i];

            if (realBeds == null || realBeds.Length <= i || realBeds[i] == null || realBeds[i].IsEmpty)
            {
                if (idealSeed == seedA) neededA++; else neededB++;
                neededSoil++;
            }
            else
            {
                isCompletelyEmpty = false;
                string realSeedName = realBeds[i].SeedName;
                if (!realSeedName.Contains(idealSeed.Replace(" Seeds", ""), StringComparison.OrdinalIgnoreCase))
                {
                    if (idealSeed == seedA) neededA++; else neededB++;
                    neededSoil++;
                }
            }
        }

        if (isCompletelyEmpty) { neededA++; neededTemp++;}

        int ownedA = ownedSeeds.TryGetValue(seedA, out int a) ? a : 0;
        int ownedB = ownedSeeds.TryGetValue(seedB, out int b) ? b : 0;
        int ownedSoil = ownedSoils.TryGetValue("Grade 3 Thanalan Topsoil", out int g) ? g : 0;
        int ownedTemp = ownedSoils.TryGetValue("Potting Soil", out int p) ? p : 0;

        bool canPlant = (ownedA >= neededA) && (ownedB >= neededB) && (ownedSoil >= neededSoil) && (ownedTemp >= neededTemp);

        return (canPlant, neededA, neededB, neededSoil, neededTemp, ownedA, ownedB, ownedSoil, ownedTemp, seedA, seedB, chosenBlueprint);
    }
    public void Draw()
    {
        var currentProfile = plugin.GetCurrentCharacterProfile();
        if (currentProfile == null) return;

        ScanInventory();

        LocationContext currentLocation = plugin.LocationManager.CurrentLocation;
        int lastHouseViewMode = plugin.LocationManager.LastHouseViewMode;
        bool isBusy = plugin.Farming.CurrentState != FarmingState.Idle;

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Active Plot/Pot Planning");
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 155f);

        ImGui.BeginDisabled(isBusy);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.7f, 1f));
        if (ImGui.Button("🔍 Sync / Scan Active", new Vector2(150, 25)))
        {
            plugin.Farming.IsIndoors = currentLocation != LocationContext.Outdoors;

            if (plugin.Farming.IsIndoors)
            {
                if (currentLocation == LocationContext.House) plugin.Farming.ActiveIndoorPots = lastHouseViewMode == 0 ? currentProfile.PersonalPlanters : currentProfile.FCPlanters;
                else if (currentLocation == LocationContext.FCApartment) plugin.Farming.ActiveIndoorPots = currentProfile.FCApartmentPlanters;
                else if (currentLocation == LocationContext.PersonalApartment) plugin.Farming.ActiveIndoorPots = currentProfile.PersonalApartmentPlanters;
            }
            else
            {
                plugin.Farming.IsPersonalPlot = selectedTargetPlot < currentProfile.PersonalEstateSize;
                plugin.Farming.CurrentPlotIndex = plugin.Farming.IsPersonalPlot ? selectedTargetPlot : selectedTargetPlot - currentProfile.PersonalEstateSize;
            }
            plugin.Farming.ScanGarden();
        }
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Dynamically configures your layout based on your current physical location.");
        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.Checkbox("Manual Context Override", ref manualOverride);
        if (manualOverride)
        {
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200f);
            string[] contextOptions = { "Outdoors", "House (FC/Personal)", "FC Private Chamber", "Personal Apartment" };
            ImGui.Combo("##ManualContext", ref manualContextSelection, contextOptions, contextOptions.Length);
            currentLocation = (LocationContext)(manualContextSelection + 1);
        }

        bool isIndoors = currentLocation != LocationContext.Outdoors;

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        int maxBeds = 8;
        string contextTitle = "";
        GardenBedState[]? activePots = null;

        // --- PLOT RESOLUTION LOGIC ---
        if (!isIndoors)
        {
            List<string> plotNames = new();
            List<(bool isPersonal, int index)> plotRefs = new();

            for (int i = 0; i < currentProfile.PersonalEstateSize; i++) { plotNames.Add($"Personal Plot {i + 1}"); plotRefs.Add((true, i)); }
            for (int i = 0; i < currentProfile.FCEstateSize; i++) { plotNames.Add($"FC Plot {i + 1}"); plotRefs.Add((false, i)); }

            if (plotNames.Count == 0) { ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "No outdoor estates configured! Go to the Settings tab."); return; }

            if (currentProfile.AutoSelectPlotByGps && Plugin.ObjectTable.LocalPlayer != null)
            {
                float closestDistance = 20.0f;
                int closestIndex = -1;
                for (int i = 0; i < plotRefs.Count; i++)
                {
                    var p = plotRefs[i].isPersonal ? currentProfile.PersonalPlots[plotRefs[i].index] : currentProfile.FCPlots[plotRefs[i].index];
                    if (p.HasGps)
                    {
                        float dist = Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, p.GetGpsVector());
                        if (dist < closestDistance) { closestDistance = dist; closestIndex = i; }
                    }
                }
                if (closestIndex != -1 && !manualOverride) selectedTargetPlot = closestIndex;
            }

            if (selectedTargetPlot >= plotNames.Count) selectedTargetPlot = 0;

            var target = plotRefs[selectedTargetPlot];
            var activePlot = target.isPersonal ? currentProfile.PersonalPlots[target.index] : currentProfile.FCPlots[target.index];
            maxBeds = activePlot.BedCount;
            activePots = activePlot.Beds;

            ImGui.SetNextItemWidth(200f);
            ImGui.BeginDisabled(currentProfile.AutoSelectPlotByGps && activePlot.HasGps && !manualOverride);
            ImGui.Combo("Plan For Plot", ref selectedTargetPlot, plotNames.ToArray(), plotNames.Count);
            ImGui.EndDisabled();
            ImGuiHelpers.ScaledDummy(10.0f);
        }
        else
        {
            if (currentLocation == LocationContext.House)
            {
                if (lastHouseViewMode == 0) { contextTitle = "Personal House Planters"; maxBeds = currentProfile.PersonalPlanterCount; activePots = currentProfile.PersonalPlanters; }
                else { contextTitle = "Free Company Planters"; maxBeds = currentProfile.FCPlanterCount; activePots = currentProfile.FCPlanters; }
            }
            else if (currentLocation == LocationContext.FCApartment) { contextTitle = "FC Private Chamber Pots"; maxBeds = currentProfile.FCApartmentPlanterCount; activePots = currentProfile.FCApartmentPlanters; }
            else if (currentLocation == LocationContext.PersonalApartment) { contextTitle = "Personal Apartment Pots"; maxBeds = currentProfile.PersonalApartmentPlanterCount; activePots = currentProfile.PersonalApartmentPlanters; }

            if (maxBeds == 0) { ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), $"No {contextTitle} configured! Go to the Stats tab."); return; }

            ImGui.TextColored(new Vector4(0.2f, 0.7f, 0.8f, 1f), $"Planning for: {contextTitle}");
            ImGuiHelpers.ScaledDummy(10.0f);
        }

        // ====================================================
        // THE TRAFFIC COP (Progressive Disclosure)
        // ====================================================

        // 1. Check if the active plot is completely full
        bool isFullyPlanted = true;
        if (activePots != null)
        {
            for (int i = 0; i < maxBeds; i++)
            {
                if (activePots[i] == null || activePots[i].IsEmpty) { isFullyPlanted = false; break; }
            }
        }

        // 2. If it's full (and we aren't overriding), block the UI
        if (isFullyPlanted && !manualOverride && activePots != null && activePots.Length > 0)
        {
            DrawFullyPlantedScreen();
            return;
        }

        // 3. Draw the Easy/Advanced Toggle
        DrawModeToggle();

        // 4. Route to the chosen UI
        if (_plantingMode == 0)
        {
            DrawEasyModeUI(maxBeds, isIndoors, activePots!, isBusy, currentProfile);
        }
        else
        {
            if (isIndoors) DrawIndoorUI(currentProfile, maxBeds, activePots!, isBusy);
            else DrawOutdoorUI(currentProfile, maxBeds, activePots!, isBusy);
        }
    }

    private void DrawModeToggle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5f);

        ImGui.PushStyleColor(ImGuiCol.Button, _plantingMode == 0 ? new Vector4(0.2f, 0.6f, 0.2f, 1f) : new Vector4(0.2f, 0.2f, 0.2f, 1f));
        if (ImGui.Button("🌱 Easy Mode (Auto-Planner)", new Vector2(ImGui.GetContentRegionAvail().X / 2f - 5, 30))) _plantingMode = 0;
        ImGui.PopStyleColor();

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, _plantingMode == 1 ? new Vector4(0.6f, 0.3f, 0.9f, 1f) : new Vector4(0.2f, 0.2f, 0.2f, 1f));
        if (ImGui.Button("⚙️ Advanced Mode (Manual)", new Vector2(ImGui.GetContentRegionAvail().X, 30))) _plantingMode = 1;
        ImGui.PopStyleColor();

        ImGui.PopStyleVar();
        ImGuiHelpers.ScaledDummy(15.0f);
    }

    private void DrawFullyPlantedScreen()
    {
        ImGuiHelpers.ScaledDummy(60.0f);

        string msg1 = "🌱 This plot is currently fully planted! 🌱";
        string msg2 = "Head over to the Tending tab to water and fertilize your crops.";

        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(msg1).X) / 2);
        ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1f), msg1);

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(msg2).X) / 2);
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), msg2);

        ImGuiHelpers.ScaledDummy(30.0f);

        string msg3 = "(If you want to plan ahead or uproot, check 'Manual Context Override' above).";
        ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(msg3).X) / 2);
        ImGui.TextColored(new Vector4(0.4f, 0.4f, 0.4f, 1f), msg3);
    }

    // ====================================================
    // EASY MODE (The Shopping List Logic)
    // ====================================================
    private void DrawEasyModeUI(int maxBeds, bool isIndoors, GardenBedState[] activePots, bool isBusy, CharacterProfile currentProfile)
    {
        if (isIndoors)
        {
            DrawIndoorEasyMode(maxBeds, activePots, isBusy);
            return;
        }
        if (maxBeds < 8)
        {
            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), "Easy Mode crossbreeding requires an 8-bed outdoor plot.");
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "For smaller plots, please use Advanced Mode.");
            return;
        }

        // STEP 1
        ImGui.TextColored(new Vector4(0.3f, 0.7f, 0.9f, 1f), "Step 1: What is your ultimate goal?");
        ImGuiHelpers.ScaledDummy(5.0f);

        string[] easyGoals = { "Thavnairian Onions (Auto-Best Cross)" };
        ImGui.SetNextItemWidth(250f);
        ImGui.Combo("##EasyGoal", ref _easyGoalSelection, easyGoals, easyGoals.Length);
        ImGuiHelpers.ScaledDummy(15.0f);

        // STEP 2: Calculate Needs
        ImGui.TextColored(new Vector4(0.3f, 0.7f, 0.9f, 1f), "Step 2: Gather Supplies");
        ImGuiHelpers.ScaledDummy(5.0f);

        var reqs = CalculateEasyModeRequirements(activePots, maxBeds);

        if (reqs.reqA == 0 && reqs.reqB == 0)
        {
            ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1f), "Plot is currently perfectly planted for this cross!");
            return;
        }

        // Draw the Checklist dynamically based on the missing spots
        if (reqs.reqA > 0) DrawChecklistItem(reqs.ownedA >= reqs.reqA, $"{reqs.reqA}x {reqs.seedA.Replace(" Seeds", "")}", reqs.ownedA);
        if (reqs.reqB > 0) DrawChecklistItem(reqs.ownedB >= reqs.reqB, $"{reqs.reqB}x {reqs.seedB.Replace(" Seeds", "")}", reqs.ownedB);
        if (reqs.reqSoil > 0) DrawChecklistItem(reqs.ownedSoil >= reqs.reqSoil, $"{reqs.reqSoil}x Grade 3 Thanalan Topsoil", reqs.ownedSoil);
        if (reqs.reqTemp > 0) DrawChecklistItem(reqs.ownedTemp >= reqs.reqTemp, $"{reqs.reqTemp}x Potting Soil (For Temp Seed)", reqs.ownedTemp);

        ImGuiHelpers.ScaledDummy(20.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(20.0f);

        // --- NEW: EXPECTED YIELDS UI ---
        ImGui.TextColored(new Vector4(0.3f, 0.7f, 0.9f, 1f), "Expected Child Seeds:");
        ImGuiHelpers.ScaledDummy(5.0f);

        if (plugin.CrossbreedManager.IsLoaded)
        {
            string cA = GardenData.SeedToCropMap.TryGetValue(reqs.seedA, out var ca) ? ca : reqs.seedA;
            string cB = GardenData.SeedToCropMap.TryGetValue(reqs.seedB, out var cb) ? cb : reqs.seedB;
            string result = plugin.CrossbreedManager.GetCross(cA, cB);

            if (result != "Unknown / None")
            {
                var yields = result.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < yields.Length; i++)
                {
                    if (i > 0) ImGui.SameLine();

                    string yieldName = yields[i].Trim();
                    Vector2 startPos = ImGui.GetCursorScreenPos();
                    Vector2 boxSize = new Vector2(65, 75);

                    var drawList = ImGui.GetWindowDrawList();
                    drawList.AddRectFilled(startPos, startPos + boxSize, ImGui.GetColorU32(ImGuiCol.Button), 5.0f);

                    uint iconId = GardenData.GetIconIdForName(yieldName);
                    if (iconId != 0)
                    {
                        var iconTex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                        if (iconTex != null) drawList.AddImage(iconTex.Handle, startPos + new Vector2(12, 5), startPos + new Vector2(52, 45));
                    }

                    string disp = yieldName.Replace(" Seeds", "");
                    if (disp.Contains("Thav", StringComparison.OrdinalIgnoreCase)) disp = "Thav Onion";
                    if (disp.Contains("Royal Kukuru", StringComparison.OrdinalIgnoreCase)) disp = "Kukuru";

                    float maxTextWidth = boxSize.X - 6.0f;
                    if (ImGui.CalcTextSize(disp).X > maxTextWidth)
                    {
                        for (int charCount = disp.Length; charCount > 0; charCount--)
                        {
                            string testName = disp.Substring(0, charCount) + "...";
                            if (ImGui.CalcTextSize(testName).X <= maxTextWidth) { disp = testName; break; }
                        }
                    }

                    Vector2 tSize = ImGui.CalcTextSize(disp);
                    drawList.AddText(new Vector2(startPos.X + (boxSize.X - tSize.X) / 2.0f, startPos.Y + 50), ImGui.GetColorU32(ImGuiCol.Text), disp);
                    ImGui.Dummy(boxSize);
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(yieldName);
                }
            }
        }

        ImGuiHelpers.ScaledDummy(25.0f);
        // -------------------------------

        // STEP 3: Execution
        bool canPlant = reqs.canPlant && !isBusy;

        ImGui.BeginDisabled(!canPlant);
        ImGui.PushStyleColor(ImGuiCol.Button, canPlant ? new Vector4(0.2f, 0.8f, 0.2f, 1f) : new Vector4(0.3f, 0.3f, 0.3f, 1f));
        if (ImGui.Button("✅ Plant It For Me", new Vector2(-1, 50)))
        {
            UseTemporarySeed = true;

            // Just copy the exact blueprint the math function decided was best!
            for (int i = 0; i < 8; i++)
            {
                plannedSeeds[i] = reqs.bestBlueprint[i];
                plannedSoils[i] = "Grade 3 Thanalan Topsoil";
            }

            plugin.Farming.IsIndoors = false;
            plugin.Farming.IsPersonalPlot = selectedTargetPlot < currentProfile.PersonalEstateSize;
            plugin.Farming.CurrentPlotIndex = plugin.Farming.IsPersonalPlot ? selectedTargetPlot : selectedTargetPlot - currentProfile.PersonalEstateSize;

            var script = GenerateScript(PatternType.FourByFour, maxBeds, false, activePots);
            plugin.Farming.StartPlanting(script);
        }
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        if (!canPlant)
        {
            ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), "Gather the missing items in your checklist to unlock Auto-Plant.");
        }
    }

    // ====================================================
    // INDOOR EASY MODE
    // Pots can't crossbreed, so the only sensible auto-plant target is
    // Thavnairian Onion seeds (the rare standalone end-product). Fills every
    // empty pot with Thav seeds + Grade 3 Thanalan Topsoil; skips pots that
    // already have a plant. Gracefully clamps to inventory when short on
    // supplies (plants as many as it can) instead of refusing outright.
    // ====================================================
    private void DrawIndoorEasyMode(int maxBeds, GardenBedState[] activePots, bool isBusy)
    {
        const string seedName = "Thavnairian Onion Seeds";
        const string soilName = "Grade 3 Thanalan Topsoil";

        int emptyCount = 0;
        for (int i = 0; i < maxBeds; i++)
        {
            if (activePots != null && i < activePots.Length && activePots[i] != null && activePots[i].IsEmpty)
                emptyCount++;
        }

        int ownedSeeds = InventoryHelper.GetItemCount(seedName);
        int ownedSoil = InventoryHelper.GetItemCount(soilName);
        int plantable = Math.Min(emptyCount, Math.Min(ownedSeeds, ownedSoil));

        ImGui.TextColored(new Vector4(0.3f, 0.7f, 0.9f, 1f), "Indoor Easy Mode");
        ImGui.TextWrapped("Fills every empty pot with Thavnairian Onion seeds and Grade 3 Thanalan Topsoil. Already-planted pots are skipped.");
        ImGuiHelpers.ScaledDummy(10.0f);

        // ── Status checklist ──
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), $"Empty pots to fill: {emptyCount} / {maxBeds}");
        ImGuiHelpers.ScaledDummy(5.0f);

        int needed = Math.Max(emptyCount, 1);
        DrawChecklistItem(ownedSeeds >= emptyCount, $"{needed}x Thavnairian Onion", ownedSeeds);
        DrawChecklistItem(ownedSoil >= emptyCount, $"{needed}x Grade 3 Thanalan Topsoil", ownedSoil);

        ImGuiHelpers.ScaledDummy(10.0f);

        if (emptyCount > 0 && plantable < emptyCount && plantable > 0)
        {
            int shortBy = emptyCount - plantable;
            ImGui.TextColored(new Vector4(1f, 0.7f, 0.2f, 1f),
                $"Short on supplies — will plant {plantable} of {emptyCount} pots ({shortBy} skipped).");
            ImGuiHelpers.ScaledDummy(5.0f);
        }

        // ── Disabled-state messaging + button ──
        string? disabledReason = null;
        if (emptyCount == 0) disabledReason = "All pots are planted.";
        else if (ownedSeeds == 0) disabledReason = "Need Thavnairian Onion Seeds.";
        else if (ownedSoil == 0) disabledReason = "Need Grade 3 Thanalan Topsoil.";
        else if (isBusy) disabledReason = "Plugin is busy.";

        bool canPlant = disabledReason == null && plantable > 0;
        string btnLabel = plantable > 0
            ? $"Plant Empty Pots With Thav Onions ({plantable}x)"
            : "Plant Empty Pots With Thav Onions";

        ImGui.BeginDisabled(!canPlant);
        ImGui.PushStyleColor(ImGuiCol.Button,
            canPlant ? new Vector4(0.2f, 0.8f, 0.2f, 1f) : new Vector4(0.3f, 0.3f, 0.3f, 1f));
        if (ImGui.Button(btnLabel, new Vector2(-1, 50)))
        {
            var steps = new List<PlantingStep>();
            int queued = 0;
            for (int i = 0; i < maxBeds && queued < plantable; i++)
            {
                if (activePots != null && i < activePots.Length && activePots[i] != null && activePots[i].IsEmpty)
                {
                    steps.Add(new PlantingStep(i, seedName, soilName));
                    queued++;
                }
            }
            if (steps.Count > 0)
            {
                plugin.Farming.IsIndoors = true;
                plugin.Farming.ActiveIndoorPots = activePots!;
                plugin.Farming.StartPlanting(steps);
            }
        }
        ImGui.PopStyleColor();
        ImGui.EndDisabled();

        if (disabledReason != null)
        {
            ImGui.TextColored(new Vector4(1f, 0.5f, 0f, 1f), disabledReason);
        }
    }

    private void DrawChecklistItem(bool isMet, string name, int owned)
    {
        ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
        if (isMet) ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1f), Dalamud.Interface.FontAwesomeIcon.CheckCircle.ToIconString());
        else ImGui.TextColored(new Vector4(0.8f, 0.2f, 0.2f, 1f), Dalamud.Interface.FontAwesomeIcon.TimesCircle.ToIconString());
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.TextColored(isMet ? new Vector4(0.8f, 0.8f, 0.8f, 1f) : new Vector4(1f, 1f, 1f, 1f), name);

        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 100);
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), $"(Have: {owned})");
    }

    // ====================================================
    // ADVANCED MODE (Your untouched code)
    // ====================================================

    private void DrawIndoorUI(CharacterProfile profile, int maxBeds, GardenBedState[] activePots, bool isBusy)
    {
        var currentLayoutStatus = GetCurrentLayoutStatus(maxBeds, true, activePots);

        if (ImGui.BeginTable("IndoorPlantingTable", 2, ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("LeftInventory", ImGuiTableColumnFlags.WidthFixed, 300f);
            ImGui.TableSetupColumn("RightGrid", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Your Inventory");
            ImGuiHelpers.ScaledDummy(5.0f);
            DrawInventoryBox(250f);

            ImGui.TableNextColumn();
            ImGui.Text("Click an empty slot to configure:");
            ImGuiHelpers.ScaledDummy(5.0f);

            DrawIndoorGrid(maxBeds, activePots);

            ImGuiHelpers.ScaledDummy(15.0f);

            ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Expected Crops");
            ImGuiHelpers.ScaledDummy(5.0f);

            var topYields = GetTopPredictedYields(maxBeds, true, activePots);
            if (topYields.Count > 0)
            {
                for (int i = 0; i < topYields.Count; i++)
                {
                    if (i > 0) ImGui.SameLine();

                    string yieldName = topYields[i];
                    Vector2 startPos = ImGui.GetCursorScreenPos();
                    Vector2 boxSize = new Vector2(65, 75);
                    var drawList = ImGui.GetWindowDrawList();

                    drawList.AddRectFilled(startPos, startPos + boxSize, ImGui.GetColorU32(ImGuiCol.Button), 5.0f);

                    uint iconId = GardenData.GetIconIdForName(yieldName);
                    if (iconId != 0)
                    {
                        var iconTex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                        if (iconTex != null) drawList.AddImage(iconTex.Handle, startPos + new Vector2(12, 5), startPos + new Vector2(52, 45));
                    }

                    string disp = yieldName.Replace(" Seeds", "");
                    if (disp.Contains("Thav", StringComparison.OrdinalIgnoreCase)) disp = "Thav Onion";

                    float maxTextWidth = boxSize.X - 6.0f;
                    if (ImGui.CalcTextSize(disp).X > maxTextWidth)
                    {
                        for (int charCount = disp.Length; charCount > 0; charCount--)
                        {
                            string testName = disp.Substring(0, charCount) + "...";
                            if (ImGui.CalcTextSize(testName).X <= maxTextWidth) { disp = testName; break; }
                        }
                    }

                    Vector2 tSize = ImGui.CalcTextSize(disp);
                    drawList.AddText(new Vector2(startPos.X + (boxSize.X - tSize.X) / 2.0f, startPos.Y + 50), ImGui.GetColorU32(ImGuiCol.Text), disp);
                    ImGui.Dummy(boxSize);
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(yieldName);
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Add seeds to see expected crops.");
            }

            ImGuiHelpers.ScaledDummy(25.0f);

            float statusTextWidth = ImGui.CalcTextSize(currentLayoutStatus.Message).X;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (ImGui.GetContentRegionAvail().X - statusTextWidth) / 2.0f));
            ImGui.TextColored(currentLayoutStatus.Color, currentLayoutStatus.Message);

            ImGuiHelpers.ScaledDummy(5.0f);

            float btnGroupW = (125f * 2) + 20f;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (ImGui.GetContentRegionAvail().X - btnGroupW) / 2.0f));

            if (ImGui.Button("Clear All Slots", new Vector2(125, 30)))
            {
                for (int i = 0; i < plannedSeeds.Length; i++) { plannedSeeds[i] = ""; plannedSoils[i] = ""; }
            }

            ImGui.SameLine(0, 20f);
            ImGui.BeginDisabled(!currentLayoutStatus.CanPlant || isBusy);
            if (ImGui.Button("Plant Pots", new Vector2(125, 30)))
            {
                plugin.Farming.IsIndoors = true;
                plugin.Farming.ActiveIndoorPots = activePots;
                var script = GenerateScript(PatternType.None, maxBeds, true, activePots);
                plugin.Farming.StartPlanting(script);
            }
            ImGui.EndDisabled();

            ImGui.EndTable();
        }
    }

    private void DrawIndoorGrid(int maxBeds, GardenBedState[] activePots)
    {
        float slotW = 80f;
        float padding = 10f;
        float colWidth = ImGui.GetContentRegionAvail().X;

        if (maxBeds == 2)
        {
            float w = (slotW * 2) + padding;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colWidth - w) / 2.0f));
            DrawGardenSlot(0, activePots != null && activePots.Length > 0 ? activePots[0] : null!, true);
            ImGui.SameLine(0, padding);
            DrawGardenSlot(1, activePots != null && activePots.Length > 1 ? activePots[1] : null!, true);
        }
        else if (maxBeds == 3)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colWidth - slotW) / 2.0f));
            DrawGardenSlot(0, activePots != null && activePots.Length > 0 ? activePots[0] : null!, true);

            float w = (slotW * 2) + padding;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colWidth - w) / 2.0f));
            DrawGardenSlot(1, activePots != null && activePots.Length > 1 ? activePots[1] : null!, true);
            ImGui.SameLine(0, padding);
            DrawGardenSlot(2, activePots != null && activePots.Length > 2 ? activePots[2] : null!, true);
        }
        else if (maxBeds == 4)
        {
            float w = (slotW * 2) + padding;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colWidth - w) / 2.0f));
            DrawGardenSlot(0, activePots != null && activePots.Length > 0 ? activePots[0] : null!, true);
            ImGui.SameLine(0, padding);
            DrawGardenSlot(1, activePots != null && activePots.Length > 1 ? activePots[1] : null!, true);

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (colWidth - w) / 2.0f));
            DrawGardenSlot(2, activePots != null && activePots.Length > 2 ? activePots[2] : null!, true);
            ImGui.SameLine(0, padding);
            DrawGardenSlot(3, activePots != null && activePots.Length > 3 ? activePots[3] : null!, true);
        }
        else
        {
            for (int i = 0; i < maxBeds; i++)
            {
                DrawGardenSlot(i, activePots != null && activePots.Length > i ? activePots[i] : null!, true);
                if (i < maxBeds - 1) ImGui.SameLine(0, padding);
            }
        }
    }

    private void DrawOutdoorUI(CharacterProfile currentProfile, int maxBeds, GardenBedState[] activePots, bool isBusy)
    {
        var currentLayoutStatus = GetCurrentLayoutStatus(maxBeds, false, activePots);

        if (ImGui.BeginTable("PlantingLayoutTable", 2, ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("GridColumn", ImGuiTableColumnFlags.WidthFixed, 270f);
            ImGui.TableSetupColumn("ListColumn", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.Text("Click an empty slot to configure:");
            ImGuiHelpers.ScaledDummy(5.0f);

            if (maxBeds == 4)
            {
                if (ImGui.BeginTable("GardenGridSmall", 2)) { int[] gridMapping = { 0, 1, 3, 2 }; for (int i = 0; i < 4; i++) { if (i % 2 == 0) ImGui.TableNextRow(); ImGui.TableNextColumn(); DrawGardenSlot(gridMapping[i], activePots != null && activePots.Length > gridMapping[i] ? activePots[gridMapping[i]] : null!, false); } ImGui.EndTable(); }
            }
            else if (maxBeds == 6)
            {
                if (ImGui.BeginTable("GardenGridMedium", 3))
                {
                    int[] gridMapping = { 0, 1, 2, 5, -1, 3, -2, 4, -2 };
                    for (int i = 0; i < gridMapping.Length; i++)
                    {
                        if (i % 3 == 0 && i != 0) ImGui.TableNextRow();
                        if (gridMapping[i] == -2) { ImGui.TableNextColumn(); ImGui.Dummy(new Vector2(80, 85)); continue; }
                        ImGui.TableNextColumn();
                        if (gridMapping[i] == -1) ImGui.Dummy(new Vector2(80, 85));
                        else DrawGardenSlot(gridMapping[i], activePots != null && activePots.Length > gridMapping[i] ? activePots[gridMapping[i]] : null!, false);
                    }
                    ImGui.EndTable();
                }
            }
            else if (maxBeds == 8)
            {
                if (ImGui.BeginTable("GardenGridLarge", 3)) { int[] gridMapping = { 0, 1, 2, 7, -1, 3, 6, 5, 4 }; for (int i = 0; i < 9; i++) { if (i % 3 == 0) ImGui.TableNextRow(); ImGui.TableNextColumn(); if (gridMapping[i] == -1) ImGui.Dummy(new Vector2(80, 85)); else DrawGardenSlot(gridMapping[i], activePots != null && activePots.Length > gridMapping[i] ? activePots[gridMapping[i]] : null!, false); } ImGui.EndTable(); }
            }

            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Predicted Yields / Crosses");
            ImGuiHelpers.ScaledDummy(5.0f);

            using (var crossChild = ImRaii.Child("PredictedCrosses", new Vector2(-1, 135), true))
            {
                if (crossChild.Success)
                {
                    if (!plugin.CrossbreedManager.IsLoaded)
                    {
                        ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "crosses.csv not found in plugin folder!");
                    }
                    else
                    {
                        bool hasSeeds = false;
                        for (int i = 0; i < maxBeds; i++)
                        {
                            string seedA = (activePots != null && activePots.Length > i && activePots[i] != null && !activePots[i].IsEmpty) ? activePots[i].SeedName : plannedSeeds[i];
                            int prevBed = (i + maxBeds - 1) % maxBeds;
                            string seedB = (activePots != null && activePots.Length > prevBed && activePots[prevBed] != null && !activePots[prevBed].IsEmpty) ? activePots[prevBed].SeedName : plannedSeeds[prevBed];

                            if (!string.IsNullOrEmpty(seedA) && !string.IsNullOrEmpty(seedB))
                            {
                                hasSeeds = true;
                                string cropA = GardenData.SeedToCropMap.TryGetValue(seedA, out var ca) ? ca : seedA;
                                string cropB = GardenData.SeedToCropMap.TryGetValue(seedB, out var cb) ? cb : seedB;

                                string result = plugin.CrossbreedManager.GetCross(cropA, cropB);

                                ImGui.Text($"Bed {i + 1} x Bed {prevBed + 1}:");
                                ImGui.SameLine();

                                if (result == "Unknown / None") ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), result);
                                else
                                {
                                    var yields = result.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                    for (int yIdx = 0; yIdx < yields.Length; yIdx++)
                                    {
                                        string y = yields[yIdx].Trim();
                                        if (string.IsNullOrWhiteSpace(y)) continue;

                                        uint iconId = GardenData.GetIconIdForName(y);
                                        if (iconId != 0)
                                        {
                                            var iconTex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                                            if (iconTex != null)
                                            {
                                                Vector2 cursorPos = ImGui.GetCursorScreenPos();
                                                ImGui.GetWindowDrawList().AddImage(iconTex.Handle, cursorPos + new Vector2(0, -2), cursorPos + new Vector2(18, 16));
                                                ImGui.Dummy(new Vector2(18, 16));
                                                ImGui.SameLine();
                                            }
                                        }
                                        ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), y);
                                        if (yIdx < yields.Length - 1) { ImGui.SameLine(); ImGui.Text("/"); ImGui.SameLine(); }
                                    }
                                    ImGui.NewLine();
                                }
                            }
                        }
                        if (!hasSeeds) ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Add seeds to grid to see predictions.");
                    }
                }
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.Text("Your Inventory");
            ImGuiHelpers.ScaledDummy(5.0f);
            DrawInventoryBox(140f);

            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        if (ImGui.BeginTable("LowerLayoutTable", 3))
        {
            // Layout: Auto-Fill is the densest column (combo+button stacks);
            // Shopping Cart is a short checklist; Top Expected Yields absorbs
            // remaining width (icons reflow naturally with extra space).
            ImGui.TableSetupColumn("LeftAutoFill", ImGuiTableColumnFlags.WidthFixed, 260f);
            ImGui.TableSetupColumn("MiddleChecklist", ImGuiTableColumnFlags.WidthFixed, 220f);
            ImGui.TableSetupColumn("RightSummary", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Auto-Fill Crossbreeding Patterns");
            ImGuiHelpers.ScaledDummy(2.0f);

            ImGui.BeginDisabled(maxBeds < 8);
            ImGui.SetNextItemWidth(215f);
            ImGui.Combo("##GoalCombo", ref selectedGoal, goalOptions, goalOptions.Length);
            ImGui.SameLine();
            ImGui.Text("Goal");

            bool tempSeed = UseTemporarySeed;
            if (ImGui.Checkbox("Plant Temp Seed in Slot 1", ref tempSeed)) UseTemporarySeed = tempSeed;

            ImGuiHelpers.ScaledDummy(5.0f);

            if (selectedGoal == 0)
            {
                ImGui.SetNextItemWidth(215f);
                ImGui.Combo("##PathCombo", ref selectedPath, pathOptions, pathOptions.Length);
                ImGui.SameLine();
                ImGui.Text("Path");

                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.SetNextItemWidth(215f);
                ImGui.Combo("##RightBranchCombo", ref rightBranchLayout, layoutOptions, layoutOptions.Length);

                var rightStatus = GetFillStatus(0, rightBranchLayout, activePots!);
                ImGui.BeginDisabled(!rightStatus.CanFill);
                string rightBranchButtonText = selectedPath == 0 ? "Fill Royal Kukuru" : "Fill Flower Branch";
                if (ImGui.Button(rightBranchButtonText, new Vector2(-1, 25))) { AutoFill(0, rightBranchLayout, activePots!); }
                ImGui.EndDisabled();

                ImGui.TextColored(rightStatus.Color, rightStatus.Message);

                ImGuiHelpers.ScaledDummy(2.0f);

                ImGui.SetNextItemWidth(215f);
                ImGui.Combo("##PremiumCombo", ref premiumLayout, layoutOptions, layoutOptions.Length);

                var premStatus = GetFillStatus(1, premiumLayout, activePots!);
                ImGui.BeginDisabled(!premStatus.CanFill);
                if (ImGui.Button("Fill Premium", new Vector2(-1, 25))) { AutoFill(1, premiumLayout, activePots!); }
                ImGui.EndDisabled();

                ImGui.TextColored(premStatus.Color, premStatus.Message);
            }
            else
            {
                ImGui.SetNextItemWidth(215f);
                ImGui.Combo("##ThavCombo", ref thavLayout, layoutOptions, layoutOptions.Length);

                var thavStatus = GetFillStatus(2, thavLayout, activePots!);
                ImGui.BeginDisabled(!thavStatus.CanFill);
                if (ImGui.Button("Fill Thav Offspring", new Vector2(-1, 25))) { AutoFill(2, thavLayout, activePots!); }
                ImGui.EndDisabled();

                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.TextColored(thavStatus.Color, thavStatus.Message);
            }

            ImGui.EndDisabled();
            if (maxBeds < 8) ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1f), "Auto-fill patterns require an 8-bed garden.");

            // ----------------------------------------------------
            // --- NEW: MIDDLE CHECKLIST COLUMN ---
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Shopping Cart");
            ImGuiHelpers.ScaledDummy(5.0f);

            bool hasPlanned = false;
            var advancedSeeds = new Dictionary<string, int>();
            var advancedSoils = new Dictionary<string, int>();

            for (int i = 0; i < maxBeds; i++)
            {
                if (activePots != null && activePots.Length > i && activePots[i] != null && !activePots[i].IsEmpty && !manualOverride) continue;

                if (!string.IsNullOrEmpty(plannedSeeds[i]))
                {
                    hasPlanned = true;
                    string seed = plannedSeeds[i];
                    string soil = string.IsNullOrEmpty(plannedSoils[i]) ? "Any Soil" : plannedSoils[i];
                    if (!advancedSeeds.ContainsKey(seed)) advancedSeeds[seed] = 0;
                    if (!advancedSoils.ContainsKey(soil)) advancedSoils[soil] = 0;
                    advancedSeeds[seed]++; advancedSoils[soil]++;
                }
            }

            if (!hasPlanned)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Assign seeds to generate list.");
            }
            else
            {
                foreach (var kvp in advancedSeeds)
                {
                    int owned = ownedSeeds.TryGetValue(kvp.Key, out int o) ? o : 0;
                    DrawChecklistItem(owned >= kvp.Value, $"{kvp.Value}x {kvp.Key.Replace(" Seeds", "")}", owned);
                }
                ImGuiHelpers.ScaledDummy(2.0f);
                foreach (var kvp in advancedSoils)
                {
                    int owned = ownedSoils.TryGetValue(kvp.Key, out int o) ? o : 0;
                    DrawChecklistItem(owned >= kvp.Value, $"{kvp.Value}x {kvp.Key.Replace("Grade 3 ", "G3 ")}", owned);
                }
            }
            // ----------------------------------------------------
            // COLUMN 3: Top Expected Yields & Guess Best
            // ----------------------------------------------------
            ImGui.TableNextColumn(); // <--- This ensures we move to the 3rd column!
            ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Top Expected Yields");
            ImGuiHelpers.ScaledDummy(5.0f);

            var topYields = GetTopPredictedYields(maxBeds, false, activePots!);
            if (topYields.Count > 0)
            {
                for (int i = 0; i < topYields.Count; i++)
                {
                    if (i > 0) ImGui.SameLine();

                    string yieldName = topYields[i];
                    Vector2 startPos = ImGui.GetCursorScreenPos();
                    Vector2 boxSize = new Vector2(65, 75);

                    var drawList = ImGui.GetWindowDrawList();
                    drawList.AddRectFilled(startPos, startPos + boxSize, ImGui.GetColorU32(ImGuiCol.Button), 5.0f);

                    uint iconId = GardenData.GetIconIdForName(yieldName);
                    if (iconId != 0)
                    {
                        var iconTex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                        if (iconTex != null) drawList.AddImage(iconTex.Handle, startPos + new Vector2(12, 5), startPos + new Vector2(52, 45));
                    }

                    string disp = yieldName.Replace(" Seeds", "");
                    if (disp.Contains("Thav", StringComparison.OrdinalIgnoreCase)) disp = "Thav Onion";
                    if (disp.Contains("Royal Kukuru", StringComparison.OrdinalIgnoreCase)) disp = "Kukuru";

                    float maxTextWidth = boxSize.X - 6.0f;
                    if (ImGui.CalcTextSize(disp).X > maxTextWidth)
                    {
                        for (int charCount = disp.Length; charCount > 0; charCount--)
                        {
                            string testName = disp.Substring(0, charCount) + "...";
                            if (ImGui.CalcTextSize(testName).X <= maxTextWidth) { disp = testName; break; }
                        }
                    }

                    Vector2 tSize = ImGui.CalcTextSize(disp);
                    drawList.AddText(new Vector2(startPos.X + (boxSize.X - tSize.X) / 2.0f, startPos.Y + 50), ImGui.GetColorU32(ImGuiCol.Text), disp);
                    ImGui.Dummy(boxSize);
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip(yieldName);
                }
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No valid crosses detected. ");
            }

            ImGuiHelpers.ScaledDummy(10.0f);

            if (ImGui.Button("Guess Best Planting", new Vector2(-1, 35)))
            {
                GuessBestPlanting(maxBeds, activePots!);
            }
            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(25.0f);

        ImGui.TextColored(currentLayoutStatus.Color, currentLayoutStatus.Message);
        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.Button("Clear All Slots", new Vector2(125, 30)))
        {
            for (int i = 0; i < plannedSeeds.Length; i++) { plannedSeeds[i] = ""; plannedSoils[i] = ""; }
        }

        ImGui.SameLine(ImGui.GetWindowWidth() - 140f);
        ImGui.BeginDisabled(!currentLayoutStatus.CanPlant || isBusy);
        if (ImGui.Button("Plant Garden", new Vector2(125, 30)))
        {
            if (currentLayoutStatus.Pattern != PatternType.None || maxBeds < 8)
            {
                plugin.Farming.IsIndoors = false;
                plugin.Farming.IsPersonalPlot = selectedTargetPlot < currentProfile.PersonalEstateSize;
                plugin.Farming.CurrentPlotIndex = plugin.Farming.IsPersonalPlot ? selectedTargetPlot : selectedTargetPlot - currentProfile.PersonalEstateSize;
                var script = GenerateScript(currentLayoutStatus.Pattern, maxBeds, false, activePots!);
                plugin.Farming.StartPlanting(script);
            }
            else
            {
                ImGui.OpenPopup("Confirm Suboptimal Planting");
            }
        }
        ImGui.EndDisabled();

        if (ImGui.BeginPopupModal("Confirm Suboptimal Planting", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextColored(new Vector4(1f, 1f, 0f, 1f), "Warning: Suboptimal Layout Detected!");
            ImGui.Text("Your current grid does not match a standard crossbreeding pattern.");
            ImGui.Text("Are you absolutely sure you want to proceed and plant these seeds?");
            ImGuiHelpers.ScaledDummy(10.0f);

            if (ImGui.Button("Yes, Plant Anyway", new Vector2(140, 30)))
            {
                plugin.Farming.IsIndoors = false;
                plugin.Farming.IsPersonalPlot = selectedTargetPlot < currentProfile.PersonalEstateSize;
                plugin.Farming.CurrentPlotIndex = plugin.Farming.IsPersonalPlot ? selectedTargetPlot : selectedTargetPlot - currentProfile.PersonalEstateSize;
                var script = GenerateScript(PatternType.None, maxBeds, false, activePots!);
                plugin.Farming.StartPlanting(script);
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("No, Cancel", new Vector2(140, 30))) { ImGui.CloseCurrentPopup(); }
            ImGui.EndPopup();
        }
    }

    private void DrawInventoryBox(float height = 140f)
    {
        using (var child = ImRaii.Child("InventoryList", new Vector2(-1, height), true))
        {
            if (child.Success)
            {
                ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Seeds");
                if (ownedSeeds.Count == 0) ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No tracked seeds found.");
                foreach (var seed in ownedSeeds)
                {
                    ImGui.Text(seed.Key);
                    string countText = seed.Value.ToString();
                    ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(countText).X - 25f);
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), countText);
                }

                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.Separator();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Soils");
                if (ownedSoils.Count == 0) ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No tracked soils found.");
                foreach (var soil in ownedSoils)
                {
                    ImGui.Text(soil.Key);
                    string countText = soil.Value.ToString();
                    ImGui.SameLine(ImGui.GetWindowWidth() - ImGui.CalcTextSize(countText).X - 25f);
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), countText);
                }
            }
        }
    }

    private void DrawGardenSlot(int slotIndex, GardenBedState realBed, bool isIndoors)
    {
        bool isAlreadyPlanted = realBed != null && !realBed.IsEmpty;

        string currentSeed = isAlreadyPlanted ? realBed!.SeedName : plannedSeeds[slotIndex];
        string currentSoil = plannedSoils[slotIndex];
        bool isEmpty = string.IsNullOrEmpty(currentSeed);

        Vector2 startPos = ImGui.GetCursorScreenPos();
        Vector2 slotSize = new Vector2(80, 85);

        bool canClick = !isAlreadyPlanted || manualOverride;
        bool clicked = ImGui.InvisibleButton($"###Slot{slotIndex}", slotSize);
        if (!canClick) clicked = false;

        bool isHovered = ImGui.IsItemHovered();
        var drawList = ImGui.GetWindowDrawList();

        uint bgColor = isHovered && canClick ? ImGui.GetColorU32(ImGuiCol.ButtonHovered) : ImGui.GetColorU32(ImGuiCol.Button);
        if (isAlreadyPlanted && manualOverride) bgColor = ImGui.GetColorU32(new Vector4(0.8f, 0.2f, 0.2f, 0.4f));

        // --- NEW: Dark red tint for locked physical plants ---
        if (isAlreadyPlanted && !manualOverride) bgColor = ImGui.GetColorU32(new Vector4(0.3f, 0.1f, 0.1f, 0.5f));

        drawList.AddRectFilled(startPos, startPos + slotSize, bgColor, 5.0f);
        uint textColor = ImGui.GetColorU32(ImGuiCol.Text);

        // --- NEW: Draw the Lock Icon ---
        if (isAlreadyPlanted && !manualOverride)
        {
            ImGui.PushFont(Dalamud.Interface.UiBuilder.IconFont);
            drawList.AddText(new Vector2(startPos.X + slotSize.X - 18, startPos.Y + 5), ImGui.GetColorU32(new Vector4(0.8f, 0.3f, 0.3f, 1f)), Dalamud.Interface.FontAwesomeIcon.Lock.ToIconString());
            ImGui.PopFont();
        }

        if (isEmpty)
        {
            Vector2 emptySize = ImGui.CalcTextSize("Empty");
            drawList.AddText(new Vector2(startPos.X + (slotSize.X - emptySize.X) / 2.0f, startPos.Y + (slotSize.Y - emptySize.Y) / 2.0f), textColor, "Empty");
        }
        else
        {
            string cropName = isAlreadyPlanted ? currentSeed : (GardenData.SeedToCropMap.TryGetValue(currentSeed, out var c) ? c : currentSeed);
            uint iconId = GardenData.GetIconIdForName(cropName);

            if (iconId != 0)
            {
                var iconTex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                if (iconTex != null) drawList.AddImage(iconTex.Handle, startPos + new Vector2(20, 10), startPos + new Vector2(60, 50));
            }

            string displayName = currentSeed;
            float maxTextWidth = slotSize.X - 8.0f;
            if (ImGui.CalcTextSize(displayName).X > maxTextWidth)
            {
                for (int charCount = displayName.Length; charCount > 0; charCount--)
                {
                    string testName = displayName.Substring(0, charCount) + "...";
                    if (ImGui.CalcTextSize(testName).X <= maxTextWidth) { displayName = testName; break; }
                }
            }

            Vector2 textSize = ImGui.CalcTextSize(displayName);
            drawList.AddText(new Vector2(startPos.X + (slotSize.X - textSize.X) / 2.0f, startPos.Y + 60), textColor, displayName);
        }

        if (isIndoors && realBed != null && realBed.HasGps && Plugin.TargetManager.Target != null)
        {
            var target = Plugin.TargetManager.Target;
            if (target.BaseId == realBed.DataId && Vector3.Distance(target.Position, realBed.GetGpsVector()) < 0.5f)
            {
                drawList.AddRect(startPos, startPos + slotSize, ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.2f, 1f)), 5.0f, 0, 2.0f);
                drawList.AddText(new Vector2(startPos.X + 12, startPos.Y - 15), ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.2f, 1f)), "[TARGETED]");
            }
        }

        if (isHovered)
        {
            if (isAlreadyPlanted) ImGui.SetTooltip($"Growing: {realBed!.SeedName}\n(Cannot plant over existing crop)");
            else ImGui.SetTooltip($"Seed: {(string.IsNullOrEmpty(currentSeed) ? "None" : currentSeed)}\nSoil: {(string.IsNullOrEmpty(currentSoil) ? "None" : currentSoil)}");
        }

        if (clicked)
        {
            seedSearchFilter = "";
            soilSearchFilter = "";
            ImGui.OpenPopup($"SlotConfigPopup{slotIndex}");
        }

        if (isHovered && canClick && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) { plannedSeeds[slotIndex] = ""; plannedSoils[slotIndex] = ""; }

        if (ImGui.BeginPopup($"SlotConfigPopup{slotIndex}"))
        {
            ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), $"Configure Slot {slotIndex + 1}");
            if (isAlreadyPlanted && manualOverride) ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "DANGER: Replanting over active crop!");
            ImGui.Separator();

            if (ImGui.Button("--- Clear Slot ---", new Vector2(-1, 0))) { plannedSeeds[slotIndex] = ""; plannedSoils[slotIndex] = ""; ImGui.CloseCurrentPopup(); }
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.Text("Seed:");
            ImGui.SetNextItemWidth(200f);
            if (ImGui.BeginCombo("##SeedCombo", string.IsNullOrEmpty(plannedSeeds[slotIndex]) ? "Select Seed..." : plannedSeeds[slotIndex]))
            {
                ImGui.InputText("Search##seedSearch", ref seedSearchFilter, 50);
                ImGui.Separator();

                foreach (var seed in ownedSeeds.Keys)
                {
                    if (!string.IsNullOrEmpty(seedSearchFilter) && !seed.Contains(seedSearchFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    // Outdoor 8-bed plots are used for cross parents, not for planting exotic
                    // seeds directly. Hide exotic seeds here; the inventory panel still shows them.
                    if (!isIndoors && GardenData.ExoticSeeds.Contains(seed)) continue;
                    if (ImGui.Selectable(seed, seed == plannedSeeds[slotIndex])) plannedSeeds[slotIndex] = seed;
                }
                ImGui.EndCombo();
            }

            ImGui.Text("Soil:");
            ImGui.SetNextItemWidth(200f);
            if (ImGui.BeginCombo("##SoilCombo", string.IsNullOrEmpty(plannedSoils[slotIndex]) ? "Select Soil..." : plannedSoils[slotIndex]))
            {
                ImGui.InputText("Search##soilSearch", ref soilSearchFilter, 50);
                ImGui.Separator();

                foreach (var soil in ownedSoils.Keys)
                {
                    if (!string.IsNullOrEmpty(soilSearchFilter) && !soil.Contains(soilSearchFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    if (ImGui.Selectable(soil, soil == plannedSoils[slotIndex])) plannedSoils[slotIndex] = soil;
                }
                ImGui.EndCombo();
            }
            ImGui.EndPopup();
        }
    }

    private (bool CanPlant, PatternType Pattern, string Message, Vector4 Color) GetCurrentLayoutStatus(int maxBeds, bool isIndoors, GardenBedState[] realBeds)
    {
        bool hasSomethingToPlant = false;
        for (int i = 0; i < maxBeds; i++)
        {
            if (!string.IsNullOrEmpty(plannedSeeds[i])) hasSomethingToPlant = true;
        }

        if (!hasSomethingToPlant) return (false, PatternType.None, isIndoors ? "No new seeds queued for empty pots." : "Garden layout empty. Configure slots to begin.", new Vector4(0.5f, 0.5f, 0.5f, 1f));

        var neededSeeds = new Dictionary<string, int>();
        var neededSoils = new Dictionary<string, int>();

        for (int i = 0; i < maxBeds; i++)
        {
            if (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && !manualOverride) continue;

            if (!string.IsNullOrEmpty(plannedSeeds[i]))
            {
                if (string.IsNullOrEmpty(plannedSoils[i])) return (false, PatternType.None, $"Missing soil for Slot {i + 1}!", new Vector4(1f, 0.2f, 0.2f, 1f));

                string seed = plannedSeeds[i];
                string soil = plannedSoils[i];
                if (!neededSeeds.ContainsKey(seed)) neededSeeds[seed] = 0;
                if (!neededSoils.ContainsKey(soil)) neededSoils[soil] = 0;
                neededSeeds[seed]++;
                neededSoils[soil]++;
            }
        }

        PatternType detectedPattern = PatternType.None;

        // --- NEW: Check if garden is empty ---
        bool isCompletelyEmpty = true;
        for (int i = 0; i < maxBeds; i++)
        {
            if (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && !manualOverride)
                isCompletelyEmpty = false;
        }

        if (!isIndoors)
        {
            detectedPattern = IdentifyPattern(realBeds!);
            // --- FIXED: Now it will bypass the Temp Seed math if the garden has crops! ---
            if (UseTemporarySeed && detectedPattern != PatternType.None && isCompletelyEmpty)
            {
                string tempSoil = GetBestTempSoil();
                if (!neededSoils.ContainsKey(tempSoil)) neededSoils[tempSoil] = 0;

                if (detectedPattern == PatternType.FourByFour)
                {
                    if (!neededSeeds.ContainsKey(plannedSeeds[0])) neededSeeds[plannedSeeds[0]] = 0;
                    neededSeeds[plannedSeeds[0]]++;
                    neededSoils[tempSoil]++;
                }
                else if (detectedPattern == PatternType.RichMan || detectedPattern == PatternType.PoorMan)
                {
                    if (!neededSeeds.ContainsKey(plannedSeeds[0])) neededSeeds[plannedSeeds[0]] = 0;
                    if (!neededSeeds.ContainsKey(plannedSeeds[5])) neededSeeds[plannedSeeds[5]] = 0;
                    neededSeeds[plannedSeeds[0]]++;
                    neededSeeds[plannedSeeds[5]]++;
                    neededSoils[tempSoil] += 2;
                }
            }
        }

        foreach (var kvp in neededSeeds)
        {
            int owned = ownedSeeds.TryGetValue(kvp.Key, out int o) ? o : 0;
            if (owned < kvp.Value) return (false, PatternType.None, $"Need {kvp.Value} {kvp.Key.Replace(" Seeds", "")} (Have {owned})", new Vector4(1f, 0.2f, 0.2f, 1f));
        }

        foreach (var kvp in neededSoils)
        {
            int owned = ownedSoils.TryGetValue(kvp.Key, out int o) ? o : 0;
            if (owned < kvp.Value) return (false, PatternType.None, $"Need {kvp.Value} {kvp.Key} (Have {owned})", new Vector4(1f, 0.2f, 0.2f, 1f));
        }

        if (isIndoors) return (true, PatternType.None, "Ready to plant pots!", new Vector4(0f, 1f, 0f, 1f));
        if (detectedPattern != PatternType.None) return (true, detectedPattern, "Optimal Crossbreeding Sequence Ready!", new Vector4(0f, 1f, 0f, 1f));

        return (true, PatternType.None, "Warning: Suboptimal Layout Detected.", new Vector4(1f, 1f, 0f, 1f));
    }

    private PatternType IdentifyPattern(GardenBedState[] realBeds)
    {
        List<string> uniqueSeeds = new();
        for (int i = 0; i < 8; i++)
        {
            string s = (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && !manualOverride) ? realBeds[i].SeedName : plannedSeeds[i];
            if (string.IsNullOrEmpty(s)) return PatternType.None;
            if (!uniqueSeeds.Contains(s)) uniqueSeeds.Add(s);
        }
        if (uniqueSeeds.Count != 2) return PatternType.None;

        string a = uniqueSeeds[0];
        string b = uniqueSeeds[1];
        if (!IsValidPair(a, b)) return PatternType.None;

        bool is4x4 = true;
        for (int i = 0; i < 8; i++)
        {
            string currentSlot = (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && !manualOverride) ? realBeds[i].SeedName : plannedSeeds[i];
            if (currentSlot != (i % 2 == 0 ? a : b)) is4x4 = false;
        }
        if (is4x4) return PatternType.FourByFour;

        bool isRich = true;
        for (int i = 0; i < 8; i++)
        {
            string currentSlot = (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && !manualOverride) ? realBeds[i].SeedName : plannedSeeds[i];
            if (i == 0 || i == 2 || i == 5) { if (currentSlot != a) isRich = false; } else { if (currentSlot != b) isRich = false; }
        }
        if (isRich) return PatternType.RichMan;

        bool isPoor = true;
        for (int i = 0; i < 8; i++)
        {
            string currentSlot = (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && !manualOverride) ? realBeds[i].SeedName : plannedSeeds[i];
            if (i == 1 || i == 4 || i == 6) { if (currentSlot != b) isPoor = false; } else { if (currentSlot != a) isPoor = false; }
        }
        if (isPoor) return PatternType.PoorMan;

        return PatternType.None;
    }

    private bool IsValidPair(string s1, string s2)
    {
        string c1 = GardenData.SeedToCropMap.TryGetValue(s1, out var ca) ? ca : s1;
        string c2 = GardenData.SeedToCropMap.TryGetValue(s2, out var cb) ? cb : s2;

        if (plugin.CrossbreedManager.IsLoaded)
        {
            string cross = plugin.CrossbreedManager.GetCross(c1, c2);
            if (cross != "Unknown / None" && !cross.Contains("Dead", StringComparison.OrdinalIgnoreCase)) return true;
        }

        var validPairs = new List<(string, string)> {
            ("Old World Fig Seeds", "Mirror Apple Seeds"), ("Old World Fig Seeds", "Blood Currant Seeds"),
            ("Old World Fig Seeds", "Pixie Plum Seeds"), ("Old World Fig Seeds", "Sun Lemon Seeds"),
            ("Old World Fig Seeds", "Rolanberry Seeds"), ("Coerthan Tea Leaves", "Almonds"),
            ("Coerthan Tea Leaves", "Mandrake"), ("Krakka Root Seeds", "Midland Cabbage Seeds"),
            ("Krakka Root Seeds", "Wizard Eggplant Seeds"), ("Tantalplant Seeds", "Royal Kukuru Seeds")
        };
        foreach (var p in validPairs) { if ((s1 == p.Item1 && s2 == p.Item2) || (s1 == p.Item2 && s2 == p.Item1)) return true; }
        return false;
    }

    private string GetBestTempSoil()
    {
        foreach (var soil in GardenData.TempSoilPriority) { if (ownedSoils.TryGetValue(soil, out int amt) && amt > 0) return soil; }
        return "Potting Soil";
    }

    private (string seedA, string seedB) GetBestThavCross()
    {
        if (!plugin.CrossbreedManager.IsLoaded) return ("Tantalplant Seeds", "Royal Kukuru Seeds");

        var availableSeeds = new List<string>(ownedSeeds.Keys);
        string bestA = ""; string bestB = ""; int maxCount = -1;

        // TIER 1: Direct Thavnairian Onion Crosses
        for (int i = 0; i < availableSeeds.Count; i++)
        {
            for (int j = i + 1; j < availableSeeds.Count; j++)
            {
                string sA = availableSeeds[i]; string sB = availableSeeds[j];
                string cA = GardenData.SeedToCropMap.TryGetValue(sA, out var ca) ? ca : sA;
                string cB = GardenData.SeedToCropMap.TryGetValue(sB, out var cb) ? cb : sB;
                string result = plugin.CrossbreedManager.GetCross(cA, cB);

                if (result.Contains("Thav", StringComparison.OrdinalIgnoreCase))
                {
                    int totalOwned = ownedSeeds[sA] + ownedSeeds[sB];
                    if (totalOwned > maxCount)
                    {
                        maxCount = totalOwned;
                        bestA = ownedSeeds[sA] >= ownedSeeds[sB] ? sA : sB;
                        bestB = bestA == sA ? sB : sA;
                    }
                }
            }
        }
        if (!string.IsNullOrEmpty(bestA)) return (bestA, bestB);

        // TIER 2: Thavnairian Onion Parents (Royal Kukuru, Mimett Gourd, etc.)
        string[] parents = { "Royal Kukuru", "Mimett Gourd", "Tantalplant", "Sylkis Bud", "Nymeia Lily", "Curiel Root" };
        maxCount = -1;
        for (int i = 0; i < availableSeeds.Count; i++)
        {
            for (int j = i + 1; j < availableSeeds.Count; j++)
            {
                string sA = availableSeeds[i]; string sB = availableSeeds[j];
                string cA = GardenData.SeedToCropMap.TryGetValue(sA, out var ca) ? ca : sA;
                string cB = GardenData.SeedToCropMap.TryGetValue(sB, out var cb) ? cb : sB;
                string result = plugin.CrossbreedManager.GetCross(cA, cB);

                foreach (var p in parents)
                {
                    if (result.Contains(p, StringComparison.OrdinalIgnoreCase))
                    {
                        int totalOwned = ownedSeeds[sA] + ownedSeeds[sB];
                        if (totalOwned > maxCount)
                        {
                            maxCount = totalOwned;
                            bestA = ownedSeeds[sA] >= ownedSeeds[sB] ? sA : sB;
                            bestB = bestA == sA ? sB : sA;
                        }
                    }
                }
            }
        }
        if (!string.IsNullOrEmpty(bestA)) return (bestA, bestB);

        // TIER 3: Absolute Starter Fallback (To generate Royal Kukuru)
        return ("Old World Fig Seeds", "Mirror Apple Seeds");
    }

    private List<PlantingStep> GenerateScript(PatternType pattern, int maxBeds, bool isIndoors, GardenBedState[] realBeds)
    {
        var steps = new List<PlantingStep>();

        // --- NEW: Check if garden is empty ---
        bool isCompletelyEmpty = true;
        for (int i = 0; i < maxBeds; i++)
        {
            if (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && !manualOverride)
                isCompletelyEmpty = false;
        }

        // --- NEW: Bypass temp seed logic if not completely empty! ---
        if (pattern == PatternType.None || !UseTemporarySeed || maxBeds < 8 || isIndoors || !isCompletelyEmpty)
        {
            for (int i = 0; i < maxBeds; i++)
            {
                if (!string.IsNullOrEmpty(plannedSeeds[i]))
                {
                    if (realBeds == null || realBeds.Length <= i || realBeds[i] == null || realBeds[i].IsEmpty || manualOverride)
                        steps.Add(new PlantingStep(i, plannedSeeds[i], plannedSoils[i]));
                }
            }
            return steps;
        }

        if (pattern == PatternType.None || !UseTemporarySeed || maxBeds < 8 || isIndoors)
        {
            for (int i = 0; i < maxBeds; i++)
            {
                if (!string.IsNullOrEmpty(plannedSeeds[i]))
                {
                    if (realBeds == null || realBeds.Length <= i || realBeds[i] == null || realBeds[i].IsEmpty || manualOverride)
                        steps.Add(new PlantingStep(i, plannedSeeds[i], plannedSoils[i]));
                }
            }
            return steps;
        }

        string tempSoil = GetBestTempSoil();

        if (pattern == PatternType.PoorMan || pattern == PatternType.RichMan)
        {
            steps.Add(new PlantingStep(0, plannedSeeds[0], tempSoil));
            steps.Add(new PlantingStep(5, plannedSeeds[5], tempSoil));

            steps.Add(new PlantingStep(1, plannedSeeds[1], plannedSoils[1]));
            steps.Add(new PlantingStep(4, plannedSeeds[4], plannedSoils[4]));
            steps.Add(new PlantingStep(6, plannedSeeds[6], plannedSoils[6]));

            steps.Add(new PlantingStep(0, "", "", true));
            steps.Add(new PlantingStep(5, "", "", true));

            steps.Add(new PlantingStep(0, plannedSeeds[0], plannedSoils[0]));
            steps.Add(new PlantingStep(2, plannedSeeds[2], plannedSoils[2]));
            steps.Add(new PlantingStep(3, plannedSeeds[3], plannedSoils[3]));
            steps.Add(new PlantingStep(5, plannedSeeds[5], plannedSoils[5]));
            steps.Add(new PlantingStep(7, plannedSeeds[7], plannedSoils[7]));
        }
        else if (pattern == PatternType.FourByFour)
        {
            steps.Add(new PlantingStep(0, plannedSeeds[0], tempSoil));
            for (int i = 1; i <= 7; i++) steps.Add(new PlantingStep(i, plannedSeeds[i], plannedSoils[i]));
            steps.Add(new PlantingStep(0, "", "", true));
            steps.Add(new PlantingStep(0, plannedSeeds[0], plannedSoils[0]));
        }
        return steps;
    }

    private (bool CanFill, string Message, Vector4 Color) GetFillStatus(int branchType, int layoutType, GardenBedState[] realBeds)
    {
        string bestBranchSeed = ""; string fixedSeed = "";

        if (branchType == 0)
        {
            var branchCandidates = selectedPath == 0 ? new[] { "Mirror Apple Seeds", "Blood Currant Seeds", "Pixie Plum Seeds", "Sun Lemon Seeds", "Rolanberry Seeds" } : new[] { "Almond Seeds", "Mandrake Seeds" };
            fixedSeed = selectedPath == 0 ? "Old World Fig Seeds" : "Coerthan Tea Leaves";

            // Check bags first
            int maxCount = -1;
            foreach (var seed in branchCandidates) { if (ownedSeeds.TryGetValue(seed, out int count) && count > maxCount) { maxCount = count; bestBranchSeed = seed; } }

            // Check dirt if bags are empty!
            if (string.IsNullOrEmpty(bestBranchSeed) && realBeds != null)
            {
                foreach (var seed in branchCandidates)
                {
                    string cropName = seed.Replace(" Seeds", "").Replace(" Kernels", "");
                    for (int i = 0; i < 8; i++)
                    {
                        if (realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && realBeds[i].SeedName.Contains(cropName, StringComparison.OrdinalIgnoreCase))
                            bestBranchSeed = seed;
                    }
                }
            }
            if (string.IsNullOrEmpty(bestBranchSeed)) return (false, "Missing required cross seeds!", new Vector4(1f, 1f, 0f, 1f));
        }
        else if (branchType == 1)
        {
            var branchCandidates = new[] { "Midland Cabbage Seeds", "Wizard Eggplant Seeds" };
            fixedSeed = "Krakka Root Seeds";

            int maxCount = -1;
            foreach (var seed in branchCandidates) { if (ownedSeeds.TryGetValue(seed, out int count) && count > maxCount) { maxCount = count; bestBranchSeed = seed; } }

            if (string.IsNullOrEmpty(bestBranchSeed) && realBeds != null)
            {
                foreach (var seed in branchCandidates)
                {
                    string cropName = seed.Replace(" Seeds", "").Replace(" Kernels", "");
                    for (int i = 0; i < 8; i++)
                    {
                        if (realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && realBeds[i].SeedName.Contains(cropName, StringComparison.OrdinalIgnoreCase))
                            bestBranchSeed = seed;
                    }
                }
            }
            if (string.IsNullOrEmpty(bestBranchSeed)) return (false, "Missing required cross seeds!", new Vector4(1f, 1f, 0f, 1f));
        }
        else
        {
            var pair = GetBestThavCross();
            if (string.IsNullOrEmpty(pair.seedA)) return (false, "No Thav. Onion combos found in inventory!", new Vector4(1f, 1f, 0f, 1f));
            fixedSeed = pair.seedA;
            bestBranchSeed = pair.seedB;
        }

        int seedA_Req = 0; int seedB_Req = 0; int totalSoilReq = 0;

        for (int i = 0; i < 8; i++)
        {
            string targetSeed = "";
            if (layoutType == 0) targetSeed = (i % 2 == 0) ? fixedSeed : bestBranchSeed;
            else if (layoutType == 1) targetSeed = (i == 0 || i == 2 || i == 5) ? fixedSeed : bestBranchSeed;
            else if (layoutType == 2) targetSeed = (i == 1 || i == 4 || i == 6) ? bestBranchSeed : fixedSeed;

            if (realBeds == null || realBeds.Length <= i || realBeds[i] == null || realBeds[i].IsEmpty || manualOverride)
            {
                if (targetSeed == fixedSeed) seedA_Req++; else seedB_Req++;
                totalSoilReq++;
            }
            else
            {
                if (!realBeds[i].SeedName.Contains(targetSeed.Replace(" Seeds", ""), StringComparison.OrdinalIgnoreCase))
                {
                    if (targetSeed == fixedSeed) seedA_Req++; else seedB_Req++;
                    totalSoilReq++;
                }
            }
        }

        bool isCompletelyEmpty = true;
        for (int i = 0; i < 8; i++)
        {
            if (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && !manualOverride)
                isCompletelyEmpty = false;
        }

        if (UseTemporarySeed && isCompletelyEmpty)
        {
            if (layoutType == 0 || layoutType == 1) seedA_Req++;
            else if (layoutType == 2) seedB_Req++;
            totalSoilReq++;
        }

        int a_Owned = ownedSeeds.TryGetValue(fixedSeed, out int aC) ? aC : 0;
        int b_Owned = ownedSeeds.TryGetValue(bestBranchSeed, out int bC) ? bC : 0;

        int totalSoilOwned = 0;
        foreach (var amt in ownedSoils.Values) totalSoilOwned += amt;

        if (totalSoilOwned < totalSoilReq) return (false, $"Missing {totalSoilReq - totalSoilOwned} Soil (of any type)!", new Vector4(1f, 0.2f, 0.2f, 1f));
        if (a_Owned < seedA_Req) return (false, $"Missing {seedA_Req - a_Owned} {fixedSeed.Replace(" Seeds", "")}!", new Vector4(1f, 0.2f, 0.2f, 1f));
        if (b_Owned < seedB_Req) return (false, $"Missing {seedB_Req - b_Owned} {bestBranchSeed.Replace(" Seeds", "")}!", new Vector4(1f, 0.2f, 0.2f, 1f));

        int g3t_Owned = ownedSoils.TryGetValue("Grade 3 Thanalan Topsoil", out int gC) ? gC : 0;
        if (g3t_Owned < totalSoilReq) return (true, "Warning: Missing G3 Thanalan! Will use fallback soil.", new Vector4(1f, 1f, 0f, 1f));

        return (true, "Ready to Plant!", new Vector4(0f, 1f, 0f, 1f));
    }

    private void AutoFill(int branchType, int layoutType, GardenBedState[] realBeds)
    {
        string bestBranchSeed = ""; string fixedSeed = "";

        if (branchType == 0)
        {
            var branchCandidates = selectedPath == 0 ? new[] { "Mirror Apple Seeds", "Blood Currant Seeds", "Pixie Plum Seeds", "Sun Lemon Seeds", "Rolanberry Seeds" } : new[] { "Almond Seeds", "Mandrake Seeds" };
            fixedSeed = selectedPath == 0 ? "Old World Fig Seeds" : "Coerthan Tea Leaves";

            int maxCount = -1;
            foreach (var seed in branchCandidates) { if (ownedSeeds.TryGetValue(seed, out int count) && count > maxCount) { maxCount = count; bestBranchSeed = seed; } }

            if (string.IsNullOrEmpty(bestBranchSeed) && realBeds != null)
            {
                foreach (var seed in branchCandidates)
                {
                    string cropName = seed.Replace(" Seeds", "").Replace(" Kernels", "");
                    for (int i = 0; i < 8; i++)
                    {
                        if (realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && realBeds[i].SeedName.Contains(cropName, StringComparison.OrdinalIgnoreCase))
                            bestBranchSeed = seed;
                    }
                }
            }
            if (string.IsNullOrEmpty(bestBranchSeed)) bestBranchSeed = branchCandidates[0];
        }
        else if (branchType == 1)
        {
            var branchCandidates = new[] { "Midland Cabbage Seeds", "Wizard Eggplant Seeds" };
            fixedSeed = "Krakka Root Seeds";

            int maxCount = -1;
            foreach (var seed in branchCandidates) { if (ownedSeeds.TryGetValue(seed, out int count) && count > maxCount) { maxCount = count; bestBranchSeed = seed; } }

            if (string.IsNullOrEmpty(bestBranchSeed) && realBeds != null)
            {
                foreach (var seed in branchCandidates)
                {
                    string cropName = seed.Replace(" Seeds", "").Replace(" Kernels", "");
                    for (int i = 0; i < 8; i++)
                    {
                        if (realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty && realBeds[i].SeedName.Contains(cropName, StringComparison.OrdinalIgnoreCase))
                            bestBranchSeed = seed;
                    }
                }
            }
            if (string.IsNullOrEmpty(bestBranchSeed)) bestBranchSeed = branchCandidates[0];
        }
        else
        {
            var pair = GetBestThavCross();
            if (!string.IsNullOrEmpty(pair.seedA)) { fixedSeed = pair.seedA; bestBranchSeed = pair.seedB; }
            else { fixedSeed = "Royal Kukuru Seeds"; bestBranchSeed = "Tantalplant Seeds"; }
        }

        string seedA = fixedSeed;
        string seedB = bestBranchSeed;
        var tempAvailSoils = new Dictionary<string, int>(ownedSoils);

        string GetSafeSoil()
        {
            foreach (var soil in GardenData.CrossSoilPriority) { if (tempAvailSoils.TryGetValue(soil, out int amt) && amt > 0) { tempAvailSoils[soil]--; return soil; } }
            return "";
        }

        for (int i = 0; i < 8; i++)
        {
            if (realBeds == null || realBeds.Length <= i || realBeds[i] == null || realBeds[i].IsEmpty || manualOverride)
            {
                if (layoutType == 0) plannedSeeds[i] = (i % 2 == 0) ? seedA : seedB;
                else if (layoutType == 1) { if (i == 0 || i == 2 || i == 5) plannedSeeds[i] = seedA; else plannedSeeds[i] = seedB; }
                else if (layoutType == 2) { if (i == 1 || i == 4 || i == 6) plannedSeeds[i] = seedB; else plannedSeeds[i] = seedA; }

                string chosenSoil = GetSafeSoil();
                if (!string.IsNullOrEmpty(chosenSoil)) plannedSoils[i] = chosenSoil;
            }
        }
    }

    private void GuessBestPlanting(int maxBeds, GardenBedState[] realBeds)
    {
        if (!plugin.CrossbreedManager.IsLoaded) return;

        string bestA = ""; string bestB = "";
        int maxScore = -1; int maxCombinedCount = -1;
        var availableSeeds = new List<string>(ownedSeeds.Keys);

        // 1. Find the best possible pair we own at least 2 of each
        for (int i = 0; i < availableSeeds.Count; i++)
        {
            for (int j = i + 1; j < availableSeeds.Count; j++)
            {
                string sA = availableSeeds[i]; string sB = availableSeeds[j];
                int countA = ownedSeeds[sA]; int countB = ownedSeeds[sB];

                // Need enough total seeds to fill the empty spots!
                if (countA + countB < maxBeds || countA < 2 || countB < 2) continue;

                string cA = GardenData.SeedToCropMap.TryGetValue(sA, out var ca) ? ca : sA;
                string cB = GardenData.SeedToCropMap.TryGetValue(sB, out var cb) ? cb : sB;

                string result = plugin.CrossbreedManager.GetCross(cA, cB);
                if (result == "Unknown / None" || result.Contains("Dead")) continue;

                int pairScore = 0;
                var yields = result.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var y in yields)
                {
                    string trimY = y.Trim();
                    if (string.IsNullOrWhiteSpace(trimY)) continue;

                    int score = GardenData.GetYieldScore(trimY);

                    // --- NEW: MASSIVELY BOOST THAVNAIRIAN PATHS ---
                    if (trimY.Contains("Thav", StringComparison.OrdinalIgnoreCase)) score += 1000;
                    else if (trimY.Contains("Royal Kukuru", StringComparison.OrdinalIgnoreCase) ||
                             trimY.Contains("Mimett Gourd", StringComparison.OrdinalIgnoreCase) ||
                             trimY.Contains("Tantalplant", StringComparison.OrdinalIgnoreCase) ||
                             trimY.Contains("Sylkis", StringComparison.OrdinalIgnoreCase) ||
                             trimY.Contains("Nymeia", StringComparison.OrdinalIgnoreCase))
                    {
                        score += 500;
                    }
                    else if (trimY.Contains("Apricot", StringComparison.OrdinalIgnoreCase) ||
                             trimY.Contains("Curiel", StringComparison.OrdinalIgnoreCase))
                    {
                        score += 250;
                    }
                    // ----------------------------------------------

                    pairScore += score;
                }

                if (pairScore > maxScore || (pairScore == maxScore && (countA + countB) > maxCombinedCount))
                {
                    maxScore = pairScore;
                    maxCombinedCount = countA + countB;
                    bestA = sA; bestB = sB;
                }
            }
        }

        // 2. If we found a valid pair, apply the Best Fit Algorithm!
        if (!string.IsNullOrEmpty(bestA) && !string.IsNullOrEmpty(bestB))
        {
            // Put the one we have MORE of in the 'A' (Even) slots by default
            string defaultA = ownedSeeds[bestA] >= ownedSeeds[bestB] ? bestA : bestB;
            string defaultB = defaultA == bestA ? bestB : bestA;

            string[] blueprintNormal = new string[maxBeds];
            string[] blueprintInverse = new string[maxBeds];

            for (int i = 0; i < maxBeds; i++)
            {
                blueprintNormal[i] = (i % 2 == 0) ? defaultA : defaultB;
                blueprintInverse[i] = (i % 2 == 0) ? defaultB : defaultA;
            }

            int CalculateErrors(string[] blueprint)
            {
                int errors = 0;
                for (int i = 0; i < maxBeds; i++)
                {
                    if (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty)
                    {
                        if (!realBeds[i].SeedName.Contains(blueprint[i].Replace(" Seeds", ""), StringComparison.OrdinalIgnoreCase))
                            errors++;
                    }
                }
                return errors;
            }

            // Let them fight! Choose the blueprint with the fewest uproots required.
            string[] chosenBlueprint = (CalculateErrors(blueprintInverse) < CalculateErrors(blueprintNormal)) ? blueprintInverse : blueprintNormal;

            var tempAvailSoils = new Dictionary<string, int>(ownedSoils);
            string GetSafeSoil()
            {
                foreach (var soil in GardenData.CrossSoilPriority) { if (tempAvailSoils.TryGetValue(soil, out int amt) && amt > 0) { tempAvailSoils[soil]--; return soil; } }
                return "";
            }

            // 3. Apply the winning blueprint to the empty slots in the grid
            for (int i = 0; i < maxBeds; i++)
            {
                if (realBeds == null || realBeds.Length <= i || realBeds[i] == null || realBeds[i].IsEmpty || manualOverride)
                {
                    plannedSeeds[i] = chosenBlueprint[i];
                    plannedSoils[i] = GetSafeSoil();
                }
                else
                {
                    // Clear the planned slot so the UI natively draws the physical plant from the realBeds array!
                    plannedSeeds[i] = "";
                    plannedSoils[i] = "";
                }
            }
        }
    }

    private List<string> GetTopPredictedYields(int maxBeds, bool isIndoors, GardenBedState[] realBeds)
    {
        var uniqueYields = new HashSet<string>();

        if (isIndoors)
        {
            for (int i = 0; i < maxBeds; i++)
            {
                string seedA = (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty) ? realBeds[i].SeedName : plannedSeeds[i];
                if (!string.IsNullOrEmpty(seedA))
                {
                    string crop = GardenData.SeedToCropMap.TryGetValue(seedA, out var c) ? c : seedA;
                    uniqueYields.Add(crop);
                }
            }
        }
        else if (plugin.CrossbreedManager.IsLoaded)
        {
            for (int i = 0; i < maxBeds; i++)
            {
                string seedA = (realBeds != null && realBeds.Length > i && realBeds[i] != null && !realBeds[i].IsEmpty) ? realBeds[i].SeedName : plannedSeeds[i];
                int prevBed = (i + maxBeds - 1) % maxBeds;
                string seedB = (realBeds != null && realBeds.Length > prevBed && realBeds[prevBed] != null && !realBeds[prevBed].IsEmpty) ? realBeds[prevBed].SeedName : plannedSeeds[prevBed];

                if (!string.IsNullOrEmpty(seedA) && !string.IsNullOrEmpty(seedB))
                {
                    string cropA = GardenData.SeedToCropMap.TryGetValue(seedA, out var ca) ? ca : seedA;
                    string cropB = GardenData.SeedToCropMap.TryGetValue(seedB, out var cb) ? cb : seedB;
                    string result = plugin.CrossbreedManager.GetCross(cropA, cropB);

                    if (result != "Unknown / None")
                    {
                        var yields = result.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var y in yields)
                        {
                            string trimmed = y.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmed)) uniqueYields.Add(trimmed);
                        }
                    }
                }
            }
        }

        var sortedYields = new List<string>(uniqueYields);
        sortedYields.Sort((a, b) => GardenData.GetYieldScore(b).CompareTo(GardenData.GetYieldScore(a)));
        return sortedYields.GetRange(0, Math.Min(4, sortedYields.Count));
    }

}