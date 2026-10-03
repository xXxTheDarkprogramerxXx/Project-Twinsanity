using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ProjectTwinsanity.Character.Editor
{
    public static class BuildEditableCrash
    {
        private const string Source = "Assets/TwinsanityCharacter/Source";
        [Serializable] private class RigData { public string[] names; public int[] parents; public float[] localPositions, localRotations; }
        [Serializable] private class MeshData { public string name, texture; public float[] positions, normals, uv, colors, weights; public int[] triangles, joints; }
        [Serializable] private class MeshCollection { public MeshData[] meshes; }
        [Serializable] private class ClipData { public string name; public float duration; public bool loop; public int frameCount; public float[] times, positions, rotations; }
        [Serializable] private class ClipCollection { public int jointCount; public string[] paths; public ClipData[] clips; }

        [MenuItem("Tools/Project Twinsanity/Create Editable Crash Package")]
        public static void Build()
        {
            if (!File.Exists(Source + "/RigDefinition.json")) { Debug.LogError("Copy the package's Assets folder into your project first."); return; }
            string destination = AssetDatabase.GenerateUniqueAssetPath("Assets/TwinsanityCharacter/Generated");
            Directory.CreateDirectory(destination); Directory.CreateDirectory(destination + "/Meshes"); Directory.CreateDirectory(destination + "/Materials"); Directory.CreateDirectory(destination + "/Animations"); AssetDatabase.Refresh();
            GameObject player = null; Scene testScene = default(Scene); Scene previous = SceneManager.GetActiveScene();
            try
            {
                RigData rig = JsonUtility.FromJson<RigData>(File.ReadAllText(Source + "/RigDefinition.json"));
                MeshCollection meshes = JsonUtility.FromJson<MeshCollection>(File.ReadAllText(Source + "/CrashMeshes.json"));
                ClipCollection clips = JsonUtility.FromJson<ClipCollection>(File.ReadAllText(Source + "/CrashClips.json"));
                testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(testScene);
                player = new GameObject("Crash Player"); CharacterController capsule = player.AddComponent<CharacterController>(); capsule.height = 1.85f; capsule.center = new Vector3(0f, .925f, 0f); capsule.radius = .25f; capsule.stepOffset = .25f; capsule.skinWidth = .025f;
                EditableCrashPlayer movement = player.AddComponent<EditableCrashPlayer>(); GameObject visual = new GameObject("Visual"); visual.transform.SetParent(player.transform, false);
                Transform[] bones = new Transform[rig.names.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject(rig.names[i]).transform; bones[i].SetParent(rig.parents[i] < 0 ? visual.transform : bones[rig.parents[i]], false);
                    int p = i * 3, q = i * 4;
                    bones[i].localPosition = new Vector3(-rig.localPositions[p], rig.localPositions[p + 1], rig.localPositions[p + 2]);
                    bones[i].localRotation = new Quaternion(rig.localRotations[q], -rig.localRotations[q + 1], -rig.localRotations[q + 2], rig.localRotations[q + 3]);
                }
                Matrix4x4[] binds = new Matrix4x4[bones.Length]; for (int i = 0; i < bones.Length; i++) binds[i] = bones[i].worldToLocalMatrix * visual.transform.localToWorldMatrix;
                Dictionary<string, Material> materials = new Dictionary<string, Material>();
                Shader shader = Shader.Find("Project Twinsanity/Crash Opaque Double Sided");
                if (shader == null) throw new InvalidOperationException("Missing CrashOpaqueDoubleSided.shader. Copy the full package Assets folder into the project.");
                foreach (MeshData source in meshes.meshes)
                {
                    int count = source.positions.Length / 3; Mesh mesh = new Mesh { name = source.name }; Vector3[] vertices = new Vector3[count], normals = new Vector3[count]; Vector2[] uv = new Vector2[count]; Color[] colors = new Color[count]; BoneWeight[] weights = new BoneWeight[count];
                    for (int v = 0; v < count; v++)
                    {
                        int p = v * 3, q = v * 4, t = v * 2; vertices[v] = new Vector3(source.positions[p], source.positions[p + 1], source.positions[p + 2]); normals[v] = new Vector3(source.normals[p], source.normals[p + 1], source.normals[p + 2]); uv[v] = new Vector2(source.uv[t], source.uv[t + 1]); colors[v] = new Color(source.colors[q], source.colors[q + 1], source.colors[q + 2], 1f);
                        weights[v] = new BoneWeight { boneIndex0 = source.joints[q], boneIndex1 = source.joints[q + 1], boneIndex2 = source.joints[q + 2], boneIndex3 = source.joints[q + 3], weight0 = source.weights[q], weight1 = source.weights[q + 1], weight2 = source.weights[q + 2], weight3 = source.weights[q + 3] };
                    }
                    mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.colors = colors; mesh.triangles = source.triangles; mesh.boneWeights = weights; mesh.bindposes = binds; mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, destination + "/Meshes/" + source.name + ".asset");
                    if (!materials.TryGetValue(source.texture, out Material material))
                    {
                        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/" + source.texture); if (texture == null) throw new FileNotFoundException(source.texture);
                        material = new Material(shader) { name = Path.GetFileNameWithoutExtension(source.texture) }; material.mainTexture = texture; material.SetFloat("_Brightness", 2f); if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture); if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white); if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white); if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .05f); if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", .05f); AssetDatabase.CreateAsset(material, destination + "/Materials/" + material.name + ".mat"); materials.Add(source.texture, material);
                    }
                    GameObject part = new GameObject(source.name); part.transform.SetParent(visual.transform, false); SkinnedMeshRenderer renderer = part.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.bones = bones; renderer.rootBone = bones[0]; renderer.updateWhenOffscreen = true; if (source.texture == "F5B122D7.png") renderer.enabled = false;
                }
                Dictionary<string, AnimationClip> animations = new Dictionary<string, AnimationClip>();
                foreach (ClipData source in clips.clips)
                {
                    AnimationClip clip = new AnimationClip { name = "Crash_" + source.name, frameRate = 30f };
                    for (int joint = 0; joint < clips.jointCount; joint++)
                    {
                        for (int axis = 0; axis < 3; axis++) SetCurve(clip, clips.paths[joint], "m_LocalPosition." + "xyz"[axis], source, source.positions, clips.jointCount, joint, axis, 3);
                        for (int axis = 0; axis < 4; axis++) SetCurve(clip, clips.paths[joint], "m_LocalRotation." + "xyzw"[axis], source, source.rotations, clips.jointCount, joint, axis, 4);
                    }
                    clip.EnsureQuaternionContinuity(); AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = source.loop; AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, destination + "/Animations/" + clip.name + ".anim"); animations.Add(source.name, clip);
                }
                AnimatorController controller = MakeController(destination + "/Crash.controller", animations); Animator animator = visual.AddComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; movement.animator = animator; movement.effects = player.AddComponent<AudioSource>(); movement.effects.playOnAwake = false; movement.effects.spatialBlend = .7f; movement.spinSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/TwinsanityCharacter/Audio/Crash_Spin.wav");
                string prefabPath = destination + "/CrashPlayer.prefab"; GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, prefabPath); UnityEngine.Object.DestroyImmediate(player); player = (GameObject)PrefabUtility.InstantiatePrefab(prefab); player.transform.position = new Vector3(0f, .08f, 0f);
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Editable Ground"; floor.transform.position = new Vector3(0f, -.5f, 0f); floor.transform.localScale = new Vector3(24f, 1f, 24f);
                GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube); ramp.name = "Editable Ramp"; ramp.transform.position = new Vector3(3f, .35f, 3f); ramp.transform.localScale = new Vector3(2f, .4f, 4f); ramp.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);
                GameObject light = new GameObject("Sun"); Light sun = light.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(45f, -30f, 0f); RenderSettings.ambientLight = new Color(.5f, .5f, .5f);
                GameObject camera = new GameObject("Main Camera"); camera.tag = "MainCamera"; Camera lens = camera.AddComponent<Camera>(); lens.nearClipPlane = .05f; lens.fieldOfView = 55f; camera.AddComponent<AudioListener>(); EditableCrashCamera follow = camera.AddComponent<EditableCrashCamera>(); follow.target = player.transform; camera.transform.position = new Vector3(0f, 2f, 4.5f); camera.transform.LookAt(player.transform.position + Vector3.up); player.GetComponent<EditableCrashPlayer>().cameraTransform = camera.transform;
                EditorSceneManager.SaveScene(testScene, destination + "/CrashCharacterTest.unity"); AssetDatabase.SaveAssets(); Selection.activeObject = prefab; EditorGUIUtility.PingObject(prefab); Debug.Log("Created editable Crash prefab, 11 clips, controller and test scene in " + destination + ". Open CrashCharacterTest.unity and press Play.");
            }
            catch (Exception error) { Debug.LogException(error); }
            finally { if (testScene.IsValid() && testScene.isLoaded) EditorSceneManager.CloseScene(testScene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); AssetDatabase.Refresh(); }
        }
        private static void SetCurve(AnimationClip clip, string path, string property, ClipData source, float[] values, int jointCount, int joint, int axis, int width)
        {
            Keyframe[] keys = new Keyframe[source.frameCount]; for (int f = 0; f < keys.Length; f++) keys[f] = new Keyframe(source.times[f], values[(f * jointCount + joint) * width + axis]);
            AnimationCurve curve = new AnimationCurve(keys); for (int f = 0; f < keys.Length; f++) { AnimationUtility.SetKeyLeftTangentMode(curve, f, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve, f, AnimationUtility.TangentMode.Linear); } AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }
        private static AnimatorController MakeController(string path, Dictionary<string, AnimationClip> clips)
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path); controller.AddParameter("Speed", AnimatorControllerParameterType.Float); controller.AddParameter("Vertical", AnimatorControllerParameterType.Float); controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool); controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
            foreach (string name in new[] { "Jump", "DoubleJump", "Spin", "Slide" }) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            AnimatorStateMachine sm = controller.layers[0].stateMachine; Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>();
            AnimatorState loco = sm.AddState("Locomotion"); states.Add("Locomotion", loco); sm.defaultState = loco;
            BlendTree tree = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false }; AssetDatabase.AddObjectToAsset(tree, controller); tree.AddChild(clips["Idle"], 0f); tree.AddChild(clips["Walk"], 2.5f); tree.AddChild(clips["Run"], 6f); loco.motion = tree;
            foreach (string name in new[] { "Jump", "Apex", "Fall", "Land", "DoubleJump", "Spin", "Crouch", "Slide" }) { AnimatorState state = sm.AddState(name); state.motion = clips[name]; state.writeDefaultValues = false; states.Add(name, state); } loco.writeDefaultValues = false;
            foreach (string name in new[] { "Jump", "DoubleJump", "Spin", "Slide" }) { AnimatorStateTransition tr = sm.AddAnyStateTransition(states[name]); tr.hasExitTime = false; tr.duration = name == "DoubleJump" || name == "Spin" ? .03f : .07f; tr.canTransitionToSelf = false; tr.AddCondition(AnimatorConditionMode.If, 0f, name); }
            Transition(loco, states["Crouch"], false).AddCondition(AnimatorConditionMode.If, 0f, "Crouching"); Transition(states["Crouch"], loco, false).AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");
            Transition(loco, states["Fall"], false).AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded"); Transition(states["Crouch"], states["Fall"], false).AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            Transition(states["Jump"], states["Apex"], true); Transition(states["Apex"], states["Fall"], false).AddCondition(AnimatorConditionMode.Less, 0f, "Vertical");
            foreach (string name in new[] { "Jump", "Apex", "Fall", "DoubleJump" }) Transition(states[name], states["Land"], false).AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            Transition(states["DoubleJump"], states["Fall"], true); Transition(states["Land"], loco, true);
            foreach (string name in new[] { "Spin", "Slide" }) { Transition(states[name], loco, true).AddCondition(AnimatorConditionMode.If, 0f, "Grounded"); Transition(states[name], states["Fall"], true).AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded"); }
            return controller;
        }
        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, bool exit) { AnimatorStateTransition t = from.AddTransition(to); t.hasExitTime = exit; t.exitTime = 1f; t.duration = .06f; t.hasFixedDuration = true; return t; }
    }
}
