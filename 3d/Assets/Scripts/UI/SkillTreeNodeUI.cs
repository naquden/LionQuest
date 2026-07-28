using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum SkillNodeVisualState { Maxed, Ranked, Affordable, Unaffordable, Locked, Selected }

/// <summary>
/// One node widget in the skill tree panel. Purely visual + click passthrough —
/// SkillTreeScreenController owns all purchase logic and drives this via SetState/Setup.
/// Tier (Keystone/Major/Minor) picks the frame sprite/size; rank is shown as "N/M" or a
/// checkmark when maxed at rank 1. Selected shows a bright branch-coloured ring on top of
/// whatever affordability tint the background already has.
/// </summary>
public class SkillTreeNodeUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image selectionRing;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button button;

    [Header("Tier frames")]
    [SerializeField] private Sprite keystoneSprite;
    [SerializeField] private Sprite majorSprite;
    [SerializeField] private Sprite minorSprite;

    private static Color MaxedColor => UiPalette.Purchased;
    private static readonly Color RankedColor = Color.Lerp(UiPalette.Purchased, UiPalette.Affordable, 0.5f);
    private static Color AffordableColor => UiPalette.Affordable;
    private static Color UnaffordableColor => UiPalette.Unaffordable;
    private static Color LockedColor => UiPalette.Locked;

    public string NodeId { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    public void Setup(SkillNodeDefinition node, System.Action onClick)
    {
        NodeId = node.nodeId;
        if (nameText != null) nameText.text = node.displayName;

        if (background != null)
        {
            Sprite frame = node.tier switch
            {
                SkillNodeTier.Keystone => keystoneSprite,
                SkillNodeTier.Major => majorSprite,
                _ => minorSprite,
            };
            if (frame != null) background.sprite = frame;
        }

        float size = node.tier switch
        {
            SkillNodeTier.Keystone => 110f,
            SkillNodeTier.Major => 90f,
            _ => 70f,
        };
        Rect.sizeDelta = new Vector2(size, size);

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    public void SetState(SkillNodeVisualState state, int rank, int maxRank, int cost, Color branchColor)
    {
        if (rankText != null)
        {
            rankText.gameObject.SetActive(maxRank > 0);
            rankText.text = maxRank == 1 && rank >= 1 ? "✓" : $"{rank}/{maxRank}";
        }

        if (costText != null)
        {
            costText.gameObject.SetActive(rank < maxRank);
            costText.text = $"{cost} souls";
        }

        if (selectionRing != null)
        {
            bool selected = state == SkillNodeVisualState.Selected;
            selectionRing.gameObject.SetActive(selected);
            if (selected) selectionRing.color = branchColor;
        }

        if (background != null)
        {
            switch (state)
            {
                case SkillNodeVisualState.Maxed:
                case SkillNodeVisualState.Selected when rank >= maxRank:
                    background.color = MaxedColor;
                    break;
                case SkillNodeVisualState.Ranked:
                    background.color = RankedColor;
                    break;
                case SkillNodeVisualState.Affordable:
                    background.color = AffordableColor;
                    break;
                case SkillNodeVisualState.Unaffordable:
                    background.color = UnaffordableColor;
                    break;
                case SkillNodeVisualState.Locked:
                    background.color = LockedColor;
                    break;
                case SkillNodeVisualState.Selected:
                    background.color = rank > 0 ? RankedColor : AffordableColor;
                    break;
            }
        }

        if (button != null) button.interactable = state != SkillNodeVisualState.Locked;
    }
}
