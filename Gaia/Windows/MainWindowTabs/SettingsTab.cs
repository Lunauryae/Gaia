namespace Gaia.Windows.MainWindowTabs;

using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.Utility;
using Gaia.Core.UI;
using Gaia.Helpers;
using System.Numerics;
using static Gaia.Helpers.GardenData;

public class SettingsTab : IDrawablePage
{
    private readonly Plugin plugin;
    public string TabLabel => "  Settings  ";
    private int nukeTargetPlot = 0;
    private bool _unlockDangerZone = false;

    public SettingsTab(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void Draw()
    {
        var currentProfile = plugin.GetCurrentCharacterProfile();
        if (currentProfile == null) return;

        // --- DASHBOARD THEME ---
        ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Dashboard Theme");
        ImGuiHelpers.ScaledDummy(3.0f);
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Controls the visual style of the Dashboard tab.");
        ImGuiHelpers.ScaledDummy(4.0f);
        ImGui.SetNextItemWidth(200f);
        string[] themeNames = { "Garden Codex (Dark)", "Field Guide (Parchment)", "Kawaii (Pastel)" };
        int themeIdx = (int)plugin.Configuration.SelectedTheme;
        if (ImGui.Combo("##DashboardTheme", ref themeIdx, themeNames, themeNames.Length))
        {
            plugin.Configuration.SelectedTheme = (GaiaTheme)themeIdx;
            plugin.Configuration.Save();
        }

        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        // --- GENERAL SETTINGS ---
        ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "General Settings");
        ImGuiHelpers.ScaledDummy(5.0f);

        bool autoGps = currentProfile.AutoSelectPlotByGps;
        if (ImGui.Checkbox("Auto-Detect Active Plot by GPS", ref autoGps))
        {
            currentProfile.AutoSelectPlotByGps = autoGps;
            plugin.Configuration.Save();
        }
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Automatically switches your UI to the plot you are physically standing near.");

        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        // ==========================================
        // --- GARDEN GPS LINKS ---
        // ==========================================
        DrawGpsLinkSection(currentProfile);

        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        // ==========================================
        // --- TIMING & DELAYS ---
        // ==========================================
        ImGui.TextColored(new Vector4(0.2f, 0.7f, 0.8f, 1f), "Automation Timings (ms)");
        ImGui.TextWrapped("Fine-tune how fast the robot interacts with menus. Increase these if you experience skips or lag.");
        ImGuiHelpers.ScaledDummy(5.0f);

        var config = plugin.Configuration;
        bool changed = false;

        if (ImGui.CollapsingHeader("Menu & Interaction Delays", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= ImGui.SliderInt("Water Menu Delay", ref config.WaterMenuDelay, 100, 2000, "%d ms");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delay between opening the garden menu and clicking 'Water'.");

            changed |= ImGui.SliderInt("Menu Selection Delay", ref config.MenuSelectionDelay, 100, 2000, "%d ms");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Delay between choosing an option and the next action.");

            changed |= ImGui.SliderInt("Interaction Timeout", ref config.InteractionTimeout, 1000, 10000, "%d ms");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("How long to wait for a menu to appear before giving up.");
        }

        if (ImGui.CollapsingHeader("Animation & Flow Delays", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= ImGui.SliderInt("Harvest Animation", ref config.HarvestAnimDelay, 500, 5000, "%d ms");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Wait time for the harvest animation to complete.");

            changed |= ImGui.SliderInt("Between Plants", ref config.BetweenPlantsDelay, 100, 3000, "%d ms");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Pause time before moving from one plant to the next.");

            changed |= ImGui.SliderInt("General Delay", ref config.GeneralDelay, 50, 1000, "%d ms");
            changed |= ImGui.SliderInt("Short Tick", ref config.ShortTickDelay, 10, 500, "%d ms");
        }

        if (changed)
        {
            config.Save();
        }

        ImGuiHelpers.ScaledDummy(15.0f);

        // ==========================================
        // --- DANGER ZONE ---
        // ==========================================
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        // The Header & Toggle
        ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "Danger Zone");
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 250f); // Push checkbox to the right
        ImGui.Checkbox("Unlock Destructive Actions", ref _unlockDangerZone);

        ImGui.TextWrapped("Destructive actions that cannot be undone.");

        // The Hidden Content
        if (_unlockDangerZone)
        {
            ImGuiHelpers.ScaledDummy(15.0f);

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.9f, 0.1f, 0.1f, 1f));

