using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lives in the gameplay scene. Spawns one player per confirmed CharacterSelection slot,
/// or a single default player when launched directly (no selection data).
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints = System.Array.Empty<Transform>();
    [SerializeField] private CharacterDefinition fallbackDefinition;
    [SerializeField] private GameObject gameControllerPrefab;
    [SerializeField] private CharacterDefinition[] allCharacters = System.Array.Empty<CharacterDefinition>();
    [SerializeField] private PlayerHUD hud;
    [SerializeField] private PauseMenuController pauseMenu;

    [Tooltip("Follow camera to register spawned players with. Optional — leave empty in scenes with a fixed camera.")]
    [SerializeField] private TopDownCameraController followCamera;

    [Tooltip("Scene terrain, injected into each spawned player for ground-type speed, hole detection " +
             "and island bounds. Optional — leave empty only in deliberately terrain-free scenes.")]
    [SerializeField] private MapGenerator mapGenerator;

    [Tooltip("How many players to spawn when launched directly with no CharacterSelection data. " +
             "1 for normal scenes; raise it to stand up a local-coop scene that can be entered by " +
             "pressing Play, without walking the whole join flow first.")]
    [SerializeField] private int fallbackPlayerCount = 1;

    private void Start()
    {
        EnsureGameSaveController();

        if (CharacterSelectionData.Instance == null || CharacterSelectionData.Instance.selections.Count == 0)
        {
            SpawnFallbackPlayer();
            return;
        }

        var selections = CharacterSelectionData.Instance.selections;
        for (int i = 0; i < selections.Count; i++)
        {
            SpawnSelectedPlayer(selections[i], i);
        }
    }

    private void EnsureGameSaveController()
    {
        if (GameSaveController.Instance != null) return;

        if (gameControllerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: GameSaveController.Instance is missing and no gameControllerPrefab is assigned!");
            return;
        }

        Instantiate(gameControllerPrefab);
    }

    private void SpawnFallbackPlayer()
    {
        if (fallbackDefinition == null || fallbackDefinition.playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: no CharacterSelectionData and no fallbackDefinition assigned — cannot spawn a player.");
            return;
        }

        // Each fallback player gets its own spawn point, so a multi-player fallback scene does not
        // stack every player on one spot (co-located rigidbodies shove each other apart, which would
        // corrupt any measurement of where players actually are).
        int count = Mathf.Max(1, fallbackPlayerCount);
        if (count > spawnPoints.Length && spawnPoints.Length > 0)
        {
            Debug.LogError($"PlayerSpawner: fallbackPlayerCount is {count} but only {spawnPoints.Length} " +
                           "spawn points are assigned — the extra players would spawn on top of each other.");
            count = spawnPoints.Length;
        }

        for (int i = 0; i < count; i++)
        {
            Transform point = i < spawnPoints.Length ? spawnPoints[i] : transform;
            SpawnPlayer(fallbackDefinition, point, pairWithDevice: null, playerIndex: i);
        }
    }

    private void SpawnSelectedPlayer(CharacterSelectionData.Selection selection, int index)
    {
        CharacterDefinition definition = FindDefinition(selection.characterID);
        if (definition == null || definition.playerPrefab == null)
        {
            Debug.LogError($"PlayerSpawner: no CharacterDefinition/playerPrefab found for characterID '{selection.characterID}'.");
            return;
        }

        Transform point = index < spawnPoints.Length ? spawnPoints[index] : transform;

        InputDevice device = selection.deviceId >= 0 ? InputSystem.GetDeviceById(selection.deviceId) : null;
        SpawnPlayer(definition, point, device, index);
    }

    private void SpawnPlayer(CharacterDefinition definition, Transform point, InputDevice pairWithDevice, int playerIndex)
    {
        GameObject instance = Instantiate(definition.playerPrefab);
        instance.SetActive(false);

        // Injected before the instance is activated, so the controller's own Start (which validates
        // the reference) already sees it. A prefab cannot carry a scene reference itself, and the
        // controller is not allowed to go looking for one.
        TopDownPlayerController spawnedController = instance.GetComponent<TopDownPlayerController>();
        if (spawnedController != null) spawnedController.SetMapGenerator(mapGenerator);

        CharacterStats stats = instance.GetComponent<CharacterStats>();
        if (stats != null)
        {
            stats.characterID = definition.characterID;
            stats.definition = definition;
            // Load unlock/soul state now so HUD binding below sees correct skill-lock state —
            // CharacterStats.Start also calls this later, but Start ordering vs PlayerSpawner's
            // own Start is unenforced by Unity, so BindPlayer must not depend on it.
            stats.ReloadFromDisk();
        }

        instance.transform.position = point.position;
        instance.transform.rotation = point.rotation;

        instance.SetActive(true);

        // Pairing MUST happen after SetActive, not before: PlayerInput.OnEnable runs
        // AssignUserAndDevices(), which starts by calling m_InputUser.UnpairDevices() and then
        // auto-picks the first control scheme whose devices are free. "Keyboard&Mouse" is first in
        // InputSystem_Actions, so any pre-activation pairing is discarded and a gamepad player
        // silently ends up on the keyboard — its buttons then do nothing and the HUD prints
        // keyboard prompts. SwitchCurrentControlScheme re-pairs and picks the scheme that matches.
        if (pairWithDevice != null)
        {
            PlayerInput pi = instance.GetComponent<PlayerInput>();
            if (pi != null && !pi.SwitchCurrentControlScheme(SchemeDevicesFor(pairWithDevice)))
            {
                Debug.LogError($"PlayerSpawner: no control scheme in the input actions matches device '{pairWithDevice}' — player {playerIndex} will not respond to it.");
            }
        }

        // Players are created at runtime, so a follow camera cannot have them wired in the
        // Inspector. Registering here (rather than letting the camera search by tag) keeps the
        // no-lookup-fallbacks rule: the camera reference is the one assigned on this spawner.
        if (followCamera != null) followCamera.AddPlayer(instance.transform);

        if (pauseMenu != null)
        {
            PlayerInput spawnedInput = instance.GetComponent<PlayerInput>();
            if (spawnedInput != null) pauseMenu.RegisterPlayer(spawnedInput);

            // Also register the controller itself: the pause menu doubles as the game-over screen
            // and needs each player's OnDeath to know when the run is over.
            TopDownPlayerController deathSource = instance.GetComponent<TopDownPlayerController>();
            if (deathSource != null) pauseMenu.RegisterPlayer(deathSource);
        }
        else
        {
            Debug.LogError("PlayerSpawner: pauseMenu is not assigned — pause menu will not be able to lock this player's input, and death will not trigger game over.");
        }

        if (hud != null)
        {
            TopDownPlayerController controller = instance.GetComponent<TopDownPlayerController>();
            if (controller != null && stats != null)
            {
                hud.BindPlayer(playerIndex, controller, stats, definition);
            }
        }
        else
        {
            Debug.LogError("PlayerSpawner: hud is not assigned — no HUD will be shown.");
        }
    }

    /// <summary>
    /// The device set to hand SwitchCurrentControlScheme. A keyboard player also needs the mouse:
    /// Mouse is an optional requirement of the "Keyboard&amp;Mouse" scheme, so passing the keyboard
    /// alone matches the scheme but leaves Look/aim bindings unpaired.
    /// </summary>
    private static InputDevice[] SchemeDevicesFor(InputDevice device)
    {
        if (device is Keyboard && Mouse.current != null) return new[] { device, (InputDevice)Mouse.current };
        return new[] { device };
    }

    private CharacterDefinition FindDefinition(string characterID)
    {
        foreach (CharacterDefinition def in allCharacters)
        {
            if (def != null && def.characterID == characterID) return def;
        }
        return null;
    }
}
