using UnityEngine;

/// <summary>
/// Oscillates a CanvasGroup's alpha between two values. Purely decorative — used for
/// "press to join" prompts and the start banner so the screen doesn't sit dead still.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class UiPulse : MonoBehaviour
{
    [SerializeField] private float minAlpha = 0.55f;
    [SerializeField] private float maxAlpha = 1f;
    [SerializeField] private float cyclesPerSecond = 0.6f;

    private CanvasGroup canvasGroup;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.time * cyclesPerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
        canvasGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
    }
}
