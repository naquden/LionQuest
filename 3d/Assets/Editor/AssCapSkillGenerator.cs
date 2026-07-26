using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates/wires the AssCap Skill 1 (Whirlwind AoE) and Skill 3 (Battle Cry) assets:
/// a duplicated whirlwind effect prefab, a Skill1 AttackData asset, a BattleCry
/// BattleCrySkillData asset, and assigns both to the AssCap player prefab.
/// </summary>
public static class AssCapSkillGenerator
{
    const string CharDir = "Assets/Characters/AssassinoCapuchino";
    const string BasicSlashPrefabPath = CharDir + "/Basic Slash AssCap.prefab";
    const string WhirlwindPrefabPath = CharDir + "/Whirlwind Slash AssCap.prefab";
    const string Skill1AttackDataPath = CharDir + "/Skill1Attack.asset";
    const string BattleCryDataPath = CharDir + "/BattleCry.asset";
    const string AssCapPrefabPath = CharDir + "/AssasinoCappuchino.prefab";

    [MenuItem("LionQuest/Generate/AssCap Skills")]
    public static void Generate()
    {
        GameObject whirlwindPrefab = EnsureWhirlwindPrefab();
        AttackData skill1Data = EnsureSkill1AttackData(whirlwindPrefab);
        BattleCrySkillData battleCryData = EnsureBattleCryData();

        WireAssCapPrefab(skill1Data, battleCryData);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("AssCapSkillGenerator: done.");
    }

    // ------------------------------------------------------------------
    // Whirlwind Slash AssCap.prefab — duplicate of Basic Slash AssCap.prefab
    // ------------------------------------------------------------------

    static GameObject EnsureWhirlwindPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(WhirlwindPrefabPath);
        if (existing != null)
        {
            return ConfigureWhirlwindPrefab(existing);
        }

        GameObject basicSlash = AssetDatabase.LoadAssetAtPath<GameObject>(BasicSlashPrefabPath);
        if (basicSlash == null)
        {
            Debug.LogError($"AssCapSkillGenerator: source prefab not found at {BasicSlashPrefabPath}");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basicSlash);
        instance.name = "Whirlwind Slash AssCap";

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, WhirlwindPrefabPath);
        Object.DestroyImmediate(instance);

        return ConfigureWhirlwindPrefab(prefab);
    }

    static GameObject ConfigureWhirlwindPrefab(GameObject prefabAsset)
    {
        using (var editScope = new PrefabUtility.EditPrefabContentsScope(WhirlwindPrefabPath))
        {
            GameObject root = editScope.prefabContentsRoot;

            SphereCollider sphere = root.GetComponent<SphereCollider>();
            if (sphere == null)
            {
                Debug.LogError("AssCapSkillGenerator: Whirlwind prefab has no SphereCollider on its root.");
            }
            else
            {
                // Authored value only — CombatController.PerformAttack overrides this at
                // runtime via AttackEffect.SetRadius() using the stat-scaled radius.
                float lossyScale = Mathf.Max(Mathf.Abs(root.transform.localScale.x), Mathf.Abs(root.transform.localScale.z));
                sphere.radius = lossyScale > 0f ? 3f / lossyScale : sphere.radius;
            }
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(WhirlwindPrefabPath);
    }

    // ------------------------------------------------------------------
    // Skill1Attack.asset (AttackData)
    // ------------------------------------------------------------------

    static AttackData EnsureSkill1AttackData(GameObject whirlwindPrefab)
    {
        var data = AssetDatabase.LoadAssetAtPath<AttackData>(Skill1AttackDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<AttackData>();
            AssetDatabase.CreateAsset(data, Skill1AttackDataPath);
        }

        data.attackName = "Whirlwind";
        data.effectPrefab = whirlwindPrefab;
        data.hitWindowDuration = 0.2f;
        data.damage = 120f; // unused at runtime (TopDownPlayerController passes a stat-derived override); set for Inspector readability
        data.knockbackForce = 5f;
        data.cooldown = 10f; // unused at runtime (global skill lock owns gating); set for Inspector readability
        data.spawnAtCharacterCenter = true;
        data.spawnOffset = new Vector3(0f, 0.5f, 0f);

        EditorUtility.SetDirty(data);
        return data;
    }

    // ------------------------------------------------------------------
    // BattleCry.asset (BattleCrySkillData)
    // ------------------------------------------------------------------

    static BattleCrySkillData EnsureBattleCryData()
    {
        var data = AssetDatabase.LoadAssetAtPath<BattleCrySkillData>(BattleCryDataPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BattleCrySkillData>();
            AssetDatabase.CreateAsset(data, BattleCryDataPath);
        }

        data.skillName = "Battle Cry";
        data.duration = 5f;
        data.cooldown = 10f;

        EditorUtility.SetDirty(data);
        return data;
    }

    // ------------------------------------------------------------------
    // AssasinoCappuchino.prefab wiring
    // ------------------------------------------------------------------

    static void WireAssCapPrefab(AttackData skill1Data, BattleCrySkillData battleCryData)
    {
        using (var editScope = new PrefabUtility.EditPrefabContentsScope(AssCapPrefabPath))
        {
            GameObject root = editScope.prefabContentsRoot;
            TopDownPlayerController controller = root.GetComponentInChildren<TopDownPlayerController>(true);
            if (controller == null)
            {
                Debug.LogError($"AssCapSkillGenerator: no TopDownPlayerController found on {AssCapPrefabPath}");
                return;
            }

            var so = new SerializedObject(controller);
            so.FindProperty("skill1AttackData").objectReferenceValue = skill1Data;
            so.FindProperty("skill3BattleCryData").objectReferenceValue = battleCryData;
            so.ApplyModifiedProperties();
        }
    }
}
