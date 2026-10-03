using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Plays the existing TLAN channels. They already contain the viewer's local rotations.
public class CrashRigAnimator : MonoBehaviour
{
    private sealed class NativeClip
    {
        public int id, frameCount;
        public float fps;
        public Vector3[,] positions, scales;
        public Quaternion[,] rotations;
    }

    public float walkPlaybackSpeed = 1f;
    public float runPlaybackSpeed = 1f;
    public float blendSeconds = 0.12f;
    public int previewClip = -1;
    public int previewFrame = -1;
    public bool showBindPose;
    public string playingClip;

    [Tooltip("Aligns the original TLRG skeleton to the centred TLGO mesh. Use zero only if your mesh export has already been realigned.")]
    public Vector3 skeletonOriginOffset = new Vector3(0f, -0.5f, 0f);

    // Kept so existing CrashPlayableCharacter code still compiles. No longer applied.
    [HideInInspector] public float runGroundOffset;

    [SerializeField] private Transform[] joints;
    private int[] parents;
    private Vector3[] bindPositions, blendPositions, blendScales;
    private Quaternion[] bindRotations, blendRotations;
    private readonly Dictionary<int, NativeClip> clips = new Dictionary<int, NativeClip>();
    private readonly List<Mesh> ownedMeshes = new List<Mesh>();
    private bool initialized, failed;
    private int activeClip = -1;
    private float clipTime, blendTime, movement, verticalSpeed, spinProgress, landingTime;
    private float doubleJumpProgress = -1f;
    private bool grounded = true, spinning, crouching, sliding;

    private void Awake()
    {
        // Create() adds this component before it finishes creating the mesh parts.
        // Defer bind-pose rebuilding until LateUpdate, when those renderers exist.
        if (joints == null || joints.Length != 51) joints = FindOrderedJoints();
    }

    public void Initialize(Transform[] bones)
    {
        if (initialized || failed) return;
        joints = FindOrderedJoints();
        if (bones == null) return;
        foreach (Transform bone in bones)
            if (TryGetJointIndex(bone, out int index) && index >= 0 && index < joints.Length) joints[index] = bone;
    }

    private void PrepareRig()
    {
        if (joints == null || joints.Length != 51) joints = FindOrderedJoints();
        for (int j = 0; j < joints.Length; j++)
            if (joints[j] == null) throw new InvalidDataException("Missing Transform 'Crash joint " + j + "'.");

        TextAsset source = Resources.Load<TextAsset>("Characters/CrashRig");
        if (source == null) throw new FileNotFoundException("Missing Characters/CrashRig.bytes.");
        parents = new int[joints.Length];
        bindPositions = new Vector3[joints.Length];
        bindRotations = new Quaternion[joints.Length];
        blendPositions = new Vector3[joints.Length];
        blendRotations = new Quaternion[joints.Length];
        blendScales = new Vector3[joints.Length];
        using (BinaryReader reader = new BinaryReader(new MemoryStream(source.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLRG" || reader.ReadInt32() != joints.Length)
                throw new InvalidDataException("CrashRig.bytes is not the expected 51-joint TLRG rig.");
            for (int j = 0; j < joints.Length; j++)
            {
                parents[j] = reader.ReadInt32();
                bindPositions[j] = ReadVector(reader);
                bindRotations[j] = ReadRotation(reader);
                if (parents[j] < -1 || parents[j] >= j) throw new InvalidDataException("Invalid parent for Crash joint " + j + ".");
                if (parents[j] < 0) bindPositions[j] += skeletonOriginOffset;
                if (parents[j] >= 0 && joints[j].parent != joints[parents[j]])
                    throw new InvalidDataException("Crash joint " + j + " must be a direct child of Crash joint " + parents[j] + ".");
            }
        }

        ApplyBindPose();
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0) throw new InvalidDataException("Crash has no SkinnedMeshRenderer parts.");
        HashSet<Transform> knownBones = new HashSet<Transform>(joints);
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            if (renderer.sharedMesh == null) continue;
            Transform[] rendererBones = renderer.bones;
            if (rendererBones.Length == 0) continue;
            bool belongsToCrash = true;
            foreach (Transform bone in rendererBones)
                if (bone == null || !knownBones.Contains(bone)) belongsToCrash = false;
            if (!belongsToCrash) continue;

            Matrix4x4[] bindPoses = new Matrix4x4[rendererBones.Length];
            for (int j = 0; j < rendererBones.Length; j++)
                bindPoses[j] = rendererBones[j].worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            // Change a runtime copy, preserving the imported/shared mesh asset.
            Mesh mesh = Instantiate(renderer.sharedMesh);
            mesh.name = renderer.sharedMesh.name + " (aligned bind pose)";
            mesh.bindposes = bindPoses;
            renderer.sharedMesh = mesh;
            ownedMeshes.Add(mesh);
        }

        LoadAnimations();
        initialized = true;
    }

    private void LoadAnimations()
    {
        TextAsset source = Resources.Load<TextAsset>("Characters/CrashAnimations");
        if (source == null) throw new FileNotFoundException("Missing Characters/CrashAnimations.bytes.");
        using (BinaryReader reader = new BinaryReader(new MemoryStream(source.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLAN" || reader.ReadInt32() != joints.Length)
                throw new InvalidDataException("CrashAnimations.bytes does not match the original rig.");
            int count = reader.ReadInt32();
            if (count < 1 || count > 1000) throw new InvalidDataException("Invalid animation clip count.");
            for (int c = 0; c < count; c++)
            {
                NativeClip clip = new NativeClip { id = reader.ReadInt32(), frameCount = reader.ReadInt32(), fps = reader.ReadSingle() };
                if (clip.frameCount < 1 || clip.frameCount > 10000 || !Finite(clip.fps) || clip.fps <= 0f)
                    throw new InvalidDataException("Invalid native clip " + clip.id + ".");
                clip.positions = new Vector3[clip.frameCount, joints.Length];
                clip.rotations = new Quaternion[clip.frameCount, joints.Length];
                clip.scales = new Vector3[clip.frameCount, joints.Length];
                for (int f = 0; f < clip.frameCount; f++)
                    for (int j = 0; j < joints.Length; j++)
                    {
                        clip.positions[f, j] = ReadVector(reader);
                        clip.rotations[f, j] = ReadRotation(reader);
                        clip.scales[f, j] = ReadVector(reader);
                        if (parents[j] < 0) clip.positions[f, j] += skeletonOriginOffset;
                    }
                clips.Add(clip.id, clip);
            }
        }
        if (!clips.ContainsKey(0)) throw new InvalidDataException("Missing Crash idle clip 0.");
    }

    public void SetMovement(float amount, bool onGround, bool spin, float progress, bool crouch, bool slide, float vertical, float doubleJump)
    {
        if (!grounded && onGround) landingTime = 0.14f;
        movement = amount;
        grounded = onGround;
        spinning = spin;
        spinProgress = progress;
        crouching = crouch;
        sliding = slide;
        verticalSpeed = vertical;
        doubleJumpProgress = doubleJump;
    }

    private void LateUpdate()
    {
        if (failed) return;
        if (!initialized)
        {
            try { PrepareRig(); }
            catch (Exception exception) { failed = true; Debug.LogException(exception, this); return; }
        }
        if (showBindPose)
        {
            ApplyBindPose();
            activeClip = -1;
            playingClip = "Bind pose";
            return;
        }

        landingTime = Mathf.Max(0f, landingTime - Time.deltaTime);
        int desired = previewClip >= 0 ? previewClip : SelectClip();
        if (!clips.TryGetValue(desired, out NativeClip clip)) { desired = 0; clip = clips[0]; }
        if (activeClip != desired)
        {
            for (int j = 0; j < joints.Length; j++)
            {
                blendPositions[j] = joints[j].localPosition;
                blendRotations[j] = joints[j].localRotation;
                blendScales[j] = joints[j].localScale;
            }
            activeClip = desired;
            clipTime = 0f;
            blendTime = 0f;
        }

        float speed = desired == 1 ? walkPlaybackSpeed : desired == 2 ? runPlaybackSpeed : 1f;
        clipTime += Time.deltaTime * Mathf.Max(0f, speed);
        blendTime += Time.deltaTime;
        bool loop = desired == 0 || desired == 1 || desired == 2 || desired == 8;
        int last = clip.frameCount - 1;
        // The viewer uses the last frame as the endpoint of its looping interval.
        float frame = loop && last > 0 ? Mathf.Repeat(clipTime * clip.fps, last) : Mathf.Min(clipTime * clip.fps, last);
        if (previewClip < 0 && desired == 3 && spinning) frame = Mathf.Clamp01(spinProgress) * last;
        if (previewClip < 0 && desired == 13 && doubleJumpProgress >= 0f) frame = Mathf.Clamp01(doubleJumpProgress) * last;
        bool scrub = previewClip >= 0 && previewFrame >= 0;
        if (scrub) frame = Mathf.Clamp(previewFrame, 0, last);
        int from = Mathf.FloorToInt(frame), to = Mathf.Min(from + 1, last);
        float fraction = frame - from;
        float blend = scrub || blendSeconds <= 0f ? 1f : Mathf.Clamp01(blendTime / blendSeconds);
        for (int j = 0; j < joints.Length; j++)
        {
            Vector3 position = Vector3.Lerp(clip.positions[from, j], clip.positions[to, j], fraction);
            Quaternion rotation = Quaternion.Slerp(clip.rotations[from, j], clip.rotations[to, j], fraction);
            Vector3 scale = Vector3.Lerp(clip.scales[from, j], clip.scales[to, j], fraction);
            joints[j].localPosition = Vector3.Lerp(blendPositions[j], position, blend);
            joints[j].localRotation = Quaternion.Slerp(blendRotations[j], rotation, blend);
            joints[j].localScale = Vector3.Lerp(blendScales[j], scale, blend);
        }
        playingClip = "Native clip " + desired + " / frame " + frame.ToString("F2");
    }

    private void ApplyBindPose()
    {
        for (int j = 0; j < joints.Length; j++)
        {
            joints[j].localPosition = bindPositions[j];
            joints[j].localRotation = bindRotations[j];
            joints[j].localScale = Vector3.one;
        }
    }

    private Transform[] FindOrderedJoints()
    {
        Transform[] result = new Transform[51];
        foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            if (TryGetJointIndex(bone, out int index) && index >= 0 && index < result.Length) result[index] = bone;
        return result;
    }

    private static bool TryGetJointIndex(Transform bone, out int index)
    {
        index = -1;
        const string prefix = "Crash joint ";
        return bone != null && bone.name.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(bone.name.Substring(prefix.Length), out index);
    }

    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    private static Vector3 ReadVector(BinaryReader reader)
    {
        Vector3 value = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z)) throw new InvalidDataException("Non-finite animation vector.");
        return value;
    }

    private static Quaternion ReadRotation(BinaryReader reader)
    {
        Quaternion q = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        float length = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (!Finite(length) || length < 0.000001f) throw new InvalidDataException("Invalid animation quaternion.");
        return new Quaternion(q.x / length, q.y / length, q.z / length, q.w / length);
    }

    private int SelectClip()
    {
        if (spinning) return 3;
        if (doubleJumpProgress >= 0f) return 13;
        if (sliding) return 54;
        if (!grounded) return verticalSpeed > 3f ? 4 : verticalSpeed > -1f ? 5 : 7;
        if (landingTime > 0f) return 6;
        if (crouching) return 8;
        return movement > 0.8f ? 2 : movement > 0.05f ? 1 : 0;
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
    }
}
