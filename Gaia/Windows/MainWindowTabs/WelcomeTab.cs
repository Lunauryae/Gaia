namespace Gaia.Windows.MainWindowTabs;

public class WelcomeTab : IDrawablePage
{
    private readonly Plugin plugin;
    public string TabLabel => "  Setup Wizard  ";
    public readonly uint[] ValidPatches = { 2003757, 2003756, 2003755 };
    private string gpsErrorContext = "";
    private string gpsErrorMessage = "";
    private long gpsErrorTimeout = 0;

    public WelcomeTab(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public void Draw()
    {
        var currentProfile = plugin.GetCurrentCharacterProfile();
        if (currentProfile == null) return;

        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Welcome to GAIA - Garden Management!");
        ImGui.TextWrapped("Before the robot can automate your farming, we need to teach it exactly where your garden plots are located.");
        ImGuiHelpers.ScaledDummy(10.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        // --- STEP 1: ESTATE SIZES ---
        ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1f), "Step 1: Configure Your Estates");
        ImGui.TextWrapped("Select your house size below. The robot will automatically calculate the maximum number of outdoor garden plots you can own!");
        ImGuiHelpers.ScaledDummy(10.0f);

        string[] houseSizes = { "None / Apartment Only", "Small (Cottage)", "Medium (House)", "Large (Mansion)" };
        string[] plotCounts = { "0 Plots", "1 Plot", "2 Plots", "3 Plots" };

        // --- PERSONAL ESTATE ---
        ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Personal Estate");

        int pSize = currentProfile.PersonalHouseSize;
        ImGui.SetNextItemWidth(250f);
        if (ImGui.Combo("House Size##Personal", ref pSize, houseSizes, houseSizes.Length))
        {
            currentProfile.PersonalHouseSize = pSize;
            // The Magic Auto-Fill!
            currentProfile.PersonalEstateSize = pSize;
            plugin.Configuration.Save();
        }

        int pPlots = currentProfile.PersonalEstateSize;
        ImGui.SetNextItemWidth(250f);
        if (ImGui.Combo("Placed Garden Plots##Personal", ref pPlots, plotCounts, plotCounts.Length))
        {
            currentProfile.PersonalEstateSize = pPlots;
            plugin.Configuration.Save();
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        // --- FREE COMPANY ESTATE ---
        ImGui.TextColored(new Vector4(0.6f, 0.3f, 0.9f, 1f), "Free Company Estate");

        int fcSize = currentProfile.FCHouseSize;
        ImGui.SetNextItemWidth(250f);
        if (ImGui.Combo("House Size##FC", ref fcSize, houseSizes, houseSizes.Length))
        {
            currentProfile.FCHouseSize = fcSize;
            // The Magic Auto-Fill!
            currentProfile.FCEstateSize = fcSize;
            plugin.Configuration.Save();
        }

        int fcPlots = currentProfile.FCEstateSize;
        ImGui.SetNextItemWidth(250f);
        if (ImGui.Combo("Placed Garden Plots##FC", ref fcPlots, plotCounts, plotCounts.Length))
        {
            currentProfile.FCEstateSize = fcPlots;
            plugin.Configuration.Save();
        }

        ImGuiHelpers.ScaledDummy(15.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        // --- STEP 2: THE CHECKLIST ---
        ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1f), "Step 2: Link your GPS Coordinates");
        ImGui.TextWrapped("Travel to your estates, stand near each garden patch, and click the link button to pinpoint its exact location.");
        ImGuiHelpers.ScaledDummy(10.0f);

        bool allLinked = true;

        if (currentProfile.PersonalEstateSize > 0)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), "Personal Estate Checklist:");
            for (int i = 0; i < currentProfile.PersonalEstateSize; i++)
            {
                var plot = currentProfile.PersonalPlots[i];
                DrawGpsRow($"Personal Plot {i + 1}", plot, currentProfile);
                if (!plot.HasGps) allLinked = false;
            }
            ImGuiHelpers.ScaledDummy(10.0f);
        }

        if (currentProfile.FCEstateSize > 0)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.8f, 1f), "Free Company Estate Checklist:");
            for (int i = 0; i < currentProfile.FCEstateSize; i++)
            {
                var plot = currentProfile.FCPlots[i];
                DrawGpsRow($"FC Plot {i + 1}", plot, currentProfile);
                if (!plot.HasGps) allLinked = false;
            }
        }

        if (currentProfile.PersonalEstateSize == 0 && currentProfile.FCEstateSize == 0)
        {
            ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1f), "You have 0 outdoor plots configured. You can skip this step!");
        }

        ImGuiHelpers.ScaledDummy(20.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(10.0f);

        // --- STEP 3: FINISH BUTTON ---
        ImGui.BeginDisabled(!allLinked);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.8f, 0.3f, 1f));
        if (ImGui.Button("Complete Setup & Launch Gaia", new Vector2(-1, 40)))
        {
            currentProfile.HasCompletedOnboarding = true;
            plugin.Configuration.Save();
        }
        ImGui.PopStyleColor(2);
        ImGui.EndDisabled();

        if (!allLinked)
        {
            ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), "Please link all the plots listed above to continue.");
        }
    }

    private void DrawGpsRow(string label, GardenPlotState plot, CharacterProfile currentProfile)
    {
        ImGui.Text($"  {(plot.HasGps ? "[✔]" : "[  ]")} {label}");
        ImGui.SameLine(180f);

        if (plot.HasGps)
        {
            ImGui.TextColored(new Vector4(0.2f, 1f, 0.2f, 1f), "Linked!");
            ImGui.SameLine();
            if (ImGui.Button($"Clear##{label}"))
            {
                plot.GpsX = 0; plot.GpsY = 0; plot.GpsZ = 0; plot.TopExpectedYield = "";
                for (int i = 0; i < 8; i++) plot.Beds[i] = new GardenBedState();
                plugin.Configuration.Save();
            }
        }
        else
        {
            if (ImGui.Button($"📍 Link Nearest Garden##{label}"))
            {
                if (Plugin.ObjectTable.LocalPlayer != null)
                {
                    var nearestPatch = Plugin.ObjectTable
                        .Where(obj => obj.ObjectKind == ObjectKind.EventObj && ValidPatches.Contains(obj.BaseId))
                        .OrderBy(p => Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, p.Position))
                        .FirstOrDefault();

                    if (nearestPatch != null)
                    {
                        string duplicatePlotName = "";
                        for (int i = 0; i < currentProfile.PersonalEstateSize; i++)
                        {
                            if (currentProfile.PersonalPlots[i].HasGps && Vector3.Distance(currentProfile.PersonalPlots[i].GetGpsVector(), nearestPatch.Position) < 0.5f)
                            {
                                duplicatePlotName = $"Personal Plot {i + 1}"; break;
                            }
                        }
                        if (string.IsNullOrEmpty(duplicatePlotName))
                        {
                            for (int i = 0; i < currentProfile.FCEstateSize; i++)
                            {
                                if (currentProfile.FCPlots[i].HasGps && Vector3.Distance(currentProfile.FCPlots[i].GetGpsVector(), nearestPatch.Position) < 0.5f)
                                {
                                    duplicatePlotName = $"FC Plot {i + 1}"; break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(duplicatePlotName))
                        {
                            gpsErrorContext = label;
                            gpsErrorMessage = "(Can't Link Same Garden twice)";
                            gpsErrorTimeout = Environment.TickCount64 + 4000;
                            Plugin.ChatGui.PrintError($"[Gaia] Garden is already linked to another plot!! ({duplicatePlotName})");
                        }
                        else
                        {
                            int expectedSize = nearestPatch.BaseId == 2003755 ? 4 : nearestPatch.BaseId == 2003756 ? 6 : 8;

                            var allValidBeds = Plugin.ObjectTable
                                .Where(obj => obj.ObjectKind == ObjectKind.EventObj && obj.BaseId == nearestPatch.BaseId)
                                .OrderBy(obj => obj.GameObjectId).ToList();

                            List<List<Dalamud.Game.ClientState.Objects.Types.IGameObject>> chunks = new();
                            List<Dalamud.Game.ClientState.Objects.Types.IGameObject> currentChunk = new();

                            foreach (var bed in allValidBeds)
                            {
                                if (currentChunk.Count == 0) currentChunk.Add(bed);
                                else if (bed.GameObjectId - currentChunk.Last().GameObjectId <= 2 && currentChunk.Count < expectedSize) currentChunk.Add(bed);
                                else { chunks.Add(currentChunk); currentChunk = new List<Dalamud.Game.ClientState.Objects.Types.IGameObject> { bed }; }
                            }
                            if (currentChunk.Count > 0) chunks.Add(currentChunk);

                            var myPatch = chunks.FirstOrDefault(c => c.Any(b => b.GameObjectId == nearestPatch.GameObjectId));
                            int finalCount = myPatch != null ? myPatch.Count : expectedSize;

                            plot.GpsX = nearestPatch.Position.X;
                            plot.GpsY = nearestPatch.Position.Y;
                            plot.GpsZ = nearestPatch.Position.Z;
                            plot.PatchId = nearestPatch.BaseId;
                            plot.BedCount = finalCount;
                            plugin.Configuration.Save();
                        }
                    }
                    else
                    {
                        gpsErrorContext = label;
                        gpsErrorMessage = "(No garden patch found nearby!)";
                        gpsErrorTimeout = Environment.TickCount64 + 4000;
                        Plugin.ChatGui.PrintError("[Gaia] No garden patch found nearby! Please stand closer.");
                    }
                }
            }

            if (Environment.TickCount64 < gpsErrorTimeout && gpsErrorContext == label)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1f, 0.2f, 0.2f, 1f), gpsErrorMessage);
            }
        }
    }
}