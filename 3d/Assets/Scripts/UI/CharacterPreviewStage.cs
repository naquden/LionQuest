using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One player slot's live 3D character preview: a dedicated camera + anchor renders the
/// showcased character prefab into a RenderTexture displayed by a RawImage. Turntables the
/// character and periodically fires its skill animations, mirroring StartScreenDiorama.
/// </summary>
public class CharacterPreviewStage : MonoBehaviour
{
    public const string PreviewLayerName = "CharacterPreview";

    [SerializeField] private Camera previewCamera;
    [SerializeField] private Transform characterAnchor;
    [SerializeField] private RawImage target;
    [SerializeField] private Vector2Int renderSize = new Vector2Int(512, 640);
    [SerializeField] private float degreesPerSecond = 35f;
    [SerializeField] private float skillInterval = 4.5f;

    private RenderTexture renderTexture;
    private GameObject currentInstance;
    private CharacterAnimator currentAnimator;
    private float skillTimer;
    private int nextSkillIndex;

    private void Awake() => EnsureInitialized();

    /// <summary>Awake() only runs in Play mode — editor tooling (headless screenshot capture)
    /// drives this component in edit mode, where Awake never fires. Show() calls this directly
    /// so the RenderTexture/RawImage wiring exists regardless of how this got invoked.</summary>
    private void EnsureInitialized()
    {
        if (renderTexture != null) return;

        if (previewCamera == null || characterAnchor == null || target == null)
        {
            Debug.LogError("CharacterPreviewStage: previewCamera/characterAnchor/target not assigned!");
            return;
        }

        renderTexture = new RenderTexture(renderSize.x, renderSize.y, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();
        previewCamera.targetTexture = renderTexture;
        target.texture = renderTexture;
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            if (previewCamera != null) previewCamera.targetTexture = null;
            renderTexture.Release();
            Object.Destroy(renderTexture);
        }
    }

    public void Show(CharacterDefinition definition)
    {
        EnsureInitialized();
        Hide();
        if (definition == null || definition.playerPrefab == null) return;

        currentInstance = Instantiate(definition.playerPrefab, characterAnchor.position, characterAnchor.rotation, characterAnchor);
        StripGameplayComponents(currentInstance);
        SetLayerRecursively(currentInstance, previewCamera.gameObject.layer);

        currentAnimator = currentInstance.GetComponentInChildren<CharacterAnimator>(true);
        skillTimer = 0f;
        nextSkillIndex = 0;

        if (gameObject.activeInHierarchy) enabled = true;
    }

    /// <summary>In Play mode Unity's per-frame render loop drives previewCamera automatically.
    /// Outside Play mode (headless screenshot capture) nothing renders it — callers doing
    /// edit-mode capture must force a render before reading back the RenderTexture.</summary>
    public void RenderNow()
    {
        if (previewCamera != null) previewCamera.Render();
    }

    public void Hide()
    {
        if (currentInstance != null)
        {
            DestroySafely(currentInstance);
            currentInstance = null;
        }
        currentAnimator = null;
    }

    /// <summary>Destroy() throws outside Play mode — this stage is also driven from editor
    /// tooling (headless screenshot capture) to preview state changes without entering Play.</summary>
    private static void DestroySafely(Object obj)
    {
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void Update()
    {
        if (currentInstance == null) return;

        currentInstance.transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);

        if (currentAnimator == null) return;

        skillTimer += Time.deltaTime;
        if (skillTimer < skillInterval) return;
        skillTimer = 0f;

        switch (nextSkillIndex)
        {
            case 0: currentAnimator.TriggerSkill1(); break;
            case 1: currentAnimator.TriggerSkill2(); break;
            default: currentAnimator.TriggerSkill3(); break;
        }
        nextSkillIndex = (nextSkillIndex + 1) % 3;
    }

    /// <summary>
    /// Turns the instantiated player prefab into an inert display model.
    ///
    /// Order matters: PlayerInput is destroyed before the rest because it claims an input device on
    /// enable, and a preview model holding a gamepad steals it from the selection screen driving the
    /// preview. TopDownPlayerController must go for its own reasons — it snaps its transform to
    /// groundLevel and applies movement forces, both of which fight the anchor this model is parented
    /// to.
    ///
    /// Runtime Destroy is deferred to end of frame, so these components still get one Awake/Start/
    /// OnEnable each. That is why the controller reports its missing-MapGenerator error here and why
    /// it is expected: it is a preview, not a player. Nothing observable survives the frame.
    /// </summary>
    private static void StripGameplayComponents(GameObject instance)
    {
        foreach (var input in instance.GetComponentsInChildren<UnityEngine.InputSystem.PlayerInput>(true))
            DestroySafely(input);
        foreach (var controller in instance.GetComponentsInChildren<TopDownPlayerController>(true))
            DestroySafely(controller);
        foreach (var stats in instance.GetComponentsInChildren<CharacterStats>(true))
            DestroySafely(stats);
        foreach (var combat in instance.GetComponentsInChildren<CombatController>(true))
            DestroySafely(combat);
        foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true))
            rb.isKinematic = true;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
