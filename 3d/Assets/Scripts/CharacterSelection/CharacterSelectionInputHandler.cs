using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lives on SelectionPlayer.prefab. One instance per joined PlayerInput (spawned by
/// PlayerInputManager). Bridges its UI-map actions to CharacterSelectionManager.
/// </summary>
[RequireComponent(typeof(PlayerInput))]
public class CharacterSelectionInputHandler : MonoBehaviour
{
    private PlayerInput playerInput;
    private int slotIndex = -1;

    private InputAction navigateAction;
    private InputAction submitAction;
    private InputAction cancelAction;
    private InputAction startAction;
    private InputAction upgradeAction;

    private const float NavigateRepeatDelay = 0.35f;
    private float lastNavigateTime = -999f;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        navigateAction = playerInput.actions["UI/Navigate"];
        submitAction = playerInput.actions["UI/Submit"];
        cancelAction = playerInput.actions["UI/Cancel"];
        startAction = playerInput.actions["UI/Start"];
        upgradeAction = playerInput.actions["UI/Upgrade"];
    }

    private void Start()
    {
        if (CharacterSelectionManager.Instance != null)
        {
            slotIndex = CharacterSelectionManager.Instance.AssignSlot(playerInput);
        }
        else
        {
            Debug.LogError("CharacterSelectionInputHandler: CharacterSelectionManager.Instance is missing!");
        }
    }

    private void Update()
    {
        if (slotIndex < 0 || CharacterSelectionManager.Instance == null) return;

        if (CharacterSelectionManager.Instance.IsSlotUpgrading(slotIndex))
        {
            UpdateUpgradePanelInput();
            return;
        }

        if (upgradeAction != null && upgradeAction.WasPressedThisFrame())
        {
            CharacterSelectionManager.Instance.TryOpenUpgrade(slotIndex);
            return;
        }

        if (submitAction != null && submitAction.WasPressedThisFrame())
        {
            CharacterSelectionManager.Instance.TryConfirm(slotIndex);
        }

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
        {
            CharacterSelectionManager.Instance.TryLeave(slotIndex);
        }

        if (startAction != null && startAction.WasPressedThisFrame())
        {
            CharacterSelectionManager.Instance.TryStart(slotIndex);
        }

        if (navigateAction != null)
        {
            float y = navigateAction.ReadValue<Vector2>().y;
            if (Mathf.Abs(y) > 0.5f && Time.time - lastNavigateTime > NavigateRepeatDelay)
            {
                lastNavigateTime = Time.time;
                CharacterSelectionManager.Instance.NavigateCarousel(slotIndex, y > 0f ? -1 : 1);
            }
        }
    }

    /// <summary>While the skill tree panel is open: Navigate y moves the node cursor, Navigate x
    /// switches skill tabs, Submit buys the selected node, Cancel closes the panel.</summary>
    private void UpdateUpgradePanelInput()
    {
        if (submitAction != null && submitAction.WasPressedThisFrame())
        {
            CharacterSelectionManager.Instance.UpgradeSubmit(slotIndex);
        }

        if (cancelAction != null && cancelAction.WasPressedThisFrame())
        {
            CharacterSelectionManager.Instance.UpgradeCancel(slotIndex);
        }

        if (navigateAction == null) return;
        Vector2 dir = navigateAction.ReadValue<Vector2>();
        if (Time.time - lastNavigateTime <= NavigateRepeatDelay) return;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y) && Mathf.Abs(dir.x) > 0.5f)
        {
            lastNavigateTime = Time.time;
            CharacterSelectionManager.Instance.UpgradeSwitchTab(slotIndex, dir.x > 0f ? 1 : -1);
        }
        else if (Mathf.Abs(dir.y) > 0.5f)
        {
            lastNavigateTime = Time.time;
            CharacterSelectionManager.Instance.UpgradeNavigate(slotIndex, dir.y > 0f ? -1 : 1);
        }
    }
}
