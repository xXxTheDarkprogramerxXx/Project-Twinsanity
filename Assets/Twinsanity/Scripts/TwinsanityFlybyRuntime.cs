using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// The dedicated FrontEnd_FlyBy_Iceberg object from beach.rm2 (OGI 1033).
public static class TwinsanityFlybyRuntime
{
    [Serializable] private class Manifest { public Joint[] joints; public Part[] parts; }
    [Serializable] private class Joint { public int index; public int parent; public Vector3 position; public Quaternion rotation; }
    [Serializable] private class Part { public string name; public int joint; }

    public static GameObject Create(Transform parent)
    {
        TextAsset data = Resources.Load<TextAsset>("Flyby/manifest");
        Shader opaque = Resources.Load<Shader>("Logo/LogoVertexColor");
        Shader ice = Resources.Load<Shader>("Flyby/FlybyIce");
        if (data == null || opaque == null || ice == null) throw new FileNotFoundException("Missing FrontEnd_FlyBy_Iceberg resources.");
        Manifest manifest = JsonUtility.FromJson<Manifest>(data.text);
        GameObject root = new GameObject("Original front-end iceberg, Cortex and Uka Uka");
        root.transform.SetParent(parent, false);
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
            if (part.name == "Caustics") continue;
            TextAsset asset = Resources.Load<TextAsset>("Flyby/" + part.name);
            if (asset == null) throw new FileNotFoundException("Missing flyby part " + part.name);
            using (BinaryReader reader = new BinaryReader(new MemoryStream(asset.bytes)))
            {
                if (new string(reader.ReadChars(4)) != "TLGO") throw new InvalidDataException("Invalid flyby mesh " + part.name);
                int chunks = reader.ReadInt32();
                for (int c = 0; c < chunks; c++)
                {
                    uint tex = reader.ReadUInt32();
                    int count = reader.ReadInt32();
                    int triCount = reader.ReadInt32();
                    Vector3[] vertices = new Vector3[count];
                    Vector2[] uv = new Vector2[count];
                    Color32[] colors = new Color32[count];
                    for (int i = 0; i < count; i++)
                    {
                        vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                        uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                        colors[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                    }
                    int[] triangles = new int[triCount * 3];
                    for (int i = 0; i < triangles.Length; i++) triangles[i] = (int)reader.ReadUInt32();
                    Mesh mesh = new Mesh { name = "FrontEnd_FlyBy_Iceberg " + part.name };
                    if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = vertices;
                    mesh.uv = uv;
                    mesh.colors32 = colors;
                    mesh.triangles = triangles;
                    mesh.RecalculateNormals();
                    Material material = new Material(part.name == "Iceberg" ? ice : opaque) { name = part.name + " original material" };
                    material.mainTexture = Resources.Load<Texture2D>("Flyby/Textures/" + tex.ToString("X8"));
                    if (material.mainTexture == null) throw new FileNotFoundException("Missing flyby texture " + tex.ToString("X8"));
                    GameObject obj = new GameObject(part.name);
                    obj.transform.SetParent(joints[part.joint], false);
                    obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                    obj.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
            }
        }
        return root;
    }
}
