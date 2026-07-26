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

    [Header("Skill 1 - Whirlwind")]
    public int skill1DamageLevel = 1;
    public int skill1RadiusLevel = 1;

    [Header("Skill Cooldowns")]
    public int skill1CooldownLevel = 1;
    public int skill2CooldownLevel = 1;
    public int skill3CooldownLevel = 1;

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
    public int GetSpeedCost() => speedLevel * 5;
    public int GetSkill1DamageCost() => skill1DamageLevel * 5;
    public int GetSkill1RadiusCost() => skill1RadiusLevel * 5;

    public int GetSkillCooldownCost(int skillIndex)
    {
        switch (skillIndex)
        {
            case 1: return skill1CooldownLevel * 5;
            case 2: return skill2CooldownLevel * 5;
            case 3: return skill3CooldownLevel * 5;
            default:
                Debug.LogError($"[{characterID}] GetSkillCooldownCost: invalid skillIndex {skillIndex}");
                return int.MaxValue;
        }
    }

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

    public bool TryUpgradeSpeed()
    {
        int cost = GetSpeedCost();
        if (souls >= cost)
        {
            souls -= cost;
            speedLevel++;
            SaveStats();
            OnStatsUpgraded?.Invoke();
            OnSoulsChanged?.Invoke(souls);
            return true;
        }
        return false;
    }

    public bool TryUpgradeSkill1Damage()
    {
        int cost = GetSkill1DamageCost();
        if (souls >= cost)
        {
            souls -= cost;
            skill1DamageLevel++;
            SaveStats();
            OnStatsUpgraded?.Invoke();
            OnSoulsChanged?.Invoke(souls);
            return true;
        }
        return false;
    }

    public bool TryUpgradeSkill1Radius()
    {
        int cost = GetSkill1RadiusCost();
        if (souls >= cost)
        {
            souls -= cost;
            skill1RadiusLevel++;
            SaveStats();
            OnStatsUpgraded?.Invoke();
            OnSoulsChanged?.Invoke(souls);
            return true;
        }
        return false;
    }

    public bool TryUpgradeSkillCooldown(int skillIndex)
    {
        int cost = GetSkillCooldownCost(skillIndex);
        if (souls >= cost)
        {
            souls -= cost;
            switch (skillIndex)
            {
                case 1: skill1CooldownLevel++; break;
                case 2: skill2CooldownLevel++; break;
                case 3: skill3CooldownLevel++; break;
                default:
                    Debug.LogError($"[{characterID}] TryUpgradeSkillCooldown: invalid skillIndex {skillIndex}");
                    return false;
            }
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

    /// <summary>Whirlwind (Skill 1) damage multiplier applied to basic-attack damage. 2x at level 1.</summary>
    public float GetSkill1DamageMultiplier()
    {
        return 2f + ((skill1DamageLevel - 1) * 0.25f);
    }

    /// <summary>Whirlwind (Skill 1) hit-circle radius in world units. 3.0 at level 1.</summary>
    public float GetSkill1Radius()
    {
        return 3f + ((skill1RadiusLevel - 1) * 0.5f);
    }

    /// <summary>Battle Cry (Skill 3) temporary move-speed multiplier. 2x at level 1.</summary>
    public float GetBattleCryMultiplier()
    {
        return 2f + ((speedLevel - 1) * 0.25f);
    }

    /// <summary>Multiplier applied to a skill's base cooldown (1-indexed). Floors at 0.4 (max 60% reduction).</summary>
    public float GetSkillCooldownMultiplier(int skillIndex)
    {
        int level;
        switch (skillIndex)
        {
            case 1: level = skill1CooldownLevel; break;
            case 2: level = skill2CooldownLevel; break;
            case 3: level = skill3CooldownLevel; break;
            default:
                Debug.LogError($"[{characterID}] GetSkillCooldownMultiplier: invalid skillIndex {skillIndex}");
                return 1f;
        }
        return Mathf.Max(0.4f, 1f - ((level - 1) * 0.1f));
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
        data.skill1DamageLevel = skill1DamageLevel;
        data.skill1RadiusLevel = skill1RadiusLevel;
        data.skill1CooldownLevel = skill1CooldownLevel;
        data.skill2CooldownLevel = skill2CooldownLevel;
        data.skill3CooldownLevel = skill3CooldownLevel;
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
        skill1DamageLevel = data.skill1DamageLevel;
        skill1RadiusLevel = data.skill1RadiusLevel;
        skill1CooldownLevel = data.skill1CooldownLevel;
        skill2CooldownLevel = data.skill2CooldownLevel;
        skill3CooldownLevel = data.skill3CooldownLevel;

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
        skill1DamageLevel = 1;
        skill1RadiusLevel = 1;
        skill1CooldownLevel = 1;
        skill2CooldownLevel = 1;
        skill3CooldownLevel = 1;
        SaveStats();

        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
        Debug.Log($"[{characterID}] Stats reset!");
    }
}
