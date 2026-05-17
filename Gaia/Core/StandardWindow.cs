namespace Gaia.Core.UI;

using static ColorUtil;

// ──────────────────────────────────────────────────────────────
//  StandardWindow  —  themed Window base for Gaia.
//
//  Pushes the full ImGui colour set in PreDraw, pops in PostDraw.
//  Tabs never manage push/pop — they just draw content.
//
//  Subclass and override GetCurrentPalette() to return the active
//  theme. PreDraw reads it each frame so theme hot-swapping works.
//
//  Usage:
//    public class MyMainWindow : StandardWindow
//    {
//        public MyMainWindow() : base("My Window###MyId") { }
//        protected override ThemePalette GetCurrentPalette()
//            => ThemeRegistry.Get(myPlugin.Config.Theme);
//        public override void Draw() { /* tab content */ }
//    }
// ──────────────────────────────────────────────────────────────
public abstract class StandardWindow : Window, IDisposable
{
    private int _colorCount;
    private int _varCount;

    protected StandardWindow(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        : base(name, flags) { }

    /// <summary>
    /// Return the active ThemePalette. Called every frame in PreDraw.
    /// Override this in your concrete window to read from config.
    /// </summary>
    protected abstract ThemePalette GetCurrentPalette();

    // ── PreDraw / PostDraw ─────────────────────────────────────

    public override void PreDraw()
    {
        _colorCount = 0;
        _varCount = 0;

        var t = GetCurrentPalette();

        // Style vars — rounded, comfortable spacing
        PushVar(ImGuiStyleVar.WindowRounding,    12f);
        PushVar(ImGuiStyleVar.FrameRounding,      7f);
        PushVar(ImGuiStyleVar.TabRounding,         6f);
        PushVar(ImGuiStyleVar.ScrollbarRounding,   6f);
        PushVar(ImGuiStyleVar.GrabRounding,        6f);
        PushVar(ImGuiStyleVar.WindowPadding,     new Vector2(12f, 12f));
        PushVar(ImGuiStyleVar.FramePadding,      new Vector2(8f,  5f));
        PushVar(ImGuiStyleVar.ItemSpacing,       new Vector2(8f,  6f));

        if (t.IsLight)
            PushLightColors(t);
        else
            PushDarkColors(t);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(_colorCount);
        ImGui.PopStyleVar(_varCount);
    }

    public virtual void Dispose() { }

    // ──────────────────────────────────────────────────────────
    //  DARK THEME  —  light text on dark backgrounds.
    //
    //  Seed fields used:
    //    Background, Panel2 (Lerp target), Title,
    //    Accent, AccentHi, AccentActive,
    //    Text, TextDim, Border
    // ──────────────────────────────────────────────────────────
    private void PushDarkColors(ThemePalette t)
    {
        var bg    = t.Background;
        var surf  = t.Panel2;          // primary surface for derivations
        var title = t.Title;
        var acc   = t.Accent;
        var accH  = t.AccentHi;
        var accA  = t.AccentActive;
        var text  = t.Text;
        var dim   = t.TextDim;
        var bord  = t.Border;

        var frameBg    = Lerp(bg, surf, 0.6f);
        var frameBgHov = Lerp(bg, surf, 0.9f);
        var frameBgAct = surf;

        // Window
        PushColor(ImGuiCol.WindowBg,             bg);
        PushColor(ImGuiCol.ChildBg,              Lerp(bg, surf, 0.3f));
        PushColor(ImGuiCol.PopupBg,              surf);

        // Text
        PushColor(ImGuiCol.Text,                 text);
        PushColor(ImGuiCol.TextDisabled,         WithAlpha(dim, 0.55f));

        // Borders
        PushColor(ImGuiCol.Border,               bord);
        PushColor(ImGuiCol.BorderShadow,         V(0, 0, 0, 0));

        // Frames
        PushColor(ImGuiCol.FrameBg,              frameBg);
        PushColor(ImGuiCol.FrameBgHovered,       frameBgHov);
        PushColor(ImGuiCol.FrameBgActive,        frameBgAct);

        // Title bar
        PushColor(ImGuiCol.TitleBg,              title);
        PushColor(ImGuiCol.TitleBgActive,        title);
        PushColor(ImGuiCol.TitleBgCollapsed,     title);

        // Menu / Scrollbar
        PushColor(ImGuiCol.MenuBarBg,            bg);
        PushColor(ImGuiCol.ScrollbarBg,          WithAlpha(bg, 0.4f));
        PushColor(ImGuiCol.ScrollbarGrab,        WithAlpha(acc, 0.4f));
        PushColor(ImGuiCol.ScrollbarGrabHovered, WithAlpha(acc, 0.65f));
        PushColor(ImGuiCol.ScrollbarGrabActive,  acc);

        // Controls
        PushColor(ImGuiCol.CheckMark,            acc);
        PushColor(ImGuiCol.SliderGrab,           acc);
        PushColor(ImGuiCol.SliderGrabActive,     accH);

        // Buttons
        PushColor(ImGuiCol.Button,               WithAlpha(acc, 0.22f));
        PushColor(ImGuiCol.ButtonHovered,        WithAlpha(accH, 0.38f));
        PushColor(ImGuiCol.ButtonActive,         WithAlpha(accA, 0.55f));

        // Headers
        PushColor(ImGuiCol.Header,               WithAlpha(acc, 0.25f));
        PushColor(ImGuiCol.HeaderHovered,        WithAlpha(accH, 0.40f));
        PushColor(ImGuiCol.HeaderActive,         WithAlpha(accA, 0.55f));

        // Separators
        PushColor(ImGuiCol.Separator,            bord);
        PushColor(ImGuiCol.SeparatorHovered,     WithAlpha(acc, 0.6f));
        PushColor(ImGuiCol.SeparatorActive,      acc);

        // Resize grips
        PushColor(ImGuiCol.ResizeGrip,           WithAlpha(acc, 0.18f));
        PushColor(ImGuiCol.ResizeGripHovered,    WithAlpha(accH, 0.38f));
        PushColor(ImGuiCol.ResizeGripActive,     WithAlpha(accA, 0.55f));

        // Tabs  (NOTE: TabBg and TabSelected do NOT exist in this Dalamud version)
        PushColor(ImGuiCol.Tab,                  WithAlpha(acc, 0.22f));
        PushColor(ImGuiCol.TabHovered,           WithAlpha(accH, 0.50f));
        PushColor(ImGuiCol.TabActive,            WithAlpha(acc, 0.55f));
        PushColor(ImGuiCol.TabUnfocused,         WithAlpha(acc, 0.10f));
        PushColor(ImGuiCol.TabUnfocusedActive,   WithAlpha(acc, 0.30f));

        // Tables
        PushColor(ImGuiCol.TableRowBgAlt,        V(1f, 1f, 1f, 0.025f));
        PushColor(ImGuiCol.TableBorderLight,     bord);
        PushColor(ImGuiCol.TableBorderStrong,    WithAlpha(bord, bord.W + 0.3f));
    }

    // ──────────────────────────────────────────────────────────
    //  LIGHT THEME  —  dark text on light backgrounds.
    // ──────────────────────────────────────────────────────────
    private void PushLightColors(ThemePalette t)
    {
        var bg    = t.Background;
        var surf  = t.Panel2;
        var title = t.Title;
        var acc   = t.Accent;
        var accH  = t.AccentHi;
        var accA  = t.AccentActive;
        var text  = t.Text;
        var dim   = t.TextDim;
        var bord  = t.Border;

        var frameBg    = V(1f, 1f, 1f, 0.60f);
        var frameBgHov = V(1f, 1f, 1f, 0.85f);
        var frameBgAct = V(1f, 1f, 1f, 1.00f);

        // Window
        PushColor(ImGuiCol.WindowBg,             bg);
        PushColor(ImGuiCol.ChildBg,              surf);
        PushColor(ImGuiCol.PopupBg,              bg);

        // Text
        PushColor(ImGuiCol.Text,                 text);
        PushColor(ImGuiCol.TextDisabled,         WithAlpha(dim, 0.55f));

        // Borders
        PushColor(ImGuiCol.Border,               bord);
        PushColor(ImGuiCol.BorderShadow,         V(0, 0, 0, 0));

        // Frames
        PushColor(ImGuiCol.FrameBg,              frameBg);
        PushColor(ImGuiCol.FrameBgHovered,       frameBgHov);
        PushColor(ImGuiCol.FrameBgActive,        frameBgAct);

        // Title bar
        PushColor(ImGuiCol.TitleBg,              title);
        PushColor(ImGuiCol.TitleBgActive,        title);
        PushColor(ImGuiCol.TitleBgCollapsed,     title);

        // Menu / Scrollbar
        PushColor(ImGuiCol.MenuBarBg,            bg);
        PushColor(ImGuiCol.ScrollbarBg,          WithAlpha(surf, 0.6f));
        PushColor(ImGuiCol.ScrollbarGrab,        WithAlpha(acc, 0.5f));
        PushColor(ImGuiCol.ScrollbarGrabHovered, WithAlpha(acc, 0.75f));
        PushColor(ImGuiCol.ScrollbarGrabActive,  accH);

        // Controls
        PushColor(ImGuiCol.CheckMark,            acc);
        PushColor(ImGuiCol.SliderGrab,           acc);
        PushColor(ImGuiCol.SliderGrabActive,     accH);

        // Buttons
        PushColor(ImGuiCol.Button,               WithAlpha(acc, 0.18f));
        PushColor(ImGuiCol.ButtonHovered,        WithAlpha(accH, 0.30f));
        PushColor(ImGuiCol.ButtonActive,         WithAlpha(accA, 0.45f));

        // Headers
        PushColor(ImGuiCol.Header,               WithAlpha(acc, 0.18f));
        PushColor(ImGuiCol.HeaderHovered,        WithAlpha(accH, 0.28f));
        PushColor(ImGuiCol.HeaderActive,         WithAlpha(accA, 0.40f));

        // Separators
        PushColor(ImGuiCol.Separator,            bord);
        PushColor(ImGuiCol.SeparatorHovered,     WithAlpha(acc, 0.55f));
        PushColor(ImGuiCol.SeparatorActive,      acc);

        // Resize grips
        PushColor(ImGuiCol.ResizeGrip,           WithAlpha(acc, 0.18f));
        PushColor(ImGuiCol.ResizeGripHovered,    WithAlpha(accH, 0.35f));
        PushColor(ImGuiCol.ResizeGripActive,     WithAlpha(accA, 0.50f));

        // Tabs
        PushColor(ImGuiCol.Tab,                  WithAlpha(acc, 0.16f));
        PushColor(ImGuiCol.TabHovered,           WithAlpha(accH, 0.30f));
        PushColor(ImGuiCol.TabActive,            WithAlpha(acc, 0.35f));
        PushColor(ImGuiCol.TabUnfocused,         WithAlpha(acc, 0.08f));
        PushColor(ImGuiCol.TabUnfocusedActive,   WithAlpha(acc, 0.22f));

        // Tables
        PushColor(ImGuiCol.TableRowBgAlt,        V(0f, 0f, 0f, 0.03f));
        PushColor(ImGuiCol.TableBorderLight,     bord);
        PushColor(ImGuiCol.TableBorderStrong,    WithAlpha(bord, bord.W + 0.3f));
    }

    // ── Push helpers (counted for PostDraw pop) ────────────────

    private void PushColor(ImGuiCol col, Vector4 v)
    {
        ImGui.PushStyleColor(col, v);
        _colorCount++;
    }

    private void PushVar(ImGuiStyleVar sv, float f)
    {
        ImGui.PushStyleVar(sv, f);
        _varCount++;
    }

    private void PushVar(ImGuiStyleVar sv, Vector2 v)
    {
        ImGui.PushStyleVar(sv, v);
        _varCount++;
    }
}
