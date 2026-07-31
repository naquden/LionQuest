using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One category tab down the left of the upgrade screen (replaces the old elbow link widget of the
/// graph layout — kept the file/type name so existing prefab and generator references stay valid).
/// Shows the category name, how many upgrades are owned in it, and a LOCKED marker for a skill whose
/// Unlock row has not been bought. The selected tab is filled with the category's accent colour.
/// </summary>
public class SkillTreeLinkUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image selectionRing;
    [SerializeField] private Image accentBar;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private Button button;

    public void Setup(string title, Color accentColor, System.Action onClick)
    {
        if (titleText != null) titleText.text = title;
        if (accentBar != null) accentBar.color = accentColor;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    /// <summary>`owned`/`total` counts ranks bought vs available in this category. `paneFocused` is
    /// whether the navigation cursor currently lives in the category list rather than the row list —
    /// the selected tab stays marked either way, but only glows while it owns the cursor.</summary>
    public void SetState(bool selected, bool paneFocused, bool locked, int owned, int total, Color accentColor)
    {
        if (subtitleText != null)
        {
            subtitleText.text = locked ? "Locked" : $"{owned} / {total} bought";
            subtitleText.color = locked ? UiPalette.TextMuted : UiPalette.TextLight;
        }

        if (titleText != null)
        {
            titleText.color = selected ? UiPalette.TextLight : UiPalette.TextMuted;
        }

        if (background != null)
        {
            background.color = selected
                ? new Color(accentColor.r * 0.45f, accentColor.g * 0.45f, accentColor.b * 0.45f, 1f)
                : UiPalette.SurfaceInset;
        }

        if (accentBar != null)
        {
            accentBar.color = selected ? accentColor : new Color(accentColor.r, accentColor.g, accentColor.b, 0.35f);
        }

        // Only the tab that owns the cursor gets the outline — same treatment as a focused row, so
        // one glowing outline on screen always answers "where will Up/Down go?".
        if (selectionRing != null)
        {
            bool showRing = selected && paneFocused;
            selectionRing.gameObject.SetActive(showRing);
            if (showRing) selectionRing.color = UiPalette.Gold;
        }
    }
}
