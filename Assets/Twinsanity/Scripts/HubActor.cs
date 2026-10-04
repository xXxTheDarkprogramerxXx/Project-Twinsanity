using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// Native RM2 actor playback. This does not use or modify CrashRigAnimator.
public sealed class HubActor : MonoBehaviour
{
    public HubActorDefinition definition;
    public Transform[] bones;
    public Transform visual;
    public bool enableBehaviour = true;
    public bool showBindPose;
    [Tooltip("-1 follows behaviour. Otherwise enter an original animation ID from clipIds.")]
    public int previewClip = -1;
    [Tooltip("-1 plays normally; a nonnegative value freezes the preview on that frame.")]
    public int previewFrame = -1;
    public float playbackSpeed = 1f;
    public float brightness = 2f;
    public float wanderRadius = 2f;
    public float moveSpeed = 1.1f;
    public float contactRadius = 0.65f;
    public int fruitRemaining = 5;

    private sealed class Clip { public int id, frames; public float fps; public Vector3[] positions, scales; public Quaternion[] rotations; }
    private sealed class Part { public uint texture; public Mesh mesh; }
    private sealed class Asset
    {
        public int[] parents;
        public Vector3[] bindPositions;
        public Quaternion[] bindRotations;
        public Part[] parts;
        public Dictionary<int, Clip> clips = new Dictionary<int, Clip>();
    }
    private static readonly Dictionary<string, Asset> assets = new Dictionary<string, Asset>();
    private Asset asset;
    private int activeClip = -1;
    private float clock, actionRemaining, nextDecision, nextHit;
    private bool defeated;
    private Vector3 home, destination;
    private AudioSource audioSource;
    private float appliedBrightness = float.NaN;
    private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    private static Quaternion ReadRotation(BinaryReader reader) => new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

