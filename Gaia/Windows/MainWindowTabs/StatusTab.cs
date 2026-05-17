using Gaia.Helpers;
using Gaia.Manager;

namespace Gaia.Windows.MainWindowTabs;

public class StatusTab : IDrawablePage
{
    private readonly Plugin plugin;
    public string TabLabel => "  Status  ";

    private readonly string[] estateSizes = { "None (0 Plots)", "Small (1 Plot)", "Medium (2 Plots)", "Large (3 Plots)" };


    public StatusTab(Plugin plugin)
    {
        this.plugin = plugin;
    }


    private string[] GetPotsArray(int maxPots)
    {
        string[] arr = new string[maxPots + 1];
        for (int i = 0; i <= maxPots; i++) arr[i] = $"{i} Pot{(i == 1 ? "" : "s")}";
        return arr;
    }
    private float GetCropGrowTime(string seedName)
    {
        if (string.IsNullOrEmpty(seedName)) return 120f;
        string lower = seedName.ToLower();

        if (lower.Contains("thavnairian onion")) return 240f; // 10 Days

        if (lower.Contains("glazenut") || lower.Contains("blood currant") || lower.Contains("nymeia lily") ||
            lower.Contains("pearl roselle") || lower.Contains("royal kukuru") || lower.Contains("broombush") ||
            lower.Contains("jute") || lower.Contains("chives")) return 168f; // 7 Days

        if (lower.Contains("krakka")) return 72f; // 3 Days

        return 120f; // 5 Days (Default for standard crosses)
    }

    public void Draw()
    {
        var currentProfile = plugin.GetCurrentCharacterProfile();

        // If we aren't fully logged in yet, just don't draw the UI
        if (currentProfile == null)
        {
            ImGui.Text("Waiting for character to load...");
            return;
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        // --- FETCH CURRENT CONFIG ---
        int pSize = currentProfile.PersonalEstateSize;
        int fSize = currentProfile.FCEstateSize;
        int pPots = currentProfile.PersonalPlanterCount;
        int fPots = currentProfile.FCPlanterCount;
        int pApt = currentProfile.PersonalApartmentPlanterCount;
        int fApt = currentProfile.FCApartmentPlanterCount;

        // --- CALCULATE MAX LIMITS BASED ON HOUSE SIZE ---
        int maxPPots = pSize == 0 ? 0 : pSize == 1 ? 2 : pSize == 2 ? 3 : 4;
        int maxFPots = fSize == 0 ? 0 : fSize == 1 ? 2 : fSize == 2 ? 3 : 4;

        if (pPots > maxPPots) { pPots = maxPPots; currentProfile.PersonalPlanterCount = pPots; plugin.Configuration.Save(); }
        if (fPots > maxFPots) { fPots = maxFPots; currentProfile.FCPlanterCount = fPots; plugin.Configuration.Save(); }

        var pPotsOptions = GetPotsArray(maxPPots);
        var fPotsOptions = GetPotsArray(maxFPots);
        var aptOptions = GetPotsArray(2);

        // ==========================================
        // --- THE FRAMED CONFIGURATION CONTAINER ---
        // ==========================================
        if (ImGui.BeginTable("ConfigContainerTable", 1, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            // Row 1: The Centered Header
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiHelpers.ScaledDummy(5.0f);

            string titleText = "Global Garden Dashboard";
            string subText = "- Tracks water timers and expected yields across all your estates.";

            float titleWidth = ImGui.CalcTextSize(titleText).X;
            float subWidth = ImGui.CalcTextSize(subText).X;
            float totalHeaderWidth = titleWidth + ImGui.GetStyle().ItemSpacing.X + subWidth;

            float headerOffsetX = Math.Max(0, (ImGui.GetContentRegionAvail().X - totalHeaderWidth) / 2.0f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + headerOffsetX);

            ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), titleText);
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), subText);

