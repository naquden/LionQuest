using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central audio entry point: pooled SFX one-shots, a single crossfaded music source, and the three
/// volume scalars (master/music/sfx) every slider in the game controls.
///
/// Lives on GameController.prefab (DontDestroyOnLoad, same lifetime as GameSaveController) so it
/// survives every scene transition — music can crossfade INTO a new scene rather than cutting and
/// restarting, and a volume change made on one screen is still in effect after loading the next.
///
/// No AudioMixer asset: mixer assets are painful to author correctly from pure editor script (SetFloat
/// on exposed parameters needs the parameter to already be exposed via the mixer's own UI, which has
/// no public scripting API), and this project only needs three flat volume scalars, not a routing
/// graph. AudioSource.volume achieves the same audible result for master/music/sfx faders.
/// </summary>
public class AudioDirector : MonoBehaviour
{
    public static AudioDirector Instance { get; private set; }

    [Header("SFX Pool")]
    [Tooltip("Number of pooled one-shot AudioSources. Sized for a busy four-player wave clear: " +
             "several simultaneous hits, a couple of skill casts, and pickup dings should each get " +
             "their own source rather than cutting each other off.")]
    [SerializeField] private int sfxPoolSize = 12;

    [Tooltip("Named-event -> AudioClip lookup. Gameplay code plays sounds by name (PlaySfx(\"Hit\")) " +
             "rather than each script carrying its own clip reference — see SoundBank's doc comment.")]
    [SerializeField] private SoundBank soundBank;

    [Header("Music")]
    [Tooltip("Seconds to crossfade between the previous music track and a newly played one.")]
    [SerializeField] private float musicCrossfadeDuration = 1.5f;

    private const string MasterVolumeKey = "audio.masterVolume";
    private const string MusicVolumeKey = "audio.musicVolume";
    private const string SfxVolumeKey = "audio.sfxVolume";

    private readonly List<AudioSource> sfxSources = new List<AudioSource>();
    private int nextSfxSourceIndex;

    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private AudioSource activeMusicSource;
    private Coroutine musicCrossfadeRoutine;
    private AudioClip currentMusicClip;

    private float masterVolume = 1f;
    private float musicVolume = 1f;
    private float sfxVolume = 1f;

    /// <summary>Raised after any of the three volumes change, with (master, music, sfx) — sliders
    /// bind to this rather than polling, so multiple sliders open across scenes (unlikely today, but
    /// cheap to support) stay in sync with each other.</summary>
    public System.Action<float, float, float> OnVolumesChanged;

    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildSfxPool();
        BuildMusicSources();
        LoadVolumePrefs();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildSfxPool()
    {
        for (int i = 0; i < sfxPoolSize; i++)
        {
            var go = new GameObject($"SfxSource_{i}");
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D — this is a top-down game with a single shared camera, not per-player positional audio.
            sfxSources.Add(source);
        }
    }

    private void BuildMusicSources()
    {
        musicSourceA = BuildMusicSource("MusicSourceA");
        musicSourceB = BuildMusicSource("MusicSourceB");
        activeMusicSource = musicSourceA;
    }

    private AudioSource BuildMusicSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    // ------------------------------------------------------------------
    // SFX
    // ------------------------------------------------------------------

    /// <summary>
    /// Plays a one-shot SFX from the pool. Round-robins through sfxSources rather than searching for
    /// an idle one — with a pool sized for the busiest realistic moment, stealing the oldest-playing
    /// source is a smaller audible artifact than dropping a new sound because every source looked
    /// "busy" from a single stale IsPlaying check.
    /// </summary>
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        if (sfxSources.Count == 0)
        {
            Debug.LogError("AudioDirector: PlaySfx called but the SFX pool is empty — sfxPoolSize is 0?");
            return;
        }

        AudioSource source = sfxSources[nextSfxSourceIndex];
        nextSfxSourceIndex = (nextSfxSourceIndex + 1) % sfxSources.Count;

