using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central manager for game logic, saves, and player tracking.
/// Handles global events like enemy kills and save operations.
/// </summary>
public class GameSaveController : MonoBehaviour
{
    public static GameSaveController Instance;
    
    [Header("Debug")]
    [SerializeField] private bool refreshData;
    [SerializeField] private GameSaveData debugSaveData;

    private List<CharacterStats> activePlayers = new List<CharacterStats>();

    /// <summary>
    /// One CharacterSaveData per characterID, shared by every CharacterStats instance bound to that
    /// character. This is the fix for the game's save model ("each character has its own collected
    /// souls; characters are shared between players"): before this, each CharacterStats instance
    /// loaded its OWN copy of the same characterID's data in LoadStats(), so two players who both
    /// picked "AssCap" had two independent souls counters that both wrote to the same disk record —
    /// whichever instance saved last won, and OnEnemyKilled awarded a kill's soul to both, doubling
    /// it. Routing every read/write through one shared object per characterID makes "shared
    /// character" literally true in memory, not just on disk.
    /// </summary>
    private readonly Dictionary<string, CharacterSaveData> sharedRecords = new Dictionary<string, CharacterSaveData>();

    /// <summary>characterIDs with in-memory changes not yet written to disk. Flushed on a timer
    /// (see Update) and immediately for meaningful transactions (purchases, leaving the run, quit) —
    /// see FlushCharacter/FlushAllDirtyRecords. Without debouncing, AddSoulsToCharacter wrote to disk
    /// on every single enemy kill, which in a four-player wave clear is a synchronous file write
    /// several times a second.</summary>
    private readonly HashSet<string> dirtyCharacterIDs = new HashSet<string>();

    private const float FlushIntervalSeconds = 3f;
    private float nextFlushTime;

    /// <summary>
    /// Every live player controller, registered by the controllers themselves in OnEnable.
    ///
    /// This exists so enemies have somewhere to ask "who should I chase?". They used to call
    /// GameObject.FindGameObjectWithTag("Player") once in Start and keep that transform forever,
    /// which meant every enemy in a four-player game chased whichever player happened to be first in
    /// the tag list — the other three could stand in a crowd untouched — and no enemy ever retargeted
    /// when that one player died. A registry rather than a per-enemy scene search also keeps the
    /// project's no-lookup-fallbacks rule: enemies read a list that players opt into.
    /// </summary>
    private readonly List<TopDownPlayerController> playerControllers = new List<TopDownPlayerController>();

    /// <summary>Live player controllers, in registration order. Read-only view for callers.</summary>
    public IReadOnlyList<TopDownPlayerController> PlayerControllers => playerControllers;

    private void OnValidate()
    {
        if (refreshData)
        {
            refreshData = false;
            LoadSaveDataToInspector();
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (dirtyCharacterIDs.Count == 0 || Time.unscaledTime < nextFlushTime) return;
        FlushAllDirtyRecords();
    }

    private void OnApplicationQuit()
    {
        FlushAllDirtyRecords();
    }

    private void OnDestroy()
    {
        // Scene unload / editor stop mid-run must not silently drop up to FlushIntervalSeconds of
        // unsaved souls/purchases — this is the same "don't lose progress on the way out" guarantee
        // OnApplicationQuit provides, for every other path that ends this controller's lifetime.
        if (Instance == this) FlushAllDirtyRecords();
    }

    // --- Shared per-character records ---

    /// <summary>
    /// The one CharacterSaveData for this characterID, loading it from disk on first access. Callers
    /// (CharacterStats) read AND write through this same object — there is exactly one souls counter
    /// and one purchasedSkillNodes list per character in memory, no matter how many players are
    /// currently piloting it.
    /// </summary>
    public CharacterSaveData GetSharedRecord(string characterID)
    {
        if (!sharedRecords.TryGetValue(characterID, out CharacterSaveData record))
        {
            record = SaveManager.LoadCharacter(characterID);
            sharedRecords[characterID] = record;
        }
        return record;
    }

    /// <summary>Marks a character's record as having unsaved changes. Called by CharacterStats after
    /// mutating the shared record it got from GetSharedRecord — this controller does not mutate souls/
    /// purchases itself, so it cannot detect changes on its own.</summary>
    public void MarkCharacterDirty(string characterID)
    {
        dirtyCharacterIDs.Add(characterID);
    }

    /// <summary>Writes one character's record to disk immediately, bypassing the debounce timer.
    /// Used for transactions where losing the write to a crash/quit racing the timer would be a
    /// player-visible loss: a skill purchase, or leaving the run via the pause menu.</summary>
    public void FlushCharacter(string characterID)
    {
        if (!sharedRecords.TryGetValue(characterID, out CharacterSaveData record)) return;

        SaveManager.SaveCharacter(record);
        dirtyCharacterIDs.Remove(characterID);
    }

    /// <summary>Writes every dirty character's record to disk and resets the debounce timer. Public
    /// so PauseMenuController's save-on-game-over/victory path (and other broad "save everything"
    /// call sites) can flush by the same route CharacterStats' per-kill soul gains use, rather than
    /// walking the scene for every CharacterStats instance.</summary>
    public void FlushAllDirtyRecords()
    {
        foreach (string characterID in dirtyCharacterIDs)
        {
            if (sharedRecords.TryGetValue(characterID, out CharacterSaveData record))
            {
                SaveManager.SaveCharacter(record);
            }
        }
        dirtyCharacterIDs.Clear();
        nextFlushTime = Time.unscaledTime + FlushIntervalSeconds;
    }

    // --- Player Registration ---
    
    public void RegisterPlayer(CharacterStats player)
    {
        if (!activePlayers.Contains(player))
        {
            activePlayers.Add(player);
            // Debug.Log($"[GameController] Registered player: {player.characterID}");
        }
    }
    
    public void UnregisterPlayer(CharacterStats player)
    {
        if (activePlayers.Contains(player))
        {
            activePlayers.Remove(player);
            // Debug.Log($"[GameController] Unregistered player: {player.characterID}");
        }
    }
    
    public void RegisterPlayerController(TopDownPlayerController controller)
    {
        if (controller != null && !playerControllers.Contains(controller))
        {
            playerControllers.Add(controller);
        }
    }

    public void UnregisterPlayerController(TopDownPlayerController controller)
    {
        playerControllers.Remove(controller);
    }

    /// <summary>
    /// The living player nearest <paramref name="from"/>, or null when every player is down.
    ///
    /// Enemies call this repeatedly rather than caching a target for life, so a kill or a revive
    /// redistributes aggro instead of leaving enemies locked onto a corpse. Squared distances — this
    /// runs once per enemy per retarget interval and the actual distance is never needed.
    /// </summary>
    public TopDownPlayerController FindNearestLivingPlayer(Vector3 from)
    {
        TopDownPlayerController nearest = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < playerControllers.Count; i++)
        {
            TopDownPlayerController candidate = playerControllers[i];
            if (candidate == null || !candidate.IsAlive) continue;

            float sqr = (candidate.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                nearest = candidate;
            }
        }

        return nearest;
    }

