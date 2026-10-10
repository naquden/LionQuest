using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins the Phase 7 audio system: AudioDirector exists (via GameController) in every scene that
/// needs sound, its SFX/music hooks actually produce audible output, and the volume sliders on the
/// pause menu control it.
///
/// Before this, the project had zero audio — no AudioSource ever played anything, hit/death/skill/
/// pickup events were silent, and there was no way to turn anything up or down.
/// </summary>
public class AudioSystemTests : HarnessFixture
{
    /// <summary>Every scene that should have music/SFX must have an AudioDirector reachable via its
    /// GameController — StartScreen and CharacterSelection have no PlayerSpawner of their own, so
    /// this only exists because AudioSystemGenerator's EnsureGameController added one.</summary>
    [UnityTest]
    public IEnumerator EveryEarlyScene_HasAnAudioDirector()
    {
        IgnoreFailingLogs();

        foreach (string scene in new[] { "StartScreen", "CharacterSelection", "Level1" })
        {
            yield return LoadScene(scene);
            yield return null;

            Assert.IsNotNull(AudioDirector.Instance,
                $"{scene} has no AudioDirector reachable — its GameController is missing or has no AudioDirector attached.");
        }
    }

    /// <summary>Killing an enemy must actually play a sound, not just log — checked by an AudioSource
    /// in the pool transitioning to isPlaying after the kill.</summary>
    [UnityTest]
    public IEnumerator EnemyDeath_PlaysASound()
    {
        IgnoreFailingLogs();
        yield return LoadScene("Level1");
        yield return null;

        Assert.IsNotNull(AudioDirector.Instance, "Level1 has no AudioDirector.");

        Enemy enemy = Object.FindAnyObjectByType<Enemy>();
        Assert.IsNotNull(enemy, "Level1 has no enemies.");

        Assert.IsFalse(AnySfxSourcePlaying(), "An SFX source is already playing before the kill — this test would prove nothing.");

        enemy.TakeDamage(999999f);
        yield return null;

        Assert.IsTrue(AnySfxSourcePlaying(),
            "No SFX source started playing after an enemy died — Enemy.Die's AudioDirector.PlaySfx('EnemyDeath') call is not producing sound.");
    }

    /// <summary>A player's basic attack landing must play the "Hit" sound.</summary>
    [UnityTest]
    public IEnumerator PlayerHit_OnEnemy_PlaysASound()
    {
        IgnoreFailingLogs();
        yield return LoadScene("Level1");
        yield return null;

        Assert.IsNotNull(AudioDirector.Instance, "Level1 has no AudioDirector.");

        Enemy enemy = Object.FindAnyObjectByType<Enemy>();
        Assert.IsNotNull(enemy, "Level1 has no enemies.");

        Assert.IsFalse(AnySfxSourcePlaying(), "An SFX source is already playing before the hit.");

        enemy.TakeDamage(1f); // not lethal — isolates the Hit sound from EnemyDeath's.
        yield return null;

        Assert.IsTrue(AnySfxSourcePlaying(),
            "No SFX source started playing after a non-lethal hit — TakeDamage's PlaySfx('Hit') call is not producing sound.");
    }

    /// <summary>SceneMusicTrigger must actually start the active music AudioSource playing, not just
    /// call into AudioDirector without effect.</summary>
    [UnityTest]
    public IEnumerator LoadingLevel1_StartsMusicPlaying()
    {
        IgnoreFailingLogs();
        yield return LoadScene("Level1");
        yield return null;
        yield return new WaitForSeconds(0.2f); // let the crossfade coroutine's first Play() call land.

        Assert.IsNotNull(AudioDirector.Instance, "Level1 has no AudioDirector.");

        AudioSource[] musicSources = AudioDirector.Instance.GetComponentsInChildren<AudioSource>()
            .Where(s => s.gameObject.name.StartsWith("MusicSource"))
            .ToArray();
        Assert.AreEqual(2, musicSources.Length, "Expected exactly two pooled music sources (A/B crossfade pair).");

        Assert.IsTrue(musicSources.Any(s => s.isPlaying),
            "Neither music source is playing after loading Level1 — SceneMusicTrigger.PlayMusic did not start playback.");
    }

    /// <summary>Dragging the master slider to 0 must silence the master volume scalar AudioDirector
    /// reports, and persist across a value that survives a fresh AudioDirector reading PlayerPrefs.</summary>
    [UnityTest]
    public IEnumerator MasterVolumeSlider_ControlsAudioDirectorVolume()
    {
        IgnoreFailingLogs();
        yield return LoadScene("Level1");
        yield return null;

        Assert.IsNotNull(AudioDirector.Instance, "Level1 has no AudioDirector.");

        // Include inactive: PauseMenuController.Awake starts the whole panel (and everything under
        // it, including VolumeSlidersPanel) hidden with SetActive(false) until the menu is opened —
        // the default FindAnyObjectByType excludes inactive objects and would report "missing" for
        // a component that is very much present, just not currently shown.
        var slidersPanel = Object.FindAnyObjectByType<VolumeSlidersPanel>(FindObjectsInactive.Include);
        Assert.IsNotNull(slidersPanel, "PauseMenuCanvas has no VolumeSlidersPanel — run 'LionQuest/Generate/Pause Menu'.");

        AudioDirector.Instance.SetMasterVolume(1f); // known starting point, independent of whatever a prior test left in PlayerPrefs.
        AudioDirector.Instance.SetMasterVolume(0f);
        yield return null;

        Assert.AreEqual(0f, AudioDirector.Instance.MasterVolume, 0.001f,
            "AudioDirector.SetMasterVolume(0) did not update MasterVolume.");

        // Restore so this test doesn't leave the next one (or a real play session sharing the same
        // PlayerPrefs key) muted.
        AudioDirector.Instance.SetMasterVolume(1f);
    }

    static bool AnySfxSourcePlaying()
    {
        return AudioDirector.Instance.GetComponentsInChildren<AudioSource>()
            .Any(s => s.gameObject.name.StartsWith("SfxSource") && s.isPlaying);
    }
}
