using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnableSeagullFlight
{
    [MenuItem("Twinsanity/Hub/Enable Seagull Fly Away in Current Scene")]
    public static void Enable()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play mode first."); return; }
        Scene scene = SceneManager.GetActiveScene(); int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (HubActor actor in root.GetComponentsInChildren<HubActor>(true))
        {
            if (actor.definition == null || !actor.definition.resource.EndsWith("_685") || actor.GetComponent<HubSeagullFlight>() != null) continue;
            Undo.AddComponent<HubSeagullFlight>(actor.gameObject); count++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Enabled seagull animation + home-flight behaviour on " + count + " placed birds. Save the scene. Meshes, textures, brightness, and existing placement transforms were retained.");
    }
}
