using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;
using Gaia.Helpers;

namespace Gaia.Manager;

public enum FarmingState
{
    Idle = 0,
    TargetAndInteract = 1,
    DismissDialogue = 2,
    MenuSelection = 3,
    AdvanceNext = 4,
    PlantingWaitMenu = 5,
    PlantingInsertSoil = 6,
    PlantingInsertSeed = 7,
    PlantingExecute = 8,
    PlantingConfirmYesNo = 9,
    WaitingForMenuClose = 10,
    FertilizeVerify = 11,
    BouncerQuitMenu = 15 // --- ADD THIS ---
}

public class PlantingStep
{
    public int SlotIndex;
    public string Seed;
    public string Soil;
    public bool IsUproot;

    public PlantingStep(int slot, string seed, string soil, bool isUproot = false)
    {
        SlotIndex = slot;
        Seed = seed;
        Soil = soil;
        IsUproot = isUproot;
    }
}

public class FarmingManager
{
    private readonly Plugin _plugin;

    private FarmingState _currentState = FarmingState.Idle;
    private long _lastStateChangeTime = 0;
    private int _startingFertilizerCount = 0;
    private bool _hasSeenContextMenu = false;
    private bool _sessionPlotActionLogged = false;

    public FarmingState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                _lastStateChangeTime = Environment.TickCount64;
            }
        }
    }

    public int CurrentPlotIndex { get; set; } = 0;
    public bool IsPersonalPlot { get; set; } = true;
    public bool IsIndoors { get; set; } = false;
    public GardenBedState[] ActiveIndoorPots { get; set; } = null!;

    public List<int> VisitOrder { get; private set; } = new List<int>();

    public List<IGameObject> PlantsToWater { get; private set; } = new List<IGameObject>();
    public List<PlantingStep> ActivePlantingScript { get; private set; } = new List<PlantingStep>();
    public int CurrentScriptIndex { get; private set; } = 0;
    public int CurrentPlantIndex { get; private set; } = 0;

    public bool IsHarvesting { get; private set; } = false;
    public bool IsPlanting { get; private set; } = false;
    public bool IsFertilizing { get; private set; } = false;
    public bool IsUprootingAll { get; private set; } = false;
    public bool WaitingForUser { get; set; } = false;
    public bool IsScanning { get; private set; } = false;

    private bool _isCurrentBedEmpty = true;
    private bool _isCurrentPlantMature = false;
    private HashSet<int> _bedsPlantedThisSession = new HashSet<int>();

    private long _nextActionTime = 0;
    public readonly uint[] ValidPatches = { 2003757, 2003756, 2003755 };

    public string LastWarningMessage { get; private set; } = "";
    public long LastWarningTime { get; private set; } = 0;

    // Auto-Replant orchestrator hook. When non-null, fires after Stop() completes.
    // Set by AutoReplantOrchestrator before each phase; orchestrator's callback
    // pops the next phase or wraps up the cycle. Manual abort path nulls this
    // before calling Stop() so no phase advance fires.
    public Action? OnCycleCompleteCallback { get; set; }

    // Optional slot filter applied by BuildVisitOrder. Lets the orchestrator
    // run harvest/water on a subset of beds (e.g., 5x3 Sustain non-parents)
    // without forking the existing Start* entry points. Cleared on Stop().
    public HashSet<int>? SlotFilter { get; set; }

    public FarmingManager(Plugin plugin) => _plugin = plugin;

    public void SetWarning(string msg)
    {
        LastWarningMessage = msg;
        LastWarningTime = Environment.TickCount64 + 4000;
        Plugin.ChatGui.PrintError($"[Gaia] {msg}");
    }

    private void PrintYellow(string message) => Plugin.ChatGui.Print(new XivChatEntry { Message = $"[Gaia] {message}", Type = XivChatType.Echo });

    
    public unsafe void Stop()
    {
        CurrentState = FarmingState.Idle;
        _sessionPlotActionLogged = false;
        WaitingForUser = false;
        IsPlanting = false;
        IsHarvesting = false;
        IsUprootingAll = false;
        IsFertilizing = false;
        IsScanning = false;
        PlantsToWater.Clear();
        VisitOrder.Clear();
        _bedsPlantedThisSession.Clear();
        CurrentPlantIndex = 0;
        CurrentScriptIndex = 0;
        _isCurrentPlantMature = false;
        LastWarningTime = 0;
        SlotFilter = null;

        var selectWrapper = Plugin.GameGui.GetAddonByName("SelectString", 1);
        if (selectWrapper.Address != IntPtr.Zero) ((AddonSelectString*)selectWrapper.Address)->AtkUnitBase.Close(true);

        var talkWrapper = Plugin.GameGui.GetAddonByName("Talk", 1);
        if (talkWrapper.Address != IntPtr.Zero) ((AddonTalk*)talkWrapper.Address)->AtkUnitBase.Close(true);

        var gardeningAddon = GetGardeningAddon();
        if (gardeningAddon != null) gardeningAddon->Close(true);

        var yesnoWrapper = Plugin.GameGui.GetAddonByName("SelectYesno", 1);
        if (yesnoWrapper.Address != IntPtr.Zero) ((AtkUnitBase*)yesnoWrapper.Address)->Close(true);

        // Fire the orchestrator hook AFTER full reset. Snapshot + clear so a
        // callback that triggers a new phase doesn't see stale subscription.
        var cb = OnCycleCompleteCallback;
        if (cb != null)
        {
            OnCycleCompleteCallback = null;
            cb.Invoke();
        }
    }

    private List<IGameObject> GetBedsForPatch(IGameObject anchorBed)
    {
        if (anchorBed == null) return new List<IGameObject>();

        int expectedSize = anchorBed.BaseId == 2003755 ? 4 : anchorBed.BaseId == 2003756 ? 6 : 8;

        var allValidBeds = Plugin.ObjectTable
            .Where(obj => obj.ObjectKind == ObjectKind.EventObj && obj.BaseId == anchorBed.BaseId)
            .OrderBy(obj => obj.GameObjectId)
            .ToList();

        List<List<IGameObject>> chunks = new List<List<IGameObject>>();
        List<IGameObject> currentChunk = new List<IGameObject>();

        foreach (var bed in allValidBeds)
        {
            if (currentChunk.Count == 0) currentChunk.Add(bed);
            else
            {
                if (bed.GameObjectId - currentChunk.Last().GameObjectId <= 2 && currentChunk.Count < expectedSize) currentChunk.Add(bed);
                else { chunks.Add(currentChunk); currentChunk = new List<IGameObject> { bed }; }
            }
        }
        if (currentChunk.Count > 0) chunks.Add(currentChunk);

        var myPatch = chunks.FirstOrDefault(c => c.Any(b => b.GameObjectId == anchorBed.GameObjectId));

        if (myPatch != null) return myPatch.OrderByDescending(b => b.GameObjectId).ToList();
        return new List<IGameObject>();
    }

    private List<IGameObject> FindBedsForPlanting()
    {
        var currentProfile = _plugin.GetCurrentCharacterProfile();
        if (currentProfile == null) return new List<IGameObject>();

        if (IsIndoors)
        {
            var pots = new List<IGameObject>();
            if (ActiveIndoorPots == null) return pots;

            for (int i = 0; i < ActiveIndoorPots.Length; i++)
            {
                var potState = ActiveIndoorPots[i];
                if (!potState.HasGps) { pots.Add(null!); continue; }

                var obj = Plugin.ObjectTable.FirstOrDefault(o =>
                    (o.ObjectKind == ObjectKind.EventObj || o.ObjectKind == ObjectKind.HousingEventObject) &&
                    o.BaseId == potState.DataId &&
                    Vector3.Distance(o.Position, potState.GetGpsVector()) < 0.5f);

                pots.Add(obj!);
            }
            return pots;
        }
        else
        {
            var activePlot = IsPersonalPlot ? currentProfile.PersonalPlots[CurrentPlotIndex] : currentProfile.FCPlots[CurrentPlotIndex];

            if (activePlot.HasGps)
            {
                var savedGps = activePlot.GetGpsVector();
                var anchorBed = Plugin.ObjectTable.FirstOrDefault(p =>
                    p.ObjectKind == ObjectKind.EventObj &&
                    ValidPatches.Contains(p.BaseId) &&
                    Vector3.Distance(savedGps, p.Position) < 0.5f);

                return GetBedsForPatch(anchorBed!);
            }
            else
            {
                var nearestBed = Plugin.ObjectTable
                    .Where(p => p.ObjectKind == ObjectKind.EventObj && ValidPatches.Contains(p.BaseId))
                    .OrderBy(p => Vector3.Distance(Plugin.ObjectTable.LocalPlayer!.Position, p.Position))
                    .FirstOrDefault();

                return GetBedsForPatch(nearestBed!);
            }
        }
    }

    private void BuildVisitOrder()
    {
        PlantsToWater = FindBedsForPlanting();
        VisitOrder.Clear();

        var validIndices = new List<int>();
        for (int i = 0; i < PlantsToWater.Count; i++)
        {
            if (PlantsToWater[i] == null) continue;
            if (SlotFilter != null && !SlotFilter.Contains(i)) continue;
            validIndices.Add(i);
        }

        if (Plugin.ObjectTable.LocalPlayer != null)
        {
            VisitOrder = validIndices.OrderBy(i => Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, PlantsToWater[i].Position)).ToList();
        }
        else
        {
            VisitOrder = validIndices;
        }
    }

    public string[] ScannedCrops { get; private set; } = new string[8] { "", "", "", "", "", "", "", "" };
    public void ClearScannedCrops() { for (int i = 0; i < 8; i++) ScannedCrops[i] = "Empty"; }

    public void PrepareForScan()
    {
        // 1. Force pure scanning mode so it doesn't try to water/fertilize
        IsScanning = true;
        IsHarvesting = false;
        IsPlanting = false;
        IsFertilizing = false;

        // 2. Drop the old target so FFXIV doesn't get confused
        if (Plugin.TargetManager.Target != null)
        {
            Plugin.TargetManager.Target = null;
        }

        // 3. Auto-update the CurrentPlotIndex based on GPS if we are outdoors!
        if (!IsIndoors && Plugin.ObjectTable.LocalPlayer != null)
        {
            var profile = _plugin.GetCurrentCharacterProfile();
            if (profile != null && profile.AutoSelectPlotByGps)
            {
                float closestDist = 20.0f;
                int bestPlotIndex = CurrentPlotIndex;
                bool bestIsPersonal = IsPersonalPlot;

                // Check Personal Plots
                for (int i = 0; i < profile.PersonalEstateSize; i++)
                {
                    if (profile.PersonalPlots[i].HasGps)
                    {
                        float dist = System.Numerics.Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, profile.PersonalPlots[i].GetGpsVector());
                        if (dist < closestDist) { closestDist = dist; bestPlotIndex = i; bestIsPersonal = true; }
                    }
                }
                // Check FC Plots
                for (int i = 0; i < profile.FCEstateSize; i++)
                {
                    if (profile.FCPlots[i].HasGps)
                    {
                        float dist = System.Numerics.Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, profile.FCPlots[i].GetGpsVector());
                        if (dist < closestDist) { closestDist = dist; bestPlotIndex = i; bestIsPersonal = false; }
                    }
                }

                // Lock in the physical reality!
                CurrentPlotIndex = bestPlotIndex;
                IsPersonalPlot = bestIsPersonal;
            }
        }
    }

    public void ScanGarden()
    {
        if (Plugin.ObjectTable.LocalPlayer == null) return;
        BuildVisitOrder();
        if (VisitOrder.Count == 0) return;

        CurrentPlantIndex = 0;
        IsHarvesting = false; IsPlanting = false; IsUprootingAll = false; IsScanning = true;
        CurrentState = FarmingState.TargetAndInteract;
        _nextActionTime = Environment.TickCount64;
        PrintYellow("Initiating Garden Scan...");
    }

    public void UprootAllBeds()
    {
        if (Plugin.ObjectTable.LocalPlayer == null) return;
        BuildVisitOrder();
        if (VisitOrder.Count == 0) return;

        CurrentPlantIndex = 0;
        IsHarvesting = false; IsPlanting = false; IsUprootingAll = true;
        CurrentState = FarmingState.TargetAndInteract;
        _nextActionTime = Environment.TickCount64;
        PrintYellow("Initiating Unsafe Uproot Sequence... Nuking the garden!");
    }

    public void WaterNearestBed() { StartTending(false); }
    public void HarvestNearestBed() { StartTending(true); }

    private void StartTending(bool harvest)
    {
        if (Plugin.ObjectTable.LocalPlayer == null) return;
        BuildVisitOrder();
        if (VisitOrder.Count == 0) return;

        AutoLinkGpsIfMissing();

        CurrentPlantIndex = 0;
        IsHarvesting = harvest; IsPlanting = false; IsFertilizing = false; _sessionPlotActionLogged = false;
        CurrentState = FarmingState.TargetAndInteract;
        _nextActionTime = Environment.TickCount64;
    }

    public void StartFertilizing()
    {
        if (Plugin.ObjectTable.LocalPlayer == null) return;
        BuildVisitOrder();
        if (VisitOrder.Count == 0) return;

        AutoLinkGpsIfMissing();

        CurrentPlantIndex = 0;
        IsHarvesting = false; IsPlanting = false; IsFertilizing = true; _sessionPlotActionLogged = false;
        CurrentState = FarmingState.TargetAndInteract;
        _nextActionTime = Environment.TickCount64;
        PrintYellow("Initiating Fertilize Sequence...");
    }

    private bool PerformPlantingPreFlightCheck(List<PlantingStep> steps)
    {
        // Create a shopping list of everything the script requires
        Dictionary<string, int> requiredItems = new Dictionary<string, int>();

        foreach (var step in steps)
        {
            // Uprooting doesn't cost items!
            if (step.IsUproot) continue;

            // Add Soil to the list
            if (!string.IsNullOrEmpty(step.Soil))
            {
                if (requiredItems.ContainsKey(step.Soil)) requiredItems[step.Soil]++;
                else requiredItems[step.Soil] = 1;
            }

            // Add Seed to the list
            if (!string.IsNullOrEmpty(step.Seed))
            {
                if (requiredItems.ContainsKey(step.Seed)) requiredItems[step.Seed]++;
                else requiredItems[step.Seed] = 1;
            }
        }

        bool passed = true;

        // Verify the shopping list against your actual inventory
        foreach (var kvp in requiredItems)
        {
            string itemName = kvp.Key;
            int requiredQty = kvp.Value;

            // Grab the ItemId using your existing helper
            var itemData = InventoryHelper.GetItemLocation(itemName);

            if (itemData.ItemId == 0)
            {
                Plugin.ChatGui.PrintError($"[Gaia Pre-Flight] Missing Item: You do not have any '{itemName}' in your inventory!");
                passed = false;
                continue;
            }

            // Count how many we actually have across all bags
            int actualQty = InventoryHelper.GetItemCount(itemData.ItemId);

            if (actualQty < requiredQty)
            {
                Plugin.ChatGui.PrintError($"[Gaia Pre-Flight] Insufficient Item: Need {requiredQty}x '{itemName}', but you only have {actualQty}x!");
                passed = false;
            }
        }

        return passed;
    }

    public void StartPlanting(List<PlantingStep> steps)
    {
        if (Plugin.ObjectTable.LocalPlayer == null) return;

        if (!PerformPlantingPreFlightCheck(steps))
        {
            Plugin.ChatGui.PrintError("[Gaia] Planting sequence aborted due to missing supplies. Please check your inventory!");
            return;
        }


        PlantsToWater = FindBedsForPlanting();
        if (PlantsToWater.Count == 0) { Plugin.ChatGui.PrintError("[Gaia] No garden beds found!"); return; }

        AutoLinkGpsIfMissing();

        ActivePlantingScript = steps;
        CurrentScriptIndex = 0;
        _bedsPlantedThisSession.Clear();
        IsHarvesting = false; IsPlanting = true; IsFertilizing = false; _sessionPlotActionLogged = false;
        CurrentState = FarmingState.TargetAndInteract;
        _nextActionTime = Environment.TickCount64;
        PrintYellow("Initiating Planting Sequence...");
    }

    private void AutoLinkGpsIfMissing()
    {
        if (IsIndoors) return;

        var currentProfile = _plugin.GetCurrentCharacterProfile();
        if (currentProfile == null || PlantsToWater.Count == 0) return;

        var activePlot = IsPersonalPlot ? currentProfile.PersonalPlots[CurrentPlotIndex] : currentProfile.FCPlots[CurrentPlotIndex];

        if (!activePlot.HasGps)
        {
            var posTarget = PlantsToWater[VisitOrder.FirstOrDefault()];
            if (posTarget == null) return;

            bool isDuplicate = false;
            foreach (var p in currentProfile.PersonalPlots) if (p.HasGps && Vector3.Distance(p.GetGpsVector(), posTarget.Position) < 0.5f) isDuplicate = true;
            foreach (var p in currentProfile.FCPlots) if (p.HasGps && Vector3.Distance(p.GetGpsVector(), posTarget.Position) < 0.5f) isDuplicate = true;

            if (isDuplicate)
            {
                PrintYellow("Warning: Interacted with a garden already linked to another plot. Skipping auto-GPS.");
            }
            else
            {
                var patchBeds = GetBedsForPatch(posTarget);
                int bedCount = patchBeds.Count;
                int finalCount = bedCount <= 4 ? 4 : bedCount <= 6 ? 6 : 8;

                activePlot.GpsX = posTarget.Position.X;
                activePlot.GpsY = posTarget.Position.Y;
                activePlot.GpsZ = posTarget.Position.Z;
                activePlot.PatchId = posTarget.BaseId;
                activePlot.BedCount = finalCount;
                _plugin.Configuration.Save();
                PrintYellow($"Learned GPS coordinates for {(IsPersonalPlot ? "Personal" : "FC")} Plot {CurrentPlotIndex + 1}.");
            }
        }
    }

    // --- CORE UPDATE LOOP ---
    public unsafe void Update()
    {
        if (CurrentState == FarmingState.Idle || WaitingForUser) return;

        if (Environment.TickCount64 > _lastStateChangeTime + _plugin.Configuration.StateTimeout)
        {
            PrintYellow("State Timeout! Resetting to Idle.");
            Stop();
            return;
        }

        if (Environment.TickCount64 < _nextActionTime) return;

        switch (CurrentState)
        {
            case FarmingState.TargetAndInteract: HandleTargetAndInteract(); break;
            case FarmingState.DismissDialogue: HandleDismissDialogue(); break;
            case FarmingState.MenuSelection: HandleMenuSelection(); break;
            case FarmingState.PlantingExecute: HandlePlantingExecute(); break;
            case FarmingState.AdvanceNext: HandleAdvanceToNextPlant(); break;
            case FarmingState.FertilizeVerify: HandleFertilizeVerify(); break;
            case FarmingState.PlantingConfirmYesNo: HandlePlantingConfirmYesNo(); break;
            case FarmingState.PlantingWaitMenu: HandlePlantingWaitMenu(); break;
            case FarmingState.PlantingInsertSoil: HandlePlantingInsertSoil(); break;
            case FarmingState.PlantingInsertSeed: HandlePlantingInsertSeed(); break;
            case FarmingState.WaitingForMenuClose: HandleContextMenuClick(); break;
            case FarmingState.BouncerQuitMenu: HandleBouncerQuitMenu(); break;
        }
    }
    private unsafe AtkUnitBase* GetActiveInventoryAddon(out string activeName)
    {
        // FFXIV uses "Event" versions of the inventory when interacting with crops/NPCs!
        string[] names = {
            "Inventory", "InventoryEvent",
            "InventoryExpanded", "InventoryExpandedEvent",
            "InventoryLarge", "InventoryLargeEvent"
        };

        foreach (var name in names)
        {
            var ptr = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(name, 1).Address;
            if (ptr != null && ptr->IsVisible)
            {
                activeName = name;
                return ptr;
            }
        }
        activeName = "";
        return null;
    }

    private int GetTargetTabIndex(string addonName, InventoryType invType)
    {
        if (addonName == "Inventory" || addonName == "InventoryEvent")
        {
            // Normal Mode: 4 separate tabs for bags
            if (invType == InventoryType.Inventory1) return 0;
            if (invType == InventoryType.Inventory2) return 1;
            if (invType == InventoryType.Inventory3) return 2;
            if (invType == InventoryType.Inventory4) return 3;
        }
        else if (addonName == "InventoryExpanded" || addonName == "InventoryExpandedEvent")
        {
            // Expanded Mode: Bag 1/2 are on Tab 0, Bag 3/4 are on Tab 1
            if (invType == InventoryType.Inventory1 || invType == InventoryType.Inventory2) return 0;
            if (invType == InventoryType.Inventory3 || invType == InventoryType.Inventory4) return 1;
        }
        // Open All Mode (InventoryLarge / InventoryLargeEvent): Everything is on Tab 0
        return 0;
    }

    private void HandleTargetAndInteract()
    {
        unsafe
        {
            var selectWrapper = Plugin.GameGui.GetAddonByName("SelectString", 1);
            if (selectWrapper.Address != IntPtr.Zero) ((AddonSelectString*)selectWrapper.Address)->AtkUnitBase.Close(true);

            var talkWrapper = Plugin.GameGui.GetAddonByName("Talk", 1);
            if (talkWrapper.Address != IntPtr.Zero) ((AddonTalk*)talkWrapper.Address)->AtkUnitBase.Close(true);
        }

        int targetSlot = -1;
        Dalamud.Game.ClientState.Objects.Types.IGameObject? plant = null;

        if (IsPlanting)
        {
            if (CurrentScriptIndex >= ActivePlantingScript.Count) { Stop(); PrintYellow("Planting complete!"); return; }
            targetSlot = ActivePlantingScript[CurrentScriptIndex].SlotIndex;
            if (targetSlot < PlantsToWater.Count) plant = PlantsToWater[targetSlot];
        }
        else
        {
            if (CurrentPlantIndex >= VisitOrder.Count)
            {
                string msg = IsScanning ? "Garden scan complete!" : (IsHarvesting ? "Harvesting complete!" : (IsUprootingAll ? "Garden nuked successfully!" : "Tending complete!"));
                Stop();
                PrintYellow(msg);
                return;
            }
            targetSlot = VisitOrder[CurrentPlantIndex];
            plant = PlantsToWater[targetSlot];
        }

        if (plant == null)
        {
            SetWarning($"{(IsIndoors ? "Pot" : "Bed")} {targetSlot + 1} not found in world! Skipping...");
            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + _plugin.Configuration.ShortTickDelay;
            return;
        }

        var currentProfile = _plugin.GetCurrentCharacterProfile();
        GardenBedState? bedState = null;
        if (currentProfile != null)
        {
            if (IsIndoors && ActiveIndoorPots != null && targetSlot < ActiveIndoorPots.Length)
                bedState = ActiveIndoorPots[targetSlot];
            else if (!IsIndoors)
            {
                var activePlot = IsPersonalPlot ? currentProfile.PersonalPlots[CurrentPlotIndex] : currentProfile.FCPlots[CurrentPlotIndex];
                if (targetSlot < activePlot.Beds.Length) bedState = activePlot.Beds[targetSlot];
            }
        }

        // --- FIX 1: Respect the "Red X" Context Override ---
        if (IsHarvesting && bedState != null && bedState.SkipHarvest)
        {
            PrintYellow($"Skipping {(IsIndoors ? "Pot" : "Bed")} {targetSlot + 1} (Marked as Do Not Harvest).");
            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + _plugin.Configuration.ShortTickDelay;
            return;
        }

        if (IsFertilizing && bedState != null)
        {
            // --- FIX 2: Prevent accidental harvests when Fertilizing ---
            if (bedState.IsMature)
            {
                PrintYellow($"{(IsIndoors ? "Pot" : "Bed")} {targetSlot + 1} is mature. Skipping fertilizer to prevent accidental harvest!");
                CurrentState = FarmingState.AdvanceNext;
                _nextActionTime = Environment.TickCount64 + _plugin.Configuration.ShortTickDelay;
                return;
            }

            // Your original cooldown check
            if ((DateTime.Now - bedState.LastFertilizedTime).TotalMinutes < 60)
            {
                PrintYellow($"{(IsIndoors ? "Pot" : "Bed")} {targetSlot + 1} fertilized recently (< 60m). Skipping...");
                CurrentState = FarmingState.AdvanceNext;
                _nextActionTime = Environment.TickCount64 + _plugin.Configuration.ShortTickDelay;
                return;
            }
        }

        float dist = Vector3.Distance(Plugin.ObjectTable.LocalPlayer!.Position, plant!.Position);
        float maxDist = IsIndoors ? 2.5f : 8.5f;

        if (dist > maxDist)
        {
            SetWarning($"Skipping {(IsIndoors ? "Pot" : "Bed")} {targetSlot + 1} - Too far away ({dist:F1}y). Get closer!");
            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + _plugin.Configuration.ShortTickDelay;
            return;
        }

        Plugin.TargetManager.Target = plant;
        unsafe { FFXIVClientStructs.FFXIV.Client.Game.Control.TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)plant.Address, false); }

        CurrentState = FarmingState.DismissDialogue;
        _nextActionTime = Environment.TickCount64 + _plugin.Configuration.GeneralDelay;
    }

    private unsafe void HandleDismissDialogue()
    {
        try
        {
            var selectWrapper = Plugin.GameGui.GetAddonByName("SelectString", 1);
            if (selectWrapper.Address != IntPtr.Zero && ((AddonSelectString*)selectWrapper.Address)->AtkUnitBase.IsVisible)
            {
                CurrentState = FarmingState.MenuSelection;
                _nextActionTime = Environment.TickCount64 + _plugin.Configuration.ShortTickDelay;
                return;
            }

            var talkWrapper = Plugin.GameGui.GetAddonByName("Talk", 1);
            if (talkWrapper.Address != IntPtr.Zero)
            {
                var talkAddon = (AddonTalk*)talkWrapper.Address;
                if (talkAddon->AtkUnitBase.IsVisible)
                {
                    string talkText = "";
                    for (int i = 0; i < talkAddon->AtkUnitBase.UldManager.NodeListCount; i++)
                    {
                        var node = talkAddon->AtkUnitBase.UldManager.NodeList[i];
                        if (node != null && node->Type == NodeType.Text)
                        {
                            var textNode = (AtkTextNode*)node;
                            var textPtr = (byte*)textNode->NodeText.StringPtr;
                            if (textPtr != null) talkText += Dalamud.Memory.MemoryHelper.ReadStringNullTerminated((nint)textPtr) + " ";
                        }
                    }

                    _isCurrentBedEmpty = talkText.Contains("nothing", StringComparison.OrdinalIgnoreCase);
                    _isCurrentPlantMature = talkText.Contains("ready to be harvested", StringComparison.OrdinalIgnoreCase);

                    string[] trackedCrops = GardenData.CropsToLoad;
                    string foundCrop = _isCurrentBedEmpty ? "Empty" : "Unknown Crop / Growing";
                    if (!_isCurrentBedEmpty)
                    {
                        foreach (var crop in trackedCrops) { if (talkText.Contains(crop, StringComparison.OrdinalIgnoreCase)) { foundCrop = crop; break; } }
                    }

                    int targetSlot = IsPlanting ? ActivePlantingScript[CurrentScriptIndex].SlotIndex : VisitOrder[CurrentPlantIndex];

                    // We also add the wilted check right here for future-proofing!
                    bool isWilted = talkText.Contains("seen better days", StringComparison.OrdinalIgnoreCase);

                    // ALWAYS update the UI with the truth from the server, no matter what job is running!
                    ScannedCrops[targetSlot] = foundCrop;
                    var currentProfile = _plugin.GetCurrentCharacterProfile();
                    if (currentProfile != null)
                    {
                        if (IsIndoors && ActiveIndoorPots != null && targetSlot < ActiveIndoorPots.Length)
                        {
                            ActiveIndoorPots[targetSlot].IsEmpty  = _isCurrentBedEmpty;
                            ActiveIndoorPots[targetSlot].IsMature = _isCurrentPlantMature;
                            ActiveIndoorPots[targetSlot].IsWilted = isWilted;
                            if (!_isCurrentBedEmpty) ActiveIndoorPots[targetSlot].SeedName = foundCrop;
                        }
                        else if (!IsIndoors)
                        {
                            var activePlot = IsPersonalPlot ? currentProfile.PersonalPlots[CurrentPlotIndex] : currentProfile.FCPlots[CurrentPlotIndex];
                            if (targetSlot < activePlot.Beds.Length)
                            {
                                activePlot.Beds[targetSlot].IsEmpty  = _isCurrentBedEmpty;
                                activePlot.Beds[targetSlot].IsMature = _isCurrentPlantMature;
                                activePlot.Beds[targetSlot].IsWilted = isWilted;
                                if (!_isCurrentBedEmpty) activePlot.Beds[targetSlot].SeedName = foundCrop;
                            }
                        }
                        _plugin.Configuration.Save();
                    }

                    bool shouldBounce = false;

                    if (!IsHarvesting && _isCurrentPlantMature)
                    {
                        PrintYellow($"Bed {targetSlot + 1} is MATURE! Bypassing safely...");
                        shouldBounce = true;
                    }
                    else if (!IsPlanting && _isCurrentBedEmpty)
                    {
                        PrintYellow($"Bed {targetSlot + 1} is empty! Bypassing safely...");
                        shouldBounce = true;
                    }
                    else if (IsHarvesting && !_isCurrentPlantMature)
                    {
                        PrintYellow($"Bed {targetSlot + 1} is not ready! Bypassing safely...");
                        shouldBounce = true;
                    }
                    // --- ADD THIS NEW PLANTING BOUNCER ---
                    else if (IsPlanting && !_isCurrentBedEmpty)
                    {
                        // Check if the script explicitly wants to uproot this specific plant. If not, bounce it!
                        var step = ActivePlantingScript[CurrentScriptIndex];
                        if (!step.IsUproot)
                        {
                            PrintYellow($"Bed {targetSlot + 1} already has a plant in it! Cannot plant here. Bypassing safely...");
                            shouldBounce = true;
                        }
                    }
                    // -------------------------------------

                    // ALWAYS fire the Talk callback so the server lets us proceed to the next step
                    AtkValue* talkValues = stackalloc AtkValue[1];
                    talkValues[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
                    talkValues[0].Int = 0;
                    _ = talkAddon->AtkUnitBase.FireCallback(0, talkValues);

                    if (shouldBounce)
                    {
                        // Divert the bot to the new clean-up state instead of the normal menu!
                        CurrentState = FarmingState.BouncerQuitMenu;
                        _nextActionTime = Environment.TickCount64 + _plugin.Configuration.WaterMenuDelay;
                        return;
                    }

                    // If it's safe, proceed to normal interaction!
                    CurrentState = FarmingState.MenuSelection;
                    _nextActionTime = Environment.TickCount64 + _plugin.Configuration.WaterMenuDelay;
                    return;
                    // -----------------------------------

                }
            }
        }
        catch (Exception ex) { Plugin.ChatGui.PrintError($"[Gaia Error] UI Read Failed: {ex.Message}"); }

        if (Environment.TickCount64 - _lastStateChangeTime > _plugin.Configuration.InteractionTimeout)
        {
            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + _plugin.Configuration.PostTimeoutDelay;
        }
    }

    private unsafe void HandleMenuSelection()
    {
        var selectWrapper = Plugin.GameGui.GetAddonByName("SelectString", 1);
        if (selectWrapper.Address == IntPtr.Zero) return;
        var addon = (AddonSelectString*)selectWrapper.Address;
        if (!addon->AtkUnitBase.IsVisible) return;

        if (IsFertilizing)
        {
            _startingFertilizerCount = InventoryHelper.GetItemCount(_plugin.Configuration.FertilizerId);
            SendSelectStringCallback(addon, 0);

            CurrentState = FarmingState.PlantingExecute;
            _nextActionTime = Environment.TickCount64 + 800;
            return;
        }

        if (IsPlanting)
        {
            var step = ActivePlantingScript[CurrentScriptIndex];
            SendSelectStringCallback(addon, step.IsUproot ? 2 : 0);
            CurrentState = step.IsUproot ? FarmingState.PlantingConfirmYesNo : FarmingState.PlantingWaitMenu;
            _nextActionTime = Environment.TickCount64 + 600;
            return;
        }

        if (IsHarvesting)
        {
            SendSelectStringCallback(addon, 0);
            UpdateGardenData(VisitOrder[CurrentPlantIndex], true);

            // Global tracks per bed
            _plugin.Configuration.TotalBedsHarvested++;

            // --- FIXED: PLOT TRACKS PER SESSION ---
            var currentProfile = _plugin.GetCurrentCharacterProfile();
            if (currentProfile != null && !IsIndoors && !_sessionPlotActionLogged)
            {
                var activePlot = IsPersonalPlot ? currentProfile.PersonalPlots[CurrentPlotIndex] : currentProfile.FCPlots[CurrentPlotIndex];
                activePlot.TotalHarvests++;
                _sessionPlotActionLogged = true; // Lock it!
            }

            _plugin.Configuration.Save();

            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + (uint)_plugin.Configuration.HarvestAnimDelay;
        }
        else // --- WATERING ---
        {
            SendSelectStringCallback(addon, 1);
            UpdateWaterTime(VisitOrder[CurrentPlantIndex]);

            _plugin.Configuration.TotalBedsWatered++;
            _plugin.Configuration.Save();

            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + (uint)_plugin.Configuration.BetweenPlantsDelay;
        }
    }

    private unsafe void SendSelectStringCallback(AddonSelectString* addon, int index)
    {
        AtkValue* values = stackalloc AtkValue[1];
        values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
        values[0].Int = index;
        _ = addon->AtkUnitBase.FireCallback(1, values);
    }

    private unsafe void HandlePlantingWaitMenu()
    {
        if (GetGardeningAddon() != null)
        {
            CurrentState = FarmingState.PlantingInsertSoil;
            _nextActionTime = Environment.TickCount64 + _plugin.Configuration.WaterMenuDelay;
        }
    }

    private unsafe void HandlePlantingInsertSoil()
    {
        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentModule.Instance()->GetAgentByInternalId((FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId)125);
        var step = ActivePlantingScript[CurrentScriptIndex];
        var itemData = InventoryHelper.GetItemLocation(step.Soil);

        if (agent != null && itemData.ItemId != 0)
        {
            *(uint*)((byte*)agent + 0x48) = itemData.InvType;
            *(uint*)((byte*)agent + 0x4C) = itemData.Slot;
            *(uint*)((byte*)agent + 0x50) = itemData.ItemId;
        }
        CurrentState = FarmingState.PlantingInsertSeed;
        _nextActionTime = Environment.TickCount64 + _plugin.Configuration.WaterMenuDelay;
    }

    private unsafe void HandlePlantingInsertSeed()
    {
        var step = ActivePlantingScript[CurrentScriptIndex];
        var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentModule.Instance()->GetAgentByInternalId((FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentId)125);
        var itemData = InventoryHelper.GetItemLocation(step.Seed);

        if (agent != null && itemData.ItemId != 0)
        {
            *(uint*)((byte*)agent + 0x58) = itemData.InvType;
            *(uint*)((byte*)agent + 0x5C) = itemData.Slot;
            *(uint*)((byte*)agent + 0x60) = itemData.ItemId;
        }
        CurrentState = FarmingState.PlantingExecute;
        _nextActionTime = Environment.TickCount64 + _plugin.Configuration.WaterMenuDelay;
    }

    private unsafe void HandlePlantingExecute()
    {
        if (IsFertilizing)
        {
            if (IsFertilizeErrorPresent())
            {
                PrintYellow("Game reported: Already sufficiently fertilized! Skipping...");
                UpdateFertilizeTime(VisitOrder[CurrentPlantIndex]);
                CurrentState = FarmingState.AdvanceNext;
                _nextActionTime = Environment.TickCount64 + _plugin.Configuration.SkipPlantDelay;
                return;
            }

            // --- THE FIX: Look for the FFXIV Event Inventory popup ---
            if (!IsEventInventoryOpen())
            {
                if (Environment.TickCount64 > _lastStateChangeTime + _plugin.Configuration.InventoryWaitTimeout)
                {
                    PrintYellow("Inventory failed to open (Likely already fertilized). Skipping.");
                    UpdateFertilizeTime(VisitOrder[CurrentPlantIndex]);
                    CurrentState = FarmingState.AdvanceNext;
                }
                else
                {
                    _nextActionTime = Environment.TickCount64 + _plugin.Configuration.FertilizePollDelay;
                }
                return;
            }

            uint fishmealId = _plugin.Configuration.FertilizerId;
            var liveData = InventoryHelper.GetItemLocation(fishmealId);

            if (liveData.ItemId == 0)
            {
                SetWarning("Fish Meal not found in bags.");
                CurrentState = FarmingState.AdvanceNext;
                return;
            }

            var agent = AgentInventoryContext.Instance();
            if (agent != null)
            {
                _startingFertilizerCount = InventoryHelper.GetItemCount(_plugin.Configuration.FertilizerId);
                _hasSeenContextMenu = false;

                // The Agent backend ignores visual tabs entirely, so we just fire the context menu directly!
                agent->OpenForItemSlot((InventoryType)liveData.InvType, (int)liveData.Slot, 0, 0);

                CurrentState = FarmingState.WaitingForMenuClose;
                _nextActionTime = Environment.TickCount64 + _plugin.Configuration.FertilizePollDelay;
                return;
            }
        }

        // --- Standard Planting logic ---
        var addon = GetGardeningAddon();
        if (addon != null)
        {
            AtkValue* values = stackalloc AtkValue[1];
            values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
            values[0].Int = 0;
            _ = addon->FireCallback(0, values);
        }

        CurrentState = FarmingState.PlantingConfirmYesNo;
        _nextActionTime = Environment.TickCount64 + _plugin.Configuration.MenuSelectionDelay;
    }

    private unsafe void HandleContextMenuClick()
    {
        var contextMenu = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("ContextMenu", 1).Address;
        bool isVisible = contextMenu != null && contextMenu->IsVisible;

        if (Environment.TickCount64 > _lastStateChangeTime + _plugin.Configuration.MenuInteractionTimeout)
        {
            PrintYellow("[Gaia] Context menu interaction timed out.");
            if (isVisible) contextMenu->Close(true);
            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + _plugin.Configuration.SkipPlantDelay;
            return;
        }

        if (!isVisible)
        {
            if (_hasSeenContextMenu)
            {
                CurrentState = FarmingState.FertilizeVerify;
                _nextActionTime = Environment.TickCount64 + _plugin.Configuration.MenuSuccessDelay;
            }
            else
            {
                _nextActionTime = Environment.TickCount64 + _plugin.Configuration.FertilizePollDelay;
            }
            return;
        }

        _hasSeenContextMenu = true;

        // --- NEW: CROSS-PLATFORM MEMORY CLICK ---
        // The ContextMenu requires 5 values. 
        // values[0] = 0 (Left Click Action)
        // values[1] = 0 (Index 0 in the list, which is always "Use" or "Plant")
        AtkValue* values = stackalloc AtkValue[5];
        values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
        values[0].Int = 0;
        values[1].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
        values[1].Int = 0;
        values[2].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
        values[2].Int = 0;
        values[3].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
        values[3].Int = 0;
        values[4].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
        values[4].Int = 0;

        _ = contextMenu->FireCallback(5, values);
        // ----------------------------------------

        _nextActionTime = Environment.TickCount64 + _plugin.Configuration.NumpadPollDelay;
    }

    private unsafe void HandlePlantingConfirmYesNo()
    {
        var yesno = (AtkUnitBase*)Plugin.GameGui.GetAddonByName("SelectYesno", 1).Address;
        if (yesno != null && yesno->IsVisible)
        {
            AtkValue* values = stackalloc AtkValue[1];
            values[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
            values[0].Int = 0;
            _ = yesno->FireCallback(0, values);

            var currentProfile = _plugin.GetCurrentCharacterProfile();
            if (currentProfile != null)
            {
                if (IsPlanting)
                {
                    var step = ActivePlantingScript[CurrentScriptIndex];
                    var bed = GetActiveBedAt(currentProfile, step.SlotIndex);
                    if (bed != null)
                    {
                        if (!step.IsUproot)
                        {
                            _bedsPlantedThisSession.Add(step.SlotIndex);
                            bed.IsEmpty  = false;
                            bed.IsWilted = false;
                            bed.SeedName = step.Seed.Replace(" Seeds", "").Replace(" Kernels", "");
                            bed.IsMature = false;
                            bed.LastWateredTime = DateTime.Now;
                            bed.PlantedTime = DateTime.Now;
                            _plugin.Configuration.TotalPlantsPlanted++;
                        }
                        else
                        {
                            bed.IsEmpty  = true;
                            bed.IsWilted = false;
                            bed.SeedName = "";
                            bed.PlantedTime = DateTime.MinValue;
                        }
                        _plugin.Configuration.Save();
                    }
                }
                else if (IsUprootingAll)
                {
                    int targetSlot = VisitOrder[CurrentPlantIndex];
                    var bed = GetActiveBedAt(currentProfile, targetSlot);
                    if (bed != null)
                    {
                        bed.IsEmpty  = true;
                        bed.IsWilted = false;
                        bed.SeedName = "";
                        bed.IsMature = false;
                        bed.PlantedTime = DateTime.MinValue;
                        _plugin.Configuration.Save();
                    }
                }
            }

            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + (IsUprootingAll ? 4500 : 3000);
        }
    }

    private void HandleFertilizeVerify()
    {
        int currentCount = InventoryHelper.GetItemCount(_plugin.Configuration.FertilizerId);

        if (currentCount < _startingFertilizerCount && _startingFertilizerCount != 0)
        {
            int targetSlot = VisitOrder[CurrentPlantIndex];

            UpdateFertilizeTime(targetSlot);

            _plugin.Configuration.TotalBedsFertilized++;
            _plugin.Configuration.Save();

            PrintYellow($"[Gaia] Successfully fertilized {(IsIndoors ? "Pot" : "Bed")} {targetSlot + 1}!");

            _startingFertilizerCount = 0;
            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + _plugin.Configuration.FertilizeSuccessDelay;
            return;
        }

        if (Environment.TickCount64 > _lastStateChangeTime + _plugin.Configuration.FertilizeVerifyTimeout)
        {
            PrintYellow("[Gaia] Fertilize timed out or was rejected. Moving on without saving time.");
            _startingFertilizerCount = 0;
            CurrentState = FarmingState.AdvanceNext;
            _nextActionTime = Environment.TickCount64 + _plugin.Configuration.SkipPlantDelay;
        }
    }

    private unsafe AtkUnitBase* GetGardeningAddon()
    {
        string[] names = { "HousingGardening", "HousingPlant", "HousingAgriculture", "HousingIndoorPlant" };
        foreach (var name in names)
        {
            var ptr = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(name, 1).Address;
            if (ptr != null && ptr->IsVisible) return ptr;
        }
        return null;
    }

    // Resolves the GardenBedState that belongs to the current operating context.
    // Indoor: ActiveIndoorPots[slot]. Outdoor: the active personal/FC plot's bed.
    // Returns null if slot is out of range or the context is uninitialised.
    // Used to be inlined in HandlePlantingConfirmYesNo with an unconditional
    // outdoor lookup, which silently scrambled outdoor plot data whenever
    // indoor planting ran. See "fix indoor planting writes outdoor state" commit.
    private GardenBedState? GetActiveBedAt(CharacterProfile profile, int slot)
    {
        if (IsIndoors)
        {
            if (ActiveIndoorPots != null && slot >= 0 && slot < ActiveIndoorPots.Length)
                return ActiveIndoorPots[slot];
            return null;
        }
        var plots = IsPersonalPlot ? profile.PersonalPlots : profile.FCPlots;
        if (CurrentPlotIndex < 0 || CurrentPlotIndex >= plots.Length) return null;
        var plot = plots[CurrentPlotIndex];
        if (slot < 0 || slot >= plot.Beds.Length) return null;
        return plot.Beds[slot];
    }

    private void HandleAdvanceToNextPlant()
    {
        Plugin.TargetManager.Target = null;

        if (IsPlanting) CurrentScriptIndex++; else CurrentPlantIndex++;

        CurrentState = FarmingState.TargetAndInteract;

        _nextActionTime = Environment.TickCount64 + (IsIndoors ? _plugin.Configuration.AdvanceIndoorDelay : _plugin.Configuration.AdvanceOutdoorDelay);
    }

    private void UpdateGardenData(int slot, bool isEmpty)
    {
        var currentProfile = _plugin.GetCurrentCharacterProfile();
        if (currentProfile == null) return;

        if (IsIndoors && ActiveIndoorPots != null && slot < ActiveIndoorPots.Length)
        {
            ActiveIndoorPots[slot].IsEmpty  = isEmpty;
            ActiveIndoorPots[slot].IsWilted = false;
            ActiveIndoorPots[slot].IsMature = false;
            if (isEmpty)
            {
                ActiveIndoorPots[slot].SeedName = "";
                ActiveIndoorPots[slot].PlantedTime = DateTime.MinValue;
                ActiveIndoorPots[slot].SkipHarvest = false;
            }
        }
        else if (!IsIndoors)
        {
            var activePlot = IsPersonalPlot ? currentProfile.PersonalPlots[CurrentPlotIndex] : currentProfile.FCPlots[CurrentPlotIndex];
            if (slot < activePlot.Beds.Length)
            {
                activePlot.Beds[slot].IsEmpty  = isEmpty;
                activePlot.Beds[slot].IsWilted = false;
                activePlot.Beds[slot].IsMature = false;
                if (isEmpty)
                {
                    activePlot.Beds[slot].SeedName = "";
                    activePlot.Beds[slot].PlantedTime = DateTime.MinValue;
                    activePlot.Beds[slot].SkipHarvest = false;
                }
            }
        }
        _plugin.Configuration.Save();
    }

    private void UpdateWaterTime(int slot)
    {
        var currentProfile = _plugin.GetCurrentCharacterProfile();
        if (currentProfile == null) return;

        if (IsIndoors && ActiveIndoorPots != null && slot < ActiveIndoorPots.Length)
        {
            ActiveIndoorPots[slot].LastWateredTime = DateTime.Now;
        }
        else if (!IsIndoors)
        {
            var activePlot = IsPersonalPlot ? currentProfile.PersonalPlots[CurrentPlotIndex] : currentProfile.FCPlots[CurrentPlotIndex];
            if (slot < activePlot.Beds.Length)
            {
                // --- FIXED: ONLY LOG THIS ONCE PER BOT RUN ---
                if (!_sessionPlotActionLogged)
                {
                    if (activePlot.Beds[slot].LastWateredTime > DateTime.MinValue)
                    {
                        float hoursSince = (float)(DateTime.Now - activePlot.Beds[slot].LastWateredTime).TotalHours;
                        activePlot.AddWateringDataPoint(hoursSince);
                    }
                    activePlot.TotalWaterings++;
                    _sessionPlotActionLogged = true; // Lock it!
                }

                activePlot.Beds[slot].LastWateredTime = DateTime.Now;
            }
        }
        _plugin.Configuration.Save();
    }

    private void UpdateFertilizeTime(int slot)
    {
        var currentProfile = _plugin.GetCurrentCharacterProfile();
        if (currentProfile == null) return;

        if (IsIndoors && ActiveIndoorPots != null && slot < ActiveIndoorPots.Length)
        {
            ActiveIndoorPots[slot].LastFertilizedTime = DateTime.Now;
        }
        else if (!IsIndoors)
        {
            var activePlot = IsPersonalPlot ? currentProfile.PersonalPlots[CurrentPlotIndex] : currentProfile.FCPlots[CurrentPlotIndex];
            if (slot < activePlot.Beds.Length)
            {
                // --- FIXED: ONLY LOG THIS ONCE PER BOT RUN ---
                if (!_sessionPlotActionLogged)
                {
                    activePlot.TotalFertilizings++;
                    _sessionPlotActionLogged = true; // Lock it!
                }

                activePlot.Beds[slot].LastFertilizedTime = DateTime.Now;
            }
        }
        _plugin.Configuration.Save();
    }
    private unsafe bool IsFertilizeErrorPresent()
    {
        string[] addons = { "_TextError", "Toast" };
        foreach (var name in addons)
        {
            var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(name, 1).Address;
            if (addon == null || !addon->IsVisible) continue;

            for (int i = 0; i < addon->UldManager.NodeListCount; i++)
            {
                var node = addon->UldManager.NodeList[i];
                if (node != null && node->Type == NodeType.Text)
                {
                    var textPtr = ((AtkTextNode*)node)->NodeText.StringPtr;
                    if (!textPtr.HasValue) continue;

                    var text = Dalamud.Memory.MemoryHelper.ReadStringNullTerminated((nint)textPtr.Value);
                    if (text.Contains("sufficiently fertilized", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        }
        return false;
    }
    private unsafe bool IsEventInventoryOpen()
    {
        // FFXIV uses special "Event" grid windows when you are handing over an item like Fertilizer
        string[] names = { "InventoryGrid0E", "InventoryGrid1E", "InventoryGrid2E", "InventoryGrid3E" };
        foreach (var name in names)
        {
            var ptr = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(name, 1).Address;
            if (ptr != null && ptr->IsVisible) return true;
        }
        return false;
    }

    // Call this from your Plugin.cs when Gaia starts up!
    public void Initialize()
    {
        Plugin.ChatGui.ChatMessage += OnChatMessage;
    }

    // Call this from your Plugin.cs when Gaia shuts down!
    public void Dispose()
    {
        Plugin.ChatGui.ChatMessage -= OnChatMessage;
    }

    private unsafe void HandleBouncerQuitMenu()
    {
        var selectWrapper = Plugin.GameGui.GetAddonByName("SelectString", 1);
        if (selectWrapper.Address != IntPtr.Zero)
        {
            var selectAddon = (FFXIVClientStructs.FFXIV.Client.UI.AddonSelectString*)selectWrapper.Address;
            if (selectAddon->AtkUnitBase.IsVisible)
            {
                // Fire the universal "Escape/Cancel" callback (-1) to safely tell the server we are done
                AtkValue* cancelValues = stackalloc AtkValue[1];
                cancelValues[0].Type = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType.Int;
                cancelValues[0].Int = -1;
                selectAddon->AtkUnitBase.FireCallback(1, cancelValues);

                // Now the server lock is broken, and we are safe to move to the next plant!
                CurrentState = FarmingState.AdvanceNext;
                _nextActionTime = Environment.TickCount64 + _plugin.Configuration.GeneralDelay;
                return;
            }
        }

        // Just in case the menu lags, don't let the bot get stuck forever
        if (Environment.TickCount64 - _lastStateChangeTime > _plugin.Configuration.InteractionTimeout)
        {
            CurrentState = FarmingState.AdvanceNext;
        }
    }

    private void OnChatMessage(Dalamud.Game.Chat.IHandleableChatMessage chatMsg)
    {
        // FFXIV uses specific chat types for system/loot messages (usually 57 or 62).
        // We also only care if the bot has been active recently, so we don't track manual gathering out in the wild.
        if (CurrentState == FarmingState.Idle && Environment.TickCount64 > _lastStateChangeTime + 10000) return;

        string text = chatMsg.Message.TextValue;

        // FFXIV Loot messages always start with this exact phrasing
        if (text.StartsWith("You obtain", StringComparison.OrdinalIgnoreCase))
        {
            // This Regex looks for: "You obtain " + [Optional: "a ", "an ", or a Number] + [The Item Name] + "."
            var match = System.Text.RegularExpressions.Regex.Match(text, @"You obtain (?:a |an |(?<qty>\d+) )?(?<item>.+?)(?:\.|$)");

            if (match.Success)
            {
                string itemName = match.Groups["item"].Value;
                int qty = 1; // Default to 1 if it said "a" or "an"

                if (match.Groups["qty"].Success)
                {
                    int.TryParse(match.Groups["qty"].Value, out qty);
                }

                // FFXIV chat sometimes lowercases items or adds plural 's'. 
                // We Title Case it so "thavnairian onions" becomes "Thavnairian Onions" in your Stats dictionary.
                itemName = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(itemName.ToLower());

                // Ensure the dictionary exists (safety check)
                if (_plugin.Configuration.LifetimeHarvestYields == null)
                    _plugin.Configuration.LifetimeHarvestYields = new Dictionary<string, int>();

                // Add or Update the tracker
                if (_plugin.Configuration.LifetimeHarvestYields.ContainsKey(itemName))
                    _plugin.Configuration.LifetimeHarvestYields[itemName] += qty;
                else
                    _plugin.Configuration.LifetimeHarvestYields[itemName] = qty;

                _plugin.Configuration.Save();

                PrintYellow($"[Stats] Logged harvest: {itemName} x{qty}");
            }
        }
    }
}