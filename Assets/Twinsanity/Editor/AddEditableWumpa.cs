using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AddEditableWumpa
{
    [MenuItem("Twinsanity/Hub/Add Wumpa at Selected Object")]
    public static void Add()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Transform selected = Selection.activeTransform;
        if (selected == null) { Debug.LogError("Select a scene object where you want the Wumpa."); return; }
        GameObject pickup = new GameObject("Wumpa pickup");
        pickup.transform.SetParent(selected.parent, false);
        pickup.transform.position = selected.position + Vector3.up * .65f;
        GameObject model = BeachLevelRuntime.BuildItemModel("Wumpa", pickup.transform);
        model.transform.localScale = Vector3.one * .7f;
        string folder = "Assets/Twinsanity/Baked/WumpaPickups";
        if (!AssetDatabase.IsValidFolder("Assets/Twinsanity/Baked")) AssetDatabase.CreateFolder("Assets/Twinsanity", "Baked");
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Twinsanity/Baked", "WumpaPickups");
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            AssetDatabase.CreateAsset(filter.sharedMesh, AssetDatabase.GenerateUniqueAssetPath(folder + "/WumpaMesh.asset"));
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null && !AssetDatabase.Contains(renderer.sharedMaterial))
                AssetDatabase.CreateAsset(renderer.sharedMaterial, AssetDatabase.GenerateUniqueAssetPath(folder + "/WumpaMaterial.mat"));
        }
        SphereCollider collider = pickup.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = .7f;
        pickup.AddComponent<BeachItem>().kind = "Wumpa";
        Undo.RegisterCreatedObjectUndo(pickup, "Add editable Wumpa pickup");
        Selection.activeGameObject = pickup;
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(pickup.scene);
    }
}
