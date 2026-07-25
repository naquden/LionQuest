using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum SlotPhase { Empty, Selecting, Ready }

public class PlayerSlotState
{
    public SlotPhase phase = SlotPhase.Empty;
    public PlayerInput playerInput;
    public int carouselIndex;
    public CharacterDefinition selectedCharacter;
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
    [SerializeField] private string gameplaySceneName = "SampleScene";

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
            slots[i].SetEmpty();
        }
    }

    private bool IsUnlocked(CharacterDefinition def)
    {
        return def.unlockedByDefault;
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
                    CharacterSaveData save = SaveManager.LoadCharacter(selected.characterID);
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
        CharacterSaveData save = SaveManager.LoadCharacter(selected.characterID);
        slots[slotIndex].UpdateCarousel(state.carouselIndex);
        slots[slotIndex].UpdateStats(selected, save);
    }

    public void TryStart(int slotIndex)
    {
        if (states[slotIndex].phase != SlotPhase.Ready) return;
        if (!CanStartGame()) return;

        StartGame();
    }

    private bool CanStartGame() => states.Any(s => s.phase == SlotPhase.Ready);

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
