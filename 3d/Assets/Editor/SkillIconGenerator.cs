using UnityEditor;
using UnityEngine;

/// <summary>
/// Fills in missing HUD skill icons with procedural glyphs from UiTheme, so slots aren't
/// blank until real art exists. Only touches assets whose icon field is still unassigned —
/// never overwrites real art added later. Dual entry point per project convention.
/// </summary>
public static class SkillIconGenerator
{
    [MenuItem("LionQuest/Generate/Skill Icons")]
    public static void Generate()
    {
        int assigned = 0;
        assigned += AssignIcons<ProjectileSkillData>(UiTheme.DiamondSprite);
        assigned += AssignIcons<BattleCrySkillData>(UiTheme.RingSprite);
        assigned += AssignIcons<AttackData>(UiTheme.SquareSprite);
        assigned += AssignIcons<LaserBeamSkillData>(UiTheme.CircleSprite);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"SkillIconGenerator: assigned placeholder icons to {assigned} asset(s).");
    }

    static int AssignIcons<T>(Sprite glyph) where T : ScriptableObject
    {
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) continue;

            var so = new SerializedObject(asset);
            var iconProp = so.FindProperty("icon");
            if (iconProp == null || iconProp.objectReferenceValue != null) continue;

            iconProp.objectReferenceValue = glyph;
            so.ApplyModifiedProperties();
            count++;
        }
        return count;
    }
}
