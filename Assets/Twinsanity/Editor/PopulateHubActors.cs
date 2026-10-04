using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PopulateHubActors
{
    private const string Baked = "Assets/Twinsanity/Baked/HubActors";
    [MenuItem("Twinsanity/Hub/Populate Missing Actors in Current Scene")]
    public static void PopulateCurrent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play mode first."); return; }
        Scene scene = SceneManager.GetActiveScene();
        BeachLevelRuntime runtime = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            runtime = root.GetComponentInChildren<BeachLevelRuntime>(true);
            if (runtime != null) break;
        }
        if (runtime == null || runtime.transform.Find("Editable Chunks") == null)
        {
            Debug.LogError("This scene has no saved Editable Chunks. Use Twinsanity > Hub > Create Populated Editable Scene Copy first."); return;
        }
        Populate(runtime);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Hub actors populated. Save the scene normally. Existing item transforms, meshes, Crash, and chunk visibility were retained.");
    }
    public static void Populate(BeachLevelRuntime runtime)
    {
        EnsureFolder("Assets/Twinsanity/Baked"); EnsureFolder(Baked);
        Transform chunks = runtime.transform.Find("Editable Chunks");
        if (chunks == null) throw new InvalidOperationException("No editable chunks.");
        Undo.RegisterFullObjectHierarchyUndo(chunks.gameObject, "Populate missing native hub actors");
        int added = 0, skipped = 0;
        BeachSourceInstance[] markers = chunks.GetComponentsInChildren<BeachSourceInstance>(true);
        try
        {
            for (int i = 0; i < markers.Length; i++)
            {
                BeachSourceInstance marker = markers[i];
                // This is deliberately additive: never replace a user's saved visual or existing item.
                if (marker.hasGameplay || marker.GetComponent<HubActor>() != null || marker.GetComponentInChildren<Renderer>(true) != null) { skipped++; continue; }
                HubActorDefinition definition = HubActorCatalog.Find(marker.chunk, marker.objectId);
                if (definition == null) { skipped++; continue; }
                HubActor actor = HubActorCatalog.Attach(marker.gameObject, definition);
                SaveVisualAssets(actor);
                marker.hasGameplay = definition.kind == "Chicken" || definition.kind == "Crab" || definition.kind == "Worm" || definition.kind == "WumpaTree" || definition.kind == "Enemy";
                marker.name = marker.chunk + " instance " + marker.instanceId + " " + definition.sourceName;
                EditorUtility.SetDirty(marker); EditorUtility.SetDirty(actor); added++;
                EditorUtility.DisplayProgressBar("Populating original hub actors", definition.sourceName, (i + 1f) / markers.Length);
            }
            AssetDatabase.SaveAssets();
        }
        finally { EditorUtility.ClearProgressBar(); }
        Debug.Log("Added " + added + " original actor placements; retained/skipped " + skipped + " other placements. Inactive chunk contents remain saved and editable.");
    }
    private static void SaveVisualAssets(HubActor actor)
    {
        SkinnedMeshRenderer[] renderers = actor.visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int p = 0; p < renderers.Length; p++)
        {
            SkinnedMeshRenderer renderer = renderers[p];
            string meshPath = Baked + "/" + actor.definition.resource + "_part_" + p + ".asset";
            Mesh existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existingMesh == null) AssetDatabase.CreateAsset(renderer.sharedMesh, meshPath);
            else { UnityEngine.Object.DestroyImmediate(renderer.sharedMesh); renderer.sharedMesh = existingMesh; }
            Material generated = renderer.sharedMaterial;
            string materialPath = Baked + "/" + actor.definition.resource + "_material_" + generated.mainTexture.name + ".mat";
            Material existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existingMaterial == null) AssetDatabase.CreateAsset(generated, materialPath);
            else renderer.sharedMaterial = existingMaterial;
            // A generated material may be shared by several parts. Do not destroy it during this loop.
        }
    }
    [MenuItem("Twinsanity/Hub/Show All Chunks for Editing")]
    public static void ShowAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            BeachLevelRuntime runtime = root.GetComponentInChildren<BeachLevelRuntime>(true);
            if (runtime == null) continue;
            Transform chunks = runtime.transform.Find("Editable Chunks");
            if (chunks == null) continue;
            foreach (Transform chunk in chunks) { Undo.RecordObject(chunk.gameObject, "Show hub chunks"); chunk.gameObject.SetActive(true); }
            EditorSceneManager.MarkSceneDirty(runtime.gameObject.scene);
        }
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
