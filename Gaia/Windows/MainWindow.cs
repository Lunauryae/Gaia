using Gaia.Core.UI;
using Gaia.Helpers;
using static Dalamud.Interface.Windowing.Window;

namespace Gaia.Windows;

public class MainWindow : StandardWindow
{
    private readonly Plugin _plugin;
    private readonly List<IDrawablePage> _tabs;
    private readonly WelcomeTab _welcomeTab;

    private string _currentTab = "Planting";
    private string _lastTab = "";
    private int _lastMaxRows = -1;

    public MainWindow(Plugin plugin)
        : base("GAIA - Garden Management###GaiaMainWindow")
    {
        _plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(680, 720),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        _tabs = new List<IDrawablePage>
        {
            new PlantingTab(plugin),
            new GardenTab(plugin),
            new StatusTab(plugin),
            new StatisticsTab(plugin),
            new SettingsTab(plugin),
        };
        _welcomeTab = new WelcomeTab(plugin);
    }

    // ── StandardWindow requires this — returns the active palette each frame ──
    protected override ThemePalette GetCurrentPalette() =>
        ThemeRegistry.GetByLegacyIndex((int)_plugin.Configuration.SelectedTheme);

    // NOTE: PreDraw/PostDraw are handled by StandardWindow.
    // The full ~40 ImGui colour push + 8 style vars happen automatically.

    // ── Draw ──────────────────────────────────────────────────
    public override void Draw()
    {
        var profile = _plugin.GetCurrentCharacterProfile();
        if (profile == null)
        {
            ImGui.TextDisabled("Waiting for character data...");
            return;
        }

        if (!profile.HasCompletedOnboarding)
        {
            DrawWelcomeTabBar();
            return;
        }

        DrawMainTabBar(profile);
        AdaptWindowSize(profile);
    }

    // ──────────────────────────────────────────────────────────
    //  WELCOME TAB BAR
    // ──────────────────────────────────────────────────────────
    private void DrawWelcomeTabBar()
    {
        var t = GetCurrentPalette();
        FolderTabBar.PushStyle(t);
        if (ImGui.BeginTabBar("GaiaWelcomeTabBar", ImGuiTabBarFlags.None))
        {
            FolderTabBar.Tab("  Setup Wizard  ", _welcomeTab.Draw);
            ImGui.EndTabBar();
        }
        FolderTabBar.PopStyle();
    }

    // ──────────────────────────────────────────────────────────
    //  MAIN TAB BAR  — folder-style tabs
    //  Tab order: Planting | Garden | Status | Stats | Settings
    // ──────────────────────────────────────────────────────────
    private void DrawMainTabBar(CharacterProfile profile)
    {
        var t = GetCurrentPalette();
        FolderTabBar.PushStyle(t);
        if (ImGui.BeginTabBar("GaiaMainTabBar", ImGuiTabBarFlags.None))
        {
            foreach (var tab in _tabs)
                FolderTabBar.Tab(tab.TabLabel, tab.TabLabel.Trim(), ref _currentTab, tab.Draw);
            ImGui.EndTabBar();
        }
        FolderTabBar.PopStyle();
    }

    // ──────────────────────────────────────────────────────────
    //  ADAPTIVE WINDOW SIZE
    // ──────────────────────────────────────────────────────────
    private void AdaptWindowSize(CharacterProfile profile)
    {
        if (_currentTab != _lastTab)
        {
            float h = _currentTab == "Status"
                ? Math.Max(profile.PersonalEstateSize, profile.FCEstateSize) >= 3 ? 1050f : 760f
                : 760f;
            ImGui.SetWindowSize(new Vector2(ImGui.GetWindowSize().X, h));
            _lastTab = _currentTab;
        }

        if (_currentTab == "Status")
        {
            int rows = Math.Max(profile.PersonalEstateSize, profile.FCEstateSize);
            if (_lastMaxRows >= 0 && rows != _lastMaxRows)
            {
                float h = rows switch { 1 => 680f, 2 => 720f, _ => 1050f };
                ImGui.SetWindowSize(new Vector2(ImGui.GetWindowSize().X, h));
            }
            _lastMaxRows = rows;
        }
    }
}
