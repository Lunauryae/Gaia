namespace Gaia.Core.UI;

using static ColorUtil;

// ──────────────────────────────────────────────────────────────
//  CoreTheme  —  every theme available in Gaia.
//
//  Each enum entry maps 1:1 to a ThemePalette instance.
// ──────────────────────────────────────────────────────────────
public enum CoreTheme
{
    GardenCodex       = 0,
    FieldGuide        = 1,
    Kawaii             = 2,

    PerfectlyPurple      = 10,
    PerfectlyPurpleDay   = 11,
    PrettyInPink         = 12,
    PrettyInPinkDay      = 13,
    GlamourousGreen      = 14,
    GlamourousGreenDay   = 15,
    BeautifullyBlue      = 16,
    BeautifullyBlueDay   = 17,

    Lunar        = 20,
}

// ──────────────────────────────────────────────────────────────
//  ThemeRegistry  —  palette factory.
//
//  Field guide for StandardWindow's PreDraw:
//    Background → WindowBg, MenuBarBg, ScrollbarBg
//    Panel2     → Lerp seed for FrameBg / ChildBg / PopupBg
//    Title      → TitleBg / TitleBgActive / TitleBgCollapsed
//    Accent / AccentHi / AccentActive → buttons, tabs, sliders…
//    Text / TextDim → all text colours
//    Border     → separators, resize grips, table borders
//    IsLight    → switches derivation strategy (white FrameBg)
//
//  Extended fields (Panel, Panel3, semantic colours, etc.) are
//  for custom draw-list rendering in tabs — StandardWindow does
//  not read them.
// ──────────────────────────────────────────────────────────────
public static class ThemeRegistry
{
    /// <summary>Look up the palette for a given theme.</summary>
    public static ThemePalette Get(CoreTheme theme) => theme switch
    {
        CoreTheme.GardenCodex         => GardenCodex,
        CoreTheme.FieldGuide          => FieldGuide,
        CoreTheme.Kawaii              => Kawaii,
        CoreTheme.PerfectlyPurple     => PerfectlyPurple,
        CoreTheme.PerfectlyPurpleDay  => PerfectlyPurpleDay,
        CoreTheme.PrettyInPink        => PrettyInPink,
        CoreTheme.PrettyInPinkDay     => PrettyInPinkDay,
        CoreTheme.GlamourousGreen     => GlamourousGreen,
        CoreTheme.GlamourousGreenDay  => GlamourousGreenDay,
        CoreTheme.BeautifullyBlue     => BeautifullyBlue,
        CoreTheme.BeautifullyBlueDay  => BeautifullyBlueDay,
        CoreTheme.Lunar       => Lunar,
        _                                => GardenCodex,
    };

    /// <summary>
    /// Look up a palette by its legacy string key.
    /// Returns null if the string doesn't match any extended theme.
    /// </summary>
    public static ThemePalette? GetByLegacyName(string name) => name switch
    {
        "Perfectly Purple"       => PerfectlyPurple,
        "Perfectly Purple (Day)" => PerfectlyPurpleDay,
        "Pretty in Pink"         => PrettyInPink,
        "Pretty in Pink (Day)"   => PrettyInPinkDay,
        "Glamourous Green"       => GlamourousGreen,
        "Glamourous Green (Day)" => GlamourousGreenDay,
        "Beautifully Blue"       => BeautifullyBlue,
        "Beautifully Blue (Day)" => BeautifullyBlueDay,
        _                        => null,
    };

    /// <summary>
    /// Look up a palette by its legacy GaiaTheme int value.
    /// </summary>
    // TODO: unify GaiaTheme and CoreTheme — this bridge exists because
    // Configuration still persists the 3-value GaiaTheme enum.
    public static ThemePalette GetByLegacyIndex(int index) => index switch
    {
        1 => FieldGuide,
        2 => Kawaii,
        _ => GardenCodex,
    };

    // ═══════════════════════════════════════════════════════════
    //  GAIA THEMES
    // ═══════════════════════════════════════════════════════════

