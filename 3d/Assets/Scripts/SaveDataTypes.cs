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

    // Skill 1 (Whirlwind AoE)
    public int skill1DamageLevel = 1;
    public int skill1RadiusLevel = 1;

    // Per-skill cooldown levels
    public int skill1CooldownLevel = 1;
    public int skill2CooldownLevel = 1;
    public int skill3CooldownLevel = 1;

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
        skill1DamageLevel = 1;
        skill1RadiusLevel = 1;
        skill1CooldownLevel = 1;
        skill2CooldownLevel = 1;
        skill3CooldownLevel = 1;
        unlockedSkills = new List<string>();
    }
}
