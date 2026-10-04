using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HubActor))]
public sealed class HubActorInspector : Editor
{
    private int selectedClip, frame;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        HubActor actor = (HubActor)target;
        HubActorDefinition definition = actor.definition;
        if (definition == null || definition.clipIds == null || definition.clipIds.Length == 0) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Original RM2 animation preview", EditorStyles.boldLabel);
        string[] options = new string[definition.clipIds.Length];
        for (int i = 0; i < options.Length; i++) options[i] = definition.clipIds[i] + " — " + definition.clipNames[i];
        selectedClip = Mathf.Clamp(selectedClip, 0, options.Length - 1);
        EditorGUI.BeginChangeCheck();
        selectedClip = EditorGUILayout.Popup("Native clip", selectedClip, options);
        int id = definition.clipIds[selectedClip];
        frame = EditorGUILayout.IntSlider("Frame", frame, 0, Mathf.Max(0, actor.NativeFrameCount(id) - 1));
        bool changed = EditorGUI.EndChangeCheck();
        if (GUILayout.Button("Preview selected frame") || changed)
        {
            if (Application.isPlaying) { actor.showBindPose = false; actor.previewClip = id; actor.previewFrame = frame; }
            else { actor.PreviewNativeFrame(id, frame); SceneView.RepaintAll(); }
        }
        if (GUILayout.Button("Play selected clip (Play mode)"))
        { actor.showBindPose = false; actor.previewClip = id; actor.previewFrame = -1; }
        if (GUILayout.Button("Return to gameplay"))
        {
            actor.previewClip = -1; actor.previewFrame = -1; actor.showBindPose = false;
            if (!Application.isPlaying) { actor.PreviewNativeFrame(definition.idleClip, 0); SceneView.RepaintAll(); }
        }
        EditorGUILayout.HelpBox("Choose a native clip and scrub it in the editor. During Play mode, Return to gameplay resumes actor behaviour.", MessageType.Info);
    }
}

public sealed class HubActorTextureImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Twinsanity/Resources/HubActors/Textures/")) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.sRGBTexture = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Repeat;
    }
}
