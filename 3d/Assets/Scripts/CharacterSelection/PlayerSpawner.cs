using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

/// <summary>
/// Lives in the gameplay scene. Spawns one player per confirmed CharacterSelection slot,
/// or a single default player when launched directly (no selection data).
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints = System.Array.Empty<Transform>();
    [SerializeField] private CharacterDefinition fallbackDefinition;
    [SerializeField] private GameObject gameControllerPrefab;
    [SerializeField] private CharacterDefinition[] allCharacters = System.Array.Empty<CharacterDefinition>();

    private void Start()
    {
        EnsureGameSaveController();

        if (CharacterSelectionData.Instance == null || CharacterSelectionData.Instance.selections.Count == 0)
        {
            SpawnFallbackPlayer();
            return;
        }

        var selections = CharacterSelectionData.Instance.selections;
        for (int i = 0; i < selections.Count; i++)
        {
            SpawnSelectedPlayer(selections[i], i);
        }
    }

    private void EnsureGameSaveController()
    {
        if (GameSaveController.Instance != null) return;

        if (gameControllerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: GameSaveController.Instance is missing and no gameControllerPrefab is assigned!");
            return;
        }

        Instantiate(gameControllerPrefab);
    }

    private void SpawnFallbackPlayer()
    {
        if (fallbackDefinition == null || fallbackDefinition.playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner: no CharacterSelectionData and no fallbackDefinition assigned — cannot spawn a player.");
            return;
        }

        Transform point = spawnPoints.Length > 0 ? spawnPoints[0] : transform;
        SpawnPlayer(fallbackDefinition, point, pairWithDevice: null);
    }

    private void SpawnSelectedPlayer(CharacterSelectionData.Selection selection, int index)
    {
        CharacterDefinition definition = FindDefinition(selection.characterID);
        if (definition == null || definition.playerPrefab == null)
        {
            Debug.LogError($"PlayerSpawner: no CharacterDefinition/playerPrefab found for characterID '{selection.characterID}'.");
            return;
        }

        Transform point = index < spawnPoints.Length ? spawnPoints[index] : transform;

        InputDevice device = selection.deviceId >= 0 ? InputSystem.GetDeviceById(selection.deviceId) : null;
        SpawnPlayer(definition, point, device);
    }

    private void SpawnPlayer(CharacterDefinition definition, Transform point, InputDevice pairWithDevice)
    {
        GameObject instance = Instantiate(definition.playerPrefab);
        instance.SetActive(false);

        CharacterStats stats = instance.GetComponent<CharacterStats>();
        if (stats != null) stats.characterID = definition.characterID;

        instance.transform.position = point.position;
        instance.transform.rotation = point.rotation;

        if (pairWithDevice != null)
        {
            PlayerInput pi = instance.GetComponent<PlayerInput>();
            if (pi != null)
            {
                InputUser.PerformPairingWithDevice(pairWithDevice, pi.user);
            }
        }

        instance.SetActive(true);
    }

    private CharacterDefinition FindDefinition(string characterID)
    {
        foreach (CharacterDefinition def in allCharacters)
        {
            if (def != null && def.characterID == characterID) return def;
        }
        return null;
    }
}
