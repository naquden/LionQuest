using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LionQuest.Character
{
    /// <summary>
    /// Helper script to create floor prefabs from sprites.
    /// Attach this to a GameObject in the scene, assign a sprite, then use the context menu to create a prefab.
    /// </summary>
    public class FloorPrefabCreator : MonoBehaviour
    {
        [Header("Floor Settings")]
        [SerializeField] private Sprite floorSprite;
        [SerializeField] private string prefabName = "FloorTile";
        [SerializeField] private string prefabFolderPath = "Assets/Characters/Character/Prefabs";
        
#if UNITY_EDITOR
        [ContextMenu("Create Floor Prefab")]
        private void CreateFloorPrefab()
        {
            if (floorSprite == null)
            {
                Debug.LogError("FloorPrefabCreator: No sprite assigned! Please assign a sprite first.");
                return;
            }
            
            // Create a new GameObject
            GameObject floorObject = new GameObject(prefabName);
            
            // Add SpriteRenderer component
            SpriteRenderer spriteRenderer = floorObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = floorSprite;
            spriteRenderer.sortingOrder = 0; // Adjust as needed for your sorting layers
            
            // Optionally add a collider (uncomment if needed)
            // BoxCollider2D collider = floorObject.AddComponent<BoxCollider2D>();
            // collider.size = floorSprite.bounds.size;
            
            // Ensure the folder exists
            if (!AssetDatabase.IsValidFolder(prefabFolderPath))
            {
                string[] folders = prefabFolderPath.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    string newPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = newPath;
                }
            }
            
            // Create the prefab
            string prefabPath = $"{prefabFolderPath}/{prefabName}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(floorObject, prefabPath);
            
            Debug.Log($"FloorPrefabCreator: Created prefab at {prefabPath}");
            
            // Destroy the temporary GameObject
            DestroyImmediate(floorObject);
            
            // Select the created prefab
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        
        [ContextMenu("Create Floor Prefab from Tile Asset")]
        private void CreateFloorPrefabFromTile()
        {
            // This method can be used if you want to extract sprites from Tile assets
            // For now, use the regular CreateFloorPrefab method after extracting sprites
            Debug.Log("FloorPrefabCreator: To use Tile assets, first extract the sprite from the Tile asset, then use 'Create Floor Prefab'.");
        }
#endif
    }
}

