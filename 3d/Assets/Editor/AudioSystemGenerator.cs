using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires the audio system into the project: builds the SoundBank asset from the placeholder clips
/// ProceduralAudioGenerator writes, adds AudioDirector to GameController.prefab (so it lives for the
/// whole session alongside GameSaveController), and adds a SceneMusicTrigger to each scene that
/// should have music.
///
/// Run 'LionQuest/Generate/Placeholder Audio' before this — it needs the clips to already exist on
/// disk to wire the bank's entries.
///
/// Dual entry point per project convention.
/// </summary>
public static class AudioSystemGenerator
{
    const string SoundBankPath = "Assets/Audio/SoundBank.asset";
    const string GameControllerPrefabPath = "Assets/Prefabs/GameController.prefab";

    const string StartScreenScenePath = "Assets/Scenes/StartScreen.unity";
    const string CharacterSelectionScenePath = "Assets/Scenes/CharacterSelection.unity";
    const string Level1ScenePath = "Assets/Scenes/Level1.unity";

    [MenuItem("LionQuest/Generate/Audio System")]
    public static void Generate()
    {
        SoundBank bank = BuildSoundBank();
        WireGameController(bank);

        // Clip name, not a captured AudioClip reference: WireSceneMusic calls OpenScene(Single),
        // which unloads the previously active scene and any asset with no remaining reference —
        // exactly the failure mode that made an earlier generator (EnemyArchetypeGenerator) wire
        // WaveSpawner with null entries despite the assets existing fine on disk. Reloading by path
        // AFTER each OpenScene, inside WireSceneMusic itself, avoids it here the same way.
        WireSceneMusic(StartScreenScenePath, "Music_Menu");
        WireSceneMusic(CharacterSelectionScenePath, "Music_Menu");
        WireSceneMusic(Level1ScenePath, "Music_Gameplay");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("AudioSystemGenerator: done. AudioDirector wired into GameController.prefab; " +
                  "music triggers added to StartScreen/CharacterSelection/Level1.");
    }

    static SoundBank BuildSoundBank()
    {
        UiTheme.EnsureFolder("Assets/Audio");

        SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(SoundBankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<SoundBank>();
            AssetDatabase.CreateAsset(bank, SoundBankPath);
        }

        // (event name, generated clip name) — the clip name is ProceduralAudioGenerator's own
        // asset naming; kept as a separate column here so gameplay code's event names (e.g. "Hit")
        // don't have to match the placeholder file names 1:1 if either changes later.
        var mappings = new (string eventName, string clipName)[]
        {
            ("Hit", "SFX_Hit"),
            ("EnemyDeath", "SFX_EnemyDeath"),
            ("PlayerDeath", "SFX_PlayerDeath"),
            ("SkillCast", "SFX_SkillCast"),
            ("Pickup", "SFX_Pickup"),
            ("UIClick", "SFX_UIClick"),
            ("Victory", "SFX_Victory"),
            ("GameOver", "SFX_GameOver"),
        };

        var entries = new SoundBank.Entry[mappings.Length];
        for (int i = 0; i < mappings.Length; i++)
        {
            AudioClip clip = ProceduralAudioGenerator.Load(mappings[i].clipName);
            if (clip == null)
            {
                Debug.LogError($"AudioSystemGenerator: clip '{mappings[i].clipName}' not found — " +
                               "run 'LionQuest/Generate/Placeholder Audio' first.");
            }
            entries[i] = new SoundBank.Entry { eventName = mappings[i].eventName, clip = clip };
        }

        var so = new SerializedObject(bank);
        SerializedProperty entriesProp = so.FindProperty("entries");
        entriesProp.arraySize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
            entryProp.FindPropertyRelative("eventName").stringValue = entries[i].eventName;
            entryProp.FindPropertyRelative("clip").objectReferenceValue = entries[i].clip;
        }
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(bank);
        return bank;
    }

    static void WireGameController(SoundBank bank)
    {
        using var scope = new PrefabUtility.EditPrefabContentsScope(GameControllerPrefabPath);
        GameObject root = scope.prefabContentsRoot;

        AudioDirector director = root.GetComponent<AudioDirector>();
        if (director == null) director = root.AddComponent<AudioDirector>();

        var so = new SerializedObject(director);
        so.FindProperty("soundBank").objectReferenceValue = bank;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Instantiates GameController.prefab into the scene if it isn't there already. StartScreen and
    /// CharacterSelection have no PlayerSpawner (only Level1's does, via EnsureGameSaveController),
    /// so without this a player launching the game would hit AudioDirector.Instance == null on the
    /// very first two screens — SceneMusicTrigger's music, and every UiClickSound click, would be
    /// silent until Level1 finally creates the singleton. GameController is DontDestroyOnLoad, so
    /// creating it here means every later scene (including Level1, which also calls
    /// EnsureGameSaveController) just finds it already present.
    /// </summary>
    static void EnsureGameController(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (rootGo.GetComponentInChildren<AudioDirector>(true) != null) return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameControllerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"AudioSystemGenerator: {GameControllerPrefabPath} missing — cannot add it to {scene.path}.");
            return;
        }
        PrefabUtility.InstantiatePrefab(prefab, scene);
    }

    /// <summary>
    /// Adds (or updates) a SceneMusicTrigger GameObject in the given scene. Re-opens the scene with
    /// OpenSceneMode.Single per the project's established scene-editing pattern (see
    /// PlayerHudGenerator.WireIntoSampleScene) — every other generator that touches an existing
    /// scene does the same, rather than trying to append to whatever scene happens to be open.
    /// </summary>
    static void WireSceneMusic(string scenePath, string musicClipName)
    {
        if (!System.IO.File.Exists(scenePath))
        {
            Debug.LogError($"AudioSystemGenerator: scene not found at {scenePath} — skipping music wiring for it.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        EnsureGameController(scene);

        AudioClip musicClip = ProceduralAudioGenerator.Load(musicClipName);
        if (musicClip == null)
        {
            Debug.LogError($"AudioSystemGenerator: clip '{musicClipName}' not found — " +
                           $"run 'LionQuest/Generate/Placeholder Audio' first. Skipping music wiring for {scenePath}.");
            return;
        }

        SceneMusicTrigger existing = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            existing = rootGo.GetComponentInChildren<SceneMusicTrigger>(true);
            if (existing != null) break;
        }

        GameObject triggerGo = existing != null ? existing.gameObject : new GameObject("SceneMusicTrigger");
        SceneMusicTrigger trigger = triggerGo.GetComponent<SceneMusicTrigger>();
        if (trigger == null) trigger = triggerGo.AddComponent<SceneMusicTrigger>();

        var so = new SerializedObject(trigger);
        so.FindProperty("musicClip").objectReferenceValue = musicClip;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