        source.volume = Mathf.Clamp01(volumeScale) * sfxVolume * masterVolume;
        source.PlayOneShot(clip);
    }

    /// <summary>
    /// Plays a named SFX from soundBank. This is the call gameplay code actually uses
    /// (AudioDirector.Instance.PlaySfx("Hit")) — see SoundBank's doc comment for why events are
    /// looked up by name instead of every script holding its own AudioClip reference.
    /// </summary>
    public void PlaySfx(string eventName, float volumeScale = 1f)
    {
        if (soundBank == null)
        {
            Debug.LogError("AudioDirector: PlaySfx(string) called but no soundBank is assigned.");
            return;
        }
        PlaySfx(soundBank.GetClip(eventName), volumeScale);
    }

    // ------------------------------------------------------------------
    // Music
    // ------------------------------------------------------------------

    /// <summary>
    /// Crossfades to a new looping music track. Calling this again with the SAME clip that is already
    /// playing is a no-op — a scene reload (Retry) must not restart the loop from bar 1 and audibly
    /// stutter the music that was already playing fine.
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || clip == currentMusicClip) return;
        currentMusicClip = clip;

        if (musicCrossfadeRoutine != null) StopCoroutine(musicCrossfadeRoutine);
        musicCrossfadeRoutine = StartCoroutine(CrossfadeMusic(clip));
    }

    public void StopMusic()
    {
        currentMusicClip = null;
        if (musicCrossfadeRoutine != null) StopCoroutine(musicCrossfadeRoutine);
        musicCrossfadeRoutine = StartCoroutine(FadeOutMusic());
    }

    private System.Collections.IEnumerator CrossfadeMusic(AudioClip clip)
    {
        AudioSource fadingIn = activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;
        AudioSource fadingOut = activeMusicSource;

        fadingIn.clip = clip;
        fadingIn.volume = 0f;
        fadingIn.Play();

        float targetVolume = musicVolume * masterVolume;
        float elapsed = 0f;
        while (elapsed < musicCrossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // unscaled: music must keep fading while the pause menu freezes Time.timeScale.
            float t = musicCrossfadeDuration > 0f ? elapsed / musicCrossfadeDuration : 1f;
            fadingIn.volume = Mathf.Lerp(0f, targetVolume, t);
            fadingOut.volume = Mathf.Lerp(targetVolume, 0f, t);
            yield return null;
        }

        fadingIn.volume = targetVolume;
        fadingOut.volume = 0f;
        fadingOut.Stop();
        activeMusicSource = fadingIn;
        musicCrossfadeRoutine = null;
    }

    private System.Collections.IEnumerator FadeOutMusic()
    {
        AudioSource fadingOut = activeMusicSource;
        float startVolume = fadingOut.volume;
        float elapsed = 0f;
        while (elapsed < musicCrossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = musicCrossfadeDuration > 0f ? elapsed / musicCrossfadeDuration : 1f;
            fadingOut.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        fadingOut.volume = 0f;
        fadingOut.Stop();
        musicCrossfadeRoutine = null;
    }

    // ------------------------------------------------------------------
    // Volume
    // ------------------------------------------------------------------

    public void SetMasterVolume(float value) => SetVolumes(value, musicVolume, sfxVolume);
    public void SetMusicVolume(float value) => SetVolumes(masterVolume, value, sfxVolume);
    public void SetSfxVolume(float value) => SetVolumes(masterVolume, musicVolume, value);

    private void SetVolumes(float master, float music, float sfx)
    {
        masterVolume = Mathf.Clamp01(master);
        musicVolume = Mathf.Clamp01(music);
        sfxVolume = Mathf.Clamp01(sfx);

        // Applied immediately to whichever music source is currently active — a slider drag must be
        // heard live, not only on the next PlayMusic call.
        float targetVolume = musicVolume * masterVolume;
        if (activeMusicSource != null && musicCrossfadeRoutine == null)
        {
            activeMusicSource.volume = targetVolume;
        }

        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.Save();

        OnVolumesChanged?.Invoke(masterVolume, musicVolume, sfxVolume);
    }

    private void LoadVolumePrefs()
    {
        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
    }
}
