namespace Gaia.Core.UI;

// ──────────────────────────────────────────────────────────────
//  ThemePalette  —  every colour a theme needs in one place.
//
//  Two access patterns:
//    Vector4  → ImGui.PushStyleColor(ImGuiCol.X, palette.Accent)
//    uint     → ColorUtil.ToU32(palette.Accent)
//
//  StandardWindow reads the core seeds + IsLight flag to push the
//  full ~40 ImGuiCol set automatically in PreDraw/PostDraw.
//  Tabs and custom rendering use the extended fields directly.
// ──────────────────────────────────────────────────────────────
public class ThemePalette
{
    /// <summary>Human-readable name shown in theme pickers.</summary>
    public string DisplayName = "";

    /// <summary>
    /// When true, StandardWindow uses light-mode derivations
    /// (white-ish FrameBg, darker accent fills, etc).
    /// </summary>
    public bool IsLight = false;

    // ── Core seeds ─────────────────────────────────────────────
    // StandardWindow derives all ~40 ImGui colours from these.
    //
    public Vector4 Background;    // main window background
    public Vector4 Panel;         // card / content area fill
    public Vector4 Title;         // title bar background
    public Vector4 Accent;        // primary brand colour
    public Vector4 AccentHi;      // bright accent (hover, active text)
    public Vector4 AccentActive;  // pressed / active accent
    public Vector4 Text;          // primary readable text
    public Vector4 TextDim;       // secondary / muted text
    public Vector4 Border;        // default border (alpha may vary)

    // ── Extended surfaces ──────────────────────────────────────
    // Used by custom draw-list rendering (slot grids, cards, etc).
    // If not set explicitly, they default to derivations from core seeds.
    //
    public Vector4 Panel2;        // slightly lighter panel (rows, cards)
    public Vector4 Panel3;        // active / selected panel (brightest)
    public Vector4 PanelAlt;      // hover-state panel
    public Vector4 BorderHi;      // highlighted / hovered border

    // ── Extended text ──────────────────────────────────────────
    public Vector4 TextSub;       // secondary label text
    public Vector4 TextMuted;     // description text (dimmer than Sub)

    // ── Semantic colours ───────────────────────────────────────
    public Vector4 Success;       // green  — ready / healthy / watered
    public Vector4 SuccessHi;     // bright green for glows / text
    public Vector4 Warning;       // amber  — needs attention
    public Vector4 Danger;        // red    — error / dead / wilted
    public Vector4 DangerHi;      // bright red for glows / text

    // ── Special ────────────────────────────────────────────────
    public Vector4 Crystal;       // teal-blue (info panel, FC board)
    public Vector4 CrystalHi;     // bright crystal
    public Vector4 Amber;         // orange (fertilizer bar)
    public Vector4 Progress;      // progress bar fill

    // ── Convenience ────────────────────────────────────────────

    /// <summary>Convert a palette colour to uint for ImDrawList.</summary>
    public uint ToU32(Vector4 c) => ColorUtil.ToU32(c);

    /// <summary>Convert a palette colour to uint with alpha override.</summary>
    public uint ToU32(Vector4 c, float alpha) => ColorUtil.ToU32(c, alpha);
}
