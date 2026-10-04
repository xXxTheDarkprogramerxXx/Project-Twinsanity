using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BakeEditableBeachScene
{
    private const string ScenePath = "Assets/Twinsanity/Scenes/BeachLevel.unity";
    private const string BackupPath = "Assets/Twinsanity/Scenes/BeachLevel_Runtime.unity";
    private const string BakedPath = "Assets/Twinsanity/Baked/Beach";
    private static readonly string[] Chunks = { "Beach", "Bossarea", "Alwayson", "Huba", "Hubb", "Highpath", "Hubc", "Hubd", "Pier", "Totemex", "Docent", "Hubboat1", "Hubboat2" };
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static int assetNumber;

    [MenuItem("Twinsanity/Bake Editable Beach Scene")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Stop Play mode before baking the beach scene.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        BeachLevelRuntime runtime = UnityEngine.Object.FindObjectOfType<BeachLevelRuntime>();
        if (runtime == null) throw new InvalidOperationException("BeachLevel.unity has no BeachLevelRuntime component.");
        //if (runtime.useEditableScene || runtime.transform.Find("Editable Chunks") != null)
        //{
        //    Debug.Log("BeachLevel.unity is already editable. Edit and save it normally; baking again would erase your changes.");
        //    return;
        //}

        {
            Transform existing = runtime.transform.Find("Editable Chunks");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            Transform spawn = runtime.transform.Find("Crash Spawn");
            if (spawn != null) UnityEngine.Object.DestroyImmediate(spawn.gameObject);

            Transform ocean = runtime.transform.Find("Beach ocean");
            if (ocean != null) UnityEngine.Object.DestroyImmediate(ocean.gameObject);

            Transform sun = runtime.transform.Find("Beach sunlight");
            if (sun != null) UnityEngine.Object.DestroyImmediate(sun.gameObject);

            runtime.useEditableScene = false;
        }

        if (!File.Exists(BackupPath) && !AssetDatabase.CopyAsset(ScenePath, BackupPath))
            throw new IOException("Could not make the original scene backup.");

        try
        {
            EnsureFolder("Assets/Twinsanity/Baked");
            EnsureFolder(BakedPath);
            Materials.Clear();
            assetNumber = 0;
            GameObject editable = new GameObject("Editable Chunks");
            editable.transform.SetParent(runtime.transform, false);
            Dictionary<string, Transform> roots = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);

            foreach (string chunk in Chunks)
            {
                GameObject root = new GameObject(chunk + " streamed chunk");
                root.transform.SetParent(editable.transform, false);
                roots.Add(chunk, root.transform);
                GameObject scenery = TwinsanitySceneryRuntime.Create(root.transform, chunk);
                scenery.transform.localPosition = BeachLevelRuntime.ChunkOffset(chunk);
                scenery.transform.localRotation = BeachLevelRuntime.ChunkRotation(chunk);
                SaveMeshes(scenery, chunk + "_Scenery");
                EditorUtility.DisplayProgressBar("Baking editable beach", "Scenery: " + chunk, (Array.IndexOf(Chunks, chunk) + 1f) / (Chunks.Length + 2f));
            }

            AddCollision(roots);
            AddItems(roots);
            AddSea(runtime.transform);
            GameObject sun = new GameObject("Beach sunlight");
            sun.transform.SetParent(runtime.transform, false);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            sun.transform.rotation = Quaternion.Euler(38f, -45f, 0);

            GameObject spawn = new GameObject("Crash Spawn");
            spawn.transform.SetParent(runtime.transform, false);
            spawn.transform.position = new Vector3(-2.35f, 1.6f, -39.6f);
            AddEditableCrash(scene, spawn.transform);
            Physics.SyncTransforms();
            AddOpeningMask(roots["Beach"], spawn.transform.position);

            foreach (string chunk in Chunks) roots[chunk].gameObject.SetActive(chunk == "Beach");
            runtime.useEditableScene = true;
            EditorUtility.SetDirty(runtime);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Editable BeachLevel.unity saved. Move items under Editable Chunks in the Hierarchy and save the scene. Original: " + BackupPath);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static void AddEditableCrash(Scene scene, Transform spawn)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Twinsanity/Resources/Characters/CrashPlayer.prefab");
        if (source == null) throw new FileNotFoundException("CrashPlayer.prefab is missing.");
        GameObject avatar = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        avatar.name = "Crash Player";
        avatar.transform.SetParent(spawn, false);
        avatar.transform.localPosition = Vector3.zero;
        avatar.transform.localRotation = Quaternion.identity;
        CrashPlayableCharacter prefabRuntime = avatar.GetComponent<CrashPlayableCharacter>();
        if (prefabRuntime != null) UnityEngine.Object.DestroyImmediate(prefabRuntime);
        CrashPlayerController prefabController = avatar.GetComponent<CrashPlayerController>();
        if (prefabController != null) UnityEngine.Object.DestroyImmediate(prefabController);
        CrashRigAnimator animator = avatar.GetComponentInChildren<CrashRigAnimator>(true);
        if (animator == null) throw new InvalidDataException("CrashPlayer.prefab has no animation rig. Reimport the repaired prefab.");
        BeachPlayerController player = avatar.AddComponent<BeachPlayerController>();
        player.model = animator.transform;
        player.cameraTransform = Camera.main == null ? null : Camera.main.transform;
        player.initialCameraYaw = 180f;
    }

    private static void AddCollision(Dictionary<string, Transform> roots)
    {
        TextAsset source = Resources.Load<TextAsset>("BeachLevel/BeachCollision");
        if (source == null) throw new FileNotFoundException("BeachCollision.bytes is missing.");
        using (BinaryReader reader = new BinaryReader(new MemoryStream(source.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLC1") throw new InvalidDataException("Invalid collision data.");
            int count = reader.ReadInt32();
            for (int c = 0; c < count; c++)
            {
                string chunk = new string(reader.ReadChars(reader.ReadInt32()));
                int vertexCount = reader.ReadInt32();
                int triangleCount = reader.ReadInt32();
                Vector3[] vertices = new Vector3[vertexCount];
                int[] triangles = new int[triangleCount * 3];
                for (int i = 0; i < vertexCount; i++) vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                for (int i = 0; i < triangles.Length; i++) triangles[i] = (int)reader.ReadUInt32();
                Mesh mesh = new Mesh { name = chunk + " editable collision" };
                if (vertexCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, NextPath(chunk + "_Collision", ".asset"));
                GameObject surface = new GameObject(chunk + " walkable collision");
                surface.transform.SetParent(roots[chunk], false);
                surface.transform.localPosition = BeachLevelRuntime.ChunkOffset(chunk);
                surface.transform.localRotation = BeachLevelRuntime.ChunkRotation(chunk);
                surface.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }

    private static void AddItems(Dictionary<string, Transform> roots)
    {
        TextAsset source = Resources.Load<TextAsset>("BeachLevel/BeachInstances");
        if (source == null) throw new FileNotFoundException("BeachInstances.bytes is missing.");
        using (BinaryReader reader = new BinaryReader(new MemoryStream(source.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLI1") throw new InvalidDataException("Invalid instance data.");
            int chunks = reader.ReadInt32();
            for (int c = 0; c < chunks; c++)
            {
                string chunk = new string(reader.ReadChars(reader.ReadInt32()));
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    uint id = reader.ReadUInt32();
                    ushort objectId = reader.ReadUInt16();
                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    float yaw = reader.ReadSingle();
                    string kind = BeachLevelRuntime.ItemKind(objectId);
                    GameObject item = new GameObject(chunk + " instance " + id + " " + (kind ?? "Object " + objectId));
                    item.transform.SetParent(roots[chunk], false);
                    item.transform.position = BeachLevelRuntime.ChunkRotation(chunk) * position + BeachLevelRuntime.ChunkOffset(chunk);
                    item.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    BeachSourceInstance sourceInstance = item.AddComponent<BeachSourceInstance>();
                    sourceInstance.chunk = chunk;
                    sourceInstance.instanceId = (int)id;
                    sourceInstance.objectId = objectId;
                    sourceInstance.hasGameplay = kind != null;
                    if (kind == null) continue;
                    bool crate = kind.Contains("Crate");
                    string modelName = kind == "IronCrate" || kind == "MultipleHitCrate" || kind == "LevelCrate" ? "BasicCrate" : kind;
                    GameObject model = BeachLevelRuntime.BuildItemModel(modelName, item.transform);
                    model.transform.localScale = Vector3.one * (crate ? 1.25f : kind == "Wumpa" ? 0.7f : 1f);
                    SaveMeshes(model, chunk + "_" + id + "_" + modelName);
                    SphereCollider trigger = item.AddComponent<SphereCollider>();
                    trigger.isTrigger = true;
                    trigger.radius = crate ? 0.85f : 0.7f;
                    item.AddComponent<BeachItem>().kind = kind;
                }
            }
        }
    }

    private static void AddOpeningMask(Transform beach, Vector3 start)
    {
        Vector3 position = start + Quaternion.Euler(0, 180f, 0) * Vector3.forward * 5f + Vector3.up * 5f;
        if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore)) position = hit.point + Vector3.up * 1.25f;
        else position = start + Quaternion.Euler(0, 180f, 0) * Vector3.forward * 5f + Vector3.up * 1.25f;
        GameObject pickup = new GameObject("Opening Aku Aku pickup");
        pickup.transform.SetParent(beach, true);
        pickup.transform.position = position;
        GameObject model = BeachLevelRuntime.BuildItemModel("AkuMask", pickup.transform);
        model.transform.localScale = Vector3.one * 1.6f;
        SaveMeshes(model, "Opening_AkuMask");
        SphereCollider trigger = pickup.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.75f;
        pickup.AddComponent<BeachItem>().kind = "AkuMask";
    }

    private static void AddSea(Transform parent)
    {
        GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sea.name = "Beach ocean";
        sea.transform.SetParent(parent, false);
        sea.transform.position = new Vector3(0, -0.65f, 0);
        sea.transform.localScale = new Vector3(80, 1, 80);
        UnityEngine.Object.DestroyImmediate(sea.GetComponent<Collider>());
        Shader water = Resources.Load<Shader>("WaterSurface");
        sea.GetComponent<Renderer>().sharedMaterial = SaveMaterial(new Material(water != null ? water : Shader.Find("Unlit/Color")), "WaterSurface");
    }

    private static void SaveMeshes(GameObject root, string prefix)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            mesh.name = prefix + " mesh";
            AssetDatabase.CreateAsset(mesh, NextPath(prefix + "_Mesh", ".asset"));
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                Material material = renderer.sharedMaterial;
                string key = material.shader.name + ":" + (material.mainTexture == null ? "none" : AssetDatabase.GetAssetPath(material.mainTexture));
                renderer.sharedMaterial = SaveMaterial(material, key);
            }
        }
    }

    private static Material SaveMaterial(Material source, string key)
    {
        if (Materials.TryGetValue(key, out Material existing))
        {
            UnityEngine.Object.DestroyImmediate(source);
            return existing;
        }
        source.name = "Beach " + Materials.Count;
        AssetDatabase.CreateAsset(source, NextPath("Material", ".mat"));
        Materials.Add(key, source);
        return source;
    }

    private static string NextPath(string prefix, string extension)
    {
        string clean = prefix.Replace(' ', '_');
        return AssetDatabase.GenerateUniqueAssetPath(BakedPath + "/" + clean + "_" + assetNumber++ + extension);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
