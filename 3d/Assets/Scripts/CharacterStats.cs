using System.Collections.Generic;
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

    [Tooltip("Set by PlayerSpawner before spawn. Source of truth for this character's skill trees.")]
    public CharacterDefinition definition;

    [Header("Resources")]
    public int souls = 0;

    // Events
    public System.Action<int> OnSoulsChanged;
    public System.Action OnStatsUpgraded;

    // Purchased skill-tree node keys ("skillId/nodeId:rank"). Persisted; the tree itself is not.
    private List<string> purchasedSkillNodes = new List<string>();

    // Derived per-(skillId, effect) modifiers, rebuilt from purchasedSkillNodes whenever it changes.
    // Never serialized — the tree + purchase set are the only source of truth.
    private Dictionary<(string skillId, SkillEffect effect), (float add, float mul)> resolvedSkillValues
        = new Dictionary<(string, SkillEffect), (float, float)>();

    private int strengthLevel = 1;
    private int healthLevel = 1;
    private int speedLevel = 1;

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

    public void AddSouls(int amount)
    {
        souls += amount;
        SaveStats();
        OnSoulsChanged?.Invoke(souls);
        // Debug.Log($"[{characterID}] Gained {amount} Soul(s). Total: {souls}");
    }

    // --- Skill Tree ---

    public bool IsSkillUnlocked(string skillId)
    {
        return SkillTreeService.IsSkillUnlocked(definition, BuildSaveSnapshot(), skillId);
    }

    public int GetNodeRank(string skillId, string nodeId)
    {
        return SkillTreeService.GetRank(BuildSaveSnapshot(), skillId, nodeId);
    }

    public bool CanPurchaseNode(string skillId, string nodeId)
    {
        return SkillTreeService.CanPurchase(definition, BuildSaveSnapshot(), skillId, nodeId);
    }

    /// <summary>Spends souls, advances the node's rank, rebuilds resolved-value/attribute caches,
    /// saves, and fires events.</summary>
    public bool TryPurchaseNode(string skillId, string nodeId)
    {
        CharacterSaveData snapshot = BuildSaveSnapshot();
        if (!SkillTreeService.TryPurchase(definition, snapshot, skillId, nodeId))
        {
            return false;
        }

        souls = snapshot.souls;
        purchasedSkillNodes = snapshot.purchasedSkillNodes;

        RebuildSkillCache();
        SaveStats();
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
        return true;
    }

    /// <summary>Resolved value for a skill's effect: (SO base + sum of Add nodes, scaled by rank) *
    /// product of Mul nodes (scaled by rank). Returns fallback untouched if no purchased node
    /// targets this effect.</summary>
    public float GetSkillValue(string skillId, SkillEffect effect, float fallback)
    {
        return SkillTreeService.GetValue(resolvedSkillValues, skillId, effect, fallback);
    }

    private void RebuildSkillCache()
    {
        CharacterSaveData snapshot = BuildSaveSnapshot();
        resolvedSkillValues = SkillTreeService.ResolveSkillValues(definition, snapshot);
        strengthLevel = SkillTreeService.GetAttributeLevel(definition, snapshot, SkillEffect.Strength);
        healthLevel = SkillTreeService.GetAttributeLevel(definition, snapshot, SkillEffect.Health);
        speedLevel = SkillTreeService.GetAttributeLevel(definition, snapshot, SkillEffect.Speed);
    }

    private CharacterSaveData BuildSaveSnapshot()
    {
        CharacterSaveData data = new CharacterSaveData(characterID);
        data.souls = souls;
        data.purchasedSkillNodes = purchasedSkillNodes;
        return data;
    }

    // --- Stat Getters ---

    public float GetDamageMultiplier()
    {
        return 1f + ((strengthLevel - 1) * 0.2f);
    }

    public float GetMaxHealth()
    {
        return BaseMaxHealth + ((healthLevel - 1) * 20f);
    }

    /// <summary>Base health from the definition. Falls back to the historical 100 when definition is
    /// null — that is the prefab-asset / scene-authored player case, which PlayerSpawner never
    /// produces. Deliberately silent: SkillIdForSlot already LogErrors loudly for a null definition.</summary>
    private float BaseMaxHealth => definition != null ? definition.baseMaxHealth : 100f;

    /// <summary>Base move speed from the definition, before Body upgrades. Callers pass their own
    /// serialized fallback (the prefab's authored moveSpeed) for the null-definition case, so a
    /// scene-authored player without a definition keeps whatever speed it was given in the Inspector.</summary>
    public float GetBaseMoveSpeed(float fallback)
    {
        return definition != null ? definition.baseMoveSpeed : fallback;
    }

    /// <summary>Passive move-speed multiplier. Lvl 1 = 1.0x, +10% per level.</summary>
    public float GetMoveSpeedMultiplier()
    {
        return 1f + ((speedLevel - 1) * 0.1f);
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
        data.purchasedSkillNodes = new List<string>(purchasedSkillNodes);

        SaveManager.SaveCharacter(data);
    }

    private void LoadStats()
    {
        CharacterSaveData data = SaveManager.LoadCharacter(characterID);

        souls = data.souls;
        purchasedSkillNodes = new List<string>(data.purchasedSkillNodes);

        RebuildSkillCache();

        // Trigger initial update
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
    }

    [ContextMenu("Reset Stats")]
    public void ResetStats()
    {
        souls = 0;
        purchasedSkillNodes.Clear();
        SaveStats();

        RebuildSkillCache();
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
        Debug.Log($"[{characterID}] Stats reset!");
    }
}