    public static readonly ThemePalette GardenCodex = new()
    {
        DisplayName  = "Garden Codex",
        IsLight      = false,

        Background   = V(0.09f, 0.06f, 0.03f),
        Panel        = V(0.13f, 0.09f, 0.04f),
        Panel2       = V(0.18f, 0.13f, 0.06f),
        Panel3       = V(0.24f, 0.18f, 0.08f),
        PanelAlt     = V(0.18f, 0.13f, 0.06f),
        Title        = V(0.13f, 0.09f, 0.04f),

        Border       = V(0.42f, 0.31f, 0.12f),
        BorderHi     = V(0.65f, 0.48f, 0.18f),

        Text         = V(0.94f, 0.90f, 0.76f),
        TextSub      = V(0.70f, 0.58f, 0.35f),
        TextMuted    = V(0.55f, 0.43f, 0.22f),
        TextDim      = V(0.38f, 0.28f, 0.13f),

        Accent       = V(0.77f, 0.57f, 0.17f),
        AccentHi     = V(0.91f, 0.72f, 0.29f),
        AccentActive = V(0.60f, 0.44f, 0.12f),

        Success      = V(0.29f, 0.55f, 0.23f),
        SuccessHi    = V(0.49f, 0.75f, 0.35f),
        Warning      = V(0.77f, 0.55f, 0.10f),
        Danger       = V(0.70f, 0.22f, 0.22f),
        DangerHi     = V(0.88f, 0.38f, 0.38f),

        Crystal      = V(0.23f, 0.50f, 0.66f),
        CrystalHi    = V(0.40f, 0.70f, 0.88f),
        Amber        = V(0.80f, 0.45f, 0.08f),
        Progress     = V(0.29f, 0.48f, 0.23f),
    };

    public static readonly ThemePalette FieldGuide = new()
    {
        DisplayName  = "Field Guide",
        IsLight      = true,

        Background   = V(0.96f, 0.91f, 0.82f),
        Panel        = V(0.91f, 0.85f, 0.72f),
        Panel2       = V(0.86f, 0.79f, 0.62f),
        Panel3       = V(0.80f, 0.72f, 0.52f),
        PanelAlt     = V(0.86f, 0.79f, 0.62f),
        Title        = V(0.42f, 0.29f, 0.10f),

        Border       = V(0.55f, 0.39f, 0.19f),
        BorderHi     = V(0.72f, 0.52f, 0.28f),

        Text         = V(0.14f, 0.09f, 0.03f),
        TextSub      = V(0.36f, 0.24f, 0.10f),
        TextMuted    = V(0.50f, 0.36f, 0.18f),
        TextDim      = V(0.65f, 0.50f, 0.28f),

        Accent       = V(0.42f, 0.29f, 0.10f),
        AccentHi     = V(0.60f, 0.42f, 0.14f),
        AccentActive = V(0.34f, 0.22f, 0.07f),

        Success      = V(0.16f, 0.38f, 0.10f),
        SuccessHi    = V(0.22f, 0.52f, 0.14f),
        Warning      = V(0.54f, 0.36f, 0.00f),
        Danger       = V(0.54f, 0.16f, 0.10f),
        DangerHi     = V(0.72f, 0.24f, 0.16f),

        Crystal      = V(0.10f, 0.35f, 0.54f),
        CrystalHi    = V(0.18f, 0.50f, 0.72f),
        Amber        = V(0.70f, 0.42f, 0.05f),
        Progress     = V(0.16f, 0.36f, 0.10f),
    };

    public static readonly ThemePalette Kawaii = new()
    {
        DisplayName  = "Kawaii",
        IsLight      = true,

        Background   = V(1.00f, 0.94f, 0.97f),
        Panel        = V(1.00f, 0.91f, 0.95f),
        Panel2       = V(0.99f, 0.87f, 0.92f),
        Panel3       = V(0.97f, 0.82f, 0.89f),
        PanelAlt     = V(0.99f, 0.87f, 0.92f),
        Title        = V(0.88f, 0.38f, 0.63f),

        Border       = V(0.91f, 0.63f, 0.76f),
        BorderHi     = V(0.95f, 0.42f, 0.65f),

        Text         = V(0.35f, 0.10f, 0.23f),
        TextSub      = V(0.58f, 0.28f, 0.42f),
        TextMuted    = V(0.72f, 0.42f, 0.56f),
        TextDim      = V(0.82f, 0.62f, 0.72f),

        Accent       = V(0.88f, 0.38f, 0.63f),
        AccentHi     = V(0.95f, 0.55f, 0.76f),
        AccentActive = V(0.72f, 0.28f, 0.50f),

        Success      = V(0.42f, 0.72f, 0.35f),
        SuccessHi    = V(0.55f, 0.85f, 0.48f),
        Warning      = V(0.90f, 0.72f, 0.22f),
        Danger       = V(0.88f, 0.38f, 0.38f),
        DangerHi     = V(0.95f, 0.55f, 0.55f),

        Crystal      = V(0.38f, 0.63f, 0.90f),
        CrystalHi    = V(0.55f, 0.78f, 0.98f),
        Amber        = V(0.90f, 0.55f, 0.18f),
        Progress     = V(0.72f, 0.45f, 0.80f),
    };

