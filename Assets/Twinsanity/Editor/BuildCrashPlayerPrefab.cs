using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BuildCrashPlayerPrefab
{
    private const string PrefabPath = "Assets/Twinsanity/Resources/Characters/CrashPlayer.prefab";
    private const string ModelDataPath = "Assets/Twinsanity/Resources/Characters/CrashPlayerModel.asset";

    [MenuItem("Twinsanity/Build Crash Player Prefab")]
    public static void Build()
    {
        GameObject avatar = new GameObject("Crash - playable character");
        try
        {
            CharacterController collision = avatar.AddComponent<CharacterController>();
            collision.center = new Vector3(0f, 0.9f, 0f);
            collision.height = 1.8f;
            collision.radius = 0.42f;
            collision.stepOffset = 0.35f;
            collision.slopeLimit = 48f;
            avatar.AddComponent<CrashPlayerController>();
            avatar.AddComponent<CrashPlayableCharacter>();

            GameObject model = TwinsanityCharacterRuntime.Create("Crash", avatar.transform);
            model.transform.localPosition = new Vector3(0f, 0.48f, 0f);

            // Runtime-created Mesh and Material objects need persistent asset identities
            // before a prefab can reference them after restarting Unity.
            AssetDatabase.DeleteAsset(ModelDataPath);
            HashSet<Object> saved = new HashSet<Object>();
            bool created = false;
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh != null && saved.Add(mesh))
                {
                    if (!created) { AssetDatabase.CreateAsset(mesh, ModelDataPath); created = true; }
                    else AssetDatabase.AddObjectToAsset(mesh, ModelDataPath);
                }
                Material material = renderer.sharedMaterial;
                if (material != null && saved.Add(material))
                    AssetDatabase.AddObjectToAsset(material, ModelDataPath);
            }
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(avatar, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Crash player prefab ready: " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(avatar);
        }
    }

    public static void BuildIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        if (Resources.Load<TextAsset>("Characters/Crash") == null || Resources.Load<Shader>("Logo/GameOpaqueVertexColor") == null) return;
        try { Build(); }
        catch (System.Exception exception) { Debug.LogError("Crash prefab generation failed. Try Twinsanity > Build Crash Player Prefab. " + exception); }
    }
}
