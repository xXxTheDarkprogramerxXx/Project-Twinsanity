using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Positioned scenery meshes and textures extracted from beach.sm2.
public static class TwinsanitySceneryRuntime
{
    public static GameObject Create(Transform parent, string chunk = "Beach")
    {
        TextAsset data = Resources.Load<TextAsset>(chunk + "Scenery/" + chunk + "Scenery");
        Shader shader = Resources.Load<Shader>("BeachScenery/SceneryOpaque");
        if (data == null || shader == null) throw new FileNotFoundException("Missing beach.sm2 scenery resources.");
        GameObject root = new GameObject("Original " + chunk.ToLowerInvariant() + ".sm2 scenery");
        root.transform.SetParent(parent, false);
        using (BinaryReader reader = new BinaryReader(new MemoryStream(data.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLGO") throw new InvalidDataException("Invalid beach scenery mesh.");
            int chunks = reader.ReadInt32();
            for (int c = 0; c < chunks; c++)
            {
                uint textureId = reader.ReadUInt32();
                int count = reader.ReadInt32();
                int triangleCount = reader.ReadInt32();
                Vector3[] vertices = new Vector3[count];
                Vector2[] uv = new Vector2[count];
                Color32[] colors = new Color32[count];
                for (int i = 0; i < count; i++)
                {
                    vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    colors[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                }
                int[] triangles = new int[triangleCount * 3];
                for (int i = 0; i < triangles.Length; i++) triangles[i] = (int)reader.ReadUInt32();
                Mesh mesh = new Mesh { name = "beach.sm2 scenery " + c };
                if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.colors32 = colors;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                Material material = new Material(shader) { name = "Beach material " + textureId.ToString("X8") };
                material.mainTexture = Resources.Load<Texture2D>(chunk + "Scenery/Textures/" + textureId.ToString("X8"));
                if (material.mainTexture == null) throw new FileNotFoundException("Missing beach texture " + textureId.ToString("X8"));
                GameObject part = new GameObject("Beach mesh " + c);
                part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
        return root;
    }
}
