using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the enemy variety Level1 needs beyond its single hand-placed "grunt": a ranged skirmisher,
/// a tanky elite, and a boss, each as its own prefab + AttackData duplicated from the existing
/// "EnemyHero Variant"/"BasicAttack" pair so every archetype starts from the one enemy rig the game
/// already ships rather than a from-scratch model. Also authors the EnemyArchetype table WaveSpawner
/// reads to decide who spawns on which wave.
///
/// Why duplicate rather than share one prefab with different Inspector values per spawn: archetypes
/// need different Animator/mesh treatment eventually (a boss reads as a boss visually), and Unity has
/// no lighter-weight way to vary Enemy's serialized fields per spawn than distinct prefabs — the
/// project's generator convention already treats "prefab variant per tuning" as normal (see
/// Char67Generator duplicating Basic Slash AssCap into Basic Slash 67).
///
/// Dual entry point per project convention.
/// </summary>
public static class EnemyArchetypeGenerator
{
    const string EnemyDir = "Assets/Characters/enemy";
    const string GruntPrefabPath = EnemyDir + "/EnemyHero Variant.prefab";
    const string GruntAttackPath = EnemyDir + "/BasicAttack.asset";
    const string GruntEffectPath = EnemyDir + "/Basic Slash Enemy.prefab";

    const string RangedPrefabPath = EnemyDir + "/Enemy Ranged.prefab";
    const string RangedAttackPath = EnemyDir + "/RangedAttack.asset";
    const string ElitePrefabPath = EnemyDir + "/Enemy Elite.prefab";
    const string EliteAttackPath = EnemyDir + "/EliteAttack.asset";
    const string EliteEffectPath = EnemyDir + "/Basic Slash Elite.prefab";
    const string BossPrefabPath = EnemyDir + "/Enemy Boss.prefab";
    const string BossAttackPath = EnemyDir + "/BossAttack.asset";
    const string BossEffectPath = EnemyDir + "/Basic Slash Boss.prefab";

    // Reuses the player's shuriken Projectile component/mesh for the ranged archetype's shot — see
    // Enemy.rangedProjectilePrefab's doc comment: any prefab with a Projectile component works, and
    // authoring a new one from scratch would just be the same component on a new mesh.
    const string ProjectilePrefabPath = "Assets/Characters/AssassinoCapuchino/Shuriken.prefab";

    const string ArchetypeDir = "Assets/Data/EnemyArchetypes";
    const string GruntArchetypePath = ArchetypeDir + "/Grunt.asset";
    const string RangedArchetypePath = ArchetypeDir + "/Ranged.asset";
    const string EliteArchetypePath = ArchetypeDir + "/Elite.asset";
    const string BossArchetypePath = ArchetypeDir + "/Boss.asset";

    [MenuItem("LionQuest/Generate/Enemy Archetypes")]
    public static void Generate()
    {
        GameObject grunt = AssetDatabase.LoadAssetAtPath<GameObject>(GruntPrefabPath);
        if (grunt == null)
        {
            Debug.LogError($"EnemyArchetypeGenerator: {GruntPrefabPath} missing — run 'LionQuest/Generate/Enemy Combat' first.");
            return;
        }

        AttackData gruntAttack = AssetDatabase.LoadAssetAtPath<AttackData>(GruntAttackPath);
        if (gruntAttack == null)
        {
            Debug.LogError($"EnemyArchetypeGenerator: {GruntAttackPath} missing — run 'LionQuest/Generate/Enemy Combat' first.");
            return;
        }

        UiTheme.EnsureFolder(ArchetypeDir);

        GameObject rangedPrefab = BuildRanged(grunt);
        GameObject elitePrefab = BuildElite(grunt, gruntAttack);
        GameObject bossPrefab = BuildBoss(grunt, gruntAttack);

        // SaveAssets only here, no Refresh: an intermediate Refresh can reimport these prefabs/assets
        // and swap out their managed wrapper, which is what made an earlier version of this method
        // wire WaveSpawner with null (fileID: 0) entries despite the assets existing fine on disk —
        // AssignArray silently accepts a null Object reference and writes {fileID: 0}, no exception.
        // Only SaveAssets is needed before EnsureArchetype reads these as prefab references; the
        // reimport only needs to happen once, at the very end, same as Char67Generator's pattern.
        AssetDatabase.SaveAssets();

        EnsureArchetype(GruntArchetypePath, grunt, "Grunt", minWave: 1, weight: 3f, isBoss: false);
        EnsureArchetype(RangedArchetypePath, rangedPrefab, "Ranged", minWave: 2, weight: 2f, isBoss: false);
        EnsureArchetype(EliteArchetypePath, elitePrefab, "Elite", minWave: 3, weight: 1f, isBoss: false);
        EnsureArchetype(BossArchetypePath, bossPrefab, "Boss", minWave: 1, weight: 1f, isBoss: true);

        AssetDatabase.SaveAssets();

        WireIntoLevel1();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("EnemyArchetypeGenerator: done. Grunt/Ranged/Elite/Boss archetypes built and wired into Level1's WaveSpawner.");
    }

    // ------------------------------------------------------------------
    // Ranged: same body as the grunt, but attacks with a thrown projectile instead of melee, and
    // stops well outside melee range so it actually behaves like a skirmisher.
    // ------------------------------------------------------------------