            // 1. NUKE GARDEN
            if (ImGui.Button("Nuke Garden (Unsafe Uproot)", new Vector2(250, 30)))
            {
                nukeTargetPlot = 0;
                ImGui.OpenPopup("Confirm Nuke Garden");
            }
            ImGuiHelpers.ScaledDummy(5.0f);

            // 2. UNLINK GPS
            if (ImGui.Button("Unlink All GPS Data", new Vector2(250, 30)))
            {
                ImGui.OpenPopup("Confirm Clear GPS");
            }
            ImGuiHelpers.ScaledDummy(5.0f);

            // 3. FACTORY RESET
            if (ImGui.Button("Factory Reset Current Character", new Vector2(250, 30)))
            {
                ImGui.OpenPopup("Confirm Factory Reset");
            }

            ImGui.PopStyleColor(3);
        }
        else
        {
            // Give some blank space so the window doesn't look empty when the buttons are hidden
            ImGuiHelpers.ScaledDummy(120.0f);
        }


        // ==========================================
        // --- MODAL POPUPS ---
        // ==========================================
        DrawNukeModal(currentProfile);
        DrawGpsModal(currentProfile);
        DrawResetModal();
    }

    // ==========================================
    // --- GPS LINK SECTION (per-plot / per-pot) ---
    // ==========================================
    // Outdoor plots: "Link Nearest" walks ObjectTable for the closest garden
    // patch (same pattern as the Setup Wizard) and writes Gps + PatchId + BedCount.
    // Indoor pots: "Link Target" reads Plugin.TargetManager.Target and writes
    // GpsX/Y/Z + DataId. Once a pot has GPS+DataId, FarmingManager's
    // FindBedsForPlanting indoor branch can locate it in the world and the
    // existing [TARGETED] glow indicator in PlantingTab lights up.
    //
    // Layout: two-column table (Personal | Free Company), each column has
    // Outdoor + Indoor subsections. Per-row state colored green/red; per-row
    // buttons conditional (Re-link+Clear when linked, Link otherwise) and
    // colored (green = create/refresh link, red = destroy). A linked row whose
    // current target matches gets a subtle accent background tint — uses the
    // same DataId+position match logic as PlantingTab.cs:1085 [TARGETED].
    private static readonly Vector4 ColLinked = new(0.30f, 0.85f, 0.35f, 1f);
    private static readonly Vector4 ColUnlinked = new(1.00f, 0.35f, 0.35f, 1f);
    private static readonly Vector4 ColBtnGreenBg = new(0.18f, 0.45f, 0.20f, 0.85f);
    private static readonly Vector4 ColBtnGreenHv = new(0.22f, 0.60f, 0.25f, 1.00f);
    private static readonly Vector4 ColBtnRedBg = new(0.55f, 0.18f, 0.18f, 0.85f);
    private static readonly Vector4 ColBtnRedHv = new(0.75f, 0.22f, 0.22f, 1.00f);
    // Blue = "modify existing binding" (Re-link). Differentiates from green
    // create/refresh and red destroy actions.
    private static readonly Vector4 ColBtnBlueBg  = new(0.18f, 0.34f, 0.58f, 0.85f);
    private static readonly Vector4 ColBtnBlueHv  = new(0.26f, 0.48f, 0.78f, 1.00f);
    private static readonly Vector4 ColTargetTint = new(0.95f, 0.78f, 0.20f, 0.18f);

    private void DrawGpsLinkSection(CharacterProfile profile)
    {
        ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Garden GPS Links");
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f),
            "Re-link individual plots or pots without nuking everything. "
            + "For pots, target it in-game first. Linked rows with matching target are highlighted.");
        ImGuiHelpers.ScaledDummy(5.0f);

        var target = Plugin.TargetManager.Target;

        if (ImGui.BeginTable("##GpsLinksColumns", 2,
                ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableSetupColumn("Personal");
            ImGui.TableSetupColumn("Free Company");
            ImGui.TableNextRow();

            // ─── PERSONAL COLUMN ───
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.2f, 0.7f, 0.8f, 1f), "Personal");
            ImGuiHelpers.ScaledDummy(2.0f);

            DrawOutdoorPlotsBlock(profile.PersonalPlots, profile.PersonalEstateSize, "Personal Plot", profile, target);
            DrawIndoorPotsBlock(profile, isPersonal: true, target);

            // ─── FC COLUMN ───
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(0.2f, 0.7f, 0.8f, 1f), "Free Company");
            ImGuiHelpers.ScaledDummy(2.0f);

            DrawOutdoorPlotsBlock(profile.FCPlots, profile.FCEstateSize, "FC Plot", profile, target);
            DrawIndoorPotsBlock(profile, isPersonal: false, target);

            ImGui.EndTable();
        }
    }

    private void DrawOutdoorPlotsBlock(
        GardenPlotState[] plots, int count, string labelPrefix,
        CharacterProfile profile, Dalamud.Game.ClientState.Objects.Types.IGameObject? target)
    {
        if (count <= 0) return;
        ImGui.TextColored(new Vector4(0.55f, 0.55f, 0.65f, 1f), "Outdoor");
        for (int i = 0; i < count && i < plots.Length; i++)
            DrawPlotLinkRow($"{labelPrefix} {i + 1}", plots[i], profile, target);
        ImGuiHelpers.ScaledDummy(6.0f);
    }

    private void DrawIndoorPotsBlock(
        CharacterProfile profile, bool isPersonal,
        Dalamud.Game.ClientState.Objects.Types.IGameObject? target)
    {
        int houseCount = isPersonal ? profile.PersonalPlanterCount : profile.FCPlanterCount;
        int aptCount = isPersonal ? profile.PersonalApartmentPlanterCount : profile.FCApartmentPlanterCount;
        if (houseCount <= 0 && aptCount <= 0) return;

        ImGui.TextColored(new Vector4(0.55f, 0.55f, 0.65f, 1f), "Indoor");
        if (isPersonal)
        {
            DrawPotLinkRows("Personal House Pot", profile.PersonalPlanters, houseCount, target);
            DrawPotLinkRows("Personal Apartment Pot", profile.PersonalApartmentPlanters, aptCount, target);
        }
        else
        {
            DrawPotLinkRows("FC House Pot", profile.FCPlanters, houseCount, target);
            DrawPotLinkRows("FC Apartment Pot", profile.FCApartmentPlanters, aptCount, target);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Single row for an outdoor plot. Linked → Re-link + Clear.
    // Unlinked → Link Nearest. Tinted if current target IS this plot.
    // ─────────────────────────────────────────────────────────────
    private void DrawPlotLinkRow(
        string label, GardenPlotState plot, CharacterProfile profile,
        Dalamud.Game.ClientState.Objects.Types.IGameObject? target)
    {
        bool isCurrentTarget = plot.HasGps && target != null
            && target.BaseId == plot.PatchId
            && Vector3.Distance(target.Position, plot.GetGpsVector()) < 0.5f;
        DrawRowTint(isCurrentTarget);

        ImGui.TextColored(plot.HasGps ? ColLinked : ColUnlinked, plot.HasGps ? "[LINKED]" : "[unlinked]");
        ImGui.SameLine();
        ImGui.TextUnformatted(label);

        bool clickedLink = false;
        if (plot.HasGps)
        {
            if (BlueButton($"Re-link##plot_{label}", new Vector2(78f, 22f))) clickedLink = true;
            ImGui.SameLine();
            if (RedButton($"Clear##plot_{label}", new Vector2(58f, 22f)))
            {
                plot.GpsX = 0; plot.GpsY = 0; plot.GpsZ = 0; plot.TopExpectedYield = "";
                for (int i = 0; i < 8; i++) plot.Beds[i] = new GardenBedState();
                plugin.Configuration.Save();
            }
        }
        else
        {
            if (GreenButton($"Link Nearest##plot_{label}", new Vector2(140f, 22f))) clickedLink = true;
        }

        if (clickedLink) DoLinkNearestPlot(label, plot, profile);
    }

    private void DoLinkNearestPlot(string label, GardenPlotState plot, CharacterProfile profile)
    {
        if (Plugin.ObjectTable.LocalPlayer == null) return;
        var nearestPatch = Plugin.ObjectTable
            .Where(obj => obj.ObjectKind == ObjectKind.EventObj && ValidPatches.Contains(obj.BaseId))
            .OrderBy(p => Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, p.Position))
            .FirstOrDefault();

        if (nearestPatch == null)
        {
            Plugin.ChatGui.PrintError("[Gaia] No garden patch found nearby! Stand closer to one.");
            return;
        }

        // Duplicate guard (matches WelcomeTab's pattern)
        foreach (var p in profile.PersonalPlots)
            if (p != plot && p.HasGps && Vector3.Distance(p.GetGpsVector(), nearestPatch.Position) < 0.5f)
            { Plugin.ChatGui.PrintError("[Gaia] That patch is already linked to another plot."); return; }
        foreach (var p in profile.FCPlots)
            if (p != plot && p.HasGps && Vector3.Distance(p.GetGpsVector(), nearestPatch.Position) < 0.5f)
            { Plugin.ChatGui.PrintError("[Gaia] That patch is already linked to another plot."); return; }

        int bedCount = nearestPatch.BaseId == 2003755 ? 4
                     : nearestPatch.BaseId == 2003756 ? 6 : 8;
        plot.GpsX = nearestPatch.Position.X;
        plot.GpsY = nearestPatch.Position.Y;
        plot.GpsZ = nearestPatch.Position.Z;
        plot.PatchId = nearestPatch.BaseId;
        plot.BedCount = bedCount;
        plugin.Configuration.Save();
        Plugin.ChatGui.Print($"[Gaia] Linked {label} to nearest garden patch.");
    }

    // ─────────────────────────────────────────────────────────────
    // Rows for one indoor planter array (House / Apartment).
    // Linked → Re-link + Clear. Unlinked → Link Target (disabled
    // unless target is a housing pot). Tinted if current target IS
    // this pot.
    // ─────────────────────────────────────────────────────────────
    private void DrawPotLinkRows(
        string labelPrefix, GardenBedState[] pots, int count,
        Dalamud.Game.ClientState.Objects.Types.IGameObject? target)
    {
        if (count <= 0) return;
        bool targetIsPot = target != null
            && (target.ObjectKind == ObjectKind.HousingEventObject
             || target.ObjectKind == ObjectKind.EventObj);

        for (int i = 0; i < count && i < pots.Length; i++)
        {
            var pot = pots[i];
            string label = $"{labelPrefix} {i + 1}";

            bool isCurrentTarget = pot.HasGps && target != null
                && target.BaseId == pot.DataId
                && Vector3.Distance(target.Position, pot.GetGpsVector()) < 0.5f;
            DrawRowTint(isCurrentTarget);

            ImGui.TextColored(pot.HasGps ? ColLinked : ColUnlinked, pot.HasGps ? "[LINKED]" : "[unlinked]");
            ImGui.SameLine();
            ImGui.TextUnformatted(label);

            if (pot.HasGps)
            {
                ImGui.BeginDisabled(!targetIsPot);
                if (BlueButton($"Re-link##pot_{label}", new Vector2(78f, 22f)))
                {
                    pot.GpsX = target!.Position.X;
                    pot.GpsY = target.Position.Y;
                    pot.GpsZ = target.Position.Z;
                    pot.DataId = target.BaseId;
                    plugin.Configuration.Save();
                    Plugin.ChatGui.Print($"[Gaia] Re-linked {label} to your current target.");
                }
                ImGui.EndDisabled();
                if (!targetIsPot && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Target a housing pot in-game first (click on it).");

                ImGui.SameLine();
                if (RedButton($"Clear##pot_{label}", new Vector2(58f, 22f)))
                {
                    pot.GpsX = 0; pot.GpsY = 0; pot.GpsZ = 0; pot.DataId = 0;
                    plugin.Configuration.Save();
                }
            }
            else
            {
                ImGui.BeginDisabled(!targetIsPot);
                if (GreenButton($"Link Target##pot_{label}", new Vector2(140f, 22f)))
                {
                    pot.GpsX = target!.Position.X;
                    pot.GpsY = target.Position.Y;
                    pot.GpsZ = target.Position.Z;
                    pot.DataId = target.BaseId;
                    plugin.Configuration.Save();
                    Plugin.ChatGui.Print($"[Gaia] Linked {label} to your current target.");
                }
                ImGui.EndDisabled();
                if (!targetIsPot && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    ImGui.SetTooltip("Target a housing pot in-game first (click on it).");
            }
        }
    }

    // Paint an accent rectangle covering the row about to be drawn. Reads the
    // current cursor + line height + column width so it sits exactly behind
    // the next single-line row of widgets without needing per-call sizing.
    private static void DrawRowTint(bool active)
    {
        if (!active) return;
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        float h = ImGui.GetFrameHeight() + 2f;
        dl.AddRectFilled(pos - new Vector2(2f, 1f), pos + new Vector2(w, h), ColorUtil.ToU32(ColTargetTint), 3f);
    }

    private static bool GreenButton(string id, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, ColBtnGreenBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColBtnGreenHv);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColBtnGreenHv);
        bool clicked = ImGui.Button(id, size);
        ImGui.PopStyleColor(3);
        return clicked;
    }

    private static bool RedButton(string id, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, ColBtnRedBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColBtnRedHv);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColBtnRedHv);
        bool clicked = ImGui.Button(id, size);
        ImGui.PopStyleColor(3);
        return clicked;
    }

    private static bool BlueButton(string id, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, ColBtnBlueBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColBtnBlueHv);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColBtnBlueHv);
        bool clicked = ImGui.Button(id, size);
        ImGui.PopStyleColor(3);
        return clicked;
    }

    private void DrawNukeModal(CharacterProfile currentProfile)
    {
        // Center the popup on the screen
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("Confirm Nuke Garden", ref UnusedBool(), ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "WARNING: This will permanently uproot all plants!");
            ImGui.Text("Select which plot you want the robot to destroy:");
            ImGuiHelpers.ScaledDummy(10.0f);

            // Build the combo box exactly like TendingTab
            string[] plotNames = new string[currentProfile.PersonalEstateSize + currentProfile.FCEstateSize];
            int index = 0;
            for (int i = 0; i < currentProfile.PersonalEstateSize; i++) plotNames[index++] = $"Personal Plot {i + 1}";
            for (int i = 0; i < currentProfile.FCEstateSize; i++) plotNames[index++] = $"FC Plot {i + 1}";

            ImGui.SetNextItemWidth(250f);
            ImGui.Combo("##NukeTarget", ref nukeTargetPlot, plotNames, plotNames.Length);

            ImGuiHelpers.ScaledDummy(15.0f);

            if (ImGui.Button("Yes, Nuke it!", new Vector2(120, 30)))
            {
                bool isPersonal = nukeTargetPlot < currentProfile.PersonalEstateSize;
                plugin.Farming.IsPersonalPlot = isPersonal;
                plugin.Farming.CurrentPlotIndex = isPersonal ? nukeTargetPlot : nukeTargetPlot - currentProfile.PersonalEstateSize;

                plugin.Farming.UprootAllBeds(); // Start the robot!
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(120, 30)))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void DrawGpsModal(CharacterProfile currentProfile)
    {
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("Confirm Clear GPS", ref UnusedBool(), ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            ImGui.Text("Are you sure you want to clear all GPS plot links?");
            ImGui.Text("You will need to manually link them again in the setup wizard.");
            ImGuiHelpers.ScaledDummy(15.0f);

            if (ImGui.Button("Clear GPS", new Vector2(120, 30)))
            {
                // WIPE PERSONAL PLOTS CLEAN
                foreach (var plot in currentProfile.PersonalPlots)
                {
                    plot.GpsX = 0; plot.GpsY = 0; plot.GpsZ = 0; plot.TopExpectedYield = "";
                    for (int i = 0; i < 8; i++) plot.Beds[i] = new GardenBedState(); // Fresh soil!
                }
                // WIPE FC PLOTS CLEAN
                foreach (var plot in currentProfile.FCPlots)
                {
                    plot.GpsX = 0; plot.GpsY = 0; plot.GpsZ = 0; plot.TopExpectedYield = "";
                    for (int i = 0; i < 8; i++) plot.Beds[i] = new GardenBedState(); // Fresh soil!
                }

                currentProfile.HasCompletedOnboarding = false;
                plugin.Configuration.Save();
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel", new Vector2(120, 30))) ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }
    }

    private void DrawResetModal()
    {
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        if (ImGui.BeginPopupModal("Confirm Factory Reset", ref UnusedBool(), ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove))
        {
            ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "CRITICAL WARNING!");
            ImGui.Text("This will permanently delete all timer data, setups, and plots");
            ImGui.Text("for this specific character. It cannot be undone.");
            ImGuiHelpers.ScaledDummy(15.0f);

            if (ImGui.Button("Wipe Data", new Vector2(120, 30)))
            {
                if (Plugin.ObjectTable.LocalPlayer != null)
                {
                    ulong currentId = Plugin.PlayerState.ContentId;
                    plugin.Configuration.Characters[currentId] = new CharacterProfile();
                    plugin.Configuration.Save();
                }
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(120, 30))) ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }
    }

    // Helper to bypass the 'ref bool' requirement for Modals that don't need a close "X" in the top right
    private bool dummyBool = true;
    private ref bool UnusedBool() { dummyBool = true; return ref dummyBool; }
}