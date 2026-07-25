using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DontDestroyOnLoad carrier — passes confirmed selections from CharacterSelection into gameplay.
/// </summary>
public class CharacterSelectionData : MonoBehaviour
{
    public static CharacterSelectionData Instance { get; private set; }

    public struct Selection
    {
        public string characterID;
        public int deviceId;
    }

    // Only joined players present; order = player index 0..N-1
    public readonly List<Selection> selections = new List<Selection>();

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Clear() => selections.Clear();

    public static CharacterSelectionData GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("CharacterSelectionData");
        return go.AddComponent<CharacterSelectionData>();
    }
}
