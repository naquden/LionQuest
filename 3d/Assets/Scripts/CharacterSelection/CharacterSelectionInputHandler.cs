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

    // True while the button that spawned this player is still held. PlayerInputManager joins on
    // buttonSouth, which is also UI/Submit — so the UI map is enabled mid-press and Submit reports
    // WasPressedThisFrame on this handler's very first Update. That confirmed the carousel's default
    // character off the join press itself: the slot jumped Selecting -> Ready before the player could
    // move the carousel (NavigateCarousel ignores a Ready slot), making every character except index
    // 0 unreachable. Swallow Submit until we've seen the button actually released once.
    // Starts true because this component is created *by* the join press. If a slot ever appears
    // without one being held, the first Update clears the flag and nothing is swallowed — on that
    // frame the button is not down, so WasPressedThisFrame is false anyway.
    private bool awaitingSubmitRelease = true;

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

        // Cleared before the Upgrading early-out, so the flag can't get stuck while the panel is open.
        // The action must be enabled (a disabled InputAction reports IsPressed() == false whatever its
        // control is doing) AND show neither a held state nor a press edge this frame. Both checks are
        // needed: a held join button fails IsPressed, while a join press that already released before
        // the UI map's first poll fails only WasPressedThisFrame — the queued edge is still waiting to
        // be consumed on this exact frame, which is how the join press confirmed the default character.
        if (awaitingSubmitRelease && submitAction != null && submitAction.enabled
            && !submitAction.IsPressed() && !submitAction.WasPressedThisFrame())
        {
            awaitingSubmitRelease = false;
        }

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

        if (!awaitingSubmitRelease && submitAction != null && submitAction.WasPressedThisFrame())
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

    /// <summary>While the skill tree panel is open: Navigate moves the spatial node cursor in
    /// whichever direction is pressed, Submit buys/ranks the selected node, Cancel closes the panel.</summary>
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
        if (dir.sqrMagnitude < 0.25f) return;
        if (Time.time - lastNavigateTime <= NavigateRepeatDelay) return;

        lastNavigateTime = Time.time;
        CharacterSelectionManager.Instance.UpgradeNavigate(slotIndex, dir);
    }
}
