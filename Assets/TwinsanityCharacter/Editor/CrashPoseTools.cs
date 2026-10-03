using UnityEditor;
using UnityEngine;
namespace ProjectTwinsanity.Character.Editor
{
    public class CrashPoseTools : EditorWindow
    {
        private Animator character;
        private Transform upper, middle, end;
        private Vector3 target;
        private bool handles;
        [MenuItem("Tools/Project Twinsanity/Crash Pose Tools")]
        private static void Open() { GetWindow<CrashPoseTools>("Crash Pose Tools"); }
        private void OnEnable() { SceneView.duringSceneGui += Draw; }
        private void OnDisable() { SceneView.duringSceneGui -= Draw; }
        private void OnGUI()
        {
            character = (Animator)EditorGUILayout.ObjectField("Character Animator", character, typeof(Animator), true);
            EditorGUILayout.HelpBox("Open your prefab or test scene, assign Visual's Animator, and choose a limb. Drag its target in Scene view to pose it with IK. Record poses using Unity's Animation window. The Animator must not be playing while posing.", MessageType.Info);
            if (character == null) return;
            if (GUILayout.Button("Left Arm")) Choose("UpperArm_L", "LowerArm_L", "Hand_L");
            if (GUILayout.Button("Right Arm")) Choose("UpperArm_R", "LowerArm_R", "Hand_R");
            if (GUILayout.Button("Left Leg")) Choose("UpperLeg_L", "LowerLeg_L", "Foot_L");
            if (GUILayout.Button("Right Leg")) Choose("UpperLeg_R", "LowerLeg_R", "Foot_R");
            handles = EditorGUILayout.Toggle("Show IK Handle", handles);
            EditorGUILayout.HelpBox("For head, torso and fingers: select the named bone in Hierarchy and use Unity's Rotate tool. Changes to a pose do not automatically become animation keys; enable recording in the Animation window.", MessageType.None);
        }
        private Transform Find(string name) { foreach (Transform t in character.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }
        private void Choose(string a, string b, string c) { upper = Find(a); middle = Find(b); end = Find(c); if (end == null) return; target = end.position; handles = true; Selection.activeTransform = end; SceneView.RepaintAll(); }
        private void Draw(SceneView view)
        {
            if (!handles || character == null || upper == null || middle == null || end == null) return;
            Handles.color = Color.cyan; Handles.DrawLine(upper.position, middle.position); Handles.DrawLine(middle.position, end.position);
            EditorGUI.BeginChangeCheck(); Vector3 next = Handles.PositionHandle(target, Quaternion.identity);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObjects(new Object[] { upper, middle, end }, "Pose Crash Limb"); target = next;
            Vector3 start = upper.position; float a = Vector3.Distance(start, middle.position), b = Vector3.Distance(middle.position, end.position); Vector3 offset = target - start;
            if (offset.sqrMagnitude < .00001f || a < .0001f || b < .0001f) return;
            float distance = Mathf.Clamp(offset.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f); Vector3 direction = offset.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(middle.position - start, direction);
            if (pole.sqrMagnitude < .0001f) pole = Vector3.ProjectOnPlane(character.transform.forward, direction);
            if (pole.sqrMagnitude < .0001f) pole = Vector3.ProjectOnPlane(character.transform.right, direction);
            float along = (a * a - b * b + distance * distance) / (2f * distance); Vector3 knee = start + direction * along + pole.normalized * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            Quaternion endRotation = end.rotation; upper.rotation = Quaternion.FromToRotation(middle.position - start, knee - start) * upper.rotation; middle.rotation = Quaternion.FromToRotation(end.position - middle.position, start + direction * distance - middle.position) * middle.rotation; end.rotation = endRotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(upper); PrefabUtility.RecordPrefabInstancePropertyModifications(middle); PrefabUtility.RecordPrefabInstancePropertyModifications(end); SceneView.RepaintAll();
        }
    }
}