    // ═══════════════════════════════════════════════════════════
    //  EXTENDED THEMES  —  4 colours × night/day
    // ═══════════════════════════════════════════════════════════

    // ── PURPLE ──────────────────────────────────────────────────

    public static readonly ThemePalette PerfectlyPurple = new()
    {
        DisplayName  = "Perfectly Purple",
        IsLight      = false,

        Background   = V(0.10f, 0.06f, 0.20f),
        Panel        = V(0.14f, 0.08f, 0.28f),
        Panel2       = V(0.18f, 0.10f, 0.35f),
        Panel3       = V(0.24f, 0.14f, 0.44f),
        PanelAlt     = V(0.18f, 0.10f, 0.35f),
        Title        = V(0.14f, 0.08f, 0.27f),

        Border       = V(0.36f, 0.22f, 0.66f, 0.50f),
        BorderHi     = V(0.55f, 0.36f, 0.96f),

        Text         = V(0.94f, 0.91f, 1.00f),
        TextSub      = V(0.77f, 0.71f, 0.99f),
        TextMuted    = V(0.61f, 0.50f, 0.83f),
        TextDim      = V(0.61f, 0.50f, 0.83f),

        Accent       = V(0.55f, 0.36f, 0.96f),
        AccentHi     = V(0.65f, 0.48f, 1.00f),
        AccentActive = V(0.44f, 0.27f, 0.80f),

        Success      = V(0.29f, 0.68f, 0.50f),
        SuccessHi    = V(0.42f, 0.82f, 0.62f),
        Warning      = V(0.98f, 0.75f, 0.14f),
        Danger       = V(0.97f, 0.44f, 0.44f),
        DangerHi     = V(1.00f, 0.55f, 0.55f),

        Crystal      = V(0.18f, 0.83f, 0.75f),
        CrystalHi    = V(0.30f, 0.92f, 0.85f),
        Amber        = V(0.98f, 0.75f, 0.14f),
        Progress     = V(0.55f, 0.36f, 0.96f),
    };

    public static readonly ThemePalette PerfectlyPurpleDay = new()
    {
        DisplayName  = "Perfectly Purple (Day)",
        IsLight      = true,

        Background   = V(0.94f, 0.91f, 0.98f),
        Panel        = V(0.86f, 0.80f, 0.95f),
        Panel2       = V(0.80f, 0.74f, 0.92f),
        Panel3       = V(0.74f, 0.66f, 0.88f),
        PanelAlt     = V(0.80f, 0.74f, 0.92f),
        Title        = V(0.44f, 0.26f, 0.78f),

        Border       = V(0.55f, 0.40f, 0.85f, 0.45f),
        BorderHi     = V(0.44f, 0.26f, 0.82f),

        Text         = V(0.18f, 0.09f, 0.32f),
        TextSub      = V(0.30f, 0.18f, 0.50f),
        TextMuted    = V(0.40f, 0.28f, 0.58f),
        TextDim      = V(0.40f, 0.28f, 0.58f),

        Accent       = V(0.44f, 0.26f, 0.82f),
        AccentHi     = V(0.34f, 0.18f, 0.70f),
        AccentActive = V(0.28f, 0.14f, 0.60f),

        Success      = V(0.16f, 0.50f, 0.32f),
        SuccessHi    = V(0.22f, 0.62f, 0.40f),
        Warning      = V(0.80f, 0.60f, 0.10f),
        Danger       = V(0.82f, 0.30f, 0.30f),
        DangerHi     = V(0.92f, 0.40f, 0.40f),

        Crystal      = V(0.10f, 0.60f, 0.54f),
        CrystalHi    = V(0.18f, 0.72f, 0.64f),
        Amber        = V(0.80f, 0.60f, 0.10f),
        Progress     = V(0.44f, 0.26f, 0.82f),
    };

