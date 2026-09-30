using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Original PS2 skin and blend-skin geometry extracted from beach.rm2.
public static class TwinsanityCharacterRuntime
{
    private sealed class AnimatedWing
    {
        public Mesh mesh;
        public Vector3[] rest;
    }

    private static readonly List<AnimatedWing> wings = new List<AnimatedWing>();

    public static GameObject Create(string character, Transform parent)
    {
        if (character == "Seagull") wings.Clear();
        TextAsset data = Resources.Load<TextAsset>("Characters/" + character);
        Shader shader = Resources.Load<Shader>("Logo/GameOpaqueVertexColor");
        if (data == null || shader == null) throw new FileNotFoundException("Missing game character mesh or shader: " + character);
        GameObject root = new GameObject("Original " + character + " (beach.rm2)");
        root.transform.SetParent(parent, false);
        Transform[] bones = null;
        BinaryReader rigReader = null;
        Matrix4x4[] bindPoses = null;
        if (character == "Crash")
        {
            TextAsset rig = Resources.Load<TextAsset>("Characters/CrashRig");
            if (rig == null) throw new FileNotFoundException("Missing Crash skin weights.");
            rigReader = new BinaryReader(new MemoryStream(rig.bytes));
            if (new string(rigReader.ReadChars(4)) != "TLRG") throw new InvalidDataException("Invalid Crash rig.");
            int boneCount = rigReader.ReadInt32();
            bones = new Transform[boneCount];
            int[] parents = new int[boneCount];
            Vector3[] positions = new Vector3[boneCount];
            Quaternion[] rotations = new Quaternion[boneCount];
            for (int b = 0; b < boneCount; b++)
            {
                parents[b] = rigReader.ReadInt32();
                positions[b] = new Vector3(rigReader.ReadSingle(), rigReader.ReadSingle(), rigReader.ReadSingle());
                rotations[b] = new Quaternion(rigReader.ReadSingle(), rigReader.ReadSingle(), rigReader.ReadSingle(), rigReader.ReadSingle());
                bones[b] = new GameObject("Crash joint " + b).transform;
            }
            for (int b = 0; b < boneCount; b++)
            {
                bones[b].SetParent(parents[b] < 0 ? root.transform : bones[parents[b]], false);
                bones[b].localPosition = positions[b];
                bones[b].localRotation = rotations[b];
            }
            bindPoses = new Matrix4x4[boneCount];
            for (int b = 0; b < boneCount; b++)
                bindPoses[b] = bones[b].worldToLocalMatrix * root.transform.localToWorldMatrix;
            if (rigReader.ReadInt32() != BitConverter.ToInt32(data.bytes, 4))
                throw new InvalidDataException("Crash rig and mesh counts differ.");
            CrashRigAnimator animator = root.AddComponent<CrashRigAnimator>();
            animator.Initialize(bones);
        }
        Dictionary<uint, Material> materials = new Dictionary<uint, Material>();
        using (BinaryReader reader = new BinaryReader(new MemoryStream(data.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLGO") throw new InvalidDataException("Invalid character mesh: " + character);
            int chunks = reader.ReadInt32();
            for (int index = 0; index < chunks; index++)
            {
                uint textureId = reader.ReadUInt32();
                int count = reader.ReadInt32();
                int trianglesCount = reader.ReadInt32();
                Vector3[] vertices = new Vector3[count];
                Vector2[] uv = new Vector2[count];
                Color32[] colors = new Color32[count];
                for (int i = 0; i < count; i++)
                {
                    vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    uv[i] = new Vector2(reader.ReadSingle(), 1f - reader.ReadSingle());
                    byte red = reader.ReadByte(); byte green = reader.ReadByte(); byte blue = reader.ReadByte(); byte alpha = reader.ReadByte();
                    colors[i] = character == "Seagull" ? new Color32((byte)(red * 0.75f), (byte)(green * 0.82f), (byte)(blue * 0.63f), alpha) : new Color32(red, green, blue, alpha);
                }
                int[] triangles = new int[trianglesCount * 3];
                for (int i = 0; i < triangles.Length; i++) triangles[i] = (int)reader.ReadUInt32();
                Mesh mesh = new Mesh { name = character + " original part " + index };
                if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.colors32 = colors;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                if (bones != null)
                {
                    if (rigReader.ReadInt32() != count) throw new InvalidDataException("Crash rig vertex count mismatch.");
                    BoneWeight[] weights = new BoneWeight[count];
                    for (int v = 0; v < count; v++)
                    {
                        weights[v] = new BoneWeight {
                            boneIndex0 = rigReader.ReadByte(), boneIndex1 = rigReader.ReadByte(), boneIndex2 = rigReader.ReadByte(),
                            weight0 = rigReader.ReadSingle(), weight1 = rigReader.ReadSingle(), weight2 = rigReader.ReadSingle()
                        };
                    }
                    mesh.boneWeights = weights;
                    mesh.bindposes = bindPoses;
                }
                if (!materials.TryGetValue(textureId, out Material material))
                {
                    material = new Material(shader) { name = character + " " + textureId.ToString("X8") };
                    string textureName = textureId.ToString("X8");
                    material.mainTexture = Resources.Load<Texture2D>("Characters/Textures/" + textureName);

                    if (character == "Crash")
                        material.SetFloat("_Brightness", textureId == 0x0B75575C ? 2f : 1.35f);
                    if (material.mainTexture == null) throw new FileNotFoundException("Missing character texture " + textureId.ToString("X8"));
                    materials.Add(textureId, material);
                }
                GameObject part = new GameObject("Game mesh " + index);
                part.transform.SetParent(root.transform, false);
                if (bones != null)
                {
                    SkinnedMeshRenderer renderer = part.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh;
                    renderer.sharedMaterial = material;
                    renderer.bones = bones;
                    renderer.rootBone = bones[0];
                    renderer.updateWhenOffscreen = true;
                }
                else
                {
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                if (character == "Seagull") wings.Add(new AnimatedWing { mesh = mesh, rest = vertices });
            }
        }
        if (rigReader != null) rigReader.Dispose();
        return root;
    }

    public static void AnimateSeagull(float time, float intensity)
    {
        float flap = Mathf.Sin(time * 23f) * 0.38f * Mathf.Clamp01(intensity);
        foreach (AnimatedWing wing in wings)
        {
            Vector3[] vertices = new Vector3[wing.rest.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = wing.rest[i];
                float wingSpan = Mathf.Max(0f, Mathf.Abs(p.x) - 0.45f);
                p.x = Mathf.Sign(p.x) * (Mathf.Min(0.45f, Mathf.Abs(p.x)) + wingSpan * Mathf.Clamp01(0.25f + intensity * 0.75f));
                p.y += wingSpan * flap;
                vertices[i] = p;
            }
            wing.mesh.vertices = vertices;
            wing.mesh.RecalculateBounds();
        }
    }
}