    /// <summary>True while any registered player is still alive.</summary>
    public bool AnyPlayerAlive()
    {
        for (int i = 0; i < playerControllers.Count; i++)
        {
            if (playerControllers[i] != null && playerControllers[i].IsAlive) return true;
        }
        return false;
    }

    // --- Game Events ---

    public void OnEnemyKilled(Enemy enemy)
    {
        // Award 1 soul to each DISTINCT character currently in play, not to each player instance.
        // Two players sharing one character must earn that character +1 soul per kill, same as a
        // solo player would — otherwise a shared character's economy scales with how many
        // controllers happen to be piloting it, which contradicts "each character has its own
        // collected souls" (souls belong to the character, headcount is not a multiplier on it).
        // A player parked on their own distinct character is unaffected: co-op still earns souls
        // per kill for everyone, exactly as before, only the double-count on a SHARED character
        // is removed.
        var rewardedCharacterIDs = new HashSet<string>();
        int rewardedCount = 0;
        foreach (var player in activePlayers)
        {
            if (player == null) continue;
            if (!rewardedCharacterIDs.Add(player.characterID)) continue; // already paid this characterID this kill.

            player.AddSouls(1);
            rewardedCount++;
        }

        if (rewardedCount > 0)
        {
            // Debug.Log($"[GameController] Enemy killed! Awarded 1 soul to {rewardedCount} distinct character(s).");
        }
    }

    /// <summary>
    /// Manually triggers a save for all active players.
    /// Useful for checkpoints or "Save & Quit" buttons.
    /// </summary>
    public void SaveAllActivePlayers()
    {
        CharacterStats[] players = FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.ForceSave();
        }
        Debug.Log($"[GameSaveController] Saved data for {players.Length} players.");
    }

    /// <summary>
    /// Deletes the entire save file (All players, all progress).
    /// </summary>
    [ContextMenu("Delete All Save Data")]
    public void DeleteAllSaveData()
    {
        SaveManager.DeleteSave();

        // sharedRecords caches the pre-delete CharacterSaveData objects in memory. Without dropping
        // them here, every live CharacterStats keeps reading its already-loaded record via
        // GetSharedRecord — the disk file is gone but nothing reflects that until a fresh
        // characterID happens to be requested. dirtyCharacterIDs goes with it: a pending write for
        // data that no longer exists must not resurrect the deleted save on the next flush.
        sharedRecords.Clear();
        dirtyCharacterIDs.Clear();

        // Refresh active players to reflect reset
        CharacterStats[] players = FindObjectsByType<CharacterStats>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.ReloadFromDisk();
        }
    }

    [ContextMenu("Load Save Data to Inspector")]
    public void LoadSaveDataToInspector()
    {
        debugSaveData = SaveManager.GetRawData();
        Debug.Log("[GameController] Debug save data refreshed in Inspector.");
    }
}