    private void Load()
    {
        if (asset != null) return;
        if (definition == null || string.IsNullOrEmpty(definition.resource)) return;
        if (assets.TryGetValue(definition.resource, out asset)) return;
        TextAsset source = Resources.Load<TextAsset>("HubActors/" + definition.resource);
        if (source == null) throw new FileNotFoundException("Missing native actor " + definition.resource);
        Asset loaded = new Asset();
        using (BinaryReader reader = new BinaryReader(new MemoryStream(source.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLAC" || reader.ReadInt32() != 1) throw new InvalidDataException("Invalid native actor.");
            int joints = reader.ReadInt32(), parts = reader.ReadInt32();
            loaded.parents = new int[joints]; loaded.bindPositions = new Vector3[joints]; loaded.bindRotations = new Quaternion[joints];
            for (int b = 0; b < joints; b++)
            {
                loaded.parents[b] = reader.ReadInt32();
                loaded.bindPositions[b] = ReadVector(reader); loaded.bindRotations[b] = ReadRotation(reader);
            }
            loaded.parts = new Part[parts];
            for (int p = 0; p < parts; p++)
            {
                uint texture = reader.ReadUInt32(); int count = reader.ReadInt32(), triangleCount = reader.ReadInt32();
                Vector3[] vertices = new Vector3[count]; Vector2[] uv = new Vector2[count];
                Color32[] colors = new Color32[count]; BoneWeight[] weights = new BoneWeight[count];
                for (int v = 0; v < count; v++)
                {
                    vertices[v] = ReadVector(reader);
                    float u = reader.ReadSingle(), vCoord = reader.ReadSingle();
                    uv[v] = new Vector2(u, vCoord);
                    colors[v] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                    weights[v] = new BoneWeight { boneIndex0 = reader.ReadInt32(), boneIndex1 = reader.ReadInt32(), boneIndex2 = reader.ReadInt32(),
                        weight0 = reader.ReadSingle(), weight1 = reader.ReadSingle(), weight2 = reader.ReadSingle() };
                }
                int[] triangles = new int[triangleCount * 3];
                for (int t = 0; t < triangles.Length; t++) triangles[t] = reader.ReadInt32();
                Mesh mesh = new Mesh { name = definition.resource + " part " + p, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = vertices; mesh.uv = uv; mesh.colors32 = colors; mesh.triangles = triangles; mesh.boneWeights = weights;
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                loaded.parts[p] = new Part { texture = texture, mesh = mesh };
            }
            int clipCount = reader.ReadInt32();
            for (int c = 0; c < clipCount; c++)
            {
                Clip clip = new Clip { id = reader.ReadInt32(), frames = reader.ReadInt32(), fps = reader.ReadSingle() };
                int count = clip.frames * joints;
                clip.positions = new Vector3[count]; clip.rotations = new Quaternion[count]; clip.scales = new Vector3[count];
                for (int i = 0; i < count; i++) { clip.positions[i] = ReadVector(reader); clip.rotations[i] = ReadRotation(reader); clip.scales[i] = ReadVector(reader); }
                loaded.clips.Add(clip.id, clip);
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Actor contains unread data.");
        }
        asset = loaded; assets.Add(definition.resource, loaded);
    }

    public void BuildVisual()
    {
        Load();
        if (asset == null || visual != null) return;
        visual = new GameObject("Native Hub Visual").transform; visual.SetParent(transform, false);
        bones = new Transform[asset.parents.Length];
        for (int b = 0; b < bones.Length; b++) bones[b] = new GameObject("Hub joint " + b).transform;
        for (int b = 0; b < bones.Length; b++)
        {
            bones[b].SetParent(asset.parents[b] < 0 ? visual : bones[asset.parents[b]], false);
            bones[b].localPosition = asset.bindPositions[b]; bones[b].localRotation = asset.bindRotations[b];
        }
        Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
        for (int b = 0; b < bones.Length; b++) bindposes[b] = bones[b].worldToLocalMatrix * visual.localToWorldMatrix;
        Shader shader = Resources.Load<Shader>("Logo/GameOpaqueVertexColor");
        if (shader == null) throw new FileNotFoundException("Missing GameOpaqueVertexColor shader.");
        Dictionary<uint, Material> materials = new Dictionary<uint, Material>();
        for (int p = 0; p < asset.parts.Length; p++)
        {
            Part part = asset.parts[p]; Mesh mesh = Instantiate(part.mesh); mesh.bindposes = bindposes;
            if (!materials.TryGetValue(part.texture, out Material material))
            {
                material = new Material(shader) { name = "Hub " + part.texture.ToString("X8") };
                material.mainTexture = Resources.Load<Texture2D>("HubActors/Textures/" + part.texture.ToString("X8"));
                if (material.mainTexture == null) throw new FileNotFoundException("Missing hub texture " + part.texture.ToString("X8"));
                material.SetFloat("_Brightness", brightness); materials.Add(part.texture, material);
            }
            GameObject partObject = new GameObject("Native part " + p); partObject.transform.SetParent(visual, false);
            SkinnedMeshRenderer renderer = partObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterial = material; renderer.bones = bones;
            int root = Array.FindIndex(asset.parents, parent => parent < 0);
            renderer.rootBone = bones[Mathf.Max(0, root)]; renderer.updateWhenOffscreen = true;
        }
        // Native model space and animation translation are retained; there is no Crash centering offset.
        Sample(definition.idleClip, 0f, false);
    }

    public void ConfigureCollision()
    {
        if (definition == null) return;
        string kind = definition.kind;
        bool interactive = kind == "Chicken" || kind == "Crab" || kind == "Worm" || kind == "WumpaTree" || kind == "Enemy";
        if (!interactive || GetComponent<Collider>() != null) return;
        SphereCollider trigger = gameObject.AddComponent<SphereCollider>(); trigger.isTrigger = true;
        trigger.center = Vector3.up * (kind == "WumpaTree" ? 1f : 0.45f);
        trigger.radius = kind == "WumpaTree" ? 1.2f : contactRadius;
        Rigidbody body = gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
    }
    private void Start()
    {
        Load(); BuildVisual(); home = transform.position; destination = home;
        nextDecision = Time.time + (Mathf.Abs(home.x * .13f + home.z * .17f) % 2f);
        Play(definition.idleClip);
    }
    public void Play(int clipId, bool restart = false)
    {
        Load(); if (asset == null || !asset.clips.ContainsKey(clipId)) return;
        if (activeClip == clipId && !restart) return;
        activeClip = clipId; clock = 0f;
    }
    private void OneShot(int id)
    {
        if (asset == null || !asset.clips.TryGetValue(id, out Clip clip)) return;
        Play(id, true); actionRemaining = Mathf.Max(.1f, (clip.frames - 1) / clip.fps);
    }
    private void Update()
    {
        if (asset == null || (BeachLevelRuntime.Current != null && BeachLevelRuntime.Current.IsPaused)) return;
        if (visual != null && appliedBrightness != brightness)
        {
            appliedBrightness = brightness;
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(properties); properties.SetFloat("_Brightness", brightness); renderer.SetPropertyBlock(properties);
            }
        }
        float delta = Time.deltaTime * Mathf.Max(0f, playbackSpeed);
        if (showBindPose)
        {
            for (int b = 0; b < bones.Length; b++) { bones[b].localPosition = asset.bindPositions[b]; bones[b].localRotation = asset.bindRotations[b]; bones[b].localScale = Vector3.one; }
            return;
        }
        if (previewClip >= 0)
        {
            Play(previewClip); clock += delta;
            Sample(activeClip, previewFrame >= 0 && asset.clips.TryGetValue(activeClip, out Clip clip) ? previewFrame / clip.fps : clock, true);
            return;
        }
        if (enableBehaviour && !defeated && actionRemaining <= 0f) Behave();
        clock += delta;
        Sample(activeClip, clock, actionRemaining <= 0f && !defeated);
        if (actionRemaining > 0f)
        {
            actionRemaining -= delta;
            if (actionRemaining <= 0f) { if (defeated) gameObject.SetActive(false); else Play(definition.idleClip); }
        }
    }
    public void PreviewNativeFrame(int id, int frame)
    {
        Load();
        if (asset != null && asset.clips.TryGetValue(id, out Clip clip)) Sample(id, Mathf.Clamp(frame, 0, clip.frames - 1) / clip.fps, false);
    }
    public int NativeFrameCount(int id)
    {
        Load(); return asset != null && asset.clips.TryGetValue(id, out Clip clip) ? clip.frames : 0;
    }

    private void Sample(int id, float time, bool loop)
    {
        if (bones == null || !asset.clips.TryGetValue(id, out Clip clip) || clip.frames < 1) return;
        float frame = time * clip.fps;
        frame = loop && clip.frames > 1 ? Mathf.Repeat(frame, clip.frames - 1) : Mathf.Clamp(frame, 0, clip.frames - 1);
        int f0 = Mathf.FloorToInt(frame), f1 = Mathf.Min(f0 + 1, clip.frames - 1); float t = frame - f0;
        for (int b = 0; b < bones.Length; b++)
        {
            int i = f0 * bones.Length + b, j = f1 * bones.Length + b;
            bones[b].localPosition = Vector3.Lerp(clip.positions[i], clip.positions[j], t);
            bones[b].localRotation = Quaternion.Slerp(clip.rotations[i], clip.rotations[j], t);
            bones[b].localScale = Vector3.Lerp(clip.scales[i], clip.scales[j], t);
        }
    }
    private void Behave()
    {
        Transform player = BeachLevelRuntime.Current == null ? null : BeachLevelRuntime.Current.PlayerTransform;
        Vector3 away = player == null ? Vector3.zero : transform.position - player.position; away.y = 0f;
        bool nearby = player != null && away.sqrMagnitude < 16f && Mathf.Abs(player.position.y - transform.position.y) < 3f;
        string kind = definition.kind;
        if (kind == "Worm")
        {
            if (nearby && Time.time > nextDecision) { OneShot(definition.actionClip); PlaySound(776); nextDecision = Time.time + 2.5f; }
            return;
        }
        if (kind != "Chicken" && kind != "Crab") return;
        if (kind == "Chicken" && nearby)
        {
            if (away.sqrMagnitude < .01f) away = transform.forward;
            destination = transform.position + away.normalized * 2f;
            MoveOnGround(destination, moveSpeed * 2f); Play(definition.runClip); return;
        }
        if (Time.time >= nextDecision)
        {
            nextDecision = Time.time + UnityEngine.Random.Range(2f, 4f);
            Vector2 point = UnityEngine.Random.insideUnitCircle * wanderRadius;
            destination = home + new Vector3(point.x, 0, point.y);
            if (kind == "Chicken" && UnityEngine.Random.value < .4f) { OneShot(definition.actionClip); PlaySound(82); return; }
        }
        Vector3 delta = destination - transform.position; delta.y = 0f;
        if (delta.sqrMagnitude > .12f && MoveOnGround(destination, moveSpeed)) Play(definition.walkClip);
        else Play(definition.idleClip);
    }
    private bool MoveOnGround(Vector3 target, float speed)
    {
        Vector3 delta = target - transform.position; delta.y = 0f;
        if (delta.sqrMagnitude < .001f) return false;
        Vector3 candidate = transform.position + delta.normalized * speed * Time.deltaTime;
        if (!Physics.Raycast(candidate + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (Mathf.Abs(hit.point.y - transform.position.y) > .8f) return false;
        transform.position = new Vector3(candidate.x, hit.point.y, candidate.z);
        // Account for the user's reflected chunk basis when setting the visual's local facing.
        Vector3 localDirection = transform.parent == null ? delta : transform.parent.InverseTransformVector(delta);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(localDirection.normalized, Vector3.up), Time.deltaTime * 8f);
        return true;
    }
    public void Hit()
    {
        if (definition == null || defeated || Time.time < nextHit) return;
        nextHit = Time.time + .7f;
        string kind = definition.kind;
        if (kind == "WumpaTree")
        {
            OneShot(definition.actionClip);
            if (fruitRemaining > 0) { fruitRemaining--; SpawnFruit(transform.position + Vector3.up * 1.2f); }
            return;
        }
        if (kind != "Chicken" && kind != "Crab" && kind != "Worm" && kind != "Enemy") return;
        defeated = true;
        foreach (Collider collider in GetComponents<Collider>()) collider.enabled = false;
        OneShot(definition.hitClip); PlaySound(kind == "Chicken" ? 88 : 40);
        if (actionRemaining <= 0f) actionRemaining = .3f;
    }
    private void OnTriggerStay(Collider other)
    {
        if (defeated || definition == null) return;
        BeachPlayerController player = other.GetComponentInParent<BeachPlayerController>();
        if (player == null) return;
        if (player.attacking) { Hit(); return; }
        if (definition.kind == "Crab" || definition.kind == "Worm" || definition.kind == "Enemy") player.TakeDamage();
    }
    private void PlaySound(int id)
    {
        AudioClip clip = Resources.Load<AudioClip>("HubActors/Audio/" + definition.chunk + "_" + id);
        if (clip == null) return;
        if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 1f; audioSource.maxDistance = 25f; audioSource.playOnAwake = false; }
        audioSource.PlayOneShot(clip);
    }
    private static void SpawnFruit(Vector3 position)
    {
        Vector2 scatter = UnityEngine.Random.insideUnitCircle * 1.4f;
        position += new Vector3(scatter.x, 0f, scatter.y);
        if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out RaycastHit ground, 10f, ~0, QueryTriggerInteraction.Ignore)) position = ground.point + Vector3.up * .65f;
        GameObject fruit = new GameObject("Wumpa from tree"); fruit.transform.position = position;
        GameObject model = BeachLevelRuntime.BuildItemModel("Wumpa", fruit.transform); model.transform.localScale = Vector3.one * .7f;
        SphereCollider trigger = fruit.AddComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = .7f;
        fruit.AddComponent<BeachItem>().kind = "Wumpa";
    }
}
