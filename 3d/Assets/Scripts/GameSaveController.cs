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
    
    // --- Game Events ---
    
    public void OnEnemyKilled(Enemy enemy)
    {
        // Award 1 soul to each active player
        int rewardedCount = 0;
        foreach (var player in activePlayers)
        {
            if (player != null)
            {
                player.AddSouls(1);
                rewardedCount++;
            }
        }
        
        if (rewardedCount > 0)
        {
            // Debug.Log($"[GameController] Enemy killed! Awarded 1 soul to {rewardedCount} player(s).");
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
