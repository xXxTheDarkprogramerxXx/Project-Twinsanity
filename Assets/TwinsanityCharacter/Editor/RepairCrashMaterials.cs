using UnityEditor;
using UnityEngine;
namespace ProjectTwinsanity.Character.Editor
{
    public static class RepairCrashMaterials
    {
        [MenuItem("Tools/Project Twinsanity/Repair Crash Appearance")]
        public static void RepairAppearance()
        {
            Repair();
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/TwinsanityCharacter" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (!IsClosedLid(renderer)) continue;
                        renderer.enabled = false; changed = true; count++;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (SkinnedMeshRenderer renderer in Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>())
            {
                if (EditorUtility.IsPersistent(renderer) || !renderer.gameObject.scene.IsValid() || !IsClosedLid(renderer)) continue;
                Undo.RecordObject(renderer, "Open Crash Eyes"); renderer.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); EditorUtility.SetDirty(renderer);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
            }
            AssetDatabase.SaveAssets(); SceneView.RepaintAll(); Debug.Log("Opened Crash eyes by hiding the eight closed-eyelid surfaces; brightened materials. Updated " + count + " eyelid renderers in saved prefabs. Save your scene to keep scene-instance changes.");
        }
        private static bool IsClosedLid(SkinnedMeshRenderer renderer)
        {
            const string prefix = "Crash_Part_";
            if (!renderer.name.StartsWith(prefix) || !int.TryParse(renderer.name.Substring(prefix.Length), out int index) || index < 1 || index > 8) return false;
            string meshPath = AssetDatabase.GetAssetPath(renderer.sharedMesh);
            return meshPath.StartsWith("Assets/TwinsanityCharacter/");
        }
        [MenuItem("Tools/Project Twinsanity/Repair Crash Materials")]
        public static void Repair()
        {
            Shader shader = Shader.Find("Project Twinsanity/Crash Opaque Double Sided");
            if (shader == null) { Debug.LogError("Copy CrashOpaqueDoubleSided.shader into Assets/TwinsanityCharacter/Shaders first."); return; }
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/TwinsanityCharacter" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.Contains("/Materials/")) continue;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                if (texture == null) texture = material.mainTexture;
                Undo.RecordObject(material, "Repair Crash Material"); material.shader = shader; material.mainTexture = texture; material.SetColor("_Color", Color.white); material.SetFloat("_Brightness", 2f); material.SetFloat("_VertexColorStrength", 0f); material.renderQueue = -1; EditorUtility.SetDirty(material); count++;
            }
            AssetDatabase.SaveAssets(); SceneView.RepaintAll(); Debug.Log("Repaired " + count + " Crash materials with opaque, double-sided rendering.");
        }
    }
}
