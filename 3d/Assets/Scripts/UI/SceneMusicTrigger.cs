using UnityEngine;

/// <summary>
/// Starts this scene's music track on load. One instance per scene, wired by a generator with the
/// scene's own AudioClip reference — AudioDirector itself has no idea which scene it's in, and
/// deliberately stays that way (it is a persistent, scene-agnostic singleton).
/// </summary>
public class SceneMusicTrigger : MonoBehaviour
{
    [SerializeField] private AudioClip musicClip;

    private void Start()
    {
        if (AudioDirector.Instance == null)
        {
            Debug.LogError("SceneMusicTrigger: AudioDirector.Instance is missing — this scene's music will not play.");
            return;
        }
        if (musicClip == null)
        {
            Debug.LogError($"SceneMusicTrigger on '{gameObject.name}': musicClip is not assigned.");
            return;
        }
        AudioDirector.Instance.PlayMusic(musicClip);
    }
}
