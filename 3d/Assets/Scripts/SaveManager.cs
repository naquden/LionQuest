using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class SaveManager
{
    private static string saveFileName = "gamesave.json";

    // Cache the data so we don't read disk every frame
    private static GameSaveData currentData;

    /// <summary>
    /// When set, all reads/writes go here instead of persistentDataPath/gamesave.json.
    /// Exists so the PlayMode test harness (which seeds and deletes saves in SetUp/TearDown)
    /// cannot destroy the player's real progress — before this, running the test suite wiped
    /// the same gamesave.json the editor and player use, which looked exactly like a bug:
    /// souls back to 0 and every skill slot locked again.
    /// Null = normal behavior.
    /// </summary>
    public static string SaveFilePathOverride { get; private set; }

    /// <summary>Redirects the save file and drops the in-memory cache so the next read hits the new path.</summary>
    public static void SetSaveFilePathOverride(string absolutePath)
    {
        SaveFilePathOverride = absolutePath;
        currentData = null;
    }

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
            existing.purchasedSkillNodes = new List<string>(charData.purchasedSkillNodes);
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
            try
            {
                currentData = JsonUtility.FromJson<GameSaveData>(json);
            }
            catch (System.Exception e)
            {
                // A save that fails to parse (truncated by a crash mid-write, hand-edited, etc.) must
                // not silently reset progress to zero and then overwrite the only copy of it on the
                // next save. Move the bad file aside so it survives for recovery/debugging, and start
                // this session from a fresh save rather than crashing outright.
                string corruptPath = path + ".corrupt";
                try
                {
                    File.Copy(path, corruptPath, overwrite: true);
                    Debug.LogError($"[SaveManager] Save file at {path} failed to parse ({e.Message}) — " +
                                   $"backed up to {corruptPath} and starting a fresh save. Progress in the " +
                                   "corrupt file is not lost, just not loaded automatically.");
                }
                catch (System.Exception backupError)
                {
                    Debug.LogError($"[SaveManager] Save file at {path} failed to parse ({e.Message}), and " +
                                   $"backing it up also failed ({backupError.Message}). Starting a fresh save.");
                }
            }
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

        // Write-to-temp-then-replace: a crash/power-loss mid-write to the real path leaves a
        // truncated, unparseable save (exactly the corrupt-file case LoadGame now has to recover
        // from). File.Replace is atomic on both platforms this project targets (Windows/macOS via
        // .NET's File.Replace and, on the rare filesystem where Replace itself is unsupported, the
        // Move fallback below), so the real save file is either the old complete version or the new
        // complete version — never a partial one.
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);

        if (File.Exists(path))
        {
            try
            {
                File.Replace(tempPath, path, null);
            }
            catch (System.PlatformNotSupportedException)
            {
                File.Delete(path);
                File.Move(tempPath, path);
            }
        }
        else
        {
            File.Move(tempPath, path);
        }
        // Debug.Log($"[SaveManager] Writing to disk... Game saved to: {path}");
    }

    public static string GetSavePath()
    {
        if (!string.IsNullOrEmpty(SaveFilePathOverride)) return SaveFilePathOverride;
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