            // --- NEW: Sync Button aligned to the far right of the header row ---
            bool isBusy = plugin.Farming.CurrentState != FarmingState.Idle;
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 155f);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 3f); // Nudge it up slightly so it aligns with the text!
            ImGui.BeginDisabled(isBusy);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.7f, 1f));
            if (ImGui.Button("🔍 Sync / Scan Active", new Vector2(150, 25)))
            {
                plugin.Farming.IsIndoors = plugin.LocationManager.CurrentLocation != LocationContext.Outdoors;

                // Call our new reset method to lock the GPS and clear the state machine!
                plugin.Farming.PrepareForScan();

                plugin.Farming.ScanGarden();
            }
            ImGui.PopStyleColor();
            ImGui.EndDisabled();
            // -------------------------------------------------------------------

            ImGuiHelpers.ScaledDummy(2.0f);

            // Row 2: The Centered Dropdown Grid
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGuiHelpers.ScaledDummy(5.0f);

            // Math to perfectly center the internal grid (75 + 160 + 160 + 160 + padding buffer)
            float configTableWidth = 75f + 160f + 160f + 160f + (ImGui.GetStyle().CellPadding.X * 8);
            float gridOffsetX = Math.Max(0, (ImGui.GetContentRegionAvail().X - configTableWidth) / 2.0f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + gridOffsetX);

            if (ImGui.BeginTable("ConfigGridTable", 4, ImGuiTableFlags.None))
            {
                // Assigning strict fixed widths allows the center math to be flawless
                ImGui.TableSetupColumn("Labels", ImGuiTableColumnFlags.WidthFixed, 75f);
                ImGui.TableSetupColumn("Outdoor", ImGuiTableColumnFlags.WidthFixed, 160f);
                ImGui.TableSetupColumn("Indoor", ImGuiTableColumnFlags.WidthFixed, 160f);
                ImGui.TableSetupColumn("Apartment", ImGuiTableColumnFlags.WidthFixed, 160f);

                // Header Row
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Outdoor Plots");
                ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Indoor Pots");
                ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Apartment Pots");

                int pPlotsLinked = CountLinkedPlots(currentProfile.PersonalPlots, pSize);
                int fPlotsLinked = CountLinkedPlots(currentProfile.FCPlots, fSize);
                int pPotsLinked  = CountLinkedBeds(currentProfile.PersonalPlanters, pPots);
                int fPotsLinked  = CountLinkedBeds(currentProfile.FCPlanters, fPots);
                int pAptLinked   = CountLinkedBeds(currentProfile.PersonalApartmentPlanters, pApt);
                int fAptLinked   = CountLinkedBeds(currentProfile.FCApartmentPlanters, fApt);

                // Personal Row
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Personal");

                ImGui.TableNextColumn();
                if (DrawSizeComboWithLinks("##pSize", ref pSize, estateSizes, pPlotsLinked, pSize))
                    { currentProfile.PersonalEstateSize = pSize; plugin.Configuration.Save(); }

                ImGui.TableNextColumn();
                ImGui.BeginDisabled(maxPPots == 0);
                if (DrawCountComboWithLinks("##pPots", ref pPots, pPotsOptions, pPotsLinked, pPots))
                    { currentProfile.PersonalPlanterCount = pPots; plugin.Configuration.Save(); }
                ImGui.EndDisabled();

                ImGui.TableNextColumn();
                if (DrawCountComboWithLinks("##pApt", ref pApt, aptOptions, pAptLinked, pApt))
                    { currentProfile.PersonalApartmentPlanterCount = pApt; plugin.Configuration.Save(); }

                // FC Row
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Free Co.");

                ImGui.TableNextColumn();
                if (DrawSizeComboWithLinks("##fSize", ref fSize, estateSizes, fPlotsLinked, fSize))
                    { currentProfile.FCEstateSize = fSize; plugin.Configuration.Save(); }

                ImGui.TableNextColumn();
                ImGui.BeginDisabled(maxFPots == 0);
                if (DrawCountComboWithLinks("##fPots", ref fPots, fPotsOptions, fPotsLinked, fPots))
                    { currentProfile.FCPlanterCount = fPots; plugin.Configuration.Save(); }
                ImGui.EndDisabled();

                ImGui.TableNextColumn();
                if (DrawCountComboWithLinks("##fApt", ref fApt, aptOptions, fAptLinked, fApt))
                    { currentProfile.FCApartmentPlanterCount = fApt; plugin.Configuration.Save(); }

                ImGui.EndTable();
            }

            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        int maxRows = Math.Max(pSize, fSize);

        if (maxRows == 0 && pPots == 0 && fPots == 0 && pApt == 0 && fApt == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Please configure your estate sizes above to view the dashboard.");
            return;
        }

        // --- DASHBOARD TABLE ---
        ImGuiTableFlags tableFlags = ImGuiTableFlags.BordersInnerV |
                                     ImGuiTableFlags.BordersOuter |
                                     ImGuiTableFlags.BordersInnerH |
                                     ImGuiTableFlags.ScrollY;

        if (ImGui.BeginTable("GlobalStatsTable", 2, tableFlags, new Vector2(0f, -20f)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);

            ImGui.TableSetupColumn("Personal / Apartment", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Free Company", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            // 1. Draw Outdoor Plots
            for (int plotIndex = 0; plotIndex < maxRows; plotIndex++)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (plotIndex < pSize) DrawPlotWidget($"Personal Plot {plotIndex + 1}", currentProfile.PersonalPlots[plotIndex], plotIndex, true);
                else DrawEmptyPlotSpace();

                ImGui.TableNextColumn();
                if (plotIndex < fSize) DrawPlotWidget($"FC Plot {plotIndex + 1}", currentProfile.FCPlots[plotIndex], plotIndex, false);
                else DrawEmptyPlotSpace();
            }

            // 2. Draw Indoor Planters!
            if (pPots > 0 || fPots > 0 || pApt > 0 || fApt > 0)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                if (pPots > 0) DrawPlanterRow("Personal House Pots", currentProfile.PersonalPlanters, pPots);
                if (pApt > 0) DrawPlanterRow("Personal Apartment Pots", currentProfile.PersonalApartmentPlanters, pApt);
                if (pPots == 0 && pApt == 0) DrawEmptyPlotSpace();

                ImGui.TableNextColumn();
                if (fPots > 0) DrawPlanterRow("Free Company Pots", currentProfile.FCPlanters, fPots);
                if (fApt > 0) DrawPlanterRow("FC Apartment / Chamber Pots", currentProfile.FCApartmentPlanters, fApt);
                if (fPots == 0 && fApt == 0) DrawEmptyPlotSpace();
            }

            ImGui.EndTable();
        }
    }

    // ───────────────────────────────────────────────────────────────
    // Combo wrapper that displays "<size> (X/Y linked)" as the closed
    // label while the popup still shows raw size options for selection.
    // For pSize=0 (no plots), the closed label collapses to "—".
    // ───────────────────────────────────────────────────────────────
    private static bool DrawSizeComboWithLinks(string id, ref int selected, string[] options, int linked, int activeCount)
    {
        string preview = activeCount == 0
            ? "—"
            : $"{StripSuffixInParens(options[selected])} ({linked}/{activeCount} linked)";

        ImGui.SetNextItemWidth(-1);
        bool changed = false;
        if (ImGui.BeginCombo(id, preview))
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (ImGui.Selectable(options[i], i == selected))
                {
                    selected = i;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    // For pot-count dropdowns: closed label is "X/Y linked" or "—".
    private static bool DrawCountComboWithLinks(string id, ref int selected, string[] options, int linked, int activeCount)
    {
        string preview = activeCount == 0
            ? "—"
            : $"{linked}/{activeCount} linked";

        ImGui.SetNextItemWidth(-1);
        bool changed = false;
        if (ImGui.BeginCombo(id, preview))
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (ImGui.Selectable(options[i], i == selected))
                {
                    selected = i;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }
        return changed;
    }

    // "Medium (2 Plots)" → "Medium". Leaves strings without parens alone.
    private static string StripSuffixInParens(string s)
    {
        int paren = s.IndexOf(" (");
        return paren > 0 ? s.Substring(0, paren) : s;
    }

    private static int CountLinkedPlots(GardenPlotState[] plots, int count)
    {
        int n = 0;
        for (int i = 0; i < count && i < plots.Length; i++)
            if (plots[i] != null && plots[i].HasGps) n++;
        return n;
    }

    private static int CountLinkedBeds(GardenBedState[] beds, int count)
    {
        int n = 0;
        for (int i = 0; i < count && i < beds.Length; i++)
            if (beds[i] != null && beds[i].HasGps) n++;
        return n;
    }

    private void DrawEmptyPlotSpace()
    {
        ImGuiHelpers.ScaledDummy(40.0f);
        var text = "--- No Plot ---";
        var textWidth = ImGui.CalcTextSize(text).X;
        var columnWidth = ImGui.GetColumnWidth();

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (columnWidth - textWidth) / 2.0f);
        ImGui.TextColored(new Vector4(0.3f, 0.3f, 0.3f, 1f), text);
        ImGuiHelpers.ScaledDummy(40.0f);
    }

    private void DrawPlanterRow(string title, GardenBedState[] planters, int count)
    {
        ImGuiHelpers.ScaledDummy(10.0f);

        float titleWidth = ImGui.CalcTextSize(title).X;
        float columnWidth = ImGui.GetColumnWidth();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (columnWidth - titleWidth) / 2.0f);

        ImGui.TextColored(new Vector4(0.3f, 0.7f, 0.9f, 1f), title);
        ImGuiHelpers.ScaledDummy(5.0f);

        float gridWidth = (60 * count) + (ImGui.GetStyle().CellPadding.X * (count * 2));
        float offsetX = Math.Max(0, (columnWidth - gridWidth) / 2.0f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

        if (ImGui.BeginTable($"PlanterTable_{title}", count, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableNextRow();
            for (int i = 0; i < count; i++)
            {
                ImGui.TableNextColumn();
                DrawBedVisual(planters[i], i);
            }
            ImGui.EndTable();
        }
        ImGuiHelpers.ScaledDummy(10.0f);
    }

    private void DrawPlotWidget(string title, GardenPlotState plot, int index, bool isPersonal)
    {
        ImGuiHelpers.ScaledDummy(10.0f);
        float titleWidth = ImGui.CalcTextSize(title).X;
        float columnWidth = ImGui.GetColumnWidth();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (columnWidth - titleWidth) / 2.0f);
        // Title colors amber when the player is targeting this plot's garden patch
        // in-world; otherwise the usual purple. Mirrors the Settings GPS row tint.
        bool plotIsTargeted = TargetHighlight.MatchesPlot(Plugin.TargetManager.Target, plot);
        ImGui.TextColored(plotIsTargeted
            ? new Vector4(0.95f, 0.78f, 0.20f, 1f)
            : new Vector4(0.6f, 0.3f, 0.9f, 1f), title);
        ImGuiHelpers.ScaledDummy(5.0f);

        // --- NEW: SMART GRID MATH ---
        int cols = plot.BedCount == 4 ? 2 : 3;
        float gridWidth = (60 * cols) + (ImGui.GetStyle().CellPadding.X * (cols * 2)); // Stats tab uses 60px slots
        float offsetX = Math.Max(0, (columnWidth - gridWidth) / 2.0f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

        int[] mapping8 = { 0, 1, 2, 7, -1, 3, 6, 5, 4 };
        int[] mapping6 = { 0, 1, 2, 5, -1, 3, -2, 4, -2 };
        int[] mapping4 = { 0, 1, -1, 3, 2, -2 };

        int[] currentMapping = plot.BedCount == 4 ? mapping4 : plot.BedCount == 6 ? mapping6 : mapping8;

        if (ImGui.BeginTable($"PlotGrid_{isPersonal}_{index}", cols, ImGuiTableFlags.SizingFixedFit))
        {
            for (int i = 0; i < currentMapping.Length; i++)
            {
                if (i % cols == 0 && i != 0) ImGui.TableNextRow();

                int slotIndex = currentMapping[i];
                if (slotIndex == -2)
                {
                    ImGui.TableNextColumn();
                    ImGui.Dummy(new Vector2(60, 60)); // Hidden dummy space for spacing
                    continue;
                }

                ImGui.TableNextColumn();
                if (slotIndex == -1) DrawCenterYieldIcon(plot.TopExpectedYield);
                else DrawBedVisual(plot.Beds[slotIndex], slotIndex);
            }
            ImGui.EndTable();
        }
        ImGuiHelpers.ScaledDummy(10.0f);
    }

    private void DrawCenterYieldIcon(string yieldName)
    {
        Vector2 startPos = ImGui.GetCursorScreenPos();
        Vector2 slotSize = new Vector2(60, 60);
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(startPos, startPos + slotSize, ImGui.GetColorU32(ImGuiCol.Button), 5.0f);

        if (!string.IsNullOrEmpty(yieldName))
        {
            uint iconId = GardenData.GetIconIdForName(yieldName);
            if (iconId != 0)
            {
                var iconTex = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                if (iconTex != null) drawList.AddImage(iconTex.Handle, startPos + new Vector2(10, 10), startPos + new Vector2(50, 50));
            }
            else
            {
                string shortName = yieldName.Replace(" Seeds", "").Replace("Thavnairian", "Thav.");
                drawList.AddText(new Vector2(startPos.X + 5, startPos.Y + 25), ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.2f, 1f)), shortName);
            }
        }

        ImGui.Dummy(slotSize);
        if (!string.IsNullOrEmpty(yieldName) && ImGui.IsItemHovered()) ImGui.SetTooltip($"Top Expected Yield: {yieldName}");
    }

    private void DrawBedVisual(GardenBedState bed, int slot)
    {
        Vector2 startPos = ImGui.GetCursorScreenPos();
        Vector2 slotSize = new Vector2(60, 60);
        var drawList = ImGui.GetWindowDrawList();

        // Amber ring when this bed is the in-world target (indoor pots only —
        // outdoor sub-beds have no per-bed GPS). AddRect outline so it doesn't
        // cover content; works for all branches below regardless of bed state.
        if (TargetHighlight.MatchesBed(Plugin.TargetManager.Target, bed))
            TargetHighlight.DrawRing(drawList, startPos, slotSize);

        if (bed.IsEmpty)
        {
            drawList.AddText(new Vector2(startPos.X + 15, startPos.Y + 20), ImGui.GetColorU32(new Vector4(0.5f, 0.5f, 0.5f, 1f)), "Soil");
            ImGui.Dummy(slotSize);
            return;
        }

        if (bed.IsMature)
        {
            drawList.AddText(new Vector2(startPos.X + 10, startPos.Y + 20), ImGui.GetColorU32(new Vector4(0.2f, 1f, 0.2f, 1f)), "READY!");
            ImGui.Dummy(slotSize);
            return;
        }

        // --- WATER PROGRESS ---
        TimeSpan elapsed = DateTime.Now - bed.LastWateredTime;
        float remainingHours = 24.0f - (float)elapsed.TotalHours;
        float waterPct = Math.Max(0.0f, Math.Min(1.0f, remainingHours / 24.0f));

        Vector4 waterBarColor = new Vector4(0.2f, 0.8f, 0.2f, 1f);
        if (waterPct < 0.5f) waterBarColor = new Vector4(0.8f, 0.8f, 0.2f, 1f);
        if (waterPct < 0.1f) waterBarColor = new Vector4(0.9f, 0.2f, 0.2f, 1f);

        // --- FERTILIZE PROGRESS ---
        float minutesSinceFertilized = (float)(DateTime.Now - bed.LastFertilizedTime).TotalMinutes;
        float fertPct = Math.Max(0.0f, Math.Min(1.0f, minutesSinceFertilized / 60.0f));

        drawList.AddText(new Vector2(startPos.X + 15, startPos.Y + 5), ImGui.GetColorU32(new Vector4(0.6f, 0.9f, 0.6f, 1f)), "Grow");

        // Water Bar
        Vector2 wBarStart = startPos + new Vector2(5, 30);
        Vector2 wBarEnd = startPos + new Vector2(55, 40);
        drawList.AddRectFilled(wBarStart, wBarEnd, ImGui.GetColorU32(ImGuiCol.FrameBg), 2.0f);
        if (waterPct > 0)
        {
            Vector2 wProgressEnd = startPos + new Vector2(5 + (50 * waterPct), 40);
            drawList.AddRectFilled(wBarStart, wProgressEnd, ImGui.GetColorU32(waterBarColor), 2.0f);
        }

        // Fertilize Bar
        Vector2 fBarStart = startPos + new Vector2(5, 45);
        Vector2 fBarEnd = startPos + new Vector2(55, 55);
        drawList.AddRectFilled(fBarStart, fBarEnd, ImGui.GetColorU32(ImGuiCol.FrameBg), 2.0f);
        if (fertPct > 0)
        {
            Vector2 fProgressEnd = startPos + new Vector2(5 + (50 * fertPct), 55);
            drawList.AddRectFilled(fBarStart, fProgressEnd, ImGui.GetColorU32(new Vector4(1.0f, 0.55f, 0.0f, 1.0f)), 2.0f);
        }

        ImGui.Dummy(slotSize);

        if (ImGui.IsItemHovered())
        {
            string tooltip = $"Seed: {bed.SeedName}\n\n";

            if (bed.PlantedTime > DateTime.MinValue)
            {
                float totalGrowHours = GetCropGrowTime(bed.SeedName);
                float elapsedGrowHours = (float)(DateTime.Now - bed.PlantedTime).TotalHours;

                int totalStages = 3;
                float hoursPerStage = totalGrowHours / totalStages;

                int currentStage = Math.Min(totalStages, (int)(elapsedGrowHours / hoursPerStage) + 1);
                float hoursUntilNextStage = (currentStage * hoursPerStage) - elapsedGrowHours;
                float hoursUntilHarvest = totalGrowHours - elapsedGrowHours;

                if (hoursUntilHarvest > 0)
                {
                    tooltip += $"Growth Stage: {currentStage} / {totalStages}\n";
                    if (currentStage < totalStages)
                        tooltip += $"Time until Stage {currentStage + 1}: {Math.Max(0, hoursUntilNextStage):F1}h\n";
                    tooltip += $"Time until Harvest: {Math.Max(0, hoursUntilHarvest):F1}h\n";
                }
                else
                {
                    tooltip += "Growth Stage: Ready for Harvest!\n";
                }
            }
            else
            {
                tooltip += "Growth Time: Unknown (Planted before tracking)\n";
            }

            tooltip += $"---\nWatering: {waterPct * 100:F0}% ({Math.Max(0, remainingHours):F1}h until dry)\n";
            tooltip += $"Fertilizer: {(fertPct >= 1.0f ? "Ready to apply!" : $"Available in {(int)(60 - minutesSinceFertilized)} minutes.")}";
            ImGui.SetTooltip(tooltip);
        }
    }
}