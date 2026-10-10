using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Makes the "67" character playable by mirroring AssCap's presence: its own attack/skill
/// ScriptableObjects, the gameplay components on 67.prefab (controller, combat, stats,
/// PlayerInput, AttackPoint) and a CharacterDefinition the roster discovers automatically.
///
/// 67 is tuned as a glass-cannon brawler distinct from AssCap: faster, wider basic-attack reach,
/// less health, and a hitscan laser beam for skill 2 instead of a thrown shuriken. Its basic-attack
/// effect and the laser beam are per-character prefabs/materials (Basic Slash 67, LaserBeam); the
/// whirlwind and shuriken VFX AssCap already authored stay shared for skill 1 and skill 3.
/// Upgrade trees are authored by SkillTreeScreenGenerator (it walks every CharacterDefinition), not
/// here.
/// </summary>
public static class Char67Generator
{
    const string CharDir = "Assets/Characters/67";
    const string PrefabPath = CharDir + "/67.prefab";
    const string DefinitionPath = CharDir + "/67_Definition.asset";
    const string BasicAttackPath = CharDir + "/BasicAttack.asset";
    const string Skill1AttackPath = CharDir + "/Skill1Attack.asset";
    const string ShurikenProjectilePath = CharDir + "/ShurikenProjectile.asset";
    const string BattleCryPath = CharDir + "/BattleCry.asset";
    const string BasicSlash67PrefabPath = CharDir + "/Basic Slash 67.prefab";
    const string LaserBeamMaterialPath = CharDir + "/LaserBeam.mat";
    const string LaserBeamPrefabPath = CharDir + "/LaserBeam Prefab.prefab";
    const string LaserBeamDataPath = CharDir + "/LaserBeam.asset";

    // Shared VFX/projectile prefabs, authored for AssCap and reused here.
    const string AssCapDir = "Assets/Characters/AssassinoCapuchino";
    const string BasicSlashPrefabPath = AssCapDir + "/Basic Slash AssCap.prefab";
    const string WhirlwindPrefabPath = AssCapDir + "/Whirlwind Slash AssCap.prefab";

    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    public const string CharacterID = "67";

    // 67's identity stats — noticeably faster and squishier than AssCap's 3 moveSpeed / 100 health.
    // 4.2 (+40%) read as barely different in play; doubling AssCap's speed makes it unmistakable.
    const float BaseMoveSpeed = 6f;
    const float BaseMaxHealth = 70f;
    const float BasicAttackHitRadius = 1.3f;
    // AssCap's Basic Slash effective world radius is ~0.714m (SphereCollider radius 1.85 * root
    // localScale 0.386). Scaling the duplicated prefab by this ratio keeps the visual as wide as
    // the new hit radius.
    const float BasicSlashScaleFactor = BasicAttackHitRadius / 0.714f;

    [MenuItem("LionQuest/Generate/Character 67")]
    public static void Generate()
    {
        GameObject basicSlashPrefab = EnsureBasicSlashPrefab();
        AttackData basicAttack = EnsureBasicAttack(basicSlashPrefab);
        AttackData skill1 = EnsureSkill1();
        Material laserMaterial = EnsureLaserMaterial();
        GameObject laserBeamPrefab = EnsureLaserBeamPrefab(laserMaterial);
        LaserBeamSkillData laser = EnsureLaserBeam(laserBeamPrefab);
        BattleCrySkillData battleCry = EnsureBattleCry();

        AssetDatabase.DeleteAsset(ShurikenProjectilePath);

        AssetDatabase.SaveAssets();

        WirePrefab(basicAttack, skill1, laser, battleCry);
        EnsureDefinition();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Char67Generator: done. Run 'LionQuest/Generate/Upgrade Screen' to author its " +
                  "upgrade trees, then 'LionQuest/Generate/Character Selection' to refresh the roster.");
    }

    // ------------------------------------------------------------------
    // Data assets — same shape as AssCap's, own files so they tune apart
    // ------------------------------------------------------------------

    static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    static GameObject LoadRequiredPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"Char67Generator: required prefab missing at {path}");
        }
        return prefab;
    }

    static AttackData EnsureBasicAttack(GameObject basicSlashPrefab)
    {
        var data = EnsureAsset<AttackData>(BasicAttackPath);
        data.attackName = "Attack";
        data.effectPrefab = basicSlashPrefab;
        data.hitWindowDuration = 0.15f;
        data.damage = 60f;
        data.knockbackForce = 3f;
        data.cooldown = 0.5f;
        data.spawnAtCharacterCenter = false;
        data.spawnOffset = Vector3.zero;
        data.hitRadius = BasicAttackHitRadius;

        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>67's own basic-attack effect prefab: a rescaled duplicate of AssCap's Basic Slash,
    /// so the swing visually matches the wider BasicAttackHitRadius rather than sharing AssCap's
    /// smaller visual with a runtime-only radius override.</summary>
    static GameObject EnsureBasicSlashPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BasicSlash67PrefabPath);
        if (existing == null)
        {
            GameObject basicSlash = LoadRequiredPrefab(BasicSlashPrefabPath);
            if (basicSlash == null) return null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basicSlash);
            instance.name = "Basic Slash 67";

            existing = PrefabUtility.SaveAsPrefabAsset(instance, BasicSlash67PrefabPath);
            Object.DestroyImmediate(instance);
        }

        using (var editScope = new PrefabUtility.EditPrefabContentsScope(BasicSlash67PrefabPath))
        {
            GameObject root = editScope.prefabContentsRoot;
            // Absolute, not "*= factor": this scope re-opens the same 67-owned prefab on every
            // Generate() run, so multiplying the existing scale would compound on a second run.
            float authoredAssCapScale = 0.38609025f;
            float scale = authoredAssCapScale * BasicSlashScaleFactor;
            root.transform.localScale = new Vector3(scale, scale, scale);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(BasicSlash67PrefabPath);
    }

    static AttackData EnsureSkill1()
    {
        var data = EnsureAsset<AttackData>(Skill1AttackPath);
        data.attackName = "Whirlwind";
        data.effectPrefab = LoadRequiredPrefab(WhirlwindPrefabPath);
        data.hitWindowDuration = 0.2f;
        // damage/cooldown are Inspector-readability only: TopDownPlayerController passes a
        // stat-derived damage override and the global skill lock owns gating.
        data.damage = 120f;
        data.knockbackForce = 5f;
        data.cooldown = 10f;
        data.spawnAtCharacterCenter = true;
        data.spawnOffset = new Vector3(0f, 0.5f, 0f);

        EditorUtility.SetDirty(data);
        return data;
    }

    /// <summary>URP Unlit material for the laser beam's LineRenderer: additive-transparent, emissive
    /// cyan. Generator-authored (not `new Material` at runtime): a serialized asset reference on the
    /// prefab cannot go missing the way a runtime Shader.Find can return null in a stripped build.</summary>
    static Material EnsureLaserMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogError("Char67Generator: shader 'Universal Render Pipeline/Unlit' not found — is URP installed?");
            return null;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(LaserBeamMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, LaserBeamMaterialPath);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 1f);   // Additive
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", new Color(0.3f, 1f, 1f, 0.9f));

        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>The beam's visual + hit-resolution prefab: a LineRenderer (world-space, 2 points)
    /// driven by the LaserBeam script. No collider — LaserBeam resolves hits via Physics.RaycastAll
    /// in Fire(), it never relies on trigger callbacks.</summary>
    static GameObject EnsureLaserBeamPrefab(Material material)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(LaserBeamPrefabPath);
        if (existing == null)
        {
            var go = new GameObject("LaserBeam Prefab", typeof(LineRenderer), typeof(LaserBeam));
            existing = PrefabUtility.SaveAsPrefabAsset(go, LaserBeamPrefabPath);
            Object.DestroyImmediate(go);
        }

        using (var editScope = new PrefabUtility.EditPrefabContentsScope(LaserBeamPrefabPath))
        {
            GameObject root = editScope.prefabContentsRoot;
            LineRenderer line = root.GetComponent<LineRenderer>();
            if (line == null) line = root.AddComponent<LineRenderer>();
            if (root.GetComponent<LaserBeam>() == null) root.AddComponent<LaserBeam>();

            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward);
            line.widthMultiplier = 0.12f;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (material != null) line.sharedMaterial = material;
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(LaserBeamPrefabPath);
    }

    static LaserBeamSkillData EnsureLaserBeam(GameObject beamPrefab)
    {
        var data = EnsureAsset<LaserBeamSkillData>(LaserBeamDataPath);
        data.skillName = "Laser Beam";
        data.beamPrefab = beamPrefab;
        data.range = 20f;
        data.beamWidth = 0.12f;
        data.visualDuration = 0.25f;
        data.damage = 45f;
        data.knockbackForce = 2f;
        data.pierceCount = 1;
        data.fireDelay = 0.35f;
        data.cooldown = 3f;
        data.muzzleFallbackOffset = new Vector3(0.25f, 1.2f, 0.35f);

        EditorUtility.SetDirty(data);
        return data;
    }

    static BattleCrySkillData EnsureBattleCry()
    {
        var data = EnsureAsset<BattleCrySkillData>(BattleCryPath);
        data.skillName = "Battle Cry";
        data.duration = 5f;
        data.cooldown = 10f;

        EditorUtility.SetDirty(data);
        return data;
    }

    // ------------------------------------------------------------------
    // 67.prefab — gameplay components
    // ------------------------------------------------------------------

    static void WirePrefab(AttackData basicAttack, AttackData skill1,
        LaserBeamSkillData laser, BattleCrySkillData battleCry)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError($"Char67Generator: prefab not found at {PrefabPath}");
            return;
        }

        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (actions == null)
        {
            Debug.LogError($"Char67Generator: input actions not found at {InputActionsPath}");
        }

        using (var editScope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            GameObject root = editScope.prefabContentsRoot;

            // The FBX import ships this prefab inactive (m_IsActive: 0). PlayerSpawner toggles
            // active itself, but a preview/diorama instance is shown as-is — an inactive root
            // renders nothing and never runs Awake.
            root.SetActive(true);
            root.tag = "Player";

            Transform attackPoint = EnsureAttackPoint(root);
            ConfigureAnimator(root);

            CombatController combat = Ensure<CombatController>(root);
            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("knockbackMultiplier").floatValue = 2f;
            combatSo.FindProperty("damageMultiplier").floatValue = 1f;
            combatSo.FindProperty("targetTag").stringValue = "Enemy";
            combatSo.FindProperty("attackPoint").objectReferenceValue = attackPoint;
            combatSo.FindProperty("attackPointDistance").floatValue = 0.8f;
            combatSo.FindProperty("attackPointHeight").floatValue = 1f;
            combatSo.ApplyModifiedProperties();

            CharacterStats stats = Ensure<CharacterStats>(root);
            stats.characterID = CharacterID;
            // souls/purchasedSkillNodes live in GameSaveController's shared per-character record at
            // runtime (CharacterStats.Record), not on this component — nothing to initialize here.
            // definition stays null on the asset: PlayerSpawner assigns it per spawn, and baking
            // a reference here would be a second source of truth for the same field.
            EditorUtility.SetDirty(stats);

            Transform muzzle = FindBoneByName(root.transform, "RightHand");
            if (muzzle == null)
            {
                Debug.LogWarning($"Char67Generator: no 'RightHand' bone found under {root.name} — " +
                                  "the laser beam will fall back to a capsule-center offset at runtime.");
            }

            TopDownPlayerController controller = Ensure<TopDownPlayerController>(root);
            var controllerSo = new SerializedObject(controller);
            // Definition-driven at runtime (CharacterStats.GetBaseMoveSpeed); this authored value is
            // only the fallback for a scene-authored player with no CharacterStats.definition.
            controllerSo.FindProperty("moveSpeed").floatValue = BaseMoveSpeed;
            controllerSo.FindProperty("sprintMultiplier").floatValue = 1f;
            controllerSo.FindProperty("acceleration").floatValue = 50f;
            controllerSo.FindProperty("stopDrag").floatValue = 10f;
            controllerSo.FindProperty("rotationSpeed").floatValue = 10f;
            controllerSo.FindProperty("rotateTowardsMovement").boolValue = true;
            controllerSo.FindProperty("groundLevel").floatValue = 0f;
            controllerSo.FindProperty("groundCheckDistance").floatValue = 0.1f;
            controllerSo.FindProperty("inputActions").objectReferenceValue = actions;
            controllerSo.FindProperty("viewportMargin").floatValue = 0.1f;
            controllerSo.FindProperty("enforceViewportBounds").boolValue = true;
            controllerSo.FindProperty("characterAnimator").objectReferenceValue =
                root.GetComponentInChildren<CharacterAnimator>(true);
            controllerSo.FindProperty("combatController").objectReferenceValue = combat;
            controllerSo.FindProperty("basicAttackData").objectReferenceValue = basicAttack;
            controllerSo.FindProperty("skill1AttackData").objectReferenceValue = skill1;
            controllerSo.FindProperty("skill1BaseCooldown").floatValue = 10f;
            controllerSo.FindProperty("skill2ProjectileData").objectReferenceValue = null;
            controllerSo.FindProperty("skill2LaserData").objectReferenceValue = laser;
            controllerSo.FindProperty("laserMuzzle").objectReferenceValue = muzzle;
            controllerSo.FindProperty("skill3BattleCryData").objectReferenceValue = battleCry;
            // Dead at runtime — CharacterStats.GetMaxHealth() now reads CharacterDefinition.baseMaxHealth
            // instead. Kept authored (not deleted) as the fallback for a scene-authored player with no
            // CharacterStats.definition assigned.
            controllerSo.FindProperty("maxHealth").floatValue = BaseMaxHealth;
            controllerSo.ApplyModifiedProperties();

            EnsurePlayerInput(root, actions);
        }
    }

    static T Ensure<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }

    /// <summary>The transform CombatController spawns hit effects from. CombatController would
    /// create one at runtime, but that leaves its position implicit and unreviewable — author it
    /// here so the offset lives on disk like AssCap's does.</summary>
    static Transform EnsureAttackPoint(GameObject root)
    {
        Transform existing = root.transform.Find("AttackPoint");
        if (existing == null)
        {
            var go = new GameObject("AttackPoint");
            go.transform.SetParent(root.transform, false);
            existing = go.transform;
        }

        existing.localPosition = new Vector3(0f, 1f, 0.8f);
        existing.localRotation = Quaternion.identity;
        return existing;
    }

    /// <summary>67.prefab already carries a CharacterAnimator, but its serialized data predates
    /// maxSpeedForNormalization, so the field deserializes to 0 (a missing YAML key does not fall
    /// back to the C# initializer) and Speed is then fed raw m/s instead of a 0..1 value.</summary>
    static void ConfigureAnimator(GameObject root)
    {
        CharacterAnimator animator = root.GetComponentInChildren<CharacterAnimator>(true);
        if (animator == null) animator = root.AddComponent<CharacterAnimator>();

        var so = new SerializedObject(animator);
        so.FindProperty("maxSpeedForNormalization").floatValue = BaseMoveSpeed;
        so.FindProperty("debugLog").boolValue = false;
        so.ApplyModifiedProperties();
    }

    /// <summary>Name search for a bone transform (e.g. "RightHand"). 67's rig is animationType
    /// Generic with no avatar assigned, so Animator.GetBoneTransform cannot be used — but
    /// optimizeGameObjects is off, so the bone GameObjects are real transforms this can walk.</summary>
    static Transform FindBoneByName(Transform root, string boneName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName) return t;
        }
        return null;
    }

    static void EnsurePlayerInput(GameObject root, InputActionAsset actions)
    {
        PlayerInput pi = root.GetComponent<PlayerInput>();
        if (pi == null) pi = root.AddComponent<PlayerInput>();

        var so = new SerializedObject(pi);
        so.FindProperty("m_Actions").objectReferenceValue = actions;
        so.FindProperty("m_DefaultActionMap").stringValue = "Player";
        so.FindProperty("m_NotificationBehavior").enumValueIndex = (int)PlayerNotifications.InvokeUnityEvents;
        so.ApplyModifiedProperties();
    }

    // ------------------------------------------------------------------
    // 67_Definition.asset
    // ------------------------------------------------------------------

    static void EnsureDefinition()
    {
        var def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(DefinitionPath);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<CharacterDefinition>();
            AssetDatabase.CreateAsset(def, DefinitionPath);
        }

        def.characterID = CharacterID;
        def.displayName = "67";
        def.unlockedByDefault = true;
        def.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        def.baseMaxHealth = BaseMaxHealth;
        def.baseMoveSpeed = BaseMoveSpeed;

        EditorUtility.SetDirty(def);
    }
}