    static GameObject BuildRanged(GameObject gruntPrefab)
    {
        GameObject prefab = DuplicatePrefab(gruntPrefab, RangedPrefabPath, "Enemy Ranged");

        AttackData attack = EnsureAttackDataCopy(GruntAttackPath, RangedAttackPath);
        attack.damage = 8f;
        attack.cooldown = 1.6f;
        EditorUtility.SetDirty(attack);

        GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
        if (projectilePrefab == null)
        {
            Debug.LogError($"EnemyArchetypeGenerator: {ProjectilePrefabPath} missing — the ranged archetype will have no projectile to fire.");
        }

        using (var scope = new PrefabUtility.EditPrefabContentsScope(RangedPrefabPath))
        {
            GameObject root = scope.prefabContentsRoot;

            var enemy = root.GetComponent<Enemy>();
            var enemySo = new SerializedObject(enemy);
            enemySo.FindProperty("maxHealth").floatValue = 60f;
            enemySo.FindProperty("pullRange").floatValue = 18f;
            // Stops well outside CombatController's melee reach so it kites rather than walking into
            // the player's basic attack — the entire point of a "ranged" archetype.
            enemySo.FindProperty("stopDistance").floatValue = 8f;
            enemySo.FindProperty("attackCooldown").floatValue = attack.cooldown;
            enemySo.FindProperty("attackData").objectReferenceValue = attack;
            enemySo.FindProperty("rangedProjectilePrefab").objectReferenceValue = projectilePrefab;
            enemySo.FindProperty("rangedProjectileSpeed").floatValue = 14f;
            enemySo.ApplyModifiedProperties();

            var combat = root.GetComponent<CombatController>();
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("targetTag").stringValue = "Player";
            combatSo.ApplyModifiedProperties();
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(RangedPrefabPath);
    }

    // ------------------------------------------------------------------
    // Elite: tankier, hits harder, otherwise identical melee behaviour to the grunt — the "this one
    // is dangerous, focus it" enemy in a mixed wave.
    // ------------------------------------------------------------------

    static GameObject BuildElite(GameObject gruntPrefab, AttackData gruntAttack)
    {
        GameObject prefab = DuplicatePrefab(gruntPrefab, ElitePrefabPath, "Enemy Elite");
        GameObject effectPrefab = DuplicateEffectPrefab(gruntAttack.effectPrefab, EliteEffectPath, "Basic Slash Elite");

        AttackData attack = EnsureAttackDataCopy(GruntAttackPath, EliteAttackPath);
        attack.damage = 22f;
        attack.cooldown = 1.8f;
        attack.effectPrefab = effectPrefab;
        EditorUtility.SetDirty(attack);

        using (var scope = new PrefabUtility.EditPrefabContentsScope(ElitePrefabPath))
        {
            GameObject root = scope.prefabContentsRoot;

            var enemy = root.GetComponent<Enemy>();
            var enemySo = new SerializedObject(enemy);
            enemySo.FindProperty("maxHealth").floatValue = 260f;
            enemySo.FindProperty("attackCooldown").floatValue = attack.cooldown;
            enemySo.FindProperty("attackData").objectReferenceValue = attack;
            // A bigger body needs a bit more room from its neighbours than the grunt's default, or
            // it visually clips through them despite the separation force.
            enemySo.FindProperty("separationRadius").floatValue = 1.6f;
            enemySo.ApplyModifiedProperties();

            // Visibly bigger without a new mesh — the model scales with the transform, and its
            // Rigidbody/collider scale along with it automatically.
            root.transform.localScale = Vector3.one * 1.35f;
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(ElitePrefabPath);
    }

    // ------------------------------------------------------------------
    // Boss: much tankier and harder-hitting still, corpse lingers longer (its death is the moment
    // that ends the run, so it should still be visible when the victory screen appears), and never
    // separates from other enemies (there is only ever one on screen at a time — see WaveSpawner
    // spawning boss waves alone — so the force would never fire anyway, but zeroing it documents
    // that on the prefab itself).
    // ------------------------------------------------------------------

    static GameObject BuildBoss(GameObject gruntPrefab, AttackData gruntAttack)
    {
        GameObject prefab = DuplicatePrefab(gruntPrefab, BossPrefabPath, "Enemy Boss");
        GameObject effectPrefab = DuplicateEffectPrefab(gruntAttack.effectPrefab, BossEffectPath, "Basic Slash Boss");

        AttackData attack = EnsureAttackDataCopy(GruntAttackPath, BossAttackPath);
        attack.damage = 30f;
        attack.cooldown = 1.4f;
        attack.effectPrefab = effectPrefab;
        EditorUtility.SetDirty(attack);

        using (var scope = new PrefabUtility.EditPrefabContentsScope(BossPrefabPath))
        {
            GameObject root = scope.prefabContentsRoot;

            var enemy = root.GetComponent<Enemy>();
            var enemySo = new SerializedObject(enemy);
            enemySo.FindProperty("maxHealth").floatValue = 1400f;
            enemySo.FindProperty("attackCooldown").floatValue = attack.cooldown;
            enemySo.FindProperty("attackData").objectReferenceValue = attack;
            enemySo.FindProperty("separationForce").floatValue = 0f;
            // The boss's death is the run's win condition — its corpse should still be on screen
            // when the victory panel appears, not despawned mid-celebration.
            enemySo.FindProperty("corpseLifetime").floatValue = 30f;
            enemySo.ApplyModifiedProperties();

            root.transform.localScale = Vector3.one * 2f;
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
    }

    // ------------------------------------------------------------------
    // Shared prefab/asset helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Instantiate -> SaveAsPrefabAsset -> destroy the scene instance, same sequence
    /// Char67Generator.EnsureBasicSlashPrefab uses to fork a prefab. Re-copies from source every
    /// Generate() run rather than only-if-missing, so re-running always reflects the current grunt
    /// (matching TerrainContentGenerator's "generator force-corrects, doesn't just fill gaps" rule
    /// from the unity-headless skill).
    /// </summary>
    static GameObject DuplicatePrefab(GameObject source, string destPath, string name)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = name;
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, destPath);
        Object.DestroyImmediate(instance);
        return saved;
    }

    static GameObject DuplicateEffectPrefab(GameObject source, string destPath, string name)
    {
        if (source == null)
        {
            Debug.LogError($"EnemyArchetypeGenerator: source effect prefab is null — cannot build {destPath}.");
            return null;
        }
        return DuplicatePrefab(source, destPath, name);
    }

    static AttackData EnsureAttackDataCopy(string sourcePath, string destPath)
    {
        AttackData source = AssetDatabase.LoadAssetAtPath<AttackData>(sourcePath);
        if (AssetDatabase.LoadAssetAtPath<AttackData>(destPath) == null)
        {
            AssetDatabase.CopyAsset(sourcePath, destPath);
        }
        return AssetDatabase.LoadAssetAtPath<AttackData>(destPath);
    }

    static EnemyArchetype EnsureArchetype(string path, GameObject prefab, string name, int minWave, float weight, bool isBoss)
    {
        var archetype = AssetDatabase.LoadAssetAtPath<EnemyArchetype>(path);
        if (archetype == null)
        {
            archetype = ScriptableObject.CreateInstance<EnemyArchetype>();
            AssetDatabase.CreateAsset(archetype, path);
        }

        archetype.prefab = prefab;
        archetype.archetypeName = name;
        archetype.minWave = minWave;
        archetype.weight = weight;
        archetype.isBoss = isBoss;
        EditorUtility.SetDirty(archetype);

        return archetype;
    }

    /// <summary>
    /// Wires the archetype tables into Level1's WaveSpawner. Level1Generator owns creating the
    /// WaveSpawner GameObject itself (it needs the scene's own MapGenerator/Enemies-parent
    /// references, which only exist once that generator has built the scene) — this only fills in
    /// the archetype arrays, so it can be re-run any time after archetypes change without touching
    /// scene structure.
    ///
    /// Loads the archetype assets by path AFTER OpenScene below, not before: OpenScene(..., Single)
    /// unloads the previously active scene and any asset with no remaining reference, which silently
    /// invalidated archetype instances passed in from Generate() — Unity's `== null` reports them
    /// null with no exception, and AssignArray happily writes a null reference as {fileID: 0}. Assets
    /// loaded fresh after the scene switch have no such stale-handle problem.
    /// </summary>
    static void WireIntoLevel1()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Scenes/Level1.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);

