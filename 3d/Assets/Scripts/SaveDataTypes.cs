using System.Collections.Generic;

[System.Serializable]
public class GameSaveData
{
    // Key: CharacterID (e.g. "Warrior", "Mage"), Value: Character Data
    public List<CharacterSaveData> characters = new List<CharacterSaveData>();
}

[System.Serializable]
public class CharacterSaveData
{
    public string characterID;
    public int souls;

    // "skillId/nodeId:rank" keys, e.g. "attributes/strength_1:3", "battlecry/unlock:1".
    // The skill tree (CharacterDefinition.skillTrees) is the source of truth for effect
    // values and derived attribute levels; this list is the only persisted skill/attribute state.
    public List<string> purchasedSkillNodes = new List<string>();

    // Constructor
    public CharacterSaveData(string id)
    {
        characterID = id;
        souls = 0;
        purchasedSkillNodes = new List<string>();
    }
}
