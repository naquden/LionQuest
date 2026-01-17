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
    
    // Attributes
    public int strengthLevel = 1;
    public int healthLevel = 1;
    public int speedLevel = 1;
    
    // Skills (List of unlocked skill names or IDs)
    public List<string> unlockedSkills = new List<string>();
    
    // Constructor
    public CharacterSaveData(string id)
    {
        characterID = id;
        souls = 0;
        strengthLevel = 1;
        healthLevel = 1;
        speedLevel = 1;
        unlockedSkills = new List<string>();
    }
}
