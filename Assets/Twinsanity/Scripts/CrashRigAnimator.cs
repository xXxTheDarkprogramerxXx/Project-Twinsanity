using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Original beach.rm2 joint channels drive Crash's skinned mesh.
public class CrashRigAnimator : MonoBehaviour
{
    private sealed class NativeClip
    {
        public int id;
        public int frameCount;
        public float fps;
        public Vector3[,] positions;
        public Quaternion[,] rotations;
        public Vector3[,] scales;
    }

    public float walkPlaybackSpeed = 1f;
    public float runPlaybackSpeed = 1f;

    [Range(0f, 1f)] public float walkPoseWeight = 0.8f;
    [Range(0f, 1f)] public float runPoseWeight = 0.4f;

    public bool calibrateToIdle = true;

    [Header("Native Animation Space")]
    public bool sourceRotationsAreModelSpace = true;

    [Header("Temporary Safety")]
    public bool lockBonePositions = true;
    public bool lockBoneScales = true;
    public bool applyRootMotion = false;

    [Range(0, 50)]
    public int rootMotionJoint = 1;

    public bool autoGroundRun = true;
    public float runGroundOffset;
    public float blendSeconds = 0.12f;

    public int previewClip = -1;

    [Range(-1, 50)]
    public int debugAnimatedJoint = -1;

    public bool showBindPose;
    public string playingClip;

    [SerializeField] private Transform[] joints;

    private int[] parentJointIndices;

    private Vector3[] bindPositions;
    private Quaternion[] bindRotations;
    private Vector3[] bindScales;

    private Vector3[] blendPositions;
    private Quaternion[] blendRotations;
    private Vector3[] blendScales;

    private readonly Dictionary<int, NativeClip> clips = new Dictionary<int, NativeClip>();

    private int activeClip = -1;
    private float clipTime;
    private float blendTime;

    private float movement;
    private float verticalSpeed;
    private float spinProgress;
    private float doubleJumpProgress = -1f;
    private float landingTime;

    private bool grounded;
    private bool spinning;
    private bool crouching;
    private bool sliding;

    private static readonly float[] RunFootCorrection =
    {
        -0.280f,
        -0.209f,
        -0.080f,
         0.001f,
        -0.002f,
         0.054f,
        -0.048f,
        -0.236f,
        -0.190f,
        -0.077f,
         0.002f,
         0.012f,
         0.053f,
        -0.065f
    };

    private void Awake()
    {
        if (clips.Count > 0)
            return;

        Transform[] restored = FindOrderedJoints();

        for (int i = 0; i < restored.Length; i++)
        {
            if (restored[i] == null)
            {
                Debug.LogError("Crash model prefab is missing joint " + i, this);
                return;
            }
        }

        Initialize(restored);
    }

    public void Initialize(Transform[] bones)
    {
        if (clips.Count > 0)
            return;

        if (bones == null || bones.Length == 0)
        {
            Debug.LogError("CrashRigAnimator.Initialize received no bones.", this);
            return;
        }

        Transform[] orderedBones = new Transform[51];

        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];

            if (bone == null)
                continue;

            if (!TryGetJointIndex(bone, out int index))
                continue;

            if (index < 0 || index >= orderedBones.Length)
                continue;

            orderedBones[index] = bone;
        }

        bool missingJoint = false;

        for (int i = 0; i < orderedBones.Length; i++)
        {
            if (orderedBones[i] == null)
            {
                missingJoint = true;
                break;
            }
        }

        if (missingJoint)
        {
            Transform[] hierarchyBones = FindOrderedJoints();

            for (int i = 0; i < orderedBones.Length; i++)
            {
                if (orderedBones[i] == null)
                    orderedBones[i] = hierarchyBones[i];
            }
        }

        for (int i = 0; i < orderedBones.Length; i++)
        {
            if (orderedBones[i] == null)
            {
                Debug.LogError(
                    "Could not map Crash animation joint " +
                    i +
                    ". Expected Transform named 'Crash joint " +
                    i +
                    "'.",
                    this);

                return;
            }
        }

        joints = orderedBones;

        bindPositions = new Vector3[joints.Length];
        bindRotations = new Quaternion[joints.Length];
        bindScales = new Vector3[joints.Length];

        blendPositions = new Vector3[joints.Length];
        blendRotations = new Quaternion[joints.Length];
        blendScales = new Vector3[joints.Length];

        parentJointIndices = new int[joints.Length];

        for (int i = 0; i < joints.Length; i++)
        {
            bindPositions[i] = joints[i].localPosition;
            bindRotations[i] = NormalizeQuaternion(joints[i].localRotation);
            bindScales[i] = joints[i].localScale;

            blendPositions[i] = bindPositions[i];
            blendRotations[i] = bindRotations[i];
            blendScales[i] = bindScales[i];

            parentJointIndices[i] = FindParentJointIndex(joints[i]);
        }

        Debug.Log("Crash joint hierarchy:", this);

        for (int i = 0; i < joints.Length; i++)
        {
            Debug.Log(
                "Joint " +
                i +
                " -> Parent " +
                parentJointIndices[i],
                this);
        }

        LoadAnimations();
    }

    private void LoadAnimations()
    {
        TextAsset source = Resources.Load<TextAsset>("Characters/CrashAnimations");

        if (source == null)
            throw new FileNotFoundException("Missing native CrashAnimations.bytes.");

        using (BinaryReader reader = new BinaryReader(new MemoryStream(source.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLAN")
                throw new InvalidDataException("Invalid Crash animation data.");

            int jointCount = reader.ReadInt32();
            int clipCount = reader.ReadInt32();

            if (jointCount != joints.Length)
            {
                throw new InvalidDataException(
                    "Crash animation and rig joint counts differ. Animation=" +
                    jointCount +
                    ", Rig=" +
                    joints.Length);
            }

            for (int c = 0; c < clipCount; c++)
            {
                NativeClip clip = new NativeClip();

                clip.id = reader.ReadInt32();
                clip.frameCount = reader.ReadInt32();
                clip.fps = reader.ReadSingle();

                clip.positions = new Vector3[clip.frameCount, jointCount];
                clip.rotations = new Quaternion[clip.frameCount, jointCount];
                clip.scales = new Vector3[clip.frameCount, jointCount];

                for (int f = 0; f < clip.frameCount; f++)
                {
                    for (int j = 0; j < jointCount; j++)
                    {
                        clip.positions[f, j] = new Vector3(
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle());

                        clip.rotations[f, j] = NormalizeQuaternion(
                            new Quaternion(
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle()));

                        clip.scales[f, j] = new Vector3(
                            reader.ReadSingle(),
                            reader.ReadSingle(),
                            reader.ReadSingle());
                    }
                }

                clips.Add(clip.id, clip);
            }
        }

        Debug.Log(
            "CrashRigAnimator loaded " +
            clips.Count +
            " native animation clips with " +
            joints.Length +
            " joints.",
            this);
    }

    public void SetMovement(
        float amount,
        bool onGround,
        bool spin,
        float progress,
        bool crouch,
        bool slide,
        float vertical,
        float doubleJump)
    {
        if (!grounded && onGround)
            landingTime = 0.14f;

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
        if (joints == null || joints.Length == 0 || clips.Count == 0)
            return;

        if (showBindPose)
        {
            ApplyBindPose();

            activeClip = -1;
            playingClip = "Bind pose";

            return;
        }

        landingTime = Mathf.Max(0f, landingTime - Time.deltaTime);

        int desired =
            previewClip >= 0 && clips.ContainsKey(previewClip)
                ? previewClip
                : SelectClip();

        if (!clips.TryGetValue(desired, out NativeClip clip))
            return;

        if (!clips.TryGetValue(0, out NativeClip idle))
            return;

        if (desired != activeClip)
        {
            for (int j = 0; j < joints.Length; j++)
            {
                blendPositions[j] = joints[j].localPosition;
                blendRotations[j] = NormalizeQuaternion(joints[j].localRotation);
                blendScales[j] = joints[j].localScale;
            }

            activeClip = desired;
            clipTime = 0f;
            blendTime = 0f;
        }

        float speed =
            desired == 1
                ? walkPlaybackSpeed
                : desired == 2
                    ? runPlaybackSpeed
                    : 1f;

        clipTime += Time.deltaTime * Mathf.Max(0.05f, speed);
        blendTime += Time.deltaTime;

        bool loop =
            desired == 0 ||
            desired == 1 ||
            desired == 2 ||
            desired == 8;

        float frame =
            loop
                ? (clipTime * clip.fps) % clip.frameCount
                : Mathf.Min(clipTime * clip.fps, clip.frameCount - 1);

        if (desired == 3 && spinning)
            frame = Mathf.Clamp01(spinProgress) * (clip.frameCount - 1);

        if (desired == 13 && doubleJumpProgress >= 0f)
            frame = Mathf.Clamp01(doubleJumpProgress) * (clip.frameCount - 1);

        int from = Mathf.FloorToInt(frame);

        int to =
            loop
                ? (from + 1) % clip.frameCount
                : Mathf.Min(from + 1, clip.frameCount - 1);

        float fraction = frame - from;

        float blend =
            blendSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(blendTime / blendSeconds);

        for (int j = 0; j < joints.Length; j++)
        {
            Quaternion fromLocal =
                GetSourceLocalRotation(
                    clip,
                    from,
                    j);

            Quaternion toLocal =
                GetSourceLocalRotation(
                    clip,
                    to,
                    j);

            Quaternion sourceLocalRotation =
                NormalizeQuaternion(
                    Quaternion.Slerp(
                        fromLocal,
                        toLocal,
                        fraction));

            Quaternion idleLocalRotation =
                GetSourceLocalRotation(
                    idle,
                    0,
                    j);

            Quaternion rotation;

            if (calibrateToIdle)
            {
                Quaternion deltaRotation =
                    Quaternion.Inverse(
                        idleLocalRotation) *
                    sourceLocalRotation;

                deltaRotation =
                    NormalizeQuaternion(
                        deltaRotation);

                rotation =
                    bindRotations[j] *
                    deltaRotation;
            }
            else
            {
                rotation =
                    sourceLocalRotation;
            }

            rotation =
                NormalizeQuaternion(
                    rotation);

            float poseWeight =
                desired == 1
                    ? walkPoseWeight
                    : desired == 2
                        ? runPoseWeight
                        : 1f;

            poseWeight =
                Mathf.Clamp01(
                    poseWeight);

            rotation =
                Quaternion.Slerp(
                    bindRotations[j],
                    rotation,
                    poseWeight);

            rotation =
                NormalizeQuaternion(
                    rotation);

            Vector3 position =
                bindPositions[j];

            Vector3 scale =
                bindScales[j];

            // Keep ALL child bone translations locked while fixing rotations.
            if (!lockBonePositions)
            {
                Vector3 sourcePosition =
                    Vector3.Lerp(
                        clip.positions[from, j],
                        clip.positions[to, j],
                        fraction);

                Vector3 idlePosition =
                    idle.positions[0, j];

                position =
                    bindPositions[j] +
                    sourcePosition -
                    idlePosition;
            }

            if (!lockBoneScales)
            {
                Vector3 sourceScale =
                    Vector3.Lerp(
                        clip.scales[from, j],
                        clip.scales[to, j],
                        fraction);

                Vector3 idleScale =
                    idle.scales[0, j];

                scale =
                    new Vector3(
                        bindScales[j].x *
                        SafeScaleDivide(sourceScale.x, idleScale.x),

                        bindScales[j].y *
                        SafeScaleDivide(sourceScale.y, idleScale.y),

                        bindScales[j].z *
                        SafeScaleDivide(sourceScale.z, idleScale.z));
            }

            // Root motion stays OFF while we diagnose the rig.
            if (
                applyRootMotion &&
                j == rootMotionJoint)
            {
                Vector3 sourcePosition =
                    Vector3.Lerp(
                        clip.positions[from, j],
                        clip.positions[to, j],
                        fraction);

                Vector3 idlePosition =
                    idle.positions[0, j];

                position =
                    bindPositions[j] +
                    sourcePosition -
                    idlePosition;

                if (
                    desired == 2 &&
                    autoGroundRun &&
                    clip.frameCount ==
                    RunFootCorrection.Length)
                {
                    position.y +=
                        Mathf.Lerp(
                            RunFootCorrection[from],
                            RunFootCorrection[to],
                            fraction) *
                        poseWeight /
                        0.4f;

                    position.y +=
                        runGroundOffset;
                }
            }

            // Single-joint diagnostic.
            if (debugAnimatedJoint >= 0)
            {
                position =
                    bindPositions[j];

                scale =
                    bindScales[j];

                if (j != debugAnimatedJoint)
                    rotation = bindRotations[j];
            }

            joints[j].localPosition =
                Vector3.Lerp(
                    blendPositions[j],
                    position,
                    blend);

            joints[j].localRotation =
                Quaternion.Slerp(
                    NormalizeQuaternion(
                        blendRotations[j]),
                    rotation,
                    blend);

            joints[j].localRotation =
                NormalizeQuaternion(
                    joints[j].localRotation);

            joints[j].localScale =
                Vector3.Lerp(
                    blendScales[j],
                    scale,
                    blend);
        }

        playingClip =
            ClipName(desired) +
            (sourceRotationsAreModelSpace
                ? " - MODEL->LOCAL"
                : " - LOCAL") +
            (debugAnimatedJoint >= 0
                ? " - JOINT " + debugAnimatedJoint
                : "");
    }

    private Quaternion GetSourceLocalRotation(
        NativeClip clip,
        int frame,
        int joint)
    {
        Quaternion childRotation =
            NormalizeQuaternion(
                clip.rotations[
                    frame,
                    joint]);

        if (!sourceRotationsAreModelSpace)
            return childRotation;

        int parentIndex =
            parentJointIndices[joint];

        if (parentIndex < 0)
            return childRotation;

        Quaternion parentRotation =
            NormalizeQuaternion(
                clip.rotations[
                    frame,
                    parentIndex]);

        Quaternion localRotation =
            Quaternion.Inverse(
                parentRotation) *
            childRotation;

        return
            NormalizeQuaternion(
                localRotation);
    }

    private int FindParentJointIndex(
        Transform joint)
    {
        Transform parent =
            joint.parent;

        while (parent != null)
        {
            if (
                TryGetJointIndex(
                    parent,
                    out int index))
            {
                return index;
            }

            parent =
                parent.parent;
        }

        return -1;
    }

    private void ApplyBindPose()
    {
        for (
            int j = 0;
            j < joints.Length;
            j++)
        {
            joints[j].localPosition =
                bindPositions[j];

            joints[j].localRotation =
                bindRotations[j];

            joints[j].localScale =
                bindScales[j];
        }
    }

    private Transform[] FindOrderedJoints()
    {
        Transform[] ordered =
            new Transform[51];

        foreach (
            Transform child in
            GetComponentsInChildren<Transform>(true))
        {
            if (!TryGetJointIndex(
                    child,
                    out int index))
            {
                continue;
            }

            if (
                index < 0 ||
                index >= ordered.Length)
            {
                continue;
            }

            ordered[index] =
                child;
        }

        return ordered;
    }

    private static bool TryGetJointIndex(
        Transform transform,
        out int index)
    {
        index = -1;

        if (transform == null)
            return false;

        const string prefix =
            "Crash joint ";

        if (
            !transform.name.StartsWith(
                prefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        string value =
            transform.name.Substring(
                prefix.Length);

        return
            int.TryParse(
                value,
                out index);
    }

    private static Quaternion NormalizeQuaternion(
        Quaternion q)
    {
        float magnitude =
            Mathf.Sqrt(
                q.x * q.x +
                q.y * q.y +
                q.z * q.z +
                q.w * q.w);

        if (
            float.IsNaN(magnitude) ||
            float.IsInfinity(magnitude) ||
            magnitude < 0.000001f)
        {
            return Quaternion.identity;
        }

        float inverse =
            1f /
            magnitude;

        return new Quaternion(
            q.x * inverse,
            q.y * inverse,
            q.z * inverse,
            q.w * inverse);
    }

    private static float SafeScaleDivide(
        float value,
        float reference)
    {
        if (
            Mathf.Abs(reference) <
            0.001f)
        {
            return 1f;
        }

        return
            value /
            reference;
    }

    private int SelectClip()
    {
        if (spinning)
            return 3;

        if (doubleJumpProgress >= 0f)
            return 13;

        if (sliding)
            return 54;

        if (!grounded)
        {
            return
                verticalSpeed > 3f
                    ? 4
                    : verticalSpeed > -1f
                        ? 5
                        : 7;
        }

        if (landingTime > 0f)
            return 6;

        if (crouching)
            return 8;

        if (movement > 0.8f)
            return 2;

        if (movement > 0.05f)
            return 1;

        return 0;
    }

    private static string ClipName(
        int id)
    {
        switch (id)
        {
            case 0:
                return "Crash_Idle (0)";

            case 1:
                return "Crash_Walk (1)";

            case 2:
                return "Crash_Run (2)";

            case 3:
                return "CrashSpin_Spin (3)";

            case 4:
                return "Crash_JumpStart (4)";

            case 5:
                return "Crash_JumpApex (5)";

            case 6:
                return "Crash_JumpLand (6)";

            case 7:
                return "Crash_JumpFall (7)";

            case 8:
                return "Crash_CrouchIdle (8)";

            case 13:
                return "Crash_DoubleJumpFlip (13)";

            case 54:
                return "Crash_SlideStart (54)";

            default:
                return
                    "Native clip " +
                    id;
        }
    }
}