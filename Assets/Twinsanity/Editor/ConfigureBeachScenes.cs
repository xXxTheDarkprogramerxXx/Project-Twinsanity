using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ConfigureBeachScenes
{
    static ConfigureBeachScenes()
    {
        string title = "Assets/Twinsanity/Scenes/TitleScreen.unity";
        string beach = "Assets/Twinsanity/Scenes/BeachLevel.unity";
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool changed = false;
        if (!scenes.Exists(scene => scene.path == title)) { scenes.Add(new EditorBuildSettingsScene(title, true)); changed = true; }
        if (!scenes.Exists(scene => scene.path == beach)) { scenes.Add(new EditorBuildSettingsScene(beach, true)); changed = true; }
        if (changed) EditorBuildSettings.scenes = scenes.ToArray();
        EditorApplication.delayCall += RestoreStockBeachScene;
    }

    private static void RestoreStockBeachScene()
    {
        const string path = "Assets/Twinsanity/Scenes/BeachLevel.unity";
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(path)) return;
        string scene = File.ReadAllText(path);
        // The uploaded scene has only its root and camera. Never rebake a scene
        // containing user-placed objects or an existing Editable Chunks hierarchy.
        if (scene.Contains("m_Name: Editable Chunks")) return;
        if (scene.Split(new[] { "--- !u!1 &" }, System.StringSplitOptions.None).Length - 1 != 2) return;
        try { BakeEditableBeachScene.Bake(); }
        catch (System.Exception error) { Debug.LogError("Beach scene restore failed. Use Twinsanity > Bake Editable Beach Scene after fixing this error: " + error); }
    }
}
