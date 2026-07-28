using UnityEngine;

[CreateAssetMenu(menuName = "LionQuest/CharacterDefinition")]
public class CharacterDefinition : ScriptableObject
{
    [Tooltip("Must match CharacterStats.characterID exactly (e.g. 'AssCap')")]
    public string characterID;
    public string displayName;
    public Sprite portrait;
    public Sprite portraitLocked;
    public bool unlockedByDefault;
    public GameObject playerPrefab;
    public SkillTreeDefinition[] skillTrees = System.Array.Empty<SkillTreeDefinition>();
}
