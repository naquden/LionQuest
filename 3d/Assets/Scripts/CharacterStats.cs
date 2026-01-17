using UnityEngine;

/// <summary>
/// Manages individual character stats, currency, and progression.
/// Attach this to the Player GameObject.
/// </summary>
public class CharacterStats : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique ID for this character class (e.g. 'Warrior', 'Mage')")]
    public string characterID = "Warrior";
    
    [Header("Resources")]
    public int souls = 0;
    
    [Header("Attributes")]
    public int strengthLevel = 1;
    public int healthLevel = 1;
    public int speedLevel = 1;
    
    // Events
    public System.Action<int> OnSoulsChanged;
    public System.Action OnStatsUpgraded;
    
    private void Start()
    {
        LoadStats();
        // Ensure registration happens even if OnEnable ran before GameController was ready
        if (GameSaveController.Instance != null)
        {
            GameSaveController.Instance.RegisterPlayer(this);
        }
    }
    
    private void OnEnable()
    {
        // Will crash if GameSaveController is missing (Desired behavior)
        GameSaveController.Instance.RegisterPlayer(this);
    }
    
    private void OnDisable()
    {
        // Check for null here only because Instance might be destroyed on game quit before player
        if (GameSaveController.Instance != null)
        {
            GameSaveController.Instance.UnregisterPlayer(this);
        }
    }

    // --- Upgrade Logic ---

    public int GetStrengthCost() => strengthLevel * 5;
    public int GetHealthCost() => healthLevel * 5;

    public void AddSouls(int amount)
    {
        souls += amount;
        SaveStats();
        OnSoulsChanged?.Invoke(souls);
        // Debug.Log($"[{characterID}] Gained {amount} Soul(s). Total: {souls}");
    }

    public bool TryUpgradeStrength()
    {
        int cost = GetStrengthCost();
        if (souls >= cost)
        {
            souls -= cost;
            strengthLevel++;
            SaveStats();
            OnStatsUpgraded?.Invoke();
            OnSoulsChanged?.Invoke(souls);
            return true;
        }
        return false;
    }

    public bool TryUpgradeHealth()
    {
        int cost = GetHealthCost();
        if (souls >= cost)
        {
            souls -= cost;
            healthLevel++;
            SaveStats();
            OnStatsUpgraded?.Invoke();
            OnSoulsChanged?.Invoke(souls);
            return true;
        }
        return false;
    }

    // --- Stat Getters ---

    public float GetDamageMultiplier()
    {
        return 1f + ((strengthLevel - 1) * 0.2f);
    }

    public float GetMaxHealth()
    {
        return 100f + ((healthLevel - 1) * 20f);
    }

    // --- Save/Load ---

    public void ForceSave()
    {
        SaveStats();
    }

    public void ReloadFromDisk()
    {
        LoadStats();
    }

    private void SaveStats()
    {
        CharacterSaveData data = new CharacterSaveData(characterID);
        data.souls = souls;
        data.strengthLevel = strengthLevel;
        data.healthLevel = healthLevel;
        data.speedLevel = speedLevel;
        // Skills would be added here later
        
        SaveManager.SaveCharacter(data);
    }

    private void LoadStats()
    {
        CharacterSaveData data = SaveManager.LoadCharacter(characterID);
        
        souls = data.souls;
        strengthLevel = data.strengthLevel;
        healthLevel = data.healthLevel;
        speedLevel = data.speedLevel;
        
        // Trigger initial update
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
    }
    
    [ContextMenu("Reset Stats")]
    public void ResetStats()
    {
        souls = 0;
        strengthLevel = 1;
        healthLevel = 1;
        speedLevel = 1;
        SaveStats();
        
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
        Debug.Log($"[{characterID}] Stats reset!");
    }
}
