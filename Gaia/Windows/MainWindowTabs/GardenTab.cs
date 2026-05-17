using Gaia.Helpers;
using Gaia.Manager;

namespace Gaia.Windows.MainWindowTabs;

// ─────────────────────────────────────────────────────────────────────────────
//  GardenTab  —  three-zone layout (matches sketch):
//
//  ┌─ STAT STRIP (purple) ──────────────────────────────────┐
//  │  Active│Ready│Wilted│Thirsty│Next                      │
//  ├─ MAIN FRAME (blue outline) ────────────────────────────┤
//  │  [Tend][Harvest]   Plot Name / GPS     [Sync/Scan]     │
//  │ ┌─ GRID BOX (black fill) ──────────────────────────┐   │
//  │ │   slots scale to fill this box                   │   │
//  │ └──────────────────────────────────────────────────┘   │
//  ├─ FOOTER (red) ─────────────────────────────────────────┤
//  │  fertilizer picker · Water · Fertilize · Stop          │
//  │  status bar · GPS override link                        │
//  └────────────────────────────────────────────────────────┘
//
//  Bed clockwise mapping (Deluxe):
//    [0][1][2]
//    [7][★][3]   ★ = center info panel (-1), not a bed
//    [6][5][4]
// ─────────────────────────────────────────────────────────────────────────────
public class GardenTab : IDrawablePage
{
    private readonly Plugin _plugin;
    public string TabLabel => "  Garden  ";

    private bool _isHarvestMode;

    // 0 = Harvest All (Fresh) — harvest every non-X'd bed, no replant
    // 1 = 5×3 Sustain         — auto-protect TM/BL/BR (beds 1,6,4), harvest the other 5
    private int _harvestMode;
    private string _lastHarvestPlotKey = "";

    // skipMap  : "{plotKey}_{bedIdx}" → true = protected / do not harvest
    // protMap  : "{plotKey}_{bedIdx}" → true = auto-protected by 5×3 mode (drawn blue, not red X)
    private readonly Dictionary<string, bool> _skipMap = new();
    private readonly Dictionary<string, bool> _protMap = new();

    // Bed indices auto-protected in 5×3 mode: TM=1, BL=6, BR=4 (clockwise map)
    private static readonly int[] FiveThreeProtected = { 1, 6, 4 };
    private ThemePalette _t = null!;

    // Slot size is computed per-frame from available space — no hardcoded value
    private float _slotSz;
    private const float SlotGap = 5f;
    private const float StatStripH = 30f;
    private const float HeaderLineH = 28f;

    private static readonly int[] DeluxeMap = { 0, 1, 2, 7, -1, 3, 6, 5, 4 };
    private static readonly int[] OblongMap = { 0, 1, 2, 3, 4, 5 };
    private static readonly int[] RoundMap = { 0, 1, 2, 3 };

    public GardenTab(Plugin plugin) => _plugin = plugin;

    // ─────────────────────────────────────────────────────────
    //  Entry point
    // ─────────────────────────────────────────────────────────
    public void Draw()
    {
        var profile = _plugin.GetCurrentCharacterProfile();
        if (profile == null) { ImGui.TextDisabled("Waiting for character data..."); return; }

        _t = ThemeConfig.Get(_plugin.Configuration.SelectedTheme);
        _plugin.GardenContext.Refresh();

        // ── ZONE 1: Stat strip (always full-width at top) ──
        DrawStatStrip(profile);

        var loc = _plugin.GardenContext.CurrentLocation;

        if (loc == LocationContext.Outdoors && _plugin.GardenContext.ActivePlot != null)
            DrawAtPlot(profile);
        else if (loc == LocationContext.Unknown ||
                (loc == LocationContext.Outdoors && _plugin.GardenContext.ActivePlot == null))
            DrawTooFar(profile);
        else
            DrawIndoorContext(profile, loc);
    }

    // ─────────────────────────────────────────────────────────
    //  STAT STRIP  — Zone 1, fixed height across full width
    // ─────────────────────────────────────────────────────────
    private void DrawStatStrip(CharacterProfile profile)
    {
        int active = 0, ready = 0, wilted = 0, thirsty = 0;
        float nextHrs = float.MaxValue;

        void Scan(GardenBedState? b)
        {
            if (b == null || b.IsEmpty) return;
            active++;
            if (b.IsMature) ready++;
            if (b.IsWilted) wilted++;
            if (IsThirsty(b)) thirsty++;
            if (!b.IsMature && !b.IsWilted && b.PlantedTime != DateTime.MinValue)
            {
                float growH = GardenData.GetCropGrowTime(b.SeedName);
                float rem = growH - (float)(DateTime.Now - b.PlantedTime).TotalHours;
                if (rem > 0f && rem < nextHrs) nextHrs = rem;
            }
        }

        foreach (var p in profile.PersonalPlots.Concat(profile.FCPlots))
            if (p?.Beds != null) foreach (var b in p.Beds) Scan(b);
        foreach (var b in profile.PersonalPlanters.Concat(profile.FCPlanters)
            .Concat(profile.PersonalApartmentPlanters).Concat(profile.FCApartmentPlanters))
            Scan(b);

        string nextStr = nextHrs == float.MaxValue ? "--"
                       : nextHrs < 1f ? $"{(int)(nextHrs * 60)}m" : $"{(int)nextHrs}h";

        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;

        draw.AddRectFilled(pos, pos + new Vector2(w, StatStripH), _t.ToU32(_t.Panel), 6f);
        draw.AddRect(pos, pos + new Vector2(w, StatStripH), _t.ToU32(_t.Border), 6f, 0, 1f);

        (string label, Vector4 col)[] cells =
        {
            ($"Active  {active}",   _t.Text),
            ($"Ready  {ready}",     ready   > 0 ? _t.Success : _t.TextMuted),
            ($"Wilted  {wilted}",   wilted  > 0 ? _t.Danger  : _t.TextMuted),
            ($"Thirsty  {thirsty}", thirsty > 0 ? _t.Warning : _t.TextMuted),
            ($"Next  {nextStr}",    _t.Accent),
        };

        float cellW = w / cells.Length;
        for (int i = 0; i < cells.Length; i++)
        {
            var ts = ImGui.CalcTextSize(cells[i].label);
            var tp = new Vector2(pos.X + cellW * i + (cellW - ts.X) * .5f,
                                 pos.Y + (StatStripH - ts.Y) * .5f);
            draw.AddText(tp, _t.ToU32(cells[i].col), cells[i].label);
            if (i < cells.Length - 1)
                draw.AddLine(new Vector2(pos.X + cellW * (i + 1), pos.Y + 5f),
                             new Vector2(pos.X + cellW * (i + 1), pos.Y + StatStripH - 5f),
                             _t.ToU32(_t.Border, 0.30f));
        }
        ImGui.Dummy(new Vector2(w, StatStripH));
    }

    // ─────────────────────────────────────────────────────────
    //  AT PLOT  — three-zone frame
    // ─────────────────────────────────────────────────────────
    private void DrawAtPlot(CharacterProfile profile)
    {
        var plot = _plugin.GardenContext.ActivePlot!;
        bool isPersonal = profile.PersonalPlots.Contains(plot);
        int plotIdx = isPersonal
            ? Array.IndexOf(profile.PersonalPlots, plot)
            : Array.IndexOf(profile.FCPlots, plot);
        string plotLabel = isPersonal ? $"Personal Plot {plotIdx + 1}" : $"FC Plot {plotIdx + 1}";
        int bedCount = Math.Clamp(plot.BedCount, 0, 8);

        // ── GPS sanity check: plot exists in config but has no GPS anchor ──
        // This happens if the garden was moved or reset. Tell the user to relink.
        if (!plot.HasGps)
        {
            DrawPlotRelinkError(plotLabel);
            return;
        }

        DrawThreeZoneFrame(
            plotLabel, hasGps: true,
            () => DrawBedGrid(plot, bedCount, plotLabel),
            () =>
            {
                _lastHarvestPlotKey = plotLabel;
                if (!_isHarvestMode) DrawTendControls(isIndoor: false);
                else DrawHarvestControls(plot.Beds, AnyMature(plot.Beds));
            });
    }

