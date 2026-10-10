using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Three sliders (master/music/sfx) bound to AudioDirector. Lives on the pause menu so volume is
/// reachable from every gameplay scene without a dedicated settings screen — this project has none
/// yet, and the pause menu is the one UI guaranteed to be present everywhere a player would want to
/// adjust volume mid-run.
/// </summary>
public class VolumeSlidersPanel : MonoBehaviour
{
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private bool suppressCallback;

    private void OnEnable()
    {
        if (AudioDirector.Instance == null)
        {
            Debug.LogError("VolumeSlidersPanel: AudioDirector.Instance is missing — sliders will not reflect or control any volume.");
            return;
        }

        // Read current values without re-triggering SetMasterVolume etc. via the slider's own
        // onValueChanged — that would immediately re-save the exact prefs it just loaded, harmless
        // but pointless, and more importantly would fire OnVolumesChanged before this panel has
        // finished setting up all three sliders to their real values.
        suppressCallback = true;
        if (masterSlider != null) masterSlider.value = AudioDirector.Instance.MasterVolume;
        if (musicSlider != null) musicSlider.value = AudioDirector.Instance.MusicVolume;
        if (sfxSlider != null) sfxSlider.value = AudioDirector.Instance.SfxVolume;
        suppressCallback = false;

        if (masterSlider != null) masterSlider.onValueChanged.AddListener(HandleMasterChanged);
        if (musicSlider != null) musicSlider.onValueChanged.AddListener(HandleMusicChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
    }

    private void OnDisable()
    {
        if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(HandleMasterChanged);
        if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(HandleMusicChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(HandleSfxChanged);
    }

    private void HandleMasterChanged(float value)
    {
        if (suppressCallback || AudioDirector.Instance == null) return;
        AudioDirector.Instance.SetMasterVolume(value);
    }

    private void HandleMusicChanged(float value)
    {
        if (suppressCallback || AudioDirector.Instance == null) return;
        AudioDirector.Instance.SetMusicVolume(value);
    }

    private void HandleSfxChanged(float value)
    {
        if (suppressCallback || AudioDirector.Instance == null) return;
        AudioDirector.Instance.SetSfxVolume(value);
    }
}