    // ── PINK ────────────────────────────────────────────────────

    public static readonly ThemePalette PrettyInPink = new()
    {
        DisplayName  = "Pretty in Pink",
        IsLight      = false,

        Background   = V(0.13f, 0.05f, 0.09f),
        Panel        = V(0.18f, 0.06f, 0.12f),
        Panel2       = V(0.22f, 0.08f, 0.15f),
        Panel3       = V(0.30f, 0.10f, 0.20f),
        PanelAlt     = V(0.22f, 0.08f, 0.15f),
        Title        = V(0.18f, 0.06f, 0.12f),

        Border       = V(0.82f, 0.22f, 0.48f, 0.45f),
        BorderHi     = V(0.98f, 0.25f, 0.58f),

        Text         = V(1.00f, 0.93f, 0.96f),
        TextSub      = V(1.00f, 0.62f, 0.82f),
        TextMuted    = V(0.82f, 0.58f, 0.70f),
        TextDim      = V(0.82f, 0.58f, 0.70f),

        Accent       = V(0.98f, 0.25f, 0.58f),
        AccentHi     = V(1.00f, 0.42f, 0.68f),
        AccentActive = V(0.82f, 0.16f, 0.44f),

        Success      = V(0.29f, 0.68f, 0.50f),
        SuccessHi    = V(0.42f, 0.82f, 0.62f),
        Warning      = V(0.98f, 0.75f, 0.14f),
        Danger       = V(0.97f, 0.44f, 0.44f),
        DangerHi     = V(1.00f, 0.55f, 0.55f),

        Crystal      = V(0.18f, 0.83f, 0.75f),
        CrystalHi    = V(0.30f, 0.92f, 0.85f),
        Amber        = V(0.98f, 0.75f, 0.14f),
        Progress     = V(0.98f, 0.25f, 0.58f),
    };

    public static readonly ThemePalette PrettyInPinkDay = new()
    {
        DisplayName  = "Pretty in Pink (Day)",
        IsLight      = true,

        Background   = V(1.00f, 0.93f, 0.96f),
        Panel        = V(0.99f, 0.84f, 0.90f),
        Panel2       = V(0.97f, 0.78f, 0.86f),
        Panel3       = V(0.94f, 0.70f, 0.80f),
        PanelAlt     = V(0.97f, 0.78f, 0.86f),
        Title        = V(0.88f, 0.20f, 0.50f),

        Border       = V(0.92f, 0.55f, 0.70f, 0.55f),
        BorderHi     = V(0.88f, 0.18f, 0.48f),

        Text         = V(0.28f, 0.06f, 0.14f),
        TextSub      = V(0.42f, 0.12f, 0.24f),
        TextMuted    = V(0.55f, 0.22f, 0.36f),
        TextDim      = V(0.55f, 0.22f, 0.36f),

        Accent       = V(0.88f, 0.18f, 0.48f),
        AccentHi     = V(0.72f, 0.10f, 0.36f),
        AccentActive = V(0.60f, 0.06f, 0.28f),

        Success      = V(0.16f, 0.50f, 0.32f),
        SuccessHi    = V(0.22f, 0.62f, 0.40f),
        Warning      = V(0.80f, 0.60f, 0.10f),
        Danger       = V(0.82f, 0.30f, 0.30f),
        DangerHi     = V(0.92f, 0.40f, 0.40f),

        Crystal      = V(0.10f, 0.60f, 0.54f),
        CrystalHi    = V(0.18f, 0.72f, 0.64f),
        Amber        = V(0.80f, 0.60f, 0.10f),
        Progress     = V(0.88f, 0.18f, 0.48f),
    };

    // ── GREEN ───────────────────────────────────────────────────

