using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Rebuilds the seven extracted GameLogo parts for the ready-to-open scene.
public static class TwinsanityLogoRuntime
{
    [Serializable] private class Manifest { public Joint[] joints; public Part[] parts; }
    [Serializable] private class Joint { public int index; public int parent; public Vector3 position; public Quaternion rotation; }
    [Serializable] private class Part { public string name; public int joint; }

    public static RenderTexture Create(Transform owner, out Transform logoRoot, out Camera logoCamera)
    {
        logoRoot = null;
        logoCamera = null;
        TextAsset data = Resources.Load<TextAsset>("Logo/logo_manifest");
        Shader shader = Resources.Load<Shader>("Logo/TitleLogoText");
        if (data == null || shader == null)
        {
            Debug.LogError("Twinsanity logo Resources are missing.");
            return null;
        }
        Manifest manifest = JsonUtility.FromJson<Manifest>(data.text);
        if (manifest == null || manifest.joints == null || manifest.parts == null)
            throw new InvalidDataException("Invalid logo manifest.");

        GameObject root = new GameObject("Original GameLogo");
        root.transform.SetParent(owner, false);
        root.transform.localPosition = new Vector3(0, 0, 1000);
        logoRoot = root.transform;
        Transform[] joints = new Transform[manifest.joints.Length];
        foreach (Joint info in manifest.joints)
        {
            GameObject node = new GameObject("Joint " + info.index);
            node.transform.SetParent(info.parent == 255 ? root.transform : joints[info.parent], false);
            node.transform.localPosition = new Vector3(-info.position.x, info.position.y, info.position.z);
            node.transform.localRotation = new Quaternion(info.rotation.x, -info.rotation.y, -info.rotation.z, info.rotation.w);
            joints[info.index] = node.transform;
        }

        foreach (Part part in manifest.parts)
        {
            TextAsset bytes = Resources.Load<TextAsset>("Logo/" + part.name);
            if (bytes == null) throw new FileNotFoundException("Missing logo mesh: " + part.name);
            GameObject group = new GameObject(part.name);
            group.transform.SetParent(joints[part.joint], false);
            bool visible = part.name == "GameLogo_TextC" || part.name == "GameLogo_TextT";
            using (BinaryReader reader = new BinaryReader(new MemoryStream(bytes.bytes)))
            {
                if (new string(reader.ReadChars(4)) != "TLGO")
                    throw new InvalidDataException("Invalid logo mesh: " + part.name);
                int chunks = reader.ReadInt32();
                for (int index = 0; index < chunks; index++)
                {
                    uint textureId = reader.ReadUInt32();
                    int count = reader.ReadInt32();
                    int triangleCount = reader.ReadInt32();
                    Vector3[] vertices = new Vector3[count];
                    Vector2[] uv = new Vector2[count];
                    Color32[] colors = new Color32[count];
                    for (int v = 0; v < count; v++)
                    {
                        vertices[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        uv[v] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                        colors[v] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                    }
                    int[] triangles = new int[triangleCount * 3];
                    for (int i = 0; i < triangles.Length; i++) triangles[i] = (int)reader.ReadUInt32();
                    Mesh mesh = new Mesh { name = part.name + "_" + index };
                    if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = vertices;
                    mesh.uv = uv;
                    mesh.colors32 = colors;
                    mesh.triangles = triangles;
                    mesh.RecalculateNormals();
                    Material material = new Material(shader) { name = mesh.name };
                    material.mainTexture = Resources.Load<Texture2D>("Logo/Textures/" + textureId.ToString("X8"));
                    material.SetColor("_Tint", part.name == "GameLogo_TextC" ? new Color(1f, 0.88f, 0.10f) : new Color(1f, 0.30f, 0.04f));
                    GameObject submesh = new GameObject("Submesh " + index);
                    submesh.transform.SetParent(group.transform, false);
                    submesh.AddComponent<MeshFilter>().sharedMesh = mesh;
                    submesh.AddComponent<MeshRenderer>().sharedMaterial = material;
                    submesh.GetComponent<MeshRenderer>().enabled = visible;
                }
            }
        }

        RenderTexture target = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32);
        target.name = "Original GameLogo Render";
        target.Create();
        GameObject cameraObject = new GameObject("GameLogo Camera");
        cameraObject.transform.SetParent(owner, false);
        cameraObject.transform.localPosition = new Vector3(0, 0, 1020);
        cameraObject.transform.localRotation = Quaternion.Euler(0, 180, 0);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4.7f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.targetTexture = target;
        camera.enabled = false;
        camera.Render();
        logoCamera = camera;
        return target;
    }
}
