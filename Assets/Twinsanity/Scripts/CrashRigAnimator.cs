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
    public int jumpStartClip = 4;
    public int doubleJumpClip = 5;
    public int jumpFallClip = 7;
    public int landingClip = 6;
    public bool showBindPose;
    public string playingClip;

    [Header("Idle Variations")]
    public bool playIdleVariations = true;
    public float minimumIdleDelay = 4f;
    public float maximumIdleDelay = 8f;
    public int[] idleVariationClips = { 317, 326, 348, 349, 1247, 1501, 1561, 1562, 1563, 1625 };
    [SerializeField, HideInInspector] private int idlePlaylistVersion;

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
    private float jumpElapsed = -1f;
    private Transform spinVisual;
    private NativeClip spinClip;
    private Renderer[] normalRenderers;
    private bool[] normalVisibility;
    private bool spinVisible;
    private float idleElapsed, idleDelay;
    private int idleVariation = -1, previousIdleVariation = -1;
    private readonly List<Material> ownedMaterials = new List<Material>();

    private void Awake()
    {
        // Existing prefabs may still serialize the old four-entry default playlist.
        // Upgrade that exact default without changing a customized playlist.
        if (idlePlaylistVersion == 0)
        {
            if (idleVariationClips != null && idleVariationClips.Length == 4 && idleVariationClips[0] == 317 && idleVariationClips[1] == 326 && idleVariationClips[2] == 348 && idleVariationClips[3] == 349)
                idleVariationClips = new int[] { 317, 326, 348, 349, 1247, 1501, 1561, 1562, 1563, 1625 };
            idlePlaylistVersion = 1;
        }
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
        normalRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        normalVisibility = new bool[normalRenderers.Length];
        for (int i = 0; i < normalRenderers.Length; i++) normalVisibility[i] = normalRenderers[i].enabled;
        LoadSpinVisual();
        initialized = true;
    }

    private void LoadAnimations()
    {
        LoadAnimationFile("Characters/CrashAnimations", true);
        LoadAnimationFile("Characters/CrashExtraAnimations", false);
        if (!clips.ContainsKey(0)) throw new InvalidDataException("Missing Crash idle clip 0.");
    }

    private void LoadAnimationFile(string resourceName, bool required)
    {
        TextAsset source = Resources.Load<TextAsset>(resourceName);
        if (source == null)
        {
            if (required) throw new FileNotFoundException("Missing " + resourceName + ".bytes.");
            Debug.LogWarning("Idle variation data missing: " + resourceName + ".bytes.", this);
            return;
        }
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
                if (!clips.ContainsKey(clip.id)) clips.Add(clip.id, clip);
            }
        }
    }

    public void SetMovement(float amount, bool onGround, bool spin, float progress, bool crouch, bool slide, float vertical, float doubleJump)
    {
        // Player input takes ownership; manual preview works when the controller is disabled.
        showBindPose = false;
        previewClip = -1;
        previewFrame = -1;
        if (!grounded && onGround) landingTime = GetClipDuration(landingClip, 0.2f);
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
        if (failed || (BeachLevelRuntime.Current != null && BeachLevelRuntime.Current.IsPaused)) return;
        if (!initialized)
        {
            try { PrepareRig(); }
            catch (Exception exception) { failed = true; Debug.LogException(exception, this); return; }
        }
        if (showBindPose)
        {
            SetSpinVisible(false);
            ApplyBindPose();
            activeClip = -1;
            playingClip = "Bind pose";
            return;
        }

        landingTime = Mathf.Max(0f, landingTime - Time.deltaTime);
        if (jumpElapsed >= 0f) jumpElapsed += Time.deltaTime;
        if (previewClip < 0) UpdateIdleVariations(Time.deltaTime);
        else ResetIdleVariations();
        int desired = previewClip >= 0 ? previewClip : SelectClip();
        if (desired == 3 && spinClip != null)
        {
            SetSpinVisible(true);
            float spinFrame = previewClip >= 0 ? Mathf.Repeat(clipTime * spinClip.fps, spinClip.frameCount - 1) : Mathf.Clamp01(spinProgress) * (spinClip.frameCount - 1);
            if (previewClip >= 0 && previewFrame >= 0) spinFrame = Mathf.Clamp(previewFrame, 0, spinClip.frameCount - 1);
            int a = Mathf.FloorToInt(spinFrame), b = Mathf.Min(a + 1, spinClip.frameCount - 1);
            float t = spinFrame - a;
            spinVisual.localPosition = Vector3.Lerp(spinClip.positions[a, 0], spinClip.positions[b, 0], t) + skeletonOriginOffset;
            spinVisual.localRotation = Quaternion.Slerp(spinClip.rotations[a, 0], spinClip.rotations[b, 0], t);
            spinVisual.localScale = Vector3.Lerp(spinClip.scales[a, 0], spinClip.scales[b, 0], t);
            clipTime += Time.deltaTime;
            activeClip = -1; // Capture the body pose again when the normal graphic returns.
            playingClip = "Original spin graphic / frame " + spinFrame.ToString("F2");
            return;
        }
        if (desired == 3) desired = 0; // Never apply a one-joint spin clip to the body rig.
        SetSpinVisible(false);
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
        if (previewClip < 0 && desired == doubleJumpClip && doubleJumpProgress >= 0f) frame = Mathf.Clamp01(doubleJumpProgress) * last;
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
        playingClip = AnimationName(desired) + " / frame " + frame.ToString("F2");
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
        if (doubleJumpProgress >= 0f) return doubleJumpClip;
        if (sliding) return 54;
        if (!grounded) return jumpElapsed >= 0f && jumpElapsed < GetClipDuration(jumpStartClip, 0.44f) ? jumpStartClip : jumpFallClip;
        if (landingTime > 0f && (movement <= 0.05f || landingTime > GetClipDuration(landingClip, 0.2f) - 0.12f)) return landingClip;
        if (crouching) return 8;
        return movement > 0.8f ? 2 : movement > 0.05f ? 1 : idleVariation >= 0 ? idleVariation : 0;
    }

    public float GetClipDuration(int id, float fallback = 0.48f)
    {
        return clips.TryGetValue(id, out NativeClip clip) ? Mathf.Max(1, clip.frameCount - 1) / clip.fps : fallback;
    }

    public void NotifyJump(bool secondJump)
    {
        ResetIdleVariations();
        jumpElapsed = secondJump ? -1f : 0f;
        landingTime = 0f;
        grounded = false;
        activeClip = -1;
    }

    public void ResetActionState()
    {
        ResetIdleVariations();
        jumpElapsed = -1f;
        doubleJumpProgress = -1f;
        landingTime = 0f;
        spinning = false;
        grounded = true;
        activeClip = -1;
        if (initialized) SetSpinVisible(false);
    }

    private void SetSpinVisible(bool visible)
    {
        if (spinVisible == visible) return;
        spinVisible = visible;
        for (int i = 0; i < normalRenderers.Length; i++) normalRenderers[i].enabled = !visible && normalVisibility[i];
        if (spinVisual != null) spinVisual.gameObject.SetActive(visible);
        clipTime = 0f;
    }

    private void LoadSpinVisual()
    {
        TextAsset data = Resources.Load<TextAsset>("Characters/CrashSpin");
        if (data == null) { Debug.LogWarning("Original spin graphic missing: install Characters/CrashSpin.bytes and SpinTextures.", this); return; }
        using (BinaryReader reader = new BinaryReader(new MemoryStream(data.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLSP") throw new InvalidDataException("Invalid CrashSpin.bytes.");
            int frames = reader.ReadInt32();
            float fps = reader.ReadSingle();
            int parts = reader.ReadInt32();
            spinClip = new NativeClip { id = 3, frameCount = frames, fps = fps, positions = new Vector3[frames, 1], rotations = new Quaternion[frames, 1], scales = new Vector3[frames, 1] };
            for (int f = 0; f < frames; f++)
            {
                spinClip.positions[f, 0] = ReadVector(reader);
                spinClip.rotations[f, 0] = ReadRotation(reader);
                spinClip.scales[f, 0] = ReadVector(reader);
            }
            spinVisual = new GameObject("Original Crash spin graphic").transform;
            spinVisual.SetParent(transform, false);
            for (int part = 0; part < parts; part++)
            {
                uint texture = reader.ReadUInt32();
                bool transparent = reader.ReadBoolean();
                int count = reader.ReadInt32(), triangleCount = reader.ReadInt32();
                Vector3[] vertices = new Vector3[count];
                Vector2[] uv = new Vector2[count];
                Color32[] colors = new Color32[count];
                for (int v = 0; v < count; v++)
                {
                    vertices[v] = ReadVector(reader);
                    uv[v] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    colors[v] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                }
                int[] triangles = new int[triangleCount * 3];
                for (int t = 0; t < triangles.Length; t++) triangles[t] = reader.ReadInt32();
                Mesh mesh = new Mesh { name = "Original spin part " + part, vertices = vertices, uv = uv, colors32 = colors, triangles = triangles };
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                ownedMeshes.Add(mesh);
                Shader shader = Resources.Load<Shader>(transparent ? "Characters/SpinTextures/CrashSpinTrail" : "Logo/GameOpaqueVertexColor");
                if (shader == null) throw new FileNotFoundException("Missing original spin shader.");
                Material material = new Material(shader) { name = "Crash spin " + texture.ToString("X8") };
                material.mainTexture = Resources.Load<Texture2D>("Characters/SpinTextures/" + texture.ToString("X8"));
                if (material.mainTexture == null) throw new FileNotFoundException("Missing spin texture " + texture.ToString("X8"));
                material.SetFloat("_Brightness", 3f);
                ownedMaterials.Add(material);
                GameObject item = new GameObject("Original spin part " + part);
                item.transform.SetParent(spinVisual, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                item.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            spinVisual.gameObject.SetActive(false);
        }
    }

    private void ResetIdleVariations()
    {
        idleElapsed = 0f;
        idleDelay = 0f;
        idleVariation = -1;
    }

    private void UpdateIdleVariations(float deltaTime)
    {
        bool eligible = playIdleVariations && grounded && movement <= 0.05f && !spinning && !crouching && !sliding && doubleJumpProgress < 0f && landingTime <= 0f;
        if (!eligible) { ResetIdleVariations(); return; }
        if (idleVariation >= 0)
        {
            if (activeClip == idleVariation && clipTime >= GetClipDuration(idleVariation)) ResetIdleVariations();
            return;
        }
        if (idleDelay <= 0f)
        {
            float minimum = Mathf.Max(0.1f, minimumIdleDelay);
            idleDelay = UnityEngine.Random.Range(minimum, Mathf.Max(minimum, maximumIdleDelay));
        }
        idleElapsed += deltaTime;
        if (idleElapsed < idleDelay || idleVariationClips == null) return;
        List<int> candidates = new List<int>();
        foreach (int id in idleVariationClips)
            if (id != 0 && clips.ContainsKey(id) && !candidates.Contains(id)) candidates.Add(id);
        if (candidates.Count > 1) candidates.Remove(previousIdleVariation);
        if (candidates.Count == 0) { ResetIdleVariations(); return; }
        idleVariation = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        previousIdleVariation = idleVariation;
    }

    private static string AnimationName(int id)
    {
        switch (id)
        {
            case 0: return "Crash_Idle";
            case 1: return "Crash_Walk";
            case 2: return "Crash_Run";
            case 4: return "Crash_JumpStart";
            case 5: return "Crash_JumpApex";
            case 6: return "Crash_JumpLand";
            case 7: return "Crash_JumpFall";
            case 8: return "Crash_CrouchIdle";
            case 14: return "Crash_DeathGeneric";
            case 25: return "Crash_Crawl";
            case 27: return "Crash_BodyslamHang";
            case 54: return "Crash_CrouchToCrawl";
            case 55: return "Crash_SlideToStand";
            case 60: return "Crash_LandToRun";
            case 203: return "Crash_JumpToBodyslam";
            case 206: return "Crash_CrawlToIdle";
            case 209: return "Crash_SpinRecover";
            case 282: return "Crash_SlideJump";
            case 283: return "Crash_FallDamage";
            case 317: return "Crash_IdleYawn";
            case 326: return "Crash_IdleScratch";
            case 348: return "Crash_IdleLookBehindLeft";
            case 349: return "Crash_IdleLookBehindRight";
            case 363: return "Crash_OnEdge";
            case 364: return "Crash_OnEdgeIdle";
            case 499: return "Crash_ShuffleFeet";
            case 502: return "Crash_SkatePose";
            case 516: return "Crash_SkateBoostPose";
            case 622: return "Crash_Brawl1";
            case 625: return "Crash_BrawlGetSpanked";
            case 633: return "Crash_BrawlStrangle";
            case 635: return "Crash_Brawl2";
            case 637: return "Crash_BrawlStretch";
            case 653: return "Crash_BallPose";
            case 654: return "Crash_BallUncurl";
            case 693: return "Crash_BrawlHeadbutt";
            case 773: return "Crash_SkateIdleFront";
            case 774: return "Crash_SkateLeanLeftFront";
            case 775: return "Crash_SkateLeanRightFront";
            case 776: return "Crash_SkateIdleBack";
            case 777: return "Crash_SkateLeanLeftBack";
            case 778: return "Crash_SkateLeanRightBack";
            case 784: return "Crash_SkateKickflip";
            case 785: return "Crash_SkateBoostBack";
            case 788: return "Crash_SkateJump";
            case 789: return "Crash_RunRoll";
            case 791: return "Crash_WalkRoll";
            case 792: return "Crash_SkateBoostFront";
            case 793: return "Crash_SkateBoostRight";
            case 794: return "Crash_SkateBoostLeft";
            case 838: return "Crash_SkateGrindFront";
            case 999: return "Crash_GroundPullOut";
            case 1016: return "Crash_SkateGrindBack";
            case 1059: return "Crash_Punch";
            case 1187: return "Crash_DeathDrown";
            case 1206: return "Crash_DeathFalloff";
            case 1207: return "Crash_Damaged";
            case 1227: return "Crash_SpinThrow";
            case 1228: return "Crash_TwinThrow";
            case 1247: return "Crash_IdleLookAround";
            case 1299: return "Crash_UnkFlap";
            case 1501: return "Crash_IdleScratchHead";
            case 1561: return "Crash_IdleEarPick";
            case 1562: return "Crash_IdleButtScratch";
            case 1563: return "Crash_IdleFingerGuns";
            case 1565: return "Crash_SkateFall";
            case 1566: return "Crash_SkateFallPose";
            case 1568: return "Crash_SkateWobble";
            case 1625: return "Crash_IdleBoxing";
            default: return "Native clip " + id;
        }
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
    }
}