    public static readonly ThemePalette GlamourousGreen = new()
    {
        DisplayName  = "Glamourous Green",
        IsLight      = false,

        Background   = V(0.04f, 0.12f, 0.08f),
        Panel        = V(0.06f, 0.14f, 0.09f),
        Panel2       = V(0.07f, 0.20f, 0.13f),
        Panel3       = V(0.10f, 0.26f, 0.16f),
        PanelAlt     = V(0.07f, 0.20f, 0.13f),
        Title        = V(0.04f, 0.15f, 0.10f),

        Border       = V(0.20f, 0.64f, 0.38f, 0.45f),
        BorderHi     = V(0.22f, 0.82f, 0.50f),

        Text         = V(0.88f, 1.00f, 0.92f),
        TextSub      = V(0.50f, 0.95f, 0.72f),
        TextMuted    = V(0.48f, 0.76f, 0.58f),
        TextDim      = V(0.48f, 0.76f, 0.58f),

        Accent       = V(0.22f, 0.82f, 0.50f),
        AccentHi     = V(0.30f, 0.94f, 0.60f),
        AccentActive = V(0.14f, 0.64f, 0.36f),

        Success      = V(0.29f, 0.68f, 0.50f),
        SuccessHi    = V(0.42f, 0.82f, 0.62f),
        Warning      = V(0.98f, 0.75f, 0.14f),
        Danger       = V(0.97f, 0.44f, 0.44f),
        DangerHi     = V(1.00f, 0.55f, 0.55f),

        Crystal      = V(0.18f, 0.83f, 0.75f),
        CrystalHi    = V(0.30f, 0.92f, 0.85f),
        Amber        = V(0.98f, 0.75f, 0.14f),
        Progress     = V(0.22f, 0.82f, 0.50f),
    };

    public static readonly ThemePalette GlamourousGreenDay = new()
    {
        DisplayName  = "Glamourous Green (Day)",
        IsLight      = true,

        Background   = V(0.91f, 0.98f, 0.93f),
        Panel        = V(0.80f, 0.94f, 0.84f),
        Panel2       = V(0.74f, 0.90f, 0.78f),
        Panel3       = V(0.66f, 0.84f, 0.72f),
        PanelAlt     = V(0.74f, 0.90f, 0.78f),
        Title        = V(0.10f, 0.58f, 0.30f),

        Border       = V(0.22f, 0.72f, 0.42f, 0.45f),
        BorderHi     = V(0.10f, 0.55f, 0.28f),

        Text         = V(0.04f, 0.18f, 0.09f),
        TextSub      = V(0.08f, 0.30f, 0.16f),
        TextMuted    = V(0.16f, 0.42f, 0.24f),
        TextDim      = V(0.16f, 0.42f, 0.24f),

        Accent       = V(0.10f, 0.55f, 0.28f),
        AccentHi     = V(0.06f, 0.44f, 0.22f),
        AccentActive = V(0.04f, 0.34f, 0.16f),

        Success      = V(0.16f, 0.50f, 0.32f),
        SuccessHi    = V(0.22f, 0.62f, 0.40f),
        Warning      = V(0.80f, 0.60f, 0.10f),
        Danger       = V(0.82f, 0.30f, 0.30f),
        DangerHi     = V(0.92f, 0.40f, 0.40f),

        Crystal      = V(0.10f, 0.60f, 0.54f),
        CrystalHi    = V(0.18f, 0.72f, 0.64f),
        Amber        = V(0.80f, 0.60f, 0.10f),
        Progress     = V(0.10f, 0.55f, 0.28f),
    };

    // ── BLUE ────────────────────────────────────────────────────

    public static readonly ThemePalette BeautifullyBlue = new()
    {
        DisplayName  = "Beautifully Blue",
        IsLight      = false,

        Background   = V(0.04f, 0.08f, 0.18f),
        Panel        = V(0.06f, 0.09f, 0.18f),
        Panel2       = V(0.08f, 0.14f, 0.30f),
        Panel3       = V(0.10f, 0.16f, 0.32f),
        PanelAlt     = V(0.08f, 0.14f, 0.30f),
        Title        = V(0.05f, 0.10f, 0.22f),

        Border       = V(0.26f, 0.48f, 0.86f, 0.45f),
        BorderHi     = V(0.28f, 0.60f, 1.00f),

        Text         = V(0.90f, 0.94f, 1.00f),
        TextSub      = V(0.60f, 0.82f, 1.00f),
        TextMuted    = V(0.50f, 0.65f, 0.88f),
        TextDim      = V(0.50f, 0.65f, 0.88f),

        Accent       = V(0.28f, 0.60f, 1.00f),
        AccentHi     = V(0.40f, 0.72f, 1.00f),
        AccentActive = V(0.18f, 0.46f, 0.86f),

        Success      = V(0.29f, 0.68f, 0.50f),
        SuccessHi    = V(0.42f, 0.82f, 0.62f),
        Warning      = V(0.98f, 0.75f, 0.14f),
        Danger       = V(0.97f, 0.44f, 0.44f),
        DangerHi     = V(1.00f, 0.55f, 0.55f),

        Crystal      = V(0.18f, 0.83f, 0.75f),
        CrystalHi    = V(0.30f, 0.92f, 0.85f),
        Amber        = V(0.98f, 0.75f, 0.14f),
        Progress     = V(0.28f, 0.60f, 1.00f),
    };

