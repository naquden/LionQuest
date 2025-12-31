#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using LionQuest.Character.Tiles;

namespace LionQuest.Character.Tiles
{
    [CustomEditor(typeof(BiomeMapGenerator))]
    public class BiomeMapGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            BiomeMapGenerator generator = (BiomeMapGenerator)target;
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Quick Tile Assignment", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Auto-Find Tiles from Palette"))
            {
                AutoFindTiles(generator);
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Map Generation", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Generate Biome Map"))
            {
                generator.GenerateBiomeMap();
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This generator creates natural-looking maps with:\n" +
                "• Biomes: Clustered areas of similar tiles\n" +
                "• Trails: Curvy mud trails through grass\n" +
                "• Paths: Straight paths connecting areas\n\n" +
                "Assign at least a grass tile to get started!",
                MessageType.Info
            );
        }
        
        private void AutoFindTiles(BiomeMapGenerator generator)
        {
            SerializedObject serializedObject = new SerializedObject(generator);
            SerializedProperty grassTileProp = serializedObject.FindProperty("grassTile");
            SerializedProperty mudTileProp = serializedObject.FindProperty("mudTile");
            SerializedProperty stoneTileProp = serializedObject.FindProperty("stoneTile");
            SerializedProperty sandTileProp = serializedObject.FindProperty("sandTile");
            
            string[] tileGuids = AssetDatabase.FindAssets("t:Tile", new[] { "Assets/Free 32x32 Isometric Tileset Pack/Tile Palette/Palette Tiles" });
            
            if (tileGuids.Length > 0)
            {
                int assigned = 0;
                
                // Assign first few tiles to different biome types
                for (int i = 0; i < Mathf.Min(4, tileGuids.Length); i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(tileGuids[i]);
                    UnityEngine.Tilemaps.TileBase tile = AssetDatabase.LoadAssetAtPath<UnityEngine.Tilemaps.TileBase>(path);
                    
                    if (tile != null)
                    {
                        if (i == 0 && grassTileProp.objectReferenceValue == null)
                        {
                            grassTileProp.objectReferenceValue = tile;
                            assigned++;
                        }
                        else if (i == 1 && mudTileProp.objectReferenceValue == null)
                        {
                            mudTileProp.objectReferenceValue = tile;
                            assigned++;
                        }
                        else if (i == 2 && stoneTileProp.objectReferenceValue == null)
                        {
                            stoneTileProp.objectReferenceValue = tile;
                            assigned++;
                        }
                        else if (i == 3 && sandTileProp.objectReferenceValue == null)
                        {
                            sandTileProp.objectReferenceValue = tile;
                            assigned++;
                        }
                    }
                }
                
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(generator);
                
                Debug.Log($"BiomeMapGenerator: Auto-assigned {assigned} tiles from palette");
            }
            else
            {
                Debug.LogWarning("BiomeMapGenerator: No tiles found in the palette folder.");
            }
        }
    }
}
#endif

