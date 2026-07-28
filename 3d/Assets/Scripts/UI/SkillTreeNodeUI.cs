using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum SkillNodeVisualState { Purchased, Affordable, Unaffordable, Locked, Selected }

/// <summary>
/// One node widget in the skill tree panel. Purely visual + click passthrough —
/// SkillTreeScreenController owns all purchase logic and drives this via SetState/Setup.
/// </summary>
public class SkillTreeNodeUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button button;

    private static Color PurchasedColor => UiPalette.Purchased;
    private static Color AffordableColor => UiPalette.Affordable;
    private static Color UnaffordableColor => UiPalette.Unaffordable;
    private static Color LockedColor => UiPalette.Locked;
    private static Color SelectedOutline => UiPalette.SelectedOutline;

    public string NodeId { get; private set; }
    public RectTransform Rect => (RectTransform)transform;

    public void Setup(SkillNodeDefinition node, System.Action onClick)
    {
        NodeId = node.nodeId;
        if (nameText != null) nameText.text = node.displayName;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }

    public void SetState(SkillNodeVisualState state, int cost)
    {
        if (costText != null)
        {
            costText.gameObject.SetActive(state != SkillNodeVisualState.Purchased);
            costText.text = cost.ToString();
        }

        if (background == null) return;

        switch (state)
        {
            case SkillNodeVisualState.Purchased:
                background.color = PurchasedColor;
                break;
            case SkillNodeVisualState.Selected:
                background.color = SelectedOutline;
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
        }

        if (button != null) button.interactable = state != SkillNodeVisualState.Locked;
    }
}
