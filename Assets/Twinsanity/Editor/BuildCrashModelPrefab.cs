using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildCrashModelPrefab
{
    private const string PrefabPath = "Assets/Twinsanity/Resources/Characters/CrashModel.prefab";
    private const string GeometryPath = "Assets/Twinsanity/Resources/Characters/CrashModelGeometry.asset";

    [MenuItem("Twinsanity/Build Crash Model Prefab")]
    public static void Build()
    {
        GameObject model = TwinsanityCharacterRuntime.Create("Crash", null);
        try
        {
            AssetDatabase.DeleteAsset(GeometryPath);
            HashSet<Object> saved = new HashSet<Object>();
            bool created = false;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh != null && saved.Add(mesh))
                {
                    if (!created) { AssetDatabase.CreateAsset(mesh, GeometryPath); created = true; }
                    else AssetDatabase.AddObjectToAsset(mesh, GeometryPath);
                }
                Material material = renderer.sharedMaterial;
                if (material != null && saved.Add(material)) AssetDatabase.AddObjectToAsset(material, GeometryPath);
            }
            if (!created) throw new System.InvalidOperationException("Crash has no skinned meshes.");
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(model, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Crash model and its animation rig saved at " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(model);
        }
    }
}
