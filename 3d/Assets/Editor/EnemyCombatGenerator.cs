using UnityEditor;
using UnityEngine;

/// <summary>
/// Wires the enemy's attack so it can actually damage the player.
///
/// Enemy.Attack() and CombatController have always existed, but three pieces of wiring were missing,
/// so an enemy standing next to the player played its swing animation and dealt nothing:
///
///   1. Enemy.attackData was never assigned on the prefab (the field post-dates the prefab's last
///      serialization, so it silently deserialized as null). PerformAttack was never even called.
///   2. CombatController.targetTag on the enemy read "Enemy" — the value the PLAYER's controller
///      wants. An enemy attack would have looked for enemies to hit, never the player.
///   3. Basic Slash Enemy.prefab (the effect PerformAttack spawns) had no AttackEffect component and
///      no collider at all, so PerformAttack logged "has no AttackEffect script" and destroyed it.
///      The player's Basic Slash AssCap prefab carries both; the enemy copy never got them.
///
/// This generator force-corrects all three plus the scalars they depend on, rather than only filling
/// blanks, so a later hand-edit or an older prefab revision can't leave the enemy toothless again.
///
/// Dual entry point per project convention.
/// </summary>
public static class EnemyCombatGenerator
{
    const string EnemyPrefabPath = "Assets/Characters/enemy/EnemyHero Variant.prefab";
    const string EnemyAttackDataPath = "Assets/Characters/enemy/BasicAttack.asset";
    const string EnemyEffectPrefabPath = "Assets/Characters/enemy/Basic Slash Enemy.prefab";
    const string PlayerTag = "Player";

    /// <summary>
    /// Damage one enemy swing deals. The asset shipped at 1, which is damage in name only: at a 2s
    /// attack cooldown it would take a player with 100 health over three minutes of standing still to
    /// die. 10 makes a hit something to react to (10 swings to kill) without being punishing.
    /// </summary>
    const float EnemyAttackDamage = 10f;

    /// <summary>Distance the enemy stops and swings from. Must be within reach of the spawned effect's
    /// hit sphere (attackPointDistance 0.8 + the effect's ~0.7 world radius), or the enemy would stop
    /// short and swing at empty air forever.</summary>
    const float EnemyStopDistance = 1.5f;

    const float EnemyAttackCooldown = 2f;

    /// <summary>Hit sphere on the enemy's slash effect, in LOCAL space. The prefab root is scaled to
    /// 0.386, matching the player's slash, so 1.85 local is ~0.71 world radius.</summary>
    const float EffectColliderRadius = 1.85f;

    [MenuItem("LionQuest/Generate/Enemy Combat")]
    public static void Generate()
    {
        bool ok = true;
        ok &= FixAttackDamage();
        ok &= FixEffectPrefab();
        ok &= FixEnemyPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (ok) Debug.Log("EnemyCombatGenerator: enemy attack wired — attackData, targetTag=Player, and hit collider.");
    }

    /// <summary>Raises the swing's damage off its placeholder value of 1.</summary>
    static bool FixAttackDamage()
    {
        var attackData = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttackDataPath);
        if (attackData == null)
        {
            Debug.LogError($"EnemyCombatGenerator: {EnemyAttackDataPath} missing — enemies cannot attack.");
            return false;
        }

        var so = new SerializedObject(attackData);
        so.FindProperty("damage").floatValue = EnemyAttackDamage;
        so.FindProperty("cooldown").floatValue = EnemyAttackCooldown;
        so.ApplyModifiedProperties();
        return true;
    }

    /// <summary>
    /// Gives the enemy's slash effect the AttackEffect + trigger collider that make it deal damage.
    /// Without these, CombatController.SpawnAttackEffect rejects the prefab outright.
    /// </summary>
    static bool FixEffectPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyEffectPrefabPath);
        if (root == null)
        {
            Debug.LogError($"EnemyCombatGenerator: could not open {EnemyEffectPrefabPath}.");
            return false;
        }

        try
        {
            // AttackEffect reads its collider with GetComponent, so both must sit on the root — the
            // same object the effect prefab is instantiated as.
            var sphere = root.GetComponent<SphereCollider>();
            if (sphere == null) sphere = root.AddComponent<SphereCollider>();
            sphere.radius = EffectColliderRadius;
            sphere.center = Vector3.zero;
            // Initialize() forces isTrigger on at runtime; set it here too so the prefab is honest
            // about what it is and can't push the player around as a solid body.
            sphere.isTrigger = true;

            var effect = root.GetComponent<AttackEffect>();
            if (effect == null) effect = root.AddComponent<AttackEffect>();

            var so = new SerializedObject(effect);
            // The one value that decides whether this hurts the player or the enemy's own allies.
            so.FindProperty("targetTag").stringValue = PlayerTag;
            so.FindProperty("canHitMultiple").boolValue = true;
            // The player is usually already inside the sphere when the swing spawns (the enemy walked
            // up to it), so OnTriggerEnter alone would never fire. Stay + the initial overlap check
            // are what actually land the hit.
            so.FindProperty("useStayDetection").boolValue = true;
            so.FindProperty("doInitialOverlapCheck").boolValue = true;
            so.FindProperty("hitLayerMask").intValue = ~0;
            // Off for both: these draw a red debug sphere and log every hit in the player's build.
            so.FindProperty("debugHits").boolValue = false;
            so.FindProperty("showDebugVisual").boolValue = false;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, EnemyEffectPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return true;
    }

    /// <summary>Assigns the attack the enemy swings and points its CombatController at the player.</summary>
    static bool FixEnemyPrefab()
    {
        var attackData = AssetDatabase.LoadAssetAtPath<AttackData>(EnemyAttackDataPath);
        GameObject root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
        if (root == null)
        {
            Debug.LogError($"EnemyCombatGenerator: could not open {EnemyPrefabPath}.");
            return false;
        }

        bool ok = true;
        try
        {
            var enemy = root.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogError($"EnemyCombatGenerator: {EnemyPrefabPath} has no Enemy component.");
                ok = false;
            }
            else
            {
                var so = new SerializedObject(enemy);
                so.FindProperty("attackData").objectReferenceValue = attackData;
                so.FindProperty("stopDistance").floatValue = EnemyStopDistance;
                so.FindProperty("attackCooldown").floatValue = EnemyAttackCooldown;
                so.FindProperty("playerTag").stringValue = PlayerTag;
                so.ApplyModifiedProperties();
            }

            var combat = root.GetComponent<CombatController>();
            if (combat == null)
            {
                Debug.LogError($"EnemyCombatGenerator: {EnemyPrefabPath} has no CombatController — it cannot attack.");
                ok = false;
            }
            else
            {
                var so = new SerializedObject(combat);
                // Was "Enemy", copied from the player's setup: the enemy was hunting its own kind.
                so.FindProperty("targetTag").stringValue = PlayerTag;
                so.ApplyModifiedProperties();
            }

            if (root.GetComponent<CombatController>() != null &&
                new SerializedObject(root.GetComponent<CombatController>()).FindProperty("attackPoint").objectReferenceValue == null)
            {
                Debug.LogError($"EnemyCombatGenerator: {EnemyPrefabPath} CombatController has no attackPoint — " +
                               "the swing effect would spawn on the enemy's origin instead of in front of it.");
                ok = false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return ok;
    }
}
