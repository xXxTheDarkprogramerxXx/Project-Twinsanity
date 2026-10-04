using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FixSavedSeagullUVs
{
    [MenuItem("Twinsanity/Hub/Fix Saved Seagull UVs")]
    public static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Stop Play mode first."); return; }
        HashSet<Mesh> changed = new HashSet<Mesh>();
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (HubActor actor in root.GetComponentsInChildren<HubActor>(true))
        {
            if (actor.definition == null || !actor.definition.resource.EndsWith("_685") || actor.visual == null) continue;
            TextAsset source = Resources.Load<TextAsset>("HubActors/" + actor.definition.resource);
            if (source == null) continue;
            SkinnedMeshRenderer[] renderers = actor.visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            using (BinaryReader reader = new BinaryReader(new MemoryStream(source.bytes)))
            {
                if (new string(reader.ReadChars(4)) != "TLAC" || reader.ReadInt32() != 1) throw new InvalidDataException("Invalid seagull binary.");
                int bones = reader.ReadInt32(), parts = reader.ReadInt32(); reader.BaseStream.Position += bones * 32;
                if (renderers.Length != parts) throw new InvalidDataException("Seagull renderer count differs from original parts.");
                for (int p = 0; p < parts; p++)
                {
                    reader.ReadUInt32(); int vertices = reader.ReadInt32(), triangles = reader.ReadInt32();
                    Vector2[] uv = new Vector2[vertices];
                    for (int v = 0; v < vertices; v++)
                    {
                        reader.BaseStream.Position += 12;
                        uv[v] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                        reader.BaseStream.Position += 28;
                    }
                    reader.BaseStream.Position += triangles * 12;
                    Mesh mesh = renderers[p].sharedMesh;
                    if (mesh == null || mesh.vertexCount != vertices) throw new InvalidDataException("Seagull vertex count differs from original mesh.");
                    if (changed.Add(mesh)) { Undo.RecordObject(mesh, "Restore original seagull UVs"); mesh.uv = uv; EditorUtility.SetDirty(mesh); }
                }
            }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Restored original UVs on " + changed.Count + " saved seagull mesh assets. Geometry, weights, bones, animations and other creatures were retained.");
    }
}