    // ─────────────────────────────────────────────────────────
    //  INDOOR
    // ─────────────────────────────────────────────────────────
    private void DrawIndoorContext(CharacterProfile profile, LocationContext loc)
    {
        bool isPersonalHouse = loc == LocationContext.House
                            && _plugin.LocationManager.LastHouseViewMode == 0;

        (string label, GardenBedState[]? pots, int count) = loc switch
        {
            LocationContext.House when isPersonalHouse
                => ("Personal House Pots", profile.PersonalPlanters, profile.PersonalPlanterCount),
            LocationContext.House
                => ("FC House Pots", profile.FCPlanters, profile.FCPlanterCount),
            LocationContext.FCApartment
                => ("FC Apartment Pots", profile.FCApartmentPlanters, profile.FCApartmentPlanterCount),
            LocationContext.PersonalApartment
                => ("Personal Apartment Pots", profile.PersonalApartmentPlanters, profile.PersonalApartmentPlanterCount),
            _ => ("Indoor Pots", null, 0),
        };

        DrawThreeZoneFrame(
            label, hasGps: true,
            () =>
            {
                if (pots != null && count > 0) DrawPotStrip(pots, count);
                else ImGui.TextDisabled("No pots configured. Check the Status tab.");
            },
            () =>
            {
                if (!_isHarvestMode) DrawTendControls(isIndoor: true);
                else DrawHarvestControls(pots, AnyMature(pots));
            });
    }

    // ─────────────────────────────────────────────────────────
    //  THREE-ZONE FRAME
    //  Blue outer border → header row → black grid box → red footer
    // ─────────────────────────────────────────────────────────
    private void DrawThreeZoneFrame(
        string plotLabel, bool hasGps,
        System.Action drawGrid,
        System.Action drawFooter)
    {
        float avail = ImGui.GetContentRegionAvail().X;
        float availH = ImGui.GetContentRegionAvail().Y;
        // Harvest mode footer packs 5 stacked rows (mode buttons, description card,
        // hint, Auto-Replant checkbox, action buttons) + status bar. 185f cut off
        // the action buttons and status bar; 245f leaves ~10f slack. Tend mode
        // footer (110f) only has one button row + label + status bar — unchanged.
        float footerH = _isHarvestMode ? 245f : 110f;
        float headerH = HeaderLineH + 4f;
        float gridZoneH = MathF.Max(availH - headerH - footerH - 4f, 80f);

        var frameOrigin = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        draw.AddRect(frameOrigin - new Vector2(1f, 1f),
                     frameOrigin + new Vector2(avail + 1f, headerH + gridZoneH + footerH + 4f),
                     _t.ToU32(_t.Crystal, 0.55f), 6f, 0, 1.5f);

        DrawHeaderRow(plotLabel, hasGps);
        DrawSubTabs();

        var gridBoxOrigin = ImGui.GetCursorScreenPos();
        draw.AddRectFilled(gridBoxOrigin, gridBoxOrigin + new Vector2(avail, gridZoneH),
                           _t.ToU32(_t.Panel, 0.95f), 5f);
        draw.AddRect(gridBoxOrigin, gridBoxOrigin + new Vector2(avail, gridZoneH),
                     _t.ToU32(_t.Border, 0.35f), 5f, 0, 1f);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.BeginChild("##gridBox", new Vector2(avail, gridZoneH), false,
                             ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            drawGrid();
        ImGui.EndChild();
        ImGui.PopStyleColor();

        var footerOrigin = ImGui.GetCursorScreenPos();
        draw.AddRectFilled(footerOrigin, footerOrigin + new Vector2(avail, footerH),
                           _t.ToU32(_t.Danger, 0.06f), 5f);
        draw.AddRect(footerOrigin, footerOrigin + new Vector2(avail, footerH),
                     _t.ToU32(_t.Danger, 0.25f), 5f, 0, 1f);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        if (ImGui.BeginChild("##footer", new Vector2(avail, footerH), false,
                             ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            drawFooter();
            DrawStatusBar();
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // ─────────────────────────────────────────────────────────
    //  HEADER ROW  — [Tend][Harvest] on left, plot name centered,
    //  [Sync/Scan] on right — all on the same line
    // ─────────────────────────────────────────────────────────
    private void DrawHeaderRow(string plotName, bool hasGps)
    {
        const float syncW = 112f;
        const float tabW = 62f;

        // avail = content region width. SameLine offsets are from window left edge,
        // so we need to add the cursor start X to position things correctly.
        float windowLeft = ImGui.GetCursorPosX();
        float avail = ImGui.GetContentRegionAvail().X;

        // Tend button
        ImGui.PushStyleColor(ImGuiCol.Button, _isHarvestMode ? _t.ToU32(_t.Panel2) : _t.ToU32(_t.Panel3));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.PanelAlt));
        ImGui.PushStyleColor(ImGuiCol.Text, _isHarvestMode ? _t.ToU32(_t.TextMuted) : _t.ToU32(_t.AccentHi));
        if (ImGui.Button("Tend##hdr", new Vector2(tabW, HeaderLineH))) _isHarvestMode = false;
        ImGui.PopStyleColor(3);

        ImGui.SameLine(0f, 2f);

        // Harvest button
        ImGui.PushStyleColor(ImGuiCol.Button, _isHarvestMode ? _t.ToU32(_t.Panel3) : _t.ToU32(_t.Panel2));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.PanelAlt));
        ImGui.PushStyleColor(ImGuiCol.Text, _isHarvestMode ? _t.ToU32(_t.AccentHi) : _t.ToU32(_t.TextMuted));
        if (ImGui.Button("Harvest##hdr", new Vector2(tabW + 14f, HeaderLineH))) _isHarvestMode = true;
        ImGui.PopStyleColor(3);

        // Center plot name + GPS between tab buttons and sync button
        float usedLeft = windowLeft + tabW + tabW + 14f + 2f;
        float usedRight = windowLeft + avail - syncW - ImGui.GetStyle().ItemSpacing.X;
        float midX = (usedLeft + usedRight) * .5f;
        string fullText = hasGps ? $"{plotName}  GPS Active" : plotName;
        float fullW = ImGui.CalcTextSize(fullText).X;
        float nameX = MathF.Max(usedLeft + 4f, midX - fullW * .5f);

        ImGui.SameLine(nameX - windowLeft);  // SameLine offset is relative to window left
        ImGui.TextColored(_t.AccentHi, plotName);
        if (hasGps) { ImGui.SameLine(0f, 5f); ImGui.TextColored(_t.Success, "GPS Active"); }
        else { ImGui.SameLine(0f, 5f); ImGui.TextColored(_t.Danger, "Garden not found"); }

        // Sync/Scan pinned to the true right edge: windowLeft + avail - syncW
        ImGui.SameLine(windowLeft + avail - syncW - windowLeft);  // simplifies to avail - syncW from window padding
        ImGui.SetCursorPosX(windowLeft + avail - syncW);
        ImGui.PushStyleColor(ImGuiCol.Button, _t.ToU32(_t.Crystal, 0.30f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.Crystal, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.Text, _t.ToU32(_t.CrystalHi));
        if (ImGui.Button("Sync / Scan##hdr", new Vector2(syncW, HeaderLineH)))
        {
            _plugin.Farming.IsIndoors = _plugin.LocationManager.CurrentLocation != LocationContext.Outdoors;
            _plugin.Farming.PrepareForScan();
            _plugin.Farming.ScanGarden();
        }
        ImGui.PopStyleColor(3);
    }

    // Sub-tabs removed from header row — tabs are now inside the header row itself.
    // Keeping an empty stub so DrawThreeZoneFrame can still call it safely.
    private void DrawSubTabs() { /* merged into DrawHeaderRow */ }

    // ─────────────────────────────────────────────────────────
    //  BED GRID  — slot size computed from available space
    // ─────────────────────────────────────────────────────────
    private void DrawBedGrid(GardenPlotState plot, int bedCount, string plotKey)
    {
        int[] map = bedCount <= 4 ? RoundMap : bedCount <= 6 ? OblongMap : DeluxeMap;
        int cols = bedCount <= 4 ? 2 : 3;
        int rows = map.Length / cols;

        float avail = ImGui.GetContentRegionAvail().X;
        float availH = ImGui.GetContentRegionAvail().Y;

        // ── Harvest legend strip — centered at top of grid box ──
        float legendH = 0f;
        if (_isHarvestMode)
        {
            legendH = ImGui.GetTextLineHeight() + 8f;
            var dl = ImGui.GetWindowDrawList();
            var lp = ImGui.GetCursorScreenPos();
            const float boxSz = 9f;
            float itemH = ImGui.GetTextLineHeight();

            // Measure total legend width to center it
            float keepW = boxSz + 4f + ImGui.CalcTextSize("Keep").X;
            float skipW = boxSz + 4f + ImGui.CalcTextSize("Skip").X;
            float harvestW = boxSz + 4f + ImGui.CalcTextSize("Harvest").X;
            float gapW = 12f;
            float totalW = keepW + gapW + skipW + gapW + harvestW;
            float startX = lp.X + (avail - totalW) * .5f;
            float cy = lp.Y + legendH * .5f;

            void LegendItem(float x, uint fill, uint border, string label)
            {
                float iy = cy - boxSz * .5f;
                dl.AddRectFilled(new Vector2(x, iy), new Vector2(x + boxSz, iy + boxSz), fill, 2f);
                dl.AddRect(new Vector2(x, iy), new Vector2(x + boxSz, iy + boxSz), border, 2f);
                dl.AddText(new Vector2(x + boxSz + 4f, cy - itemH * .5f), _t.ToU32(_t.TextDim), label);
            }

            LegendItem(startX, _t.ToU32(_t.Crystal, 0.55f), _t.ToU32(_t.CrystalHi), "Keep");
            LegendItem(startX + keepW + gapW, _t.ToU32(_t.Danger, 0.45f), _t.ToU32(_t.DangerHi), "Skip");
            LegendItem(startX + keepW + gapW + skipW + gapW, _t.ToU32(_t.Success, 0.45f), _t.ToU32(_t.SuccessHi), "Harvest");

            ImGui.Dummy(new Vector2(avail, legendH));
            availH -= legendH;
        }

        // Compute slot size from remaining available space
        float slotFromW = (avail - (cols - 1) * SlotGap - 16f) / cols;
        float slotFromH = (availH - (rows - 1) * SlotGap - 16f) / rows;
        _slotSz = MathF.Min(slotFromW, slotFromH);
        _slotSz = Math.Clamp(_slotSz, 48f, 140f);

        float gridW = cols * _slotSz + (cols - 1) * SlotGap;
        float gridH = rows * _slotSz + (rows - 1) * SlotGap;
        float offX = (avail - gridW) * .5f;
        float offY = (availH - gridH) * .5f;
        offX = MathF.Max(offX, 4f);
        offY = MathF.Max(offY, 4f);

        var gridOrigin = ImGui.GetCursorScreenPos() + new Vector2(offX, offY);
        var draw = ImGui.GetWindowDrawList();
        string topYield = CalcTopYield(plot);

        for (int i = 0; i < map.Length; i++)
        {
            int row = i / cols;
            int col = i % cols;
            var slotP = gridOrigin + new Vector2(col * (_slotSz + SlotGap),
                                                   row * (_slotSz + SlotGap));
            int bedIdx = map[i];

            if (bedIdx == -1)
                DrawCenterInfoPanel(draw, slotP, topYield);
            else if (bedIdx < bedCount && bedIdx < plot.Beds.Length)
            {
                if (_isHarvestMode)
                    DrawHarvestSlot(draw, slotP, plot.Beds[bedIdx], bedIdx, plotKey);
                else
                    DrawTendSlot(draw, slotP, plot.Beds[bedIdx], bedIdx);
            }
            else
                DrawInertSlot(draw, slotP);
        }

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offY + gridH + offY);
    }

