using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One entry in a player slot's vertical carousel (portrait + name + lock icon).
/// </summary>
public class CharacterCarouselItem : MonoBehaviour
{
    [SerializeField] private Image portrait;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private TMP_Text nameText;

    private RectTransform rect;

    public void Setup(CharacterDefinition def, bool isUnlocked)
    {
        rect = (RectTransform)transform;

        Sprite sprite = isUnlocked ? def.portrait : (def.portraitLocked != null ? def.portraitLocked : def.portrait);
        if (portrait != null)
        {
            portrait.sprite = sprite;
            portrait.color = isUnlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
        }

        if (nameText != null) nameText.text = def.displayName;
        if (lockIcon != null) lockIcon.SetActive(!isUnlocked);
    }

    /// <summary>Applies the plan's ±0/±1/±2 scale+opacity falloff. offset 0 = selected item.</summary>
    public void ApplyDistanceStyle(int offset)
    {
        int abs = Mathf.Abs(offset);
        float scale = abs == 0 ? 1f : abs == 1 ? 0.8f : 0.6f;
        float alpha = abs == 0 ? 1f : abs == 1 ? 0.6f : 0.3f;
        bool visible = abs <= 2;

        gameObject.SetActive(visible);
        if (!visible) return;

        if (rect == null) rect = (RectTransform)transform;
        rect.localScale = Vector3.one * scale;

        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = alpha;
    }
}
