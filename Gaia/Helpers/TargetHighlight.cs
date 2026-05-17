using Dalamud.Game.ClientState.Objects.Types;

namespace Gaia.Helpers;

// Shared "is the player targeting this bed/plot in-world?" check + a small
// draw helper for the amber accent ring. Uses the same BaseId + 0.5y position
// match as the [TARGETED] indicator in PlantingTab.cs:1085 and the linked-row
// tint in SettingsTab (e415c4f). Only beds/plots with HasGps can ever match,
// so outdoor sub-beds (no per-bed GPS) are silently skipped.
public static class TargetHighlight
{
    // Reuses the SettingsTab ColTargetTint hue for consistency across tabs.
    private static readonly Vector4 RingColor = new(0.95f, 0.78f, 0.20f, 0.90f);

    public static bool MatchesBed(IGameObject? target, GardenBedState? bed)
    {
        if (target == null || bed == null || !bed.HasGps) return false;
        return target.BaseId == bed.DataId
            && Vector3.Distance(target.Position, bed.GetGpsVector()) < 0.5f;
    }

    public static bool MatchesPlot(IGameObject? target, GardenPlotState? plot)
    {
        if (target == null || plot == null || !plot.HasGps) return false;
        return target.BaseId == plot.PatchId
            && Vector3.Distance(target.Position, plot.GetGpsVector()) < 0.5f;
    }

    /// <summary>Draw an amber ring on top of an already-rendered slot if matched.</summary>
    public static void DrawRing(ImDrawListPtr draw, Vector2 origin, Vector2 size, float rounding = 5f, float thickness = 2.5f)
    {
        draw.AddRect(origin - new Vector2(1f, 1f), origin + size + new Vector2(1f, 1f),
                     ImGui.GetColorU32(RingColor), rounding, 0, thickness);
    }
}
