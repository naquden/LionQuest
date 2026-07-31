using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum SkillNodeVisualState { Maxed, Affordable, Unaffordable, Locked }

/// <summary>
/// One upgrade ROW in the upgrade screen (previously a node in a graph). Purely visual + click
/// passthrough — SkillTreeScreenController owns all purchase logic and drives this via Setup/SetState.
///
/// A row is designed to be readable on its own with no side panel and no legend: title, one
/// plain-language sentence, a pip strip showing owned/total ranks, the current value change
/// ("10s -> 8s"), and the price. Colour is never the only signal — the price field also spells out
/// "OWNED" / "LOCKED" / "NEED n MORE" in words.
/// </summary>
public class SkillTreeNodeUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image selectionRing;
    [SerializeField] private Image accentStripe;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text changeText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image[] rankPips = System.Array.Empty<Image>();
    [SerializeField] private Button button;

    private static Color RowNormal => UiPalette.SurfaceInset;
    private static Color RowSelected => UiPalette.Surface;
    private static Color RowLocked => UiPalette.Locked;

    public string NodeId { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    public void Setup(SkillNodeDefinition node, Color accentColor, System.Action onClick)
    {
        NodeId = node.nodeId;
        if (nameText != null) nameText.text = node.displayName;
        if (descriptionText != null) descriptionText.text = node.description;
        if (accentStripe != null) accentStripe.color = accentColor;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    /// <summary>
    /// Drives every visual on the row. `changeLine` is a "before -> after" string (may be empty),
    /// `price` is the cost of the next rank, `missingSouls` how many more souls are needed (0 when
    /// affordable).
    ///
    /// `selected` is the row cursor; `paneFocused` is whether the cursor currently LIVES in the row
    /// list at all (vs. the category list). A selected row in an unfocused pane keeps its highlight
    /// but dims the ring, so it's always obvious which side of the screen the stick will move.
    /// </summary>
    public void SetState(SkillNodeVisualState state, bool selected, bool paneFocused, int rank, int maxRank,
        int price, int missingSouls, string changeLine, Color accentColor)
    {
        for (int i = 0; i < rankPips.Length; i++)
        {
            Image pip = rankPips[i];
            if (pip == null) continue;

            bool used = i < maxRank;
            pip.gameObject.SetActive(used);
            if (!used) continue;
            pip.color = i < rank ? accentColor : UiPalette.PipEmpty;
        }

        if (changeText != null)
        {
            bool hasChange = !string.IsNullOrEmpty(changeLine);
            changeText.gameObject.SetActive(hasChange);
            if (hasChange) changeText.text = changeLine;
        }

        if (priceText != null)
        {
            switch (state)
            {
                case SkillNodeVisualState.Maxed:
                    priceText.text = maxRank > 1 ? $"MAXED  {rank}/{maxRank}" : "OWNED";
                    priceText.color = UiPalette.Purchased;
                    break;
                case SkillNodeVisualState.Locked:
                    priceText.text = "LOCKED";
                    priceText.color = UiPalette.TextMuted;
                    break;
                case SkillNodeVisualState.Unaffordable:
                    priceText.text = $"{price} souls  (need {missingSouls} more)";
                    priceText.color = UiPalette.TextMuted;
                    break;
                default:
                    priceText.text = $"{price} souls";
                    priceText.color = UiPalette.Gold;
                    break;
            }
        }

        if (nameText != null)
        {
            nameText.color = state == SkillNodeVisualState.Locked ? UiPalette.TextMuted : UiPalette.TextLight;
        }
        if (descriptionText != null)
        {
            descriptionText.color = state == SkillNodeVisualState.Locked ? UiPalette.TextMuted : UiPalette.TextMuted;
        }

        if (background != null)
        {
            background.color = state == SkillNodeVisualState.Locked
                ? RowLocked
                : (selected ? RowSelected : RowNormal);
        }

        // The gold ring is the one "you are here" marker on the whole screen, so it appears only in
        // the focused pane. An unfocused pane still remembers its selection via the lighter
        // background — that way there is never more than one ring competing for the eye.
        if (selectionRing != null)
        {
            bool showRing = selected && paneFocused;
            selectionRing.gameObject.SetActive(showRing);
            if (showRing) selectionRing.color = UiPalette.Gold;
        }

        // Locked rows can still be inspected/selected; the screen refuses the purchase itself.
        if (button != null) button.interactable = true;
    }
}
