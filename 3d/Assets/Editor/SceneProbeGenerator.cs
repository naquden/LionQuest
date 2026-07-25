using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Probe: verifies an agent can create a brand-new scene headlessly and that it
/// serializes as text YAML (unlike the legacy binary SampleScene).
/// Creates Assets/Scenes/AgentProbe.unity with a camera, light, and a cube.
/// </summary>
public static class SceneProbeGenerator
{
    const string ScenePath = "Assets/Scenes/AgentProbe.unity";

    [MenuItem("LionQuest/Generate/Scene Probe")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "ProbeCube";
        cube.transform.position = new Vector3(0f, 0.5f, 0f);

        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        string full = Path.Combine(Directory.GetCurrentDirectory(), ScenePath);
        bool isText = false;
        if (File.Exists(full))
        {
            using var r = new StreamReader(full);
            char[] head = new char[5];
            int n = r.Read(head, 0, 5);
            isText = n == 5 && new string(head) == "%YAML";
        }

        Debug.Log($"PROBE|saved={saved} exists={File.Exists(full)} isTextYAML={isText} path={ScenePath}");
    }
}