    public static readonly ThemePalette BeautifullyBlueDay = new()
    {
        DisplayName  = "Beautifully Blue (Day)",
        IsLight      = true,

        Background   = V(0.91f, 0.95f, 1.00f),
        Panel        = V(0.80f, 0.88f, 0.99f),
        Panel2       = V(0.74f, 0.84f, 0.97f),
        Panel3       = V(0.66f, 0.78f, 0.94f),
        PanelAlt     = V(0.74f, 0.84f, 0.97f),
        Title        = V(0.16f, 0.42f, 0.88f),

        Border       = V(0.30f, 0.58f, 0.94f, 0.45f),
        BorderHi     = V(0.14f, 0.40f, 0.86f),

        Text         = V(0.04f, 0.10f, 0.26f),
        TextSub      = V(0.10f, 0.20f, 0.44f),
        TextMuted    = V(0.20f, 0.35f, 0.60f),
        TextDim      = V(0.20f, 0.35f, 0.60f),

        Accent       = V(0.14f, 0.40f, 0.86f),
        AccentHi     = V(0.08f, 0.30f, 0.72f),
        AccentActive = V(0.05f, 0.22f, 0.58f),

        Success      = V(0.16f, 0.50f, 0.32f),
        SuccessHi    = V(0.22f, 0.62f, 0.40f),
        Warning      = V(0.80f, 0.60f, 0.10f),
        Danger       = V(0.82f, 0.30f, 0.30f),
        DangerHi     = V(0.92f, 0.40f, 0.40f),

        Crystal      = V(0.10f, 0.60f, 0.54f),
        CrystalHi    = V(0.18f, 0.72f, 0.64f),
        Amber        = V(0.80f, 0.60f, 0.10f),
        Progress     = V(0.14f, 0.40f, 0.86f),
    };

    // ═══════════════════════════════════════════════════════════
    //  LUNAR THEME
    // ═══════════════════════════════════════════════════════════

    public static readonly ThemePalette Lunar = new()
    {
        DisplayName  = "Lunar",
        IsLight      = false,

        Background   = V(0.04f, 0.02f, 0.08f),
        Panel        = V(0.08f, 0.04f, 0.16f),
        Panel2       = V(0.12f, 0.06f, 0.21f),
        Panel3       = V(0.16f, 0.09f, 0.27f),
        PanelAlt     = V(0.12f, 0.06f, 0.21f),
        Title        = V(0.10f, 0.04f, 0.18f),

        Border       = V(0.42f, 0.18f, 0.63f, 0.50f),
        BorderHi     = V(0.91f, 0.19f, 0.63f),

        Text         = V(0.91f, 0.85f, 0.96f),
        TextSub      = V(0.72f, 0.62f, 0.86f),
        TextMuted    = V(0.53f, 0.41f, 0.69f),
        TextDim      = V(0.53f, 0.41f, 0.69f),

        Accent       = V(0.91f, 0.19f, 0.63f),
        AccentHi     = V(1.00f, 0.31f, 0.73f),
        AccentActive = V(0.72f, 0.09f, 0.47f),

        Success      = V(0.09f, 0.78f, 0.63f),
        SuccessHi    = V(0.19f, 0.91f, 0.72f),
        Warning      = V(0.94f, 0.69f, 0.13f),
        Danger       = V(0.94f, 0.28f, 0.34f),
        DangerHi     = V(1.00f, 0.41f, 0.47f),

        Crystal      = V(0.13f, 0.82f, 0.75f),
        CrystalHi    = V(0.25f, 0.94f, 0.88f),
        Amber        = V(0.94f, 0.50f, 0.13f),
        Progress     = V(0.91f, 0.19f, 0.63f),
    };
}
