using UnityEngine;
using System.IO;
using System.Linq;

public static class SaveManager
{
    private static string saveFileName = "gamesave.json";
    
    // Cache the data so we don't read disk every frame
    private static GameSaveData currentData;

    public static GameSaveData GetRawData()
    {
        if (currentData == null) LoadGame();
        return currentData;
    }

    public static void SaveCharacter(CharacterSaveData charData)
    {
        if (currentData == null) LoadGame();

        // Update or Add character data
        var existing = currentData.characters.FirstOrDefault(c => c.characterID == charData.characterID);
        if (existing != null)
        {
            // Update existing stats
            existing.souls = charData.souls;
            existing.strengthLevel = charData.strengthLevel;
            existing.healthLevel = charData.healthLevel;
            existing.speedLevel = charData.speedLevel;
            existing.unlockedSkills = charData.unlockedSkills;
        }
        else
        {
            // Add new
            currentData.characters.Add(charData);
        }

        WriteToDisk();
    }

    public static CharacterSaveData LoadCharacter(string characterID)
    {
        if (currentData == null) LoadGame();

        var data = currentData.characters.FirstOrDefault(c => c.characterID == characterID);
        if (data != null)
        {
            return data;
        }
        
        // Return new default data if none exists
        return new CharacterSaveData(characterID);
    }

    private static void LoadGame()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            string json = File.ReadAllText(path);
            currentData = JsonUtility.FromJson<GameSaveData>(json);
        }
        
        if (currentData == null)
        {
            currentData = new GameSaveData();
        }
    }

    private static void WriteToDisk()
    {
        string path = GetSavePath();
        string json = JsonUtility.ToJson(currentData, true); // true = pretty print
        File.WriteAllText(path, json);
        // Debug.Log($"[SaveManager] Writing to disk... Game saved to: {path}");
    }

    private static string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, saveFileName);
    }
    
    // Helper to clear data
    public static void DeleteSave()
    {
        string path = GetSavePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            currentData = new GameSaveData();
            Debug.Log("[SaveManager] Save file deleted.");
        }
    }
}
