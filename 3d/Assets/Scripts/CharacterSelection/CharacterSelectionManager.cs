using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum SlotPhase { Empty, Selecting, Ready, Upgrading }

public class PlayerSlotState
{
    public SlotPhase phase = SlotPhase.Empty;
    public PlayerInput playerInput;
    public int carouselIndex;
    public CharacterDefinition selectedCharacter;
    // Phase we return to when the upgrade panel closes (Selecting or Ready).
    public SlotPhase phaseBeforeUpgrade;
}

/// <summary>
/// Singleton. Owns the 4 player slots, the character roster, and the transition to gameplay.
/// </summary>
public class CharacterSelectionManager : MonoBehaviour
{
    public static CharacterSelectionManager Instance { get; private set; }

    private const int SlotCount = 4;

    [SerializeField] private PlayerSlotUI[] slots = new PlayerSlotUI[SlotCount];
    [SerializeField] private CharacterDefinition[] allCharacters = System.Array.Empty<CharacterDefinition>();
    [SerializeField] private GameObject startPrompt;
    [SerializeField] private string gameplaySceneName = "Level1";
    [SerializeField] private SkillTreeScreenController skillTreeScreen;

    private readonly PlayerSlotState[] states = new PlayerSlotState[SlotCount];

    private void Awake()
    {
        Instance = this;
        for (int i = 0; i < SlotCount; i++)
        {
            states[i] = new PlayerSlotState();
        }
    }

    private void Start()
    {
        RefreshAllSlots();
        UpdateStartPrompt();
    }

