namespace Gaia.Windows.MainWindowTabs;

public class StatisticsTab : IDrawablePage
{
    private readonly Plugin _plugin;
    public string TabLabel => "  Stats  ";
    private int _selectedPlotIndex = 0; // Tracks which plot is currently selected in the dropdown

    public StatisticsTab(Plugin plugin)
    {
        _plugin = plugin;
    }

    public void Draw()
    {
        var currentProfile = _plugin.GetCurrentCharacterProfile();
        if (currentProfile == null) return;

        ImGuiHelpers.ScaledDummy(10.0f);

        // --- HEADER ---
        ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Lifetime Farming Analytics");
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1f), "Track your eternal suffering in the dirt.");
        ImGuiHelpers.ScaledDummy(15.0f);

        // --- THE GRIND (Global Table) ---
        ImGui.TextColored(new Vector4(0.3f, 0.7f, 0.9f, 1f), "The Grind");
        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.BeginTable("LifetimeActionsTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn("Plants Planted");
            ImGui.TableSetupColumn("Beds Watered");
            ImGui.TableSetupColumn("Beds Fertilized");
            ImGui.TableSetupColumn("Beds Harvested");
            ImGui.TableHeadersRow();

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), $"{_plugin.Configuration.TotalPlantsPlanted:N0}");
            ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), $"{_plugin.Configuration.TotalBedsWatered:N0}");
            ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), $"{_plugin.Configuration.TotalBedsFertilized:N0}");
            ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), $"{_plugin.Configuration.TotalBedsHarvested:N0}");

            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(25.0f);

        // ==========================================
        // --- NEW: PLOT ANALYTICS & GRAPHING ---
        // ==========================================
        ImGui.TextColored(new Vector4(0.9f, 0.5f, 0.2f, 1f), "Plot Analytics");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Select a plot to view its specific tending history and consistency.");
        ImGuiHelpers.ScaledDummy(5.0f);

        // Build the dynamic list of available plots
        List<string> plotNames = new();
        List<GardenPlotState> plotRefs = new();

        for (int i = 0; i < currentProfile.PersonalEstateSize; i++) { plotNames.Add($"Personal Plot {i + 1}"); plotRefs.Add(currentProfile.PersonalPlots[i]); }
        for (int i = 0; i < currentProfile.FCEstateSize; i++) { plotNames.Add($"FC Plot {i + 1}"); plotRefs.Add(currentProfile.FCPlots[i]); }

        if (plotNames.Count > 0)
        {
            if (_selectedPlotIndex >= plotNames.Count) _selectedPlotIndex = 0;

            ImGui.SetNextItemWidth(250f);
            ImGui.Combo("##AnalyticsPlotSelector", ref _selectedPlotIndex, plotNames.ToArray(), plotNames.Count);

            var activePlot = plotRefs[_selectedPlotIndex];

            ImGuiHelpers.ScaledDummy(10.0f);

            // The Graph!
            float[] graphData = activePlot.WateringIntervalHistory.ToArray();

            if (graphData.Length > 1)
            {
                ImGui.PushStyleColor(ImGuiCol.PlotLines, new Vector4(0.2f, 0.8f, 0.9f, 1f)); // A nice cyan line
                ImGui.PushStyleColor(ImGuiCol.PlotLinesHovered, new Vector4(1f, 0.8f, 0.2f, 1f)); // Gold when hovered

                // Draw the line graph: Data, Values Offset, Overlay Text, Min Y, Max Y, Size
                ImGui.PlotLines("##WateringGraph", graphData, 0, "Hours Between Watering", 0f, 24f, new Vector2(ImGui.GetContentRegionAvail().X, 80));

                ImGui.PopStyleColor(2);
            }
            else
            {
                // Placeholder box if they haven't watered enough to make a graph yet
                Vector2 boxSize = new Vector2(ImGui.GetContentRegionAvail().X, 80);
                ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetCursorScreenPos(), ImGui.GetCursorScreenPos() + boxSize, ImGui.GetColorU32(ImGuiCol.FrameBg));
                ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(10, 30));
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "Not enough data points to graph yet. Keep watering!");
                ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 40);
            }

            ImGuiHelpers.ScaledDummy(10.0f);

            // Mini-table for the individual plot stats
            if (ImGui.BeginTable("IndividualPlotStats", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Total Waters");
                ImGui.TableSetupColumn("Total Feeds");
                ImGui.TableSetupColumn("Total Harvests");
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.Text($"{activePlot.TotalWaterings}");
                ImGui.TableNextColumn(); ImGui.Text($"{activePlot.TotalFertilizings}");
                ImGui.TableNextColumn(); ImGui.Text($"{activePlot.TotalHarvests}");

                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No outdoor plots configured. Go to Settings/Status to add them.");
        }

        ImGuiHelpers.ScaledDummy(25.0f);

        // --- THE HOARD (Specific Yields Dictionary) ---
        ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1f), "The Hoard");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "Everything you've successfully ripped from the earth.");
        ImGuiHelpers.ScaledDummy(5.0f);

        var yields = _plugin.Configuration.LifetimeHarvestYields;

        if (yields == null || yields.Count == 0)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "No crops harvested yet. Get to farming!");
        }
        else
        {
            if (ImGui.BeginTable("HarvestYieldsTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(0, -10)))
            {
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Crop / Item Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Total Yield", ImGuiTableColumnFlags.WidthFixed, 100f);
                ImGui.TableHeadersRow();

                foreach (var kvp in yields.OrderByDescending(x => x.Value))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn(); ImGui.Text(kvp.Key);
                    ImGui.TableNextColumn(); ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), kvp.Value.ToString("N0"));
                }

                ImGui.EndTable();
            }
        }
    }
}