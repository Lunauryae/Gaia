using Gaia.Helpers;

namespace Gaia.Manager;

// Single-cycle "harvest → replant → fertilize → water" orchestrator.
// Reuses FarmingManager's existing Start* entry points; hooks into Stop()
// via FarmingManager.OnCycleCompleteCallback to advance to the next phase
// when the state machine returns to Idle.
//
// Pattern handling (design doc Q1, all per user approval):
//   - 5x3 Sustain  → harvest non-parents first, replant non-parents,
//                    then harvest parents, replant parents, then fert+water.
//                    Parent slots derived from the caller's _protMap.
//   - Harvest All / 4x4 / PoorMan / RichMan → bulk path:
//                    harvest everything → replant everything → fert → water.
//                    (4x4/PoorMan/RichMan fall back to bulk per design Q1.)
//   - Indoor       → same as Harvest All bulk, with replant pulling per-pot
//                    seed memory captured at cycle start.
//
// Failure handling (design Q2):
//   - Out of seeds/soil for a bed   → skip that bed, log to summary
//   - Out of fertilizer             → skip entire fertilize phase, log
//   - State-machine timeout / abort → orchestrator goes dormant (the
//                                     FarmingManager owns interaction
//                                     timeout already)
//
// Single cycle per click (Q4). Manual Stop button calls Abort() to clear
// orchestrator state before hitting FarmingManager.Stop().
public class AutoReplantOrchestrator
{
    public enum PhaseType { Harvest, Replant, Fertilize, Water }

    private class Phase
    {
        public PhaseType Type;
        public HashSet<int>? SlotFilter; // null = all slots
    }

    private readonly Plugin _plugin;
    private readonly Queue<Phase> _queue = new();
    private readonly Dictionary<int, string> _seedSnapshot = new();
    private readonly List<string> _skippedLog = new();

    private bool _active;
    private bool _isIndoorCycle;
    private int _totalSlots;

    public bool IsActive => _active;

    public AutoReplantOrchestrator(Plugin plugin) => _plugin = plugin;

    // Outdoor cycle entry. parentSlots are 5x3-Sustain "keep these planted
    // until the end" slots; pass an empty set or null for bulk Harvest All.
    public void StartOutdoorCycle(HashSet<int>? parentSlots)
    {
        if (_active) return;
        var ctx = _plugin.GardenContext;
        if (ctx.IsIndoors) { Plugin.ChatGui.PrintError("[Gaia] Auto-Replant: outdoor cycle requested but you're indoors."); return; }
        var plot = ctx.ActivePlot;
        if (plot == null || !plot.HasGps)
        {
            Plugin.ChatGui.PrintError("[Gaia] Auto-Replant: no GPS-linked outdoor plot is active.");
            return;
        }

        _isIndoorCycle = false;
        StartCycleCommon(plot.Beds, parentSlots);
    }

    // Indoor cycle entry. No parent logic — pots don't crossbreed.
    public void StartIndoorCycle()
    {
        if (_active) return;
        var ctx = _plugin.GardenContext;
        if (!ctx.IsIndoors) { Plugin.ChatGui.PrintError("[Gaia] Auto-Replant: indoor cycle requested but you're outdoors."); return; }
        var beds = ctx.ActiveBeds;
        if (beds == null || beds.Length == 0)
        {
            Plugin.ChatGui.PrintError("[Gaia] Auto-Replant: no indoor pots configured for this context.");
            return;
        }

        _isIndoorCycle = true;
        StartCycleCommon(beds, parentSlots: null);
    }

    private void StartCycleCommon(GardenBedState[] beds, HashSet<int>? parentSlots)
    {
        _seedSnapshot.Clear();
        _skippedLog.Clear();
        _queue.Clear();
        _totalSlots = beds.Length;

        // Snapshot what each non-skipped non-empty bed currently has, BEFORE
        // harvest clears SeedName. Beds marked SkipHarvest don't get harvested
        // or replanted — they're explicitly preserved by the user.
        for (int i = 0; i < beds.Length; i++)
        {
            var b = beds[i];
            if (b == null || b.IsEmpty || b.SkipHarvest) continue;
            if (!string.IsNullOrEmpty(b.SeedName))
                _seedSnapshot[i] = b.SeedName;
        }

        bool hasParents = parentSlots != null && parentSlots.Count > 0 && !_isIndoorCycle;
        if (hasParents)
        {
            var nonParents = new HashSet<int>();
            for (int i = 0; i < beds.Length; i++) if (!parentSlots!.Contains(i)) nonParents.Add(i);

            _queue.Enqueue(new Phase { Type = PhaseType.Harvest, SlotFilter = nonParents });
            _queue.Enqueue(new Phase { Type = PhaseType.Replant, SlotFilter = nonParents });
            _queue.Enqueue(new Phase { Type = PhaseType.Harvest, SlotFilter = parentSlots });
            _queue.Enqueue(new Phase { Type = PhaseType.Replant, SlotFilter = parentSlots });
        }
        else
        {
            _queue.Enqueue(new Phase { Type = PhaseType.Harvest });
            _queue.Enqueue(new Phase { Type = PhaseType.Replant });
        }
        _queue.Enqueue(new Phase { Type = PhaseType.Fertilize });
        _queue.Enqueue(new Phase { Type = PhaseType.Water });

        _active = true;
        string modeLabel = _isIndoorCycle ? "indoor" : (hasParents ? "5x3 Sustain" : "Harvest All");
        Plugin.ChatGui.Print($"[Gaia] Auto-Replant cycle starting ({modeLabel}, {_seedSnapshot.Count} beds to recycle).");
        RunNextPhase();
    }

    public void Abort()
    {
        if (!_active) return;
        _active = false;
        _queue.Clear();
        _plugin.Farming.OnCycleCompleteCallback = null;
        _plugin.Farming.SlotFilter = null;
        Plugin.ChatGui.Print("[Gaia] Auto-Replant cycle aborted.");
    }

    private void OnPhaseComplete()
    {
        if (!_active) return;
        RunNextPhase();
    }

    private void RunNextPhase()
    {
        if (_queue.Count == 0)
        {
            EmitSummary();
            _active = false;
            return;
        }

        var phase = _queue.Dequeue();

        // Pre-arm the next phase's completion hook BEFORE invoking Start*.
        // The Start* methods kick the state machine synchronously; the hook
        // fires from Stop() when the phase finishes naturally.
        _plugin.Farming.OnCycleCompleteCallback = OnPhaseComplete;

        switch (phase.Type)
        {
            case PhaseType.Harvest:
                _plugin.Farming.SlotFilter = phase.SlotFilter;
                _plugin.Farming.HarvestNearestBed();
                break;

            case PhaseType.Replant:
                _plugin.Farming.SlotFilter = null;
                var steps = BuildReplantSteps(phase.SlotFilter);
                if (steps.Count == 0)
                {
                    Plugin.ChatGui.Print("[Gaia] Auto-Replant: no beds to replant this phase.");
                    // Drop the pre-armed hook so we don't double-advance.
                    _plugin.Farming.OnCycleCompleteCallback = null;
                    RunNextPhase();
                    return;
                }
                _plugin.Farming.StartPlanting(steps);
                break;

            case PhaseType.Fertilize:
                _plugin.Farming.SlotFilter = null;
                int fertCount = InventoryHelper.GetItemCount(_plugin.Configuration.FertilizerId);
                if (fertCount == 0)
                {
                    _skippedLog.Add("Fertilize phase skipped — out of fertilizer.");
                    _plugin.Farming.OnCycleCompleteCallback = null;
                    RunNextPhase();
                    return;
                }
                _plugin.Farming.StartFertilizing();
                break;

            case PhaseType.Water:
                _plugin.Farming.SlotFilter = null;
                _plugin.Farming.WaterNearestBed();
                break;
        }
    }

    // Builds PlantingSteps from the per-bed seed snapshot. Skips beds that
    // are missing seeds/soil and logs to the summary. Default soil is G3
    // Thanalan Topsoil — the existing crossbreed setup standard.
    private List<PlantingStep> BuildReplantSteps(HashSet<int>? slotFilter)
    {
        const string defaultSoil = "Grade 3 Thanalan Topsoil";
        var steps = new List<PlantingStep>();
        var seedNeeds = new Dictionary<string, int>();
        var soilNeeds = new Dictionary<string, int>();

        var slots = _seedSnapshot.Keys.OrderBy(i => i).ToList();
        foreach (var slot in slots)
        {
            if (slotFilter != null && !slotFilter.Contains(slot)) continue;

            string cropName = _seedSnapshot[slot];
            string seedName = CropToSeedName(cropName);
            if (string.IsNullOrEmpty(seedName))
            {
                _skippedLog.Add($"Bed {slot + 1}: unknown previous seed ({cropName}).");
                continue;
            }

            int needSeed = seedNeeds.GetValueOrDefault(seedName, 0) + 1;
            int haveSeed = InventoryHelper.GetItemCount(seedName);
            if (haveSeed < needSeed)
            {
                _skippedLog.Add($"Bed {slot + 1}: out of {seedName} (need {needSeed}, have {haveSeed}).");
                continue;
            }
            int needSoil = soilNeeds.GetValueOrDefault(defaultSoil, 0) + 1;
            int haveSoil = InventoryHelper.GetItemCount(defaultSoil);
            if (haveSoil < needSoil)
            {
                _skippedLog.Add($"Bed {slot + 1}: out of {defaultSoil} (need {needSoil}, have {haveSoil}).");
                continue;
            }

            seedNeeds[seedName] = needSeed;
            soilNeeds[defaultSoil] = needSoil;
            steps.Add(new PlantingStep(slot, seedName, defaultSoil));
        }
        return steps;
    }

    // Reverse-lookup SeedToCropMap: crop name → seed name (e.g.,
    // "Krakka Root" → "Krakka Root Seeds"). Returns "" if not found.
    private static string CropToSeedName(string cropName)
    {
        if (string.IsNullOrEmpty(cropName)) return "";
        foreach (var kvp in GardenData.SeedToCropMap)
        {
            if (string.Equals(kvp.Value, cropName, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }
        return "";
    }

    private void EmitSummary()
    {
        if (_skippedLog.Count == 0)
        {
            Plugin.ChatGui.Print("[Gaia] Auto-Replant cycle complete — no skips.");
            return;
        }

        // Collapse duplicate reasons to avoid spam.
        var collapsed = new Dictionary<string, int>();
        foreach (var line in _skippedLog)
        {
            string key = CollapseKey(line);
            collapsed[key] = collapsed.GetValueOrDefault(key, 0) + 1;
        }

        Plugin.ChatGui.Print($"[Gaia] Auto-Replant cycle complete — {_skippedLog.Count} skip(s):");
        foreach (var (key, count) in collapsed)
        {
            string prefix = count > 1 ? $"  • {count}x " : "  • ";
            Plugin.ChatGui.Print(prefix + key);
        }
    }

    // "Bed 3: out of Krakka Root Seeds (need 2, have 1)." → "out of Krakka Root Seeds"
    // "Fertilize phase skipped — out of fertilizer." → unchanged
    // Strips the leading "Bed N: " prefix and the "(need ..., have ...)" suffix
    // so per-bed lines collapse by underlying reason.
    private static string CollapseKey(string line)
    {
        int colon = line.IndexOf(": ");
        string body = colon > 0 ? line.Substring(colon + 2) : line;
        int paren = body.LastIndexOf(" (");
        if (paren > 0) body = body.Substring(0, paren);
        return body.TrimEnd('.');
    }
}
