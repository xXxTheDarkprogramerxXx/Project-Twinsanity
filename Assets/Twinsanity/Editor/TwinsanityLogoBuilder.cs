using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class TwinsanityLogoBuilder
{
    [Serializable]
    private class LogoManifest
    {
        public LogoJoint[] joints;
        public LogoPart[] parts;
    }

    [Serializable]
    private class LogoJoint
    {
        public int index;
        public int parent;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    private class LogoPart
    {
        public string name;
        public int joint;
    }

    [MenuItem("Tools/Twinsanity/Create Original Logo Preview")]
    public static void CreateLogoPreview()
    {
        const string scenePath = "Assets/Twinsanity/Scenes/OriginalLogoPreview.unity";
        if (File.Exists(scenePath) && !EditorUtility.DisplayDialog("Replace logo preview?", "This will replace OriginalLogoPreview.unity.", "Replace", "Cancel"))
            return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        GameObject root = BuildLogo();
        if (root == null)
            return;

        Camera camera = Camera.main;
        camera.orthographic = true;
        camera.orthographicSize = 6.2f;
        camera.transform.position = new Vector3(0, 0, 20);
        camera.transform.rotation = Quaternion.Euler(0, 180, 0);
        camera.backgroundColor = new Color(0.08f, 0.10f, 0.18f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        Directory.CreateDirectory("Assets/Twinsanity/Scenes");
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, scenePath);
        Selection.activeGameObject = root;
        Debug.Log("Built original seven-part GameLogo preview. Rotate its root or camera to inspect the 3D asset.");
    }

    public static GameObject BuildLogo()
    {
        const string basePath = "Assets/Twinsanity/Logo/";

        TextAsset manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(basePath + "logo_manifest.json");
        if (manifestAsset == null)
        {
            Debug.LogError("Logo asset manifest is missing. Copy the entire Assets/Twinsanity folder into the Unity project.");
            return null;
        }

        LogoManifest manifest = JsonUtility.FromJson<LogoManifest>(manifestAsset.text);
        if (manifest == null || manifest.joints == null || manifest.parts == null)
        {
            Debug.LogError("Could not parse the GameLogo asset manifest.");
            return null;
        }

        GameObject root = new GameObject("Original GameLogo - rotate this to inspect");
        GameObject[] joints = new GameObject[manifest.joints.Length];
        foreach (LogoJoint info in manifest.joints)
        {
            GameObject node = new GameObject("Joint " + info.index);
            Transform parent = info.parent == 255 ? root.transform : joints[info.parent].transform;
            node.transform.SetParent(parent, false);
            node.transform.localPosition = new Vector3(-info.position.x, info.position.y, info.position.z);
            node.transform.localRotation = new Quaternion(info.rotation.x, -info.rotation.y, -info.rotation.z, info.rotation.w);
            joints[info.index] = node;
        }

        Shader shader = Shader.Find("Twinsanity/Logo Vertex Color");
        if (shader == null)
        {
            Debug.LogError("The Twinsanity logo shader is missing.");
            return null;
        }
        Directory.CreateDirectory(basePath + "Generated");
        AssetDatabase.Refresh();

        foreach (LogoPart part in manifest.parts)
        {
            TextAsset bytes = AssetDatabase.LoadAssetAtPath<TextAsset>(basePath + part.name + ".bytes");
            if (bytes == null)
            {
                Debug.LogWarning("Missing logo mesh: " + part.name);
                continue;
            }

            using (MemoryStream stream = new MemoryStream(bytes.bytes))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                string magic = new string(reader.ReadChars(4));
                if (magic != "TLGO")
                    throw new InvalidDataException("Unexpected logo mesh format: " + part.name);
                int chunks = reader.ReadInt32();
                GameObject group = new GameObject(part.name);
                group.transform.SetParent(joints[part.joint].transform, false);
                for (int index = 0; index < chunks; index++)
                {
                    uint textureId = reader.ReadUInt32();
                    int count = reader.ReadInt32();
                    int triangles = reader.ReadInt32();
                    Vector3[] positions = new Vector3[count];
                    Vector2[] uv = new Vector2[count];
                    Color32[] colors = new Color32[count];
                    for (int v = 0; v < count; v++)
                    {
                        positions[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        uv[v] = new Vector2(reader.ReadSingle(), 1f - reader.ReadSingle());
                        colors[v] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                    }

                    int[] indices = new int[triangles * 3];
                    for (int i = 0; i < indices.Length; i++)
                        indices[i] = (int)reader.ReadUInt32();
                    Mesh mesh = new Mesh { name = part.name + "_" + index };
                    if (count > 65535)
                        mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = positions;
                    mesh.uv = uv;
                    mesh.colors32 = colors;
                    mesh.triangles = indices;
                    mesh.RecalculateNormals();
                    string meshPath = basePath + "Generated/" + part.name + "_" + index + ".asset";
                    AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(mesh, meshPath);

                    string texturePath = basePath + "Textures/" + textureId.ToString("X8") + ".png";
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    Material material = new Material(shader) { name = part.name + "_" + index };
                    material.SetTexture("_MainTex", texture);
                    string materialPath = basePath + "Generated/" + part.name + "_" + index + ".mat";
                    AssetDatabase.DeleteAsset(materialPath);
                    AssetDatabase.CreateAsset(material, materialPath);

                    GameObject item = new GameObject("Submesh " + index);
                    item.transform.SetParent(group.transform, false);
                    item.AddComponent<MeshFilter>().sharedMesh = mesh;
                    item.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
            }
        }

        AssetDatabase.SaveAssets();
        return root;
    }
}
