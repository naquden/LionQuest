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
    /// <summary>
    /// The record this character's souls/purchases actually live in — shared with every other
    /// CharacterStats bound to the same characterID (GameSaveController.GetSharedRecord). This is
    /// what makes two players on the same character share one souls counter rather than each keeping
    /// (and clobbering, last-write-wins) their own copy on save — see the doc comment on
    /// GameSaveController.sharedRecords for the bug this replaces. Requiring GameSaveController.Instance
    /// rather than falling back to a private record matches OnEnable's existing contract below
    /// ("will crash if GameSaveController is missing — desired behavior"): every real CharacterStats
    /// is created by PlayerSpawner, which guarantees a GameSaveController exists first.
    /// </summary>
    private CharacterSaveData Record => GameSaveController.Instance.GetSharedRecord(characterID);

    public int souls => Record.souls;

    // Events
    public System.Action<int> OnSoulsChanged;
    public System.Action OnStatsUpgraded;

    // Purchased skill-tree node keys ("skillId/nodeId:rank"). Persisted; the tree itself is not.
    private List<string> purchasedSkillNodes => Record.purchasedSkillNodes;

    // Derived per-(skillId, effect) modifiers, rebuilt from purchasedSkillNodes whenever it changes.
    // Never serialized — the tree + purchase set are the only source of truth.
    private Dictionary<(string skillId, SkillEffect effect), (float add, float mul)> resolvedSkillValues
        = new Dictionary<(string, SkillEffect), (float, float)>();

    private int strengthLevel = 1;
    private int healthLevel = 1;
    private int speedLevel = 1;

    private void Start()
    {
        // GetSharedRecord (via Record) triggers the actual disk load the first time this characterID
        // is touched; everything past that is just syncing this instance's derived caches to it.
        RebuildSkillCache();
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);

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
        Record.souls += amount;
        MarkDirty();
        OnSoulsChanged?.Invoke(souls);
        // Debug.Log($"[{characterID}] Gained {amount} Soul(s). Total: {souls}");
    }

    // --- Skill Tree ---

    public bool IsSkillUnlocked(string skillId)
    {
        return SkillTreeService.IsSkillUnlocked(definition, Record, skillId);
    }

    public int GetNodeRank(string skillId, string nodeId)
    {
        return SkillTreeService.GetRank(Record, skillId, nodeId);
    }

    public bool CanPurchaseNode(string skillId, string nodeId)
    {
        return SkillTreeService.CanPurchase(definition, Record, skillId, nodeId);
    }

    /// <summary>Spends souls, advances the node's rank, rebuilds resolved-value/attribute caches,
    /// saves, and fires events.</summary>
    public bool TryPurchaseNode(string skillId, string nodeId)
    {
        // TryPurchase mutates the CharacterSaveData it's given in place (souls -=, purchasedSkillNodes
        // add/remove) — passing Record directly means the shared record itself is what changes, not a
        // disposable snapshot that then has to be copied back in.
        if (!SkillTreeService.TryPurchase(definition, Record, skillId, nodeId))
        {
            return false;
        }

        RebuildSkillCache();
        // A purchase is flushed immediately rather than left for the debounce timer: it is the one
        // souls-losing transaction a player would notice reverting if the process died before the
        // next periodic flush, unlike a kill's +1 soul which simply arrives again on the next kill.
        if (GameSaveController.Instance != null) GameSaveController.Instance.FlushCharacter(characterID);
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
        resolvedSkillValues = SkillTreeService.ResolveSkillValues(definition, Record);
        strengthLevel = SkillTreeService.GetAttributeLevel(definition, Record, SkillEffect.Strength);
        healthLevel = SkillTreeService.GetAttributeLevel(definition, Record, SkillEffect.Health);
        speedLevel = SkillTreeService.GetAttributeLevel(definition, Record, SkillEffect.Speed);
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

    /// <summary>Flushes this character's shared record to disk immediately, bypassing the debounce
    /// timer. Used by callers (GameSaveController.SaveAllActivePlayers, on pause/quit/leaving the run)
    /// that need a guarantee the write already happened by the time the call returns.</summary>
    public void ForceSave()
    {
        if (GameSaveController.Instance != null) GameSaveController.Instance.FlushCharacter(characterID);
    }

    /// <summary>
    /// Re-syncs THIS instance's cached derived state (resolvedSkillValues/strength/health/speedLevel)
    /// and fires its change events. Deliberately does NOT re-read the shared record from disk: with a
    /// shared record per characterID (see Record), "reload" for one instance touching an
    /// already-loaded character would mean discarding another player's unsaved in-memory progress on
    /// that same character and replacing it with the stale on-disk copy — exactly the last-write-wins
    /// race this phase exists to remove. GetSharedRecord itself only hits disk the FIRST time a given
    /// characterID is requested after a load/delete, which is the right time to actually re-read.
    /// </summary>
    public void ReloadFromDisk()
    {
        RebuildSkillCache();
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
    }

    /// <summary>Marks this character's record dirty for the next debounced flush (see
    /// GameSaveController.Update/FlushAllDirtyRecords).</summary>
    private void MarkDirty()
    {
        if (GameSaveController.Instance != null) GameSaveController.Instance.MarkCharacterDirty(characterID);
    }

    [ContextMenu("Reset Stats")]
    public void ResetStats()
    {
        Record.souls = 0;
        Record.purchasedSkillNodes.Clear();
        if (GameSaveController.Instance != null) GameSaveController.Instance.FlushCharacter(characterID);

        RebuildSkillCache();
        OnStatsUpgraded?.Invoke();
        OnSoulsChanged?.Invoke(souls);
        Debug.Log($"[{characterID}] Stats reset!");
    }
}
