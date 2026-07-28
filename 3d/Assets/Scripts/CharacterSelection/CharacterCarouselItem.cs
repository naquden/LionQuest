using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One dot in a player slot's carousel strip. Represents a character without a name/portrait —
/// the live 3D preview above shows that instead. Distance from the selected dot drives
/// scale/alpha (smoothly lerped), lock state drives a separate outline ring.
/// </summary>
public class CharacterCarouselItem : MonoBehaviour
{
    [SerializeField] private Image dot;
    [SerializeField] private GameObject lockRing;
    [SerializeField] private CanvasGroup canvasGroup;

    private const float LerpSpeed = 10f;

    private RectTransform rect;
    private float targetScale = 1f;
    private float targetAlpha = 1f;

    public void Setup(CharacterDefinition def, bool isUnlocked)
    {
        rect = (RectTransform)transform;

        if (dot != null) dot.color = isUnlocked ? UiPalette.Gold : UiPalette.TextMuted;
        if (lockRing != null) lockRing.SetActive(!isUnlocked);
    }

    /// <summary>Sets the ±0/±1/±2 scale+opacity falloff target. offset 0 = selected item.</summary>
    public void ApplyDistanceStyle(int offset)
    {
        int abs = Mathf.Abs(offset);
        bool visible = abs <= 2;
        gameObject.SetActive(visible);
        if (!visible) return;

        targetScale = abs == 0 ? 1f : abs == 1 ? 0.75f : 0.55f;
        targetAlpha = abs == 0 ? 1f : abs == 1 ? 0.6f : 0.3f;

        if (rect == null) rect = (RectTransform)transform;
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        if (rect == null) return;

        float t = Time.deltaTime * LerpSpeed;
        rect.localScale = Vector3.Lerp(rect.localScale, Vector3.one * targetScale, t);
        if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, t);
    }
}