        var archetypes = new[]
        {
            AssetDatabase.LoadAssetAtPath<EnemyArchetype>(GruntArchetypePath),
            AssetDatabase.LoadAssetAtPath<EnemyArchetype>(RangedArchetypePath),
            AssetDatabase.LoadAssetAtPath<EnemyArchetype>(EliteArchetypePath),
        };
        var bossArchetypes = new[]
        {
            AssetDatabase.LoadAssetAtPath<EnemyArchetype>(BossArchetypePath),
        };

        WaveSpawner spawner = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            spawner = rootGo.GetComponentInChildren<WaveSpawner>(true);
            if (spawner != null) break;
        }

        if (spawner == null)
        {
            Debug.LogError("EnemyArchetypeGenerator: Level1 has no WaveSpawner — run 'LionQuest/Generate/Level 1' " +
                           "(with a Level1Generator that creates one) before this generator.");
            return;
        }

        // A null slot here would silently serialize as {fileID: 0} — no exception, just an empty
        // reference sitting in the scene until someone notices WaveSpawner refuses to spawn anything.
        foreach (EnemyArchetype a in archetypes) AssertLoaded(a);
        foreach (EnemyArchetype a in bossArchetypes) AssertLoaded(a);

        var so = new SerializedObject(spawner);
        UiTheme.AssignArray(so, "archetypes", archetypes);
        UiTheme.AssignArray(so, "bossArchetypes", bossArchetypes);
        so.ApplyModifiedProperties();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    static void AssertLoaded(EnemyArchetype archetype)
    {
        if (archetype == null)
        {
            Debug.LogError("EnemyArchetypeGenerator: an archetype asset failed to (re)load after " +
                           "AssetDatabase.Refresh — WaveSpawner would have been wired with a null entry.");
        }
    }
}
