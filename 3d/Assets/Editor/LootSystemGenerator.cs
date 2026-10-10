using UnityEditor;
using UnityEngine;

/// <summary>
/// Wires the loot system end to end: an authored HealthPickup.prefab (a small glowing sphere with a
/// trigger collider — the script itself has no RequireComponent, so nothing built one until now), a
/// LootTable asset referencing it, and a LootDropper on the grunt enemy prefab pointing at that table.
/// Ranged/Elite/Boss are prefab variants of the grunt and inherit the component automatically — see
/// WireGruntPrefab's doc comment.
///
/// Before this, LootDropper/LootTable/HealthPickup all existed and worked in isolation, but no enemy
/// prefab had a LootDropper attached and no LootTable asset existed — Enemy.Die's
/// GetComponent&lt;LootDropper&gt;() call always found nothing, so killing anything dropped no loot ever.
///
/// Dual entry point per project convention.
/// </summary>
public static class LootSystemGenerator
{
    const string LootDir = "Assets/Loot";
    const string HealthPickupPrefabPath = LootDir + "/HealthPickup.prefab";
    const string LootTablePath = LootDir + "/EnemyLootTable.asset";
    const string PickupMaterialPath = LootDir + "/HealthPickup.mat";

    // Only the grunt needs wiring directly. Ranged/Elite/Boss are Unity prefab VARIANTS of the grunt
    // (EnemyArchetypeGenerator forks them via InstantiatePrefab + SaveAsPrefabAsset on the instance),
    // and none of them removes or overrides LootDropper — a variant with no such override inherits
    // the component, and its assigned lootTable reference, straight from the base at runtime. Verified
    // by LootSystemTests.EveryArchetypePrefab_InheritsAWorkingLootDropper.
    const string GruntPrefabPath = "Assets/Characters/enemy/EnemyHero Variant.prefab";

    [MenuItem("LionQuest/Generate/Loot System")]
    public static void Generate()
    {
        UiTheme.EnsureFolder(LootDir);

        GameObject pickupPrefab = BuildHealthPickupPrefab();
        BuildLootTable(pickupPrefab);
        WireGruntPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("LootSystemGenerator: done. Every enemy archetype now drops from EnemyLootTable on death " +
                  "(Ranged/Elite/Boss inherit LootDropper from the grunt as prefab variants).");
    }

    /// <summary>
    /// A small emissive sphere: visually distinct from any character/terrain asset in the project (all
    /// of which are matte), so a dropped pickup reads as "interactable item" at a glance without
    /// needing real art yet. Trigger SphereCollider — HealthPickup.OnTriggerEnter requires one but has
    /// no RequireComponent, so a prefab built without it would silently never fire.
    /// </summary>
    static GameObject BuildHealthPickupPrefab()
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        root.name = "HealthPickup";
        root.transform.localScale = Vector3.one * 0.4f;

        // The primitive's own SphereCollider is solid by default — HealthPickup needs a trigger, and
        // a solid collider on a floating pickup would also physically shove players/enemies around.
        SphereCollider collider = root.GetComponent<SphereCollider>();
        collider.isTrigger = true;

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true; // floats in place; OnTriggerEnter needs at least one Rigidbody in the pair, and the player already has one, but this keeps the pickup itself inert regardless of which body it lands under.
        rb.useGravity = false;

        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = BuildPickupMaterial();

        root.AddComponent<HealthPickup>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HealthPickupPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Emissive green so a dropped pickup is visible against the island's grass/dirt terrain
    /// without needing a light of its own.</summary>
    static Material BuildPickupMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(PickupMaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("LootSystemGenerator: shader 'Universal Render Pipeline/Lit' not found — is URP installed?");
                return null;
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, PickupMaterialPath);
        }

        var color = new Color(0.2f, 1f, 0.4f);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_EmissionColor", color * 2f);
        mat.EnableKeyword("_EMISSION");
        return mat;
    }

    static LootTable BuildLootTable(GameObject pickupPrefab)
    {
        LootTable table = AssetDatabase.LoadAssetAtPath<LootTable>(LootTablePath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<LootTable>();
            AssetDatabase.CreateAsset(table, LootTablePath);
        }

        // 15% drop chance: souls already reward every kill (GameSaveController.OnEnemyKilled), so
        // loot is a bonus on top rather than the primary reward — a health drop on most kills would
        // make damage nearly irrelevant.
        table.items = new System.Collections.Generic.List<LootTable.DropItem>
        {
            new LootTable.DropItem { prefab = pickupPrefab, dropChance = 15f },
        };
        EditorUtility.SetDirty(table);

        return table;
    }

    /// <summary>
    /// LoadPrefabContents/SaveAsPrefabAsset/UnloadPrefabContents, not EditPrefabContentsScope —
    /// EnemyCombatGenerator.FixEnemyPrefab already uses this lower-level API successfully on this
    /// same prefab elsewhere in this project.
    /// </summary>
    static void WireGruntPrefab()
    {
        LootTable table = AssetDatabase.LoadAssetAtPath<LootTable>(LootTablePath);
        if (table == null)
        {
            Debug.LogError($"LootSystemGenerator: {LootTablePath} failed to load — the grunt will have no loot table.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(GruntPrefabPath);
        if (root == null)
        {
            Debug.LogError($"LootSystemGenerator: could not open {GruntPrefabPath} — run 'LionQuest/Generate/Enemy Combat' first.");
            return;
        }

        try
        {
            LootDropper dropper = root.GetComponent<LootDropper>();
            if (dropper == null) dropper = root.AddComponent<LootDropper>();
            dropper.SetLootTable(table);

            PrefabUtility.SaveAsPrefabAsset(root, GruntPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        LootDropper verify = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath)?.GetComponent<LootDropper>();
        bool wired = verify != null &&
                     new SerializedObject(verify).FindProperty("lootTable").objectReferenceValue != null;
        if (!wired)
        {
            Debug.LogError($"LootSystemGenerator: {GruntPrefabPath}'s LootDropper still has no lootTable after wiring — " +
                           "the write did not take.");
        }
    }
}