    // ─────────────────────────────────────────────────────────
    //  CENTER INFO PANEL
    // ─────────────────────────────────────────────────────────
    private void DrawCenterInfoPanel(ImDrawListPtr draw, Vector2 origin, string topYield)
    {
        var br = origin + new Vector2(_slotSz, _slotSz);
        var center = origin + new Vector2(_slotSz * .5f, _slotSz * .5f);
        float r = _plugin.Configuration.SelectedTheme == GaiaTheme.Kawaii ? 14f : 5f;

        draw.AddRectFilled(origin, br, _t.ToU32(_t.Panel, 0.85f), r);
        draw.AddRect(origin, br, _t.ToU32(_t.Accent, 0.40f), r, 0, 1.5f);

        if (string.IsNullOrEmpty(topYield)) return;

        const string lbl = "Target";
        var ls = ImGui.CalcTextSize(lbl);
        draw.AddText(new Vector2(center.X - ls.X * .5f, origin.Y + 4f), _t.ToU32(_t.Accent), lbl);

        uint iconId = GardenData.GetIconIdForName(topYield);
        if (iconId > 0)
        {
            var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
            if (wrap != null)
            {
                float iSz = _slotSz * 0.42f;
                draw.AddImage(wrap.Handle,
                    center - new Vector2(iSz * .5f, iSz * .5f + 3f),
                    center + new Vector2(iSz * .5f, iSz * .5f - 3f));
            }
        }

        string shortName = topYield.Replace("Thavnairian", "Thav.").Replace(" Seeds", "").Replace(" Bean", "");
        var ns = ImGui.CalcTextSize(shortName);
        draw.AddText(new Vector2(center.X - ns.X * .5f, br.Y - ns.Y - 4f), _t.ToU32(_t.AccentHi), shortName);
    }

    // ─────────────────────────────────────────────────────────
    //  TEND SLOT  — crop icon + water bar + fert bar
    //  Ready beds: large icon, no bars, "Ready!" label.
    //  Growing beds: icon + two proportional progress bars.
    // ─────────────────────────────────────────────────────────
    private void DrawTendSlot(ImDrawListPtr draw, Vector2 origin, GardenBedState bed, int idx)
    {
        float sz = _slotSz;
        var br = origin + new Vector2(sz, sz);
        var ctr = origin + new Vector2(sz * .5f, sz * .5f);
        float r = _plugin.Configuration.SelectedTheme == GaiaTheme.Kawaii ? sz * 0.18f : 5f;

        if (bed.IsEmpty)
        {
            draw.AddRectFilled(origin, br, _t.ToU32(_t.Panel), r);
            draw.AddRect(origin, br, _t.ToU32(_t.Border, 0.28f), r, 0, 1f);
            const string soil = "Soil";
            var ss = ImGui.CalcTextSize(soil);
            draw.AddText(ctr - ss * .5f, _t.ToU32(_t.TextDim, 0.60f), soil);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(sz, sz));
            return;
        }

