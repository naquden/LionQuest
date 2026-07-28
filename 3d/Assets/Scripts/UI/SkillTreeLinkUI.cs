using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One elbow link between a prereq node and its dependent: a horizontal segment, a vertical
/// segment, and a diamond junction at the elbow. Grey when the source node is unranked,
/// tinted to the branch colour once the source has rank >= 1. Built/positioned by
/// SkillTreeScreenController.BuildLink at spawn time from two anchored node positions.
/// </summary>
public class SkillTreeLinkUI : MonoBehaviour
{
    [SerializeField] private RectTransform horizontalSegment;
    [SerializeField] private RectTransform verticalSegment;
    [SerializeField] private Image horizontalImage;
    [SerializeField] private Image verticalImage;
    [SerializeField] private Image junctionImage;

    private static Color UnboughtColor => UiPalette.GoldDim;

    /// <summary>Lays out the elbow from `from` to `to` (both anchoredPosition in the same
    /// parent space): horizontal run first, then vertical drop, meeting at the elbow corner.</summary>
    public void Layout(Vector2 from, Vector2 to)
    {
        Vector2 elbow = new Vector2(to.x, from.y);

        float hLength = Mathf.Abs(elbow.x - from.x);
        float vLength = Mathf.Abs(to.y - elbow.y);

        if (horizontalSegment != null)
        {
            horizontalSegment.anchoredPosition = new Vector2((from.x + elbow.x) * 0.5f, from.y);
            horizontalSegment.sizeDelta = new Vector2(Mathf.Max(hLength, 1f), horizontalSegment.sizeDelta.y);
        }

        if (verticalSegment != null)
        {
            verticalSegment.anchoredPosition = new Vector2(elbow.x, (elbow.y + to.y) * 0.5f);
            verticalSegment.sizeDelta = new Vector2(verticalSegment.sizeDelta.x, Mathf.Max(vLength, 1f));
        }

        if (junctionImage != null) junctionImage.rectTransform.anchoredPosition = elbow;
    }

    public void SetBranchColor(Color branchColor, bool active)
    {
        Color color = active ? branchColor : UnboughtColor;
        if (horizontalImage != null) horizontalImage.color = color;
        if (verticalImage != null) verticalImage.color = color;
        if (junctionImage != null) junctionImage.color = color;
    }
}
