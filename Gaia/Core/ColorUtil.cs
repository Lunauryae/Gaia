namespace Gaia.Core.UI;

/// <summary>
/// Static colour helpers used across Gaia.
/// </summary>
public static class ColorUtil
{
    /// <summary>Convert a Vector4 colour to a uint for ImDrawList calls.</summary>
    public static uint ToU32(Vector4 c) =>
        ImGui.ColorConvertFloat4ToU32(c);

    /// <summary>Convert a Vector4 colour to a uint, overriding the alpha channel.</summary>
    public static uint ToU32(Vector4 c, float alpha) =>
        ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, alpha));

    /// <summary>Linearly interpolate between two Vector4 colours.</summary>
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => new(
        a.X + (b.X - a.X) * t,
        a.Y + (b.Y - a.Y) * t,
        a.Z + (b.Z - a.Z) * t,
        a.W + (b.W - a.W) * t);

    /// <summary>Shorthand for building a Vector4 colour.</summary>
    public static Vector4 V(float r, float g, float b, float a = 1f) => new(r, g, b, a);

    /// <summary>Return a copy of the colour with the given alpha.</summary>
    public static Vector4 WithAlpha(Vector4 c, float alpha) => new(c.X, c.Y, c.Z, alpha);
}
