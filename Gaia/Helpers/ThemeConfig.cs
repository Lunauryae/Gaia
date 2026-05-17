using Gaia.Core.UI;

namespace Gaia.Helpers;

// Compatibility shim — redirects to Gaia.Core's ThemeRegistry
// so all existing tabs work unchanged.
public static class ThemeConfig
{
    public static ThemePalette Get(GaiaTheme theme) =>
        ThemeRegistry.GetByLegacyIndex((int)theme);

    public static ThemePalette GardenCodex => ThemeRegistry.GardenCodex;
    public static ThemePalette FieldGuide => ThemeRegistry.FieldGuide;
    public static ThemePalette Kawaii => ThemeRegistry.Kawaii;
}