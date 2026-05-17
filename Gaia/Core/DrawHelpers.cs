namespace Gaia.Core.UI;

/// <summary>
/// Reusable ImDrawList primitives used across Gaia.
/// Every method takes a ThemePalette so it works with any theme.
/// </summary>
public static class DrawHelpers
{
    // ──────────────────────────────────────────────────────────
    //  Section title: centered text with side rules
    // ──────────────────────────────────────────────────────────
    public static void SectionTitle(ThemePalette t, string title, float totalW)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float lh = ImGui.GetTextLineHeight();
        var sz   = ImGui.CalcTextSize(title.ToUpper());
        float tx = pos.X + (totalW - sz.X) / 2f;

        dl.AddText(new Vector2(tx, pos.Y),
            ColorUtil.ToU32(t.TextMuted), title.ToUpper());

        float ly = pos.Y + lh / 2f;
        uint lc  = ColorUtil.ToU32(t.TextMuted, 0.18f);
        dl.AddLine(new Vector2(pos.X, ly),          new Vector2(tx - 10f, ly),        lc, 1f);
        dl.AddLine(new Vector2(tx + sz.X + 10f, ly), new Vector2(pos.X + totalW, ly), lc, 1f);

        ImGui.Dummy(new Vector2(totalW, lh + 2f));
    }

    // ──────────────────────────────────────────────────────────
    //  Dark inset card with border — call BeginCard, draw content
    //  at the cursor position, then call EndCard.
    // ──────────────────────────────────────────────────────────
    public static void BeginCard(ThemePalette t, float w, float h, float rounding = 8f)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        dl.AddRectFilled(pos, pos + new Vector2(w, h),
            ColorUtil.ToU32(t.Panel2), rounding);
        dl.AddRect(pos, pos + new Vector2(w, h),
            ColorUtil.ToU32(t.Border), rounding, ImDrawFlags.None, 1f);
    }

    public static void EndCard(float w, float h)
    {
        ImGui.Dummy(new Vector2(w, h));
    }

    // ──────────────────────────────────────────────────────────
    //  Record row — key / value right-aligned on a subtle bg
    // ──────────────────────────────────────────────────────────
    public static void RecordRow(ThemePalette t, string key, string value)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        float h = 24f;

        dl.AddRectFilled(pos, pos + new Vector2(w, h),
            ColorUtil.ToU32(t.Panel2, 0.55f), 5f);

        float baseY = pos.Y + (h - ImGui.GetTextLineHeight()) / 2f;
        dl.AddText(new Vector2(pos.X + 10f, baseY),
            ColorUtil.ToU32(t.TextMuted), key);

        var valSz = ImGui.CalcTextSize(value);
        dl.AddText(new Vector2(pos.X + w - valSz.X - 10f, baseY),
            ColorUtil.ToU32(t.Text), value);

        ImGui.Dummy(new Vector2(w, h));
        ImGui.Spacing();
    }

    // ──────────────────────────────────────────────────────────
    //  Metric card — label, large value, subtitle, accent bar
    // ──────────────────────────────────────────────────────────
    public static void MetricCard(ThemePalette t, string label, string value,
        string sub, float w, float h, Vector4 accent)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();

        dl.AddRectFilled(pos, pos + new Vector2(w, h),
            ColorUtil.ToU32(t.Panel2), 8f);
        dl.AddRect(pos, pos + new Vector2(w, h),
            ColorUtil.ToU32(t.Border), 8f, ImDrawFlags.None, 1f);
        dl.AddRectFilled(pos + new Vector2(0, 8f), pos + new Vector2(3f, h - 8f),
            ColorUtil.ToU32(accent, 0.70f), 2f);

        float maxTextW = w - 16f;

        // Label
        string lbl = label.ToUpper();
        while (lbl.Length > 2 && ImGui.CalcTextSize(lbl).X > maxTextW) lbl = lbl[..^1];
        dl.AddText(pos + new Vector2(12f, 10f),
            ColorUtil.ToU32(t.TextMuted), lbl);

        // Value
        dl.AddText(pos + new Vector2(12f, 28f),
            ColorUtil.ToU32(t.Text), value);

        // Sub
        string s = sub;
        while (s.Length > 2 && ImGui.CalcTextSize(s).X > maxTextW) s = s[..^1];
        dl.AddText(pos + new Vector2(12f, 52f),
            ColorUtil.ToU32(t.TextMuted, 0.60f), s);

        ImGui.Dummy(new Vector2(w, h));
    }

    // ──────────────────────────────────────────────────────────
    //  Stat bar — label | filled track | percentage text
    //  value is 0.0–1.0
    // ──────────────────────────────────────────────────────────
    public static void StatBar(ThemePalette t, ImDrawListPtr dl,
        float x, float y, float totalW,
        string label, float value, float labelW, Vector4 fillColor)
    {
        float lh   = ImGui.GetTextLineHeight();
        float pctW = 36f;
        float barW = totalW - labelW - pctW;
        float barH = 5f;
        float barY = y + (lh - barH) / 2f + 1f;

        dl.AddText(new Vector2(x, y),
            ColorUtil.ToU32(t.TextMuted), label);

        var trackTL = new Vector2(x + labelW, barY);
        var trackBR = new Vector2(trackTL.X + barW, barY + barH);
        dl.AddRectFilled(trackTL, trackBR,
            ColorUtil.ToU32(t.Text, 0.06f), 3f);

        float fw = barW * Math.Clamp(value, 0f, 1f);
        if (fw > 0.5f)
            dl.AddRectFilled(trackTL, new Vector2(trackTL.X + fw, trackBR.Y),
                ColorUtil.ToU32(fillColor), 3f);

        dl.AddText(new Vector2(trackBR.X + 4f, y),
            ColorUtil.ToU32(t.Accent),
            $"{(int)(value * 100f)}%");
    }

    // ──────────────────────────────────────────────────────────
    //  Mini stat bar — compact variant for popout windows
    // ──────────────────────────────────────────────────────────
    public static void MiniStatBar(ThemePalette t, ImDrawListPtr dl,
        float x, float y, float w,
        string label, float value, Vector4 fillColor)
    {
        float lh     = 12f;
        float labelW = 18f;
        float pctW   = 30f;
        float barW   = w - labelW - pctW;
        float barH   = 4f;
        float barY   = y + (lh - barH) / 2f;

        dl.AddText(new Vector2(x, y - 1f),
            ColorUtil.ToU32(t.TextMuted, 0.65f), label);

        var tTL = new Vector2(x + labelW, barY);
        var tBR = new Vector2(tTL.X + barW, barY + barH);
        dl.AddRectFilled(tTL, tBR,
            ColorUtil.ToU32(t.Text, 0.06f), 2f);

        float fw = barW * Math.Clamp(value, 0f, 1f);
        if (fw > 0.5f)
            dl.AddRectFilled(tTL, new Vector2(tTL.X + fw, tBR.Y),
                ColorUtil.ToU32(fillColor), 2f);

        dl.AddText(new Vector2(tBR.X + 3f, y - 1f),
            ColorUtil.ToU32(t.Accent, 0.80f),
            $"{(int)(value * 100f)}%");
    }

    // ──────────────────────────────────────────────────────────
    //  Count pill — compact count badge
    // ──────────────────────────────────────────────────────────
    public static void CountPill(ThemePalette t, string text)
    {
        var dl  = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var sz  = ImGui.CalcTextSize(text);
        float pw = sz.X + 14f;
        float ph = sz.Y + 6f;
        dl.AddRectFilled(pos, pos + new Vector2(pw, ph),
            ColorUtil.ToU32(t.Panel2), 10f);
        dl.AddRect(pos, pos + new Vector2(pw, ph),
            ColorUtil.ToU32(t.Accent, 0.40f), 10f, ImDrawFlags.None, 0.8f);
        dl.AddText(pos + new Vector2(7f, 3f),
            ColorUtil.ToU32(t.Accent), text);
        ImGui.Dummy(new Vector2(pw, ph));
    }

    // ──────────────────────────────────────────────────────────
    //  Pill tag — generic colored pill (extracted from emote tags)
    // ──────────────────────────────────────────────────────────
    public static void PillTag(ImDrawListPtr dl, Vector2 pos, string text,
        Vector4 bgColor, Vector4 fgColor)
    {
        var sz   = ImGui.CalcTextSize(text);
        float ph = 6f, pv = 3f;
        var br   = pos + new Vector2(sz.X + ph * 2, sz.Y + pv * 2);
        dl.AddRectFilled(pos, br, ColorUtil.ToU32(bgColor), 8f);
        dl.AddRect(pos, br, ColorUtil.ToU32(fgColor, 0.35f), 8f, ImDrawFlags.None, 0.8f);
        dl.AddText(pos + new Vector2(ph, pv), ColorUtil.ToU32(fgColor), text);
    }

    // ──────────────────────────────────────────────────────────
    //  Placeholder tab — "Coming soon" message for stub tabs
    // ──────────────────────────────────────────────────────────
    public static void PlaceholderTab(ThemePalette t, string heading,
        params string[] lines)
    {
        ImGui.Dummy(new Vector2(0, 24f));
        ImGui.TextColored(t.AccentHi, heading);
        ImGui.Dummy(new Vector2(0, 6f));
        foreach (var line in lines)
            ImGui.TextDisabled(line);
        ImGui.Dummy(new Vector2(0, 12f));
        ImGui.TextColored(t.TextDim, "Coming soon.");
    }

    // ──────────────────────────────────────────────────────────
    //  Status bar — simple colored text message at bottom
    // ──────────────────────────────────────────────────────────
    public static void StatusBar(ThemePalette t, string message, Vector4? color = null)
    {
        ImGui.TextColored(color ?? t.TextMuted, message);
    }
}
