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

    [Tooltip("Health at skill-tree level 1, before Body upgrades. The character's identity stat.")]
    public float baseMaxHealth = 100f;

    [Tooltip("Walk speed in m/s before Body upgrades and Battle Cry. Feeds TopDownPlayerController.")]
    public float baseMoveSpeed = 3f;

    public SkillTreeDefinition[] skillTrees = System.Array.Empty<SkillTreeDefinition>();
}
