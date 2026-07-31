using UnityEngine;

/// <summary>
/// Warm fantasy / parchment color palette, usable from runtime scripts (unlike UiTheme,
/// which lives in Assets/Editor and is editor-only). UiTheme.cs mirrors these same values
/// for its procedural sprite/factory code — keep both in sync if the palette changes.
/// </summary>
public static class UiPalette
{
    public static readonly Color BgTop = new Color(0.165f, 0.114f, 0.071f, 1f);
    public static readonly Color BgBottom = new Color(0.078f, 0.063f, 0.047f, 1f);
    public static readonly Color Surface = new Color(0.231f, 0.165f, 0.106f, 1f);
    public static readonly Color SurfaceInset = new Color(0.129f, 0.094f, 0.063f, 1f);
    public static readonly Color Gold = new Color(0.910f, 0.769f, 0.416f, 1f);
    public static readonly Color GoldDim = new Color(0.55f, 0.46f, 0.25f, 1f);
    public static readonly Color TextLight = new Color(0.961f, 0.902f, 0.784f, 1f);
    public static readonly Color TextMuted = new Color(0.659f, 0.573f, 0.420f, 1f);
    public static readonly Color Locked = new Color(0.141f, 0.102f, 0.067f, 1f);

    /// <summary>Unfilled rank pip. Deliberately NOT Locked — that value is within 0.02 of
    /// SurfaceInset, so pips drawn in it were invisible on unselected rows.</summary>
    public static readonly Color PipEmpty = new Color(0.353f, 0.278f, 0.196f, 1f);

    public static readonly Color Purchased = new Color(0.478f, 0.620f, 0.247f, 1f);
    public static readonly Color Affordable = Gold;
    public static readonly Color Unaffordable = TextMuted;
    public static readonly Color SelectedOutline = new Color(0.769f, 0.275f, 0.184f, 1f);

    static readonly Color[] PlayerAccents =
    {
        new Color(0.769f, 0.275f, 0.184f, 1f),
        new Color(0.243f, 0.549f, 0.576f, 1f),
        new Color(0.478f, 0.620f, 0.247f, 1f),
        new Color(0.545f, 0.373f, 0.659f, 1f),
    };

    public static Color PlayerAccent(int index) => PlayerAccents[((index % PlayerAccents.Length) + PlayerAccents.Length) % PlayerAccents.Length];
}
