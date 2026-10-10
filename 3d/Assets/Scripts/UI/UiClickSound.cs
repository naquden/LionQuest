using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the "UIClick" SFX on this Button's onClick. A component rather than wiring AudioDirector
/// calls into every screen's click handler individually — UiTheme.Button (the factory every
/// generated button goes through) is editor-only and can't reference a runtime AudioDirector call
/// itself, so this is the runtime half: add it once per button via AddComponent in a generator, or
/// by hand in the Inspector for a hand-authored button.
/// </summary>
[RequireComponent(typeof(Button))]
public class UiClickSound : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(PlayClickSound);
    }

    private void OnDestroy()
    {
        Button button = GetComponent<Button>();
        if (button != null) button.onClick.RemoveListener(PlayClickSound);
    }

    private static void PlayClickSound()
    {
        if (AudioDirector.Instance != null) AudioDirector.Instance.PlaySfx("UIClick");
    }
}