    private void RefreshAllSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            slots[i].SetPlayerIndex(i);
            slots[i].SetEmpty();
        }
    }

    private bool IsUnlocked(CharacterDefinition def)
    {
        return def.unlockedByDefault;
    }

    /// <summary>
    /// Routed through GameSaveController's cached per-character record when it exists, same as
    /// SkillTreeScreenController.Open — the upgrade shop opened from TryOpenUpgrade writes purchases
    /// through GameSaveController.FlushCharacter, so reading straight from SaveManager here (a
    /// separate loaded copy) would show pre-purchase souls until the process happened to reload that
    /// characterID from disk again. GameSaveController.Instance is expected to already exist by the
    /// time this scene runs — PlayerSpawner's earlier "will crash if GameSaveController is missing"
    /// contract applies to any scene the player reaches, not just gameplay — but this call site can
    /// be reached before a player has spawned, so it falls back rather than crashing.
    /// </summary>
    private static CharacterSaveData LoadCharacterSave(string characterID)
    {
        return GameSaveController.Instance != null
            ? GameSaveController.Instance.GetSharedRecord(characterID)
            : SaveManager.LoadCharacter(characterID);
    }

    // Called by CharacterSelectionInputHandler when PlayerInputManager spawns a SelectionPlayer.
    public int AssignSlot(PlayerInput playerInput)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (states[i].phase == SlotPhase.Empty)
            {
                states[i].phase = SlotPhase.Selecting;
                states[i].playerInput = playerInput;
                states[i].carouselIndex = 0;

                if (allCharacters.Length > 0)
                {
                    CharacterDefinition selected = allCharacters[0];
                    CharacterSaveData save = LoadCharacterSave(selected.characterID);
                    slots[i].SetSelecting(allCharacters, 0, IsUnlocked, save);
                }

                UpdateStartPrompt();
                return i;
            }
        }

        Debug.LogWarning("CharacterSelectionManager: all 4 slots full, ignoring join.");
        return -1;
    }

    public void TryLeave(int slotIndex)
    {
        PlayerSlotState state = states[slotIndex];
        if (state.phase == SlotPhase.Empty) return;

        // Destroy the SelectionPlayer PlayerInputManager spawned for this device, not just clear the
        // slot state. Without this the device's PlayerInput/CharacterSelectionInputHandler kept
        // living in the scene after leaving — a ghost that still held one of PlayerInputManager's
        // m_MaxPlayerCount (4) join slots forever, so the SAME device (and eventually every device,
        // if each cycled leave once) could never rejoin: PlayerInputManager only spawns a new
        // instance for a device it does not already consider joined.
        if (state.playerInput != null) Object.Destroy(state.playerInput.gameObject);

        state.phase = SlotPhase.Empty;
        state.playerInput = null;
        state.selectedCharacter = null;
        state.carouselIndex = 0;

        slots[slotIndex].SetEmpty();
        UpdateStartPrompt();
    }

    public void TryConfirm(int slotIndex)
    {
        PlayerSlotState state = states[slotIndex];
        if (state.phase != SlotPhase.Selecting) return;
        if (allCharacters.Length == 0) return;

        CharacterDefinition character = allCharacters[state.carouselIndex];
        if (!IsUnlocked(character))
        {
            slots[slotIndex].FlashLocked();
            return;
        }

        state.phase = SlotPhase.Ready;
        state.selectedCharacter = character;
        slots[slotIndex].SetReady(character);
        UpdateStartPrompt();
    }

    public void NavigateCarousel(int slotIndex, int direction)
    {
        PlayerSlotState state = states[slotIndex];
        if (state.phase != SlotPhase.Selecting) return;
        if (allCharacters.Length == 0) return;

        state.carouselIndex = ((state.carouselIndex + direction) % allCharacters.Length + allCharacters.Length) % allCharacters.Length;

        CharacterDefinition selected = allCharacters[state.carouselIndex];
        CharacterSaveData save = LoadCharacterSave(selected.characterID);
        slots[slotIndex].UpdateCarousel(state.carouselIndex);
        slots[slotIndex].UpdateStats(selected, save);
    }

    public void TryStart(int slotIndex)
    {
        if (states[slotIndex].phase != SlotPhase.Ready) return;
        if (!CanStartGame()) return;

        StartGame();
    }

    public void TryOpenUpgrade(int slotIndex)
    {
        PlayerSlotState state = states[slotIndex];
        if (state.phase != SlotPhase.Selecting && state.phase != SlotPhase.Ready) return;
        if (allCharacters.Length == 0) return;
        if (skillTreeScreen == null) return;
        if (states.Any(s => s.phase == SlotPhase.Upgrading)) return; // one shared panel

        CharacterDefinition character = state.phase == SlotPhase.Ready ? state.selectedCharacter : allCharacters[state.carouselIndex];
        if (!IsUnlocked(character))
        {
            slots[slotIndex].FlashLocked();
            return;
        }

        state.phaseBeforeUpgrade = state.phase;
        state.phase = SlotPhase.Upgrading;
        skillTreeScreen.Open(character, () => CloseUpgrade(slotIndex));
    }

    // --- Input forwarded to the open skill tree panel while a slot is Upgrading ---

    public void UpgradeNavigate(int slotIndex, Vector2 direction)
    {
        if (!IsSlotUpgrading(slotIndex) || skillTreeScreen == null) return;
        skillTreeScreen.Navigate(direction);
    }

    public void UpgradeSubmit(int slotIndex)
    {
        if (!IsSlotUpgrading(slotIndex) || skillTreeScreen == null) return;
        skillTreeScreen.SubmitSelected();
    }

    public void UpgradeCancel(int slotIndex)
    {
        if (!IsSlotUpgrading(slotIndex) || skillTreeScreen == null) return;
        // Not Close(): from inside a category's upgrade list, Cancel steps back to the category list
        // first. The screen invokes CloseUpgrade itself once it actually closes.
        skillTreeScreen.CancelPressed();
    }

    private void CloseUpgrade(int slotIndex)
    {
        PlayerSlotState state = states[slotIndex];
        if (state.phase != SlotPhase.Upgrading) return;

        state.phase = state.phaseBeforeUpgrade;

        // Souls may have changed — refresh whichever panel is now visible.
        CharacterDefinition selected = allCharacters.Length > 0 ? allCharacters[state.carouselIndex] : null;
        if (selected != null)
        {
            CharacterSaveData save = LoadCharacterSave(selected.characterID);
            slots[slotIndex].UpdateStats(selected, save);
        }
    }

    private bool CanStartGame() => states.Any(s => s.phase == SlotPhase.Ready);

    public bool IsSlotUpgrading(int slotIndex) => states[slotIndex].phase == SlotPhase.Upgrading;

    private void UpdateStartPrompt()
    {
        if (startPrompt != null) startPrompt.SetActive(CanStartGame());
    }

    private void StartGame()
    {
        CharacterSelectionData data = CharacterSelectionData.GetOrCreate();
        data.Clear();

        foreach (PlayerSlotState state in states)
        {
            if (state.phase != SlotPhase.Ready) continue;

            data.selections.Add(new CharacterSelectionData.Selection
            {
                characterID = state.selectedCharacter.characterID,
                deviceId = state.playerInput != null && state.playerInput.devices.Count > 0
                    ? state.playerInput.devices[0].deviceId
                    : -1
            });
        }

        SceneManager.LoadScene(gameplaySceneName);
    }
}
