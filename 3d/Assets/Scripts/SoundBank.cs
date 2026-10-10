using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Named-event -> AudioClip lookup, referenced by AudioDirector. Gameplay code calls
/// AudioDirector.Instance.PlaySfx("Hit") rather than each script holding its own serialized
/// AudioClip field — with 4+ enemy archetypes (grunt/ranged/elite/boss) all needing hit/death
/// sounds, a per-prefab clip field would mean re-wiring every archetype prefab whenever a sound
/// changes. One bank asset, one place to swap a placeholder for real audio later.
/// </summary>
[CreateAssetMenu(fileName = "SoundBank", menuName = "LionQuest/Sound Bank")]
public class SoundBank : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string eventName;
        public AudioClip clip;
    }

    [SerializeField] private Entry[] entries = System.Array.Empty<Entry>();

    private Dictionary<string, AudioClip> lookup;

    public AudioClip GetClip(string eventName)
    {
        if (lookup == null) BuildLookup();

        if (lookup.TryGetValue(eventName, out AudioClip clip)) return clip;

        Debug.LogError($"SoundBank: no entry for event '{eventName}' — check the bank asset has a " +
                       "row with this exact name, or the caller has a typo.");
        return null;
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<string, AudioClip>(entries.Length);
        foreach (Entry entry in entries)
        {
            if (string.IsNullOrEmpty(entry.eventName)) continue;
            lookup[entry.eventName] = entry.clip;
        }
    }
}
