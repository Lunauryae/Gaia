namespace Gaia.Core.UI;

// ──────────────────────────────────────────────────────────────
//  FolderTabBar  —  folder-style tab styling.
//
//  Pushes style vars + colours that give tabs a folder look:
//    - Extra vertical padding so tabs are taller
//    - Top-corner rounding (bottom flush with content)
//    - Active tab visually raised — brighter bg
//    - Inactive tabs dimmer
//
//  Usage:
//    FolderTabBar.PushStyle(palette);
//    if (ImGui.BeginTabBar("MyTabs")) { … ImGui.EndTabBar(); }
//    FolderTabBar.PopStyle();
//
//  NOTE: these pushes layer ON TOP of whatever StandardWindow has
//  already pushed. The folder-tab colours override the default tab
//  colours within the BeginTabBar/EndTabBar scope.
// ──────────────────────────────────────────────────────────────
public static class FolderTabBar
{
    private const int VarCount   = 3;
    private const int ColorCount = 4;

    /// <summary>Push folder-tab styling. Must be paired with PopStyle().</summary>
    public static void PushStyle(ThemePalette t)
    {
        // Style vars
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding,      4f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,     new Vector2(2f, 6f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(6f, 4f));

        // Colours
        ImGui.PushStyleColor(ImGuiCol.Tab,        ColorUtil.ToU32(t.Panel,  0.80f));  // inactive
        ImGui.PushStyleColor(ImGuiCol.TabHovered,  ColorUtil.ToU32(t.Panel3, 0.90f));  // hovered
        ImGui.PushStyleColor(ImGuiCol.TabActive,   ImGui.GetColorU32(t.Panel3));       // active
        ImGui.PushStyleColor(ImGuiCol.Text,        ImGui.GetColorU32(t.Text));         // tab text
    }

    /// <summary>Pop folder-tab styling. Must follow PushStyle().</summary>
    public static void PopStyle()
    {
        ImGui.PopStyleColor(ColorCount);
        ImGui.PopStyleVar(VarCount);
    }

    /// <summary>
    /// Convenience helper: draw a tab item and invoke the draw action if selected.
    /// Handles BeginTabItem / EndTabItem so tabs don't have to.
    /// </summary>
    public static void Tab(string label, System.Action draw)
    {
        if (ImGui.BeginTabItem(label))
        {
            draw();
            ImGui.EndTabItem();
        }
    }

    /// <summary>
    /// Convenience helper: draw a tab item and invoke the draw action if selected,
    /// tracking which tab is currently active via the out parameter.
    /// </summary>
    public static void Tab(string label, string id, ref string currentTab, System.Action draw)
    {
        if (ImGui.BeginTabItem(label))
        {
            currentTab = id;
            draw();
            ImGui.EndTabItem();
        }
    }
}