        uint bg = bed.IsWilted ? _t.ToU32(_t.Danger, 0.20f)
                : bed.IsMature ? _t.ToU32(_t.Success, 0.28f)
                : IsThirsty(bed) ? _t.ToU32(_t.Warning, 0.18f)
                : _t.ToU32(_t.PanelAlt);
        draw.AddRectFilled(origin, br, bg, r);

        uint border = bed.IsWilted ? _t.ToU32(_t.Danger)
                    : bed.IsMature ? _t.ToU32(_t.Success)
                    : IsThirsty(bed) ? _t.ToU32(_t.Warning, 0.80f)
                    : _t.ToU32(_t.Border, 0.50f);
        draw.AddRect(origin, br, border, r, 0, bed.IsMature ? 2.5f : 1.5f);

        if (bed.IsWilted)
        {
            const string dead = "Dead";
            var ds = ImGui.CalcTextSize(dead);
            draw.AddText(ctr - ds * .5f, _t.ToU32(_t.Danger), dead);
            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(sz, sz));
            return;
        }

        // ── READY STATE: large icon + "Ready!" label, no bars ──
        if (bed.IsMature)
        {
            float iconSz = sz * 0.58f;
            uint iconId = GardenData.GetIconIdForName(bed.SeedName);
            if (iconId > 0)
            {
                var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                if (wrap != null)
                    draw.AddImage(wrap.Handle,
                        new Vector2(ctr.X - iconSz * .5f, origin.Y + 5f),
                        new Vector2(ctr.X + iconSz * .5f, origin.Y + 5f + iconSz));
            }
            const string rdy = "Ready!";
            var rs = ImGui.CalcTextSize(rdy);
            draw.AddText(new Vector2(ctr.X - rs.X * .5f, br.Y - rs.Y - 4f),
                         _t.ToU32(_t.SuccessHi), rdy);

            ImGui.SetCursorScreenPos(origin);
            ImGui.Dummy(new Vector2(sz, sz));
            // Tooltip still useful on ready slots
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Seed: {bed.SeedName}\nReady to harvest!");
            return;
        }

        // ── GROWING STATE: icon (top half) + two progress bars (bottom) ──
        float barH = MathF.Max(9f, sz * 0.11f);
        float barGap = MathF.Max(3f, sz * 0.035f);
        float barPad = MathF.Max(5f, sz * 0.06f);
        float bar2T = br.Y - barH - barPad;
        float bar1T = bar2T - barH - barGap;

        float iconAreaH = bar1T - origin.Y - 6f;
        float iconSzG = MathF.Min(iconAreaH - 2f, sz * 0.52f);
        iconSzG = MathF.Max(iconSzG, 14f);

        uint iconIdG = GardenData.GetIconIdForName(bed.SeedName);
        if (iconIdG > 0)
        {
            var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconIdG)).GetWrapOrDefault();
            if (wrap != null)
            {
                float iconX = ctr.X - iconSzG * .5f;
                float iconY = origin.Y + 4f + (iconAreaH - iconSzG) * .5f;
                if (_plugin.Configuration.SelectedTheme == GaiaTheme.Kawaii)
                    iconX += MathF.Sin((float)ImGui.GetTime() * 1.8f + idx * 0.9f) * 2.5f;
                draw.AddImage(wrap.Handle, new Vector2(iconX, iconY), new Vector2(iconX + iconSzG, iconY + iconSzG));
            }
        }

        // Status dot
        draw.AddCircleFilled(origin + new Vector2(sz - 7f, 7f), 4f,
            IsThirsty(bed) ? _t.ToU32(_t.Warning) : _t.ToU32(_t.Accent, 0.70f));

        // Water freshness bar
        float waterRemHrs = 24f - (float)(DateTime.Now - bed.LastWateredTime).TotalHours;
        float waterPct = Math.Clamp(waterRemHrs / 24f, 0f, 1f);
        Vector4 waterCol = waterPct > .50f ? _t.Success : waterPct > .20f ? _t.Warning : _t.Danger;
        var wTL = new Vector2(origin.X + barPad, bar1T);
        var wBR = new Vector2(br.X - barPad, bar1T + barH);
        draw.AddRectFilled(wTL, wBR, _t.ToU32(_t.Panel), 3f);
        if (waterPct > 0f)
            draw.AddRectFilled(wTL, new Vector2(wTL.X + (wBR.X - wTL.X) * waterPct, wBR.Y),
                               _t.ToU32(waterCol), 3f);
        // Always draw % label — use small font if bar is short
        string wTxt = $"{(int)(waterPct * 100)}%";
        var wSz = ImGui.CalcTextSize(wTxt);
        draw.AddText(new Vector2((wTL.X + wBR.X) * .5f - wSz.X * .5f,
                                  bar1T + MathF.Max(0f, (barH - wSz.Y) * .5f)),
                     _t.ToU32(_t.Text), wTxt);

        // Fertilizer timer bar
        float minsElapsed = (float)(DateTime.Now - bed.LastFertilizedTime).TotalMinutes;
        float fertPct = Math.Clamp(minsElapsed / 60f, 0f, 1f);
        var fTL = new Vector2(origin.X + barPad, bar2T);
        var fBR = new Vector2(br.X - barPad, bar2T + barH);
        draw.AddRectFilled(fTL, fBR, _t.ToU32(_t.Panel), 3f);
        if (fertPct > 0f)
            draw.AddRectFilled(fTL, new Vector2(fTL.X + (fBR.X - fTL.X) * fertPct, fBR.Y),
                               _t.ToU32(_t.Amber, 0.85f), 3f);
        string fTxt = fertPct >= 1f ? "Ready" : $"{Math.Max(0, (int)(60 - minsElapsed))}m";
        var fSz = ImGui.CalcTextSize(fTxt);
        draw.AddText(new Vector2((fTL.X + fBR.X) * .5f - fSz.X * .5f,
                                  bar2T + MathF.Max(0f, (barH - fSz.Y) * .5f)),
                     _t.ToU32(_t.Text), fTxt);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(sz, sz));
        if (ImGui.IsItemHovered())
        {
            string tt = $"Seed: {bed.SeedName}\n";
            if (bed.PlantedTime != DateTime.MinValue)
            {
                float growH = GardenData.GetCropGrowTime(bed.SeedName);
                float rem = MathF.Max(0f, growH - (float)(DateTime.Now - bed.PlantedTime).TotalHours);
                tt += $"Time until harvest: {rem:F1}h\n";
            }
            tt += $"Water: {MathF.Max(0f, waterRemHrs):F1}h remaining\n";
            tt += fertPct >= 1f ? "Fertilizer: Ready to apply!"
                : $"Fertilizer: Ready in {MathF.Max(0f, 60f - minsElapsed):F0}m";
            ImGui.SetTooltip(tt);
        }

        if (Environment.TickCount64 < bed.InteractionErrorTimeout && !string.IsNullOrEmpty(bed.InteractionError))
        {
            var errSz = ImGui.CalcTextSize(bed.InteractionError);
            draw.AddText(new Vector2(origin.X + (sz - errSz.X) * .5f, br.Y - errSz.Y - 2f),
                         _t.ToU32(_t.Warning), bed.InteractionError);
        }

        // Amber ring when the in-game target matches this bed (indoor pots only).
        if (TargetHighlight.MatchesBed(Plugin.TargetManager.Target, bed))
            TargetHighlight.DrawRing(draw, origin, new Vector2(sz, sz), r);
    }

    // ─────────────────────────────────────────────────────────
    //  HARVEST SLOT
    //  Three visual states per slot:
    //    Normal (green border)  — will be harvested
    //    Auto-protected (blue)  — 5×3 mode locked this bed (TM/BL/BR)
    //    Manual skip (red X)    — user toggled this bed off
    //
    //  Click behaviour:
    //    Auto-protected bed → clicking manually overrides the protection
    //      (turns it into a red-X manual skip so user can recover wrong setups)
    //    Normal/skip bed → toggles skip on/off
    // ─────────────────────────────────────────────────────────
    private void DrawHarvestSlot(ImDrawListPtr draw, Vector2 origin,
                                 GardenBedState bed, int bedIdx, string plotKey)
    {
        float sz = _slotSz;
        string key = $"{plotKey}_{bedIdx}";
        bool isAutoP = _protMap.TryGetValue(key, out bool ap) && ap;   // auto-protected (blue)
        bool isSkipped = !isAutoP && (_skipMap.TryGetValue(key, out bool s) && s); // manual X (red)
        bool isProtected = isAutoP || isSkipped; // either way, won't be harvested

        var br = origin + new Vector2(sz, sz);
        var ctr = origin + new Vector2(sz * .5f, sz * .5f);
        float r = _plugin.Configuration.SelectedTheme == GaiaTheme.Kawaii ? sz * 0.18f : 5f;

        // Background
        uint bg = isAutoP ? _t.ToU32(_t.Crystal, 0.18f)
                : isSkipped ? _t.ToU32(_t.Danger, 0.15f)
                : bed.IsEmpty ? _t.ToU32(_t.Panel)
                : bed.IsMature ? _t.ToU32(_t.Success, 0.22f)
                : _t.ToU32(_t.PanelAlt);
        draw.AddRectFilled(origin, br, bg, r);

        // Border
        uint border = isAutoP ? _t.ToU32(_t.CrystalHi, 0.85f)
                    : isSkipped ? _t.ToU32(_t.Danger)
                    : bed.IsEmpty ? _t.ToU32(_t.Border, 0.25f)
                    : bed.IsMature ? _t.ToU32(_t.Success)
                    : _t.ToU32(_t.Border, 0.50f);
        draw.AddRect(origin, br, border, r, 0, isProtected ? 2f : 1.5f);

        if (bed.IsEmpty)
        {
            const string soil = "Soil";
            var ss = ImGui.CalcTextSize(soil);
            draw.AddText(ctr - ss * .5f, _t.ToU32(_t.TextDim, 0.55f), soil);
        }
        else
        {
            // Crop icon
            float iconSz = sz * 0.50f;
            uint iconId = GardenData.GetIconIdForName(bed.SeedName);
            if (iconId > 0)
            {
                var wrap = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                if (wrap != null)
                    draw.AddImage(wrap.Handle,
                        ctr - new Vector2(iconSz * .5f, iconSz * .5f + 4f),
                        ctr + new Vector2(iconSz * .5f, iconSz * .5f - 4f));
            }

            if (bed.IsMature && !isProtected)
            {
                const string rdy = "READY!";
                var rs = ImGui.CalcTextSize(rdy);
                draw.AddText(new Vector2(ctr.X - rs.X * .5f, br.Y - rs.Y - 4f),
                             _t.ToU32(_t.SuccessHi), rdy);
            }

            // Manual skip: red X
            if (isSkipped)
            {
                float pad = sz * .15f;
                uint xc = _t.ToU32(_t.DangerHi, 0.85f);
                draw.AddLine(origin + new Vector2(pad, pad), br - new Vector2(pad, pad), xc, 2.5f);
                draw.AddLine(new Vector2(br.X - pad, origin.Y + pad),
                             new Vector2(origin.X + pad, br.Y - pad), xc, 2.5f);
            }

            // Auto-protected: blue lock icon (simple padlock drawn with lines)
            if (isAutoP)
            {
                float lx = origin.X + sz * .08f;
                float ly = origin.Y + sz * .06f;
                float lsz = sz * .18f;
                // "KEEP" label
                const string keep = "KEEP";
                var ks = ImGui.CalcTextSize(keep);
                draw.AddText(new Vector2(ctr.X - ks.X * .5f, br.Y - ks.Y - 4f),
                             _t.ToU32(_t.CrystalHi, 0.90f), keep);
            }
        }

        // Amber ring when the in-game target matches this bed (indoor pots only —
        // outdoor sub-beds have no per-bed GPS so MatchesBed returns false).
        if (TargetHighlight.MatchesBed(Plugin.TargetManager.Target, bed))
            TargetHighlight.DrawRing(draw, origin, new Vector2(sz, sz), r);

        // Invisible button — clicking any protected bed makes it a manual skip instead
        // (so user can override the auto-protection if they planted in a weird direction)
        ImGui.SetCursorScreenPos(origin);
        if (ImGui.InvisibleButton($"##hs_{key}", new Vector2(sz, sz)) && !bed.IsEmpty)
        {
            if (isAutoP)
            {
                // Override auto-protection → now a manual skip
                _protMap[key] = false;
                _skipMap[key] = true;
            }
            else if (isSkipped)
            {
                // Was manually skipped → restore to harvest
                _skipMap[key] = false;
            }
            else
            {
                // Normal → manually skip
                _skipMap[key] = true;
            }
        }

        if (ImGui.IsItemHovered() && !bed.IsEmpty)
        {
            draw.AddRect(origin, br, _t.ToU32(_t.AccentHi, 0.45f), r, 0, 1.5f);
            string tip = isAutoP ? "5x3 protected (TM/BL/BR). Click to override and skip manually."
                       : isSkipped ? "Manually skipped. Click to re-enable harvest."
                       : "Will be harvested. Click to skip.";
            ImGui.SetTooltip(tip);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  INERT SLOT
    // ─────────────────────────────────────────────────────────
    private void DrawInertSlot(ImDrawListPtr draw, Vector2 origin)
    {
        draw.AddRect(origin, origin + new Vector2(_slotSz, _slotSz),
                     _t.ToU32(_t.Border, 0.10f), 5f, 0, 1f);
    }

    // ─────────────────────────────────────────────────────────
    //  POT STRIP (indoor)
    //  Renders indoor planters with the same slot quality as outdoor
    //  beds — icon, water bar, fert bar, status dot, ready state.
    //  Pots scale to fill the grid box just like outdoor slots.
    // ─────────────────────────────────────────────────────────
    private void DrawPotStrip(GardenBedState[] pots, int count)
    {
        if (count == 0) return;

        float avail = ImGui.GetContentRegionAvail().X;
        float availH = ImGui.GetContentRegionAvail().Y;

        // Scale pots to fill the available box — same logic as outdoor beds.
        // Pots sit in a single row so rows=1.
        float slotFromW = (avail - (count - 1) * SlotGap - 16f) / count;
        float slotFromH = availH - 16f;
        float potSz = Math.Clamp(MathF.Min(slotFromW, slotFromH), 60f, 140f);
        _slotSz = potSz;   // set _slotSz so DrawTendSlot uses the right size

        float totalW = count * potSz + (count - 1) * SlotGap;
        float offX = MathF.Max((avail - totalW) * .5f, 4f);
        float offY = MathF.Max((availH - potSz) * .5f, 4f);

        var origin = ImGui.GetCursorScreenPos() + new Vector2(offX, offY);
        var draw = ImGui.GetWindowDrawList();

        for (int i = 0; i < count && i < pots.Length; i++)
        {
            var slotOrigin = origin + new Vector2(i * (potSz + SlotGap), 0f);

            // Reuse the same DrawTendSlot / harvest slot logic depending on mode
            if (_isHarvestMode)
                DrawHarvestSlot(draw, slotOrigin, pots[i], i, "IndoorPots");
            else
                DrawTendSlot(draw, slotOrigin, pots[i], i);
        }

        // Advance the cursor so the footer sits right below the pots
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offY + potSz + 4f);
    }

    // ─────────────────────────────────────────────────────────
    //  TOO FAR
    // ─────────────────────────────────────────────────────────
    private void DrawTooFar(CharacterProfile profile)
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        const float h = 28f;

        draw.AddRectFilled(pos, pos + new Vector2(w, h), _t.ToU32(_t.Panel), 6f);
        draw.AddRect(pos, pos + new Vector2(w, h), _t.ToU32(_t.Danger, 0.40f), 6f, 0, 1f);
        draw.AddText(pos + new Vector2(10f, 6f), _t.ToU32(_t.TextMuted), "No Plot Detected");
        const string badge = "Out of Range";
        var bs = ImGui.CalcTextSize(badge);
        draw.AddText(pos + new Vector2(w - bs.X - 11f, 7f), _t.ToU32(_t.Danger), badge);
        ImGui.Dummy(new Vector2(w, h));

        ImGuiHelpers.ScaledDummy(10f);
        ImGui.TextColored(_t.TextMuted, "Too far from any garden.");
        ImGui.TextDisabled("Stand near a plot to auto-lock, or click one below.");
        ImGuiHelpers.ScaledDummy(10f);

        float cardW = (ImGui.GetContentRegionAvail().X - 8f) * .5f;
        const float cardH = 58f;

        var allPlots = new List<(string name, GardenPlotState plot, int globalIdx)>();
        for (int i = 0; i < profile.PersonalEstateSize; i++)
            allPlots.Add(($"Personal Plot {i + 1}", profile.PersonalPlots[i], i));
        for (int i = 0; i < profile.FCEstateSize; i++)
            allPlots.Add(($"FC Plot {i + 1}", profile.FCPlots[i], profile.PersonalEstateSize + i));

        for (int i = 0; i < allPlots.Count; i++)
        {
            var (name, plt, gIdx) = allPlots[i];
            string dist = "—";
            if (plt.HasGps && Plugin.ObjectTable.LocalPlayer != null)
            {
                float d = Vector3.Distance(Plugin.ObjectTable.LocalPlayer.Position, plt.GetGpsVector());
                dist = d < 1000f ? $"~{(int)d}y" : $"~{(d / 1000f):F1}ky";
            }
            int rdy = 0, tot = 0, wlt = 0;
            foreach (var b in plt.Beds) { if (b == null || b.IsEmpty) continue; tot++; if (b.IsMature) rdy++; if (b.IsWilted) wlt++; }
            string status = tot == 0 ? "Empty" : wlt > 0 ? $"{wlt} Wilted" : rdy > 0 ? $"{rdy}/{tot} Ready" : "Growing";
            Vector4 statusC = wlt > 0 ? _t.Danger : rdy > 0 ? _t.Success : _t.TextMuted;

            var cPos = ImGui.GetCursorScreenPos();
            draw.AddRectFilled(cPos, cPos + new Vector2(cardW, cardH), _t.ToU32(_t.Panel2), 7f);
            draw.AddRect(cPos, cPos + new Vector2(cardW, cardH), _t.ToU32(_t.Border), 7f, 0, 1f);
            draw.AddText(cPos + new Vector2(10f, 9f), _t.ToU32(_t.AccentHi), name);
            var ds = ImGui.CalcTextSize(dist);
            draw.AddText(cPos + new Vector2(cardW - ds.X - 8f, 9f), _t.ToU32(_t.TextDim), dist);
            draw.AddText(cPos + new Vector2(10f, 34f), _t.ToU32(statusC), status);

            ImGui.SetCursorScreenPos(cPos);
            if (ImGui.InvisibleButton($"##tfc{i}", new Vector2(cardW, cardH)))
            {
                _plugin.GardenContext.Refresh(gIdx);
            }
            if (ImGui.IsItemHovered())
                draw.AddRect(cPos, cPos + new Vector2(cardW, cardH), _t.ToU32(_t.Accent, 0.65f), 7f, 0, 1.5f);

            if (i % 2 == 0 && i + 1 < allPlots.Count) ImGui.SameLine(0f, 8f);
            else { ImGui.SetCursorPosY(ImGui.GetCursorPosY() + cardH + 6f); ImGui.Dummy(Vector2.Zero); }
        }

        ImGuiHelpers.ScaledDummy(8f);
        DrawStatusBar(text: "Waiting — move near a garden to begin", warn: true);
    }

    // ─────────────────────────────────────────────────────────
    //  TEND CONTROLS
    // ─────────────────────────────────────────────────────────
    private void DrawTendControls(bool isIndoor)
    {
        int fertCount = InventoryHelper.GetItemCount(_plugin.Configuration.FertilizerId);
        bool hasFert = fertCount > 0;
        bool isBusy = _plugin.Farming.CurrentState != FarmingState.Idle;

        // Fertilizer is hardcoded to Fish Meal for now.
        // FertilizerId in Configuration should already point to Fish Meal's item ID.
        ImGui.TextColored(_t.TextSub, "Fish Meal");
        ImGui.SameLine();
        ImGui.TextColored(_t.TextDim, hasFert ? $"x{fertCount} in bag" : "None in bag!");
        ImGuiHelpers.ScaledDummy(4f);

        float btnW = (ImGui.GetContentRegionAvail().X - 16f) / 3f;

        ImGui.BeginDisabled(isBusy);
        ImGui.PushStyleColor(ImGuiCol.Button, _t.ToU32(_t.Crystal, 0.30f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.Crystal, 0.52f));
        ImGui.PushStyleColor(ImGuiCol.Text, _t.ToU32(_t.CrystalHi));
        if (ImGui.Button(isIndoor ? "Water Pots##tend" : "Water Plot##tend", new Vector2(btnW, 30f)))
        { SetupFarmingContext(); _plugin.Farming.WaterNearestBed(); }
        ImGui.PopStyleColor(3);
        ImGui.EndDisabled();
        ImGui.SameLine(0f, 8f);

        ImGui.BeginDisabled(isBusy || !hasFert);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.22f, 0.31f, 0.11f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.22f, 0.31f, 0.11f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.66f, 0.85f, 0.38f, 1f));
        if (ImGui.Button(isIndoor ? "Fertilize Pots##tend" : "Fertilize Plot##tend", new Vector2(btnW, 30f)))
        { SetupFarmingContext(); _plugin.Farming.StartFertilizing(); }
        ImGui.PopStyleColor(3);
        ImGui.EndDisabled();

        if (!hasFert) { ImGui.SameLine(); ImGui.TextColored(new Vector4(1f, 0.25f, 0.25f, 1f), "Out of Fertilizer!"); }

        ImGui.SameLine(0f, 8f);

        ImGui.BeginDisabled(!isBusy);
        ImGui.PushStyleColor(ImGuiCol.Button, _t.ToU32(_t.Danger, 0.28f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.Danger, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.Text, _t.ToU32(_t.DangerHi));
        if (ImGui.Button("Stop##tend", new Vector2(btnW, 30f)))
        { _plugin.AutoReplant.Abort(); _plugin.Farming.Stop(); }
        ImGui.PopStyleColor(3);
        ImGui.EndDisabled();
    }

    // ─────────────────────────────────────────────────────────
    //  HARVEST CONTROLS
    // ─────────────────────────────────────────────────────────
    private void DrawHarvestControls(GardenBedState[]? beds, bool hasMatureCrops)
    {
        bool isBusy = _plugin.Farming.CurrentState != FarmingState.Idle;

        // ── Mode selector (2 buttons) ──
        float mW = (ImGui.GetContentRegionAvail().X - 8f) * .5f;

        bool mode0Active = _harvestMode == 0;
        ImGui.PushStyleColor(ImGuiCol.Button, mode0Active ? _t.ToU32(_t.Panel3) : _t.ToU32(_t.Panel2));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.PanelAlt));
        ImGui.PushStyleColor(ImGuiCol.Text, mode0Active ? _t.ToU32(_t.AccentHi) : _t.ToU32(_t.TextMuted));
        if (ImGui.Button("Harvest All##hm0", new Vector2(mW, 32f)))
        {
            _harvestMode = 0;
            // Clear any 5×3 auto-protections — user wants to harvest everything
            var toRemove = _protMap.Keys.ToList();
            foreach (var k in toRemove) _protMap[k] = false;
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine(0f, 8f);

        bool mode1Active = _harvestMode == 1;
        ImGui.PushStyleColor(ImGuiCol.Button, mode1Active ? _t.ToU32(_t.Panel3) : _t.ToU32(_t.Panel2));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.PanelAlt));
        ImGui.PushStyleColor(ImGuiCol.Text, mode1Active ? _t.ToU32(_t.AccentHi) : _t.ToU32(_t.TextMuted));
        if (ImGui.Button("5x3 Sustain##hm1", new Vector2(mW, 32f)))
        {
            _harvestMode = 1;
            // Auto-protect TM(1), BL(6), BR(4) for the current plot key
            // We don't have plotKey here directly, so we apply to any bed index
            // matching those positions in the current _lastHarvestPlotKey
            Apply5x3Protections(_lastHarvestPlotKey);
        }
        ImGui.PopStyleColor(3);

        ImGuiHelpers.ScaledDummy(4f);

        // ── Mode description card ──
        string modeDesc = _harvestMode == 0
            ? "Harvest every bed that isn't manually skipped. No replant. Safe default."
            : "Always harvest the 5 beds. TM / BL / BR are locked as permanent neighbours.";

        var cPos = ImGui.GetCursorScreenPos();
        float cw = ImGui.GetContentRegionAvail().X;
        float ch = ImGui.GetTextLineHeightWithSpacing() + 10f;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(cPos, cPos + new Vector2(cw, ch), _t.ToU32(_t.Panel2), 4f);
        dl.AddRectFilled(cPos, cPos + new Vector2(3f, ch), _t.ToU32(_t.Accent), 4f);
        dl.AddText(cPos + new Vector2(9f, 5f), _t.ToU32(_t.TextSub), modeDesc);
        ImGui.Dummy(new Vector2(cw, ch));

        ImGuiHelpers.ScaledDummy(4f);

        // ── Hint ──
        ImGui.TextDisabled(_harvestMode == 0
            ? "Click any bed to skip it.  Click again to re-enable."
            : "Blue beds stay planted. Click any bed to manually toggle skip.");
        ImGuiHelpers.ScaledDummy(4f);

        // ── Run Full Cycle (Auto-Replant orchestrator) ──
        // Single click runs harvest → replant → fertilize → water for the
        // current plot, with parent slots derived from _protMap (5x3 Sustain
        // mode) or none (Harvest All mode = bulk). Skips beds short on
        // seeds/soil; emits an end-of-cycle summary in chat.
        bool orchestratorActive = _plugin.AutoReplant.IsActive;
        ImGui.BeginDisabled(isBusy || orchestratorActive);
        ImGui.PushStyleColor(ImGuiCol.Button, _t.ToU32(_t.Crystal, 0.40f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.Crystal, 0.62f));
        ImGui.PushStyleColor(ImGuiCol.Text, _t.ToU32(_t.CrystalHi));
        if (ImGui.Button("Run Full Cycle##autoreplant", new Vector2(-1f, 28f)))
        {
            SyncSkipMapToBedState();
            SetupFarmingContext();
            if (_plugin.GardenContext.IsIndoors)
                _plugin.AutoReplant.StartIndoorCycle();
            else
                _plugin.AutoReplant.StartOutdoorCycle(ComputeParentSlotsForCurrentPlot());
        }
        ImGui.PopStyleColor(3);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Runs harvest -> replant -> fertilize -> water for the current plot.\n"
                + "5x3 Sustain mode keeps the blue (KEEP) beds planted while harvesting + replanting\n"
                + "the others first, then handles the parents. Skips beds short on seeds/soil and\n"
                + "logs a summary at the end.");

        ImGuiHelpers.ScaledDummy(4f);

        // ── Action buttons ──
        float btnW = (ImGui.GetContentRegionAvail().X - 8f) * .65f;

        ImGui.BeginDisabled(isBusy || !hasMatureCrops || orchestratorActive);
        ImGui.PushStyleColor(ImGuiCol.Button, _t.ToU32(_t.Success, 0.38f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.Success, 0.62f));
        ImGui.PushStyleColor(ImGuiCol.Text, _t.ToU32(_t.SuccessHi));
        if (ImGui.Button("Harvest Ready Plots##harv", new Vector2(btnW, 30f)))
        { SyncSkipMapToBedState(); SetupFarmingContext(); _plugin.Farming.HarvestNearestBed(); }
        ImGui.PopStyleColor(3);
        ImGui.EndDisabled();

        ImGui.SameLine(0f, 8f);

        ImGui.BeginDisabled(!isBusy && !orchestratorActive);
        ImGui.PushStyleColor(ImGuiCol.Button, _t.ToU32(_t.Danger, 0.28f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, _t.ToU32(_t.Danger, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.Text, _t.ToU32(_t.DangerHi));
        if (ImGui.Button("Stop##harv", new Vector2(-1f, 30f)))
        { _plugin.AutoReplant.Abort(); _plugin.Farming.Stop(); }
        ImGui.PopStyleColor(3);
        ImGui.EndDisabled();
    }

    // Map the UI's _protMap (5x3 KEEP slots) into the slot-index set the
    // orchestrator needs as "parents". Returns null when no protections are
    // active so the orchestrator falls back to bulk Harvest All mode.
    private HashSet<int>? ComputeParentSlotsForCurrentPlot()
    {
        if (string.IsNullOrEmpty(_lastHarvestPlotKey)) return null;
        var parents = new HashSet<int>();
        foreach (var (key, isProt) in _protMap)
        {
            if (!isProt) continue;
            // keys look like "{plotKey}_{bedIdx}"
            int us = key.LastIndexOf('_');
            if (us <= 0) continue;
            string prefix = key.Substring(0, us);
            if (prefix != _lastHarvestPlotKey) continue;
            if (int.TryParse(key.Substring(us + 1), out int bed)) parents.Add(bed);
        }
        return parents.Count == 0 ? null : parents;
    }

    // Apply 5×3 auto-protections to beds TM(1), BL(6), BR(4)
    private void Apply5x3Protections(string plotKey)
    {
        if (string.IsNullOrEmpty(plotKey)) return;
        foreach (int bedIdx in FiveThreeProtected)
        {
            string k = $"{plotKey}_{bedIdx}";
            _protMap[k] = true;
            _skipMap.Remove(k); // clear any manual skip on these beds
        }
    }

    // ─────────────────────────────────────────────────────────
    //  STATUS BAR
    // ─────────────────────────────────────────────────────────
    private void DrawStatusBar(string? text = null, bool warn = false)
    {
        bool isWarning = Environment.TickCount64 < _plugin.Farming.LastWarningTime;
        string statusText = text ?? (isWarning
            ? _plugin.Farming.LastWarningMessage
            : _plugin.Farming.CurrentState == FarmingState.Idle ? "Idle"
            : $"Active: {_plugin.Farming.CurrentState}");
        uint textCol = warn || isWarning ? _t.ToU32(new Vector4(1f, 0.3f, 0.3f, 1f)) : _t.ToU32(_t.TextSub);

        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        const float h = 24f;
        draw.AddRectFilled(pos, pos + new Vector2(w, h), _t.ToU32(_t.Panel), 4f);
        draw.AddRect(pos, pos + new Vector2(w, h), _t.ToU32(_t.Border), 4f, 0, 1f);
        bool isActive = _plugin.Farming.CurrentState != FarmingState.Idle;
        uint dotCol = warn ? _t.ToU32(_t.Warning) : isActive ? _t.ToU32(_t.SuccessHi) : _t.ToU32(_t.TextDim);
        draw.AddCircleFilled(pos + new Vector2(12f, h * .5f), 4f, dotCol);
        draw.AddText(pos + new Vector2(22f, (h - ImGui.GetTextLineHeight()) * .5f), textCol, statusText);
        ImGui.Dummy(new Vector2(w, h));
    }

    // ─────────────────────────────────────────────────────────
    //  PLOT RELINK ERROR
    //  Shown when a plot exists in config but has no GPS anchor.
    //  This happens if the garden was moved, reset, or never linked.
    // ─────────────────────────────────────────────────────────
    private void DrawPlotRelinkError(string plotLabel)
    {
        ImGuiHelpers.ScaledDummy(12f);

        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        const float bannerH = 28f;

        // Red "Garden not found" banner
        draw.AddRectFilled(pos, pos + new Vector2(w, bannerH), _t.ToU32(_t.Danger, 0.22f), 5f);
        draw.AddRect(pos, pos + new Vector2(w, bannerH), _t.ToU32(_t.Danger, 0.65f), 5f, 0, 1.5f);
        string errText = $"{plotLabel}  —  Garden not found";
        var es = ImGui.CalcTextSize(errText);
        draw.AddText(new Vector2(pos.X + (w - es.X) * .5f, pos.Y + (bannerH - es.Y) * .5f),
                     _t.ToU32(_t.DangerHi), errText);
        ImGui.Dummy(new Vector2(w, bannerH));

        ImGuiHelpers.ScaledDummy(16f);

        // Instructions card
        var cPos = ImGui.GetCursorScreenPos();
        float cardH = ImGui.GetTextLineHeightWithSpacing() * 5f + 20f;
        draw.AddRectFilled(cPos, cPos + new Vector2(w, cardH), _t.ToU32(_t.Panel2), 6f);
        draw.AddRect(cPos, cPos + new Vector2(w, cardH), _t.ToU32(_t.Border), 6f, 0, 1f);
        draw.AddRectFilled(cPos, cPos + new Vector2(3f, cardH), _t.ToU32(_t.Warning), 6f);

        float ty = cPos.Y + 10f;
        float lh = ImGui.GetTextLineHeightWithSpacing();

        draw.AddText(new Vector2(cPos.X + 12f, ty), _t.ToU32(_t.AccentHi),
            "This garden plot has no GPS anchor.");
        ty += lh;
        draw.AddText(new Vector2(cPos.X + 12f, ty), _t.ToU32(_t.TextSub),
            "The garden may have been moved, demolished, or was never linked.");
        ty += lh * 1.4f;
        draw.AddText(new Vector2(cPos.X + 12f, ty), _t.ToU32(_t.Text),
            "To relink:");
        ty += lh;
        draw.AddText(new Vector2(cPos.X + 22f, ty), _t.ToU32(_t.TextSub),
            "1. Stand at the garden patch");
        ty += lh;
        draw.AddText(new Vector2(cPos.X + 22f, ty), _t.ToU32(_t.TextSub),
            "2. Open the Status tab and click [Link GPS] for this plot");
        ImGui.Dummy(new Vector2(w, cardH));
    }

    // ─────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────
    private void SetupFarmingContext()
    {
        _plugin.Farming.IsIndoors = _plugin.GardenContext.IsIndoors;
        _plugin.Farming.ActiveIndoorPots = _plugin.GardenContext.ActiveBeds!;
        if (!_plugin.GardenContext.IsIndoors && _plugin.GardenContext.ActivePlot != null)
        {
            var p = _plugin.GardenContext.ActivePlot;
            var profile = _plugin.GetCurrentCharacterProfile();
            if (profile == null) return;
            bool isPersonal = profile.PersonalPlots.Contains(p);
            _plugin.Farming.IsPersonalPlot = isPersonal;
            _plugin.Farming.CurrentPlotIndex = isPersonal
                ? Array.IndexOf(profile.PersonalPlots, p)
                : Array.IndexOf(profile.FCPlots, p);
        }
    }

    // Project the UI's KEEP (blue) / SKIP (red) maps into bedState.SkipHarvest
    // so the FarmingManager harvest guard at FarmingManager.cs:600 respects them.
    // Without this, the visual markers were cosmetic-only and Harvest All ripped
    // up every marked bed. Called from the Harvest button; full overwrite — beds
    // not marked end up with SkipHarvest=false, so removing a marker takes effect.
    private void SyncSkipMapToBedState()
    {
        GardenBedState[]? beds;
        string plotKey;
        if (_plugin.GardenContext.IsIndoors)
        {
            beds = _plugin.GardenContext.ActiveBeds;
            plotKey = "IndoorPots";
        }
        else
        {
            beds = _plugin.GardenContext.ActivePlot?.Beds;
            plotKey = _lastHarvestPlotKey;
        }
        if (beds == null || string.IsNullOrEmpty(plotKey)) return;

        for (int i = 0; i < beds.Length; i++)
        {
            if (beds[i] == null) continue;
            string key = $"{plotKey}_{i}";
            bool skip = (_skipMap.TryGetValue(key, out bool s) && s)
                     || (_protMap.TryGetValue(key, out bool p) && p);
            beds[i].SkipHarvest = skip;
        }
        _plugin.Configuration.Save();
    }

    private string CalcTopYield(GardenPlotState plot)
    {
        if (!_plugin.CrossbreedManager.IsLoaded) return "";
        var yields = new HashSet<string>();
        for (int i = 0; i < plot.BedCount && i < plot.Beds.Length; i++)
        {
            string sA = plot.Beds[i].SeedName;
            int pr = (i + plot.BedCount - 1) % plot.BedCount;
            string sB = plot.Beds[pr].SeedName;
            if (string.IsNullOrEmpty(sA) || string.IsNullOrEmpty(sB) || sA == "Empty" || sB == "Empty") continue;
            string cA = GardenData.SeedToCropMap.TryGetValue(sA, out var ca) ? ca : sA;
            string cB = GardenData.SeedToCropMap.TryGetValue(sB, out var cb) ? cb : sB;
            string res = _plugin.CrossbreedManager.GetCross(cA, cB);
            if (res == "Unknown / None") continue;
            foreach (var y in res.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                string t = y.Trim();
                if (!string.IsNullOrWhiteSpace(t)) yields.Add(t);
            }
        }
        return yields.Count == 0 ? "" : yields.OrderByDescending(GardenData.GetYieldScore).First();
    }

    private static bool IsThirsty(GardenBedState b)
        => b.LastWateredTime != DateTime.MinValue && (DateTime.Now - b.LastWateredTime).TotalHours > 12.0;

    private static bool AnyMature(GardenBedState[]? beds)
    {
        if (beds == null) return false;
        foreach (var b in beds) if (b?.IsMature == true) return true;
        return false;
    }
}