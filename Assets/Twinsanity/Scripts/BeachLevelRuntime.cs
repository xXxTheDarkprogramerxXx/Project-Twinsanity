using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class BeachLevelRuntime : MonoBehaviour
{
    public static BeachLevelRuntime Current { get; private set; }

    public int wumpa;
    public int lives = 3;
    public int gems;
    public int crystals;
    public int masks;
    public Font crashFont;
    public bool useEditableScene;

    public bool IsPaused { get; private set; }
    public Transform PlayerTransform { get { return player == null ? null : player.transform; } }

    private BeachPlayerController player;
    private GUIStyle hud;
    private GUIStyle pauseStyle;
    private GUIStyle checkpointStyle;
    private float checkpointMessageUntil;
    private readonly Dictionary<string, GameObject> chunkRoots = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    private AudioClip crateSound;
    private AudioClip pickupSound;
    private AudioClip explosionSound;
    private AudioClip spinSound;
    private AudioClip akuPickupSound;
    private AudioClip checkpointSound;

    private AkuAkuFollower akuFollower;
    private int pauseSelected;

    private readonly string[] pauseOptions =
    {
        "RESUME",
        "RESTART CHECKPOINT",
        "TITLE SCREEN"
    };

    private void Awake()
    {
        Current = this;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (crashFont == null)
            crashFont = Resources.Load<Font>("Crash-Euro");

        Camera camera = Camera.main;

        if (camera == null)
        {
            camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.gameObject.AddComponent<AudioListener>();
        }

        camera.fieldOfView = 60;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 650f;
        camera.backgroundColor = new Color(0.63f, 0.51f, 0.78f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        RenderSettings.ambientLight = new Color(0.9f, 0.82f, 0.8f);

        if (!useEditableScene)
        {
            GameObject sun = new GameObject("Beach sunlight");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            sun.transform.rotation = Quaternion.Euler(38f, -45f, 0);
        }

        string[] chunks =
        {
            "Beach",
            "Bossarea",
            "Alwayson",
            "Huba",
            "Hubb",
            "Highpath",
            "Hubc",
            "Hubd",
            "Pier",
            "Totemex",
            "Docent",
            "Hubboat1",
            "Hubboat2"
        };

        Transform editableChunks = useEditableScene ? transform.Find("Editable Chunks") : null;
        if (useEditableScene && editableChunks == null)
            throw new InvalidDataException("Editable scene has no 'Editable Chunks' root. Bake it from the Twinsanity menu.");

        foreach (string chunk in chunks)
        {
            GameObject root;
            if (useEditableScene)
            {
                Transform baked = editableChunks.Find(chunk + " streamed chunk");
                if (baked == null)
                    throw new InvalidDataException("Editable scene is missing chunk " + chunk);
                root = baked.gameObject;
            }
            else
            {
                root = new GameObject(chunk + " streamed chunk");
                root.transform.SetParent(transform, false);
                GameObject scenery = TwinsanitySceneryRuntime.Create(root.transform, chunk);
                scenery.transform.localPosition = ChunkOffset(chunk);
                scenery.transform.localRotation = ChunkRotation(chunk);
            }

            chunkRoots.Add(chunk, root);
        }

        if (!useEditableScene)
        {
            BuildCollision();
            BuildSea();
            BuildItems();

            foreach (KeyValuePair<string, GameObject> chunk in chunkRoots)
                chunk.Value.SetActive(chunk.Key == "Beach");
        }

        crateSound = Resources.Load<AudioClip>("BeachAudio/CrateBreak");
        pickupSound = Resources.Load<AudioClip>("BeachAudio/Pickup");
        explosionSound = Resources.Load<AudioClip>("BeachAudio/Explosion");
        spinSound = Resources.Load<AudioClip>("BeachAudio/Crash_Spin");
        akuPickupSound = Resources.Load<AudioClip>("BeachAudio/AkuAkuPickup");
        checkpointSound = Resources.Load<AudioClip>("BeachAudio/Checkpoint");

        AudioClip beachMusic = Resources.Load<AudioClip>("TwinsanityTheme");

        if (beachMusic != null)
        {
            AudioSource soundtrack = gameObject.AddComponent<AudioSource>();
            soundtrack.clip = beachMusic;
            soundtrack.loop = true;
            soundtrack.spatialBlend = 0f;
            soundtrack.volume = 0.5f;
            soundtrack.Play();
        }

        Transform spawnMarker = useEditableScene ? transform.Find("Crash Spawn") : null;
        Transform savedCrash = spawnMarker == null ? null : spawnMarker.Find("Crash Player");
        GameObject avatar;
        if (savedCrash != null)
        {
            avatar = savedCrash.gameObject;
            player = avatar.GetComponent<BeachPlayerController>();
            if (player == null) throw new InvalidDataException("Editable Crash Player is missing BeachPlayerController.");
            if (player.model == null)
            {
                CrashRigAnimator savedRig = avatar.GetComponentInChildren<CrashRigAnimator>(true);
                if (savedRig == null) throw new InvalidDataException("Editable Crash Player is missing CrashRigAnimator.");
                player.model = savedRig.transform;
            }
        }
        else
        {
            avatar = new GameObject("Crash - playable character");
            avatar.transform.position = spawnMarker != null ? spawnMarker.position : new Vector3(-2.35f, 1.6f, -39.6f);
            CharacterController controller = avatar.AddComponent<CharacterController>();
            controller.center = new Vector3(0, 0.9f, 0);
            controller.height = 1.8f;
            controller.radius = 0.42f;
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 48f;
            player = avatar.AddComponent<BeachPlayerController>();
            GameObject model = TwinsanityCharacterRuntime.Create("Crash", avatar.transform);
            player.model = model.transform;
            model.transform.localPosition = new Vector3(0, 0.48f, 0);
        }
        player.cameraTransform = camera.transform;
        player.initialCameraYaw = 180f;

        Physics.SyncTransforms();

        if (Physics.Raycast(avatar.transform.position + Vector3.up * 8, Vector3.down, out RaycastHit ground, 24f, ~0, QueryTriggerInteraction.Ignore))
            avatar.transform.position = ground.point + Vector3.up * 0.12f;

        if (!useEditableScene)
            BuildOpeningAkuPickup(avatar.transform.position);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (Current == this)
            Current = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7))
        {
            SetPaused(!IsPaused);
            return;
        }

        if (IsPaused)
        {
            UpdatePauseMenu();
            return;
        }

        if (player == null)
            return;

        Vector3 p = player.transform.position;
        bool nearHubGate = p.x > 35f && p.z < -40f && p.z > -65f;
        if (!useEditableScene)
            chunkRoots["Huba"].SetActive(nearHubGate);
    }

    private void UpdatePauseMenu()
    {
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.JoystickButton13))
            pauseSelected = (pauseSelected + 1) % pauseOptions.Length;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.JoystickButton12))
            pauseSelected = (pauseSelected + pauseOptions.Length - 1) % pauseOptions.Length;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0))
            SelectPauseOption();
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;

        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;

        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    private void SelectPauseOption()
    {
        if (pauseSelected == 0)
        {
            SetPaused(false);
            return;
        }

        if (pauseSelected == 1)
        {
            if (player != null)
                player.RespawnAtCheckpoint(false);

            SetPaused(false);
            return;
        }

        SetPaused(false);
        SceneManager.LoadScene("TitleScreen");
    }

    public void PlayEffect(string effect, Vector3 position)
    {
        AudioClip clip = effect == "spin" ? spinSound :
            effect == "aku" ? (akuPickupSound != null ? akuPickupSound : pickupSound) :
            effect == "checkpoint" ? (checkpointSound != null ? checkpointSound : pickupSound) :
            effect == "pickup" ? pickupSound :
            effect == "explosion" ? explosionSound :
            crateSound;

        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, position, effect == "spin" ? 0.5f : 0.75f);
    }

    public static Vector3 ChunkOffset(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "bossarea": return new Vector3(41.0001f, -42.1201f, -72.132f);
            case "alwayson": return new Vector3(64f, -59f, -89.6001f);
            case "huba": return new Vector3(-70.40002f, 0, -41.6001f);
            case "hubb": return new Vector3(6.39993f, 0, -256f);
            case "highpath": return new Vector3(63.253f, -40.8557f, -217.6001f);
            case "hubc": return new Vector3(182.0632f, 0, 41.0094f);
            case "hubd": return new Vector3(278.0832f, 0, -104.8406f);
            case "pier": return new Vector3(284.8632f, -215.34f, -710.2406f);
            case "totemex": return new Vector3(282.6532f, -7.35f, -160.4406f);
            case "docent": return new Vector3(317.1232f, -14.22f, -96.7606f);
            case "hubboat1": return new Vector3(157.5482f, -16.616f, -407.5966f);
            case "hubboat2": return new Vector3(-25.8198f, -16.614f, -419.601f);
        }

        return Vector3.zero;
    }

    public static Quaternion ChunkRotation(string name)
    {
        return name.StartsWith("hubboat", StringComparison.OrdinalIgnoreCase) ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
    }

    private void BuildCollision()
    {
        TextAsset asset = Resources.Load<TextAsset>("BeachLevel/BeachCollision");

        if (asset == null)
            throw new FileNotFoundException("Missing exported RM2 beach collision.");

        using (BinaryReader reader = new BinaryReader(new MemoryStream(asset.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLC1")
                throw new InvalidDataException("Invalid beach collision data.");

            int chunks = reader.ReadInt32();

            for (int c = 0; c < chunks; c++)
            {
                string name = new string(reader.ReadChars(reader.ReadInt32()));
                int vertexCount = reader.ReadInt32();
                int triangleCount = reader.ReadInt32();

                Vector3[] vertices = new Vector3[vertexCount];
                int[] triangles = new int[triangleCount * 3];

                for (int i = 0; i < vertexCount; i++)
                    vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                for (int i = 0; i < triangles.Length; i++)
                    triangles[i] = (int)reader.ReadUInt32();

                Mesh mesh = new Mesh { name = name + ".rm2 original collision" };

                if (vertexCount > 65535)
                    mesh.indexFormat = IndexFormat.UInt32;

                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();

                GameObject surface = new GameObject(name + " walkable collision");
                surface.transform.SetParent(chunkRoots[name].transform, false);
                surface.transform.localPosition = ChunkOffset(name);
                surface.transform.localRotation = ChunkRotation(name);
                surface.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
    }

    private void BuildSea()
    {
        GameObject sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
        sea.name = "Beach ocean";
        sea.transform.SetParent(transform, false);
        sea.transform.position = new Vector3(0, -0.65f, 0);
        sea.transform.localScale = new Vector3(80, 1, 80);

        Destroy(sea.GetComponent<Collider>());

        Shader water = Resources.Load<Shader>("WaterSurface");
        sea.GetComponent<Renderer>().sharedMaterial = new Material(water != null ? water : Shader.Find("Unlit/Color"));
    }

    private void BuildItems()
    {
        TextAsset asset = Resources.Load<TextAsset>("BeachLevel/BeachInstances");

        if (asset == null)
            throw new FileNotFoundException("Missing exported RM2 object instances.");

        using (BinaryReader reader = new BinaryReader(new MemoryStream(asset.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLI1")
                throw new InvalidDataException("Invalid beach instance data.");

            int chunks = reader.ReadInt32();

            for (int c = 0; c < chunks; c++)
            {
                string chunk = new string(reader.ReadChars(reader.ReadInt32()));
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    uint instanceId = reader.ReadUInt32();
                    ushort objectId = reader.ReadUInt16();
                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    float yaw = reader.ReadSingle();

                    string kind = ItemKind(objectId);

                    if (kind == null)
                        continue;

                    bool crate = kind.Contains("Crate");

                    GameObject item = new GameObject(chunk + " instance " + instanceId + " " + kind);
                    item.transform.SetParent(chunkRoots[chunk].transform, false);
                    item.transform.position = ChunkRotation(chunk) * position + ChunkOffset(chunk);
                    item.transform.rotation = Quaternion.Euler(0, yaw, 0);

                    string modelName = kind == "IronCrate" || kind == "MultipleHitCrate" || kind == "LevelCrate" ? "BasicCrate" : kind;

                    GameObject model = BuildItemModel(modelName, item.transform);
                    model.transform.localScale = Vector3.one * (crate ? 1.25f : kind == "Wumpa" ? 0.7f : 1f);




                    SphereCollider trigger = item.AddComponent<SphereCollider>();
                    trigger.isTrigger = true;
                    trigger.radius = crate ? 0.85f : 0.7f;

                    BeachItem pickup = item.AddComponent<BeachItem>();
                    pickup.kind = kind;
                }
            }
        }
    }

    private void BuildOpeningAkuPickup(Vector3 start)
    {
        Vector3 forward = Quaternion.Euler(0, 180f, 0) * Vector3.forward;
        Vector3 position = start + forward * 5f + Vector3.up * 5f;

        if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point + Vector3.up * 1.25f;
        else
            position = start + forward * 5f + Vector3.up * 1.25f;

        GameObject pickup = new GameObject("Opening Aku Aku pickup");
        pickup.transform.SetParent(chunkRoots["Beach"].transform, true);
        pickup.transform.position = position;

        GameObject maskModel = BuildItemModel("AkuMask", pickup.transform);
        maskModel.transform.localScale = Vector3.one * 1.6f;

        SphereCollider trigger = pickup.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.75f;

        BeachItem item = pickup.AddComponent<BeachItem>();
        item.kind = "AkuMask";
    }

    public static string ItemKind(ushort objectId)
    {
        switch (objectId)
        {
            case 1: return "Wumpa";
            case 3: return "BasicCrate";
            case 4: return "NitroCrate";
            case 5: return "TntCrate";
            case 12: return "ExtraLifeCrate";
            case 15: return "IronCrate";
            case 19: return "MultipleHitCrate";
            case 266: return "CheckpointCrate";
            case 297: return "AkuCrate";
            case 268: return "LevelCrate";
            case 771: return "GemRed";
            case 773: return "GemBlue";
            case 777: return "GemClear";
            case 612: return "Crystal";
            default: return null;
        }
    }

    public static GameObject BuildItemModel(string name, Transform parent)
    {
        TextAsset asset = Resources.Load<TextAsset>("BeachItems/" + name);

        if (asset == null)
            throw new FileNotFoundException("Missing original " + name + " item model.");

        GameObject root = new GameObject(name + " original model");
        root.transform.SetParent(parent, false);

        Shader shader = Resources.Load<Shader>("Logo/GameOpaqueVertexColor");

        using (BinaryReader reader = new BinaryReader(new MemoryStream(asset.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLGO")
                throw new InvalidDataException("Invalid item mesh.");

            int chunks = reader.ReadInt32();

            for (int i = 0; i < chunks; i++)
            {
                uint textureId = reader.ReadUInt32();
                int vertexCount = reader.ReadInt32();
                int triangleCount = reader.ReadInt32();

                Vector3[] vertices = new Vector3[vertexCount];
                Vector2[] uv = new Vector2[vertexCount];
                Color32[] colors = new Color32[vertexCount];

                for (int v = 0; v < vertexCount; v++)
                {
                    vertices[v] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    uv[v] = new Vector2(1f - reader.ReadSingle(), reader.ReadSingle());
                    colors[v] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
                }

                int[] triangles = new int[triangleCount * 3];

                for (int t = 0; t < triangles.Length; t++)
                    triangles[t] = (int)reader.ReadUInt32();

                Mesh mesh = new Mesh { name = name + " game mesh " + i };
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.colors32 = colors;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();

                GameObject part = new GameObject("Part " + i);
                part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;

                Material material = new Material(shader);
                material.mainTexture = Resources.Load<Texture2D>("BeachItems/Textures/" + textureId.ToString("X8"));

                part.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        return root;
    }

    public void Collect(string kind, Vector3 position)
    {
        if (kind == "ExtraLifeCrate")
        {
            lives++;
        }
        else if (kind == "AkuCrate" || kind == "AkuMask")
        {
            AddMask(position);
        }
        else if (kind == "CheckpointCrate")
        {
            if (player != null)
                player.SetCheckpoint(position);

            checkpointMessageUntil = Time.time + 2.3f;
            PlayEffect("checkpoint", position);
        }
        else if (kind.StartsWith("Gem", StringComparison.Ordinal))
        {
            gems++;
        }
        else if (kind == "Crystal")
        {
            crystals++;
        }
        else if (kind == "Wumpa" || kind == "BasicCrate" || kind == "MultipleHitCrate")
        {
            wumpa++;

            if (wumpa >= 100)
            {
                wumpa -= 100;
                lives++;
            }
        }
    }

    private void AddMask(Vector3 position)
    {
        PlayEffect("aku", position);
        masks = Mathf.Min(2, masks + 1);

        if (akuFollower == null)
        {
            GameObject followerObject = new GameObject("Aku Aku follower");
            followerObject.transform.position = player.transform.position + Vector3.up * 1.5f;

            GameObject visual = new GameObject("Aku Aku visual");
            visual.transform.SetParent(followerObject.transform, false);

            GameObject maskModel = BuildItemModel("AkuMask", visual.transform);
            maskModel.transform.localScale = Vector3.one * 1.15f;

            akuFollower = followerObject.AddComponent<AkuAkuFollower>();
            akuFollower.target = player.transform;
        }

        akuFollower.SetLevel(masks);
    }

    public void AnimateCheckpointOpen(GameObject crate)
    {
        StartCoroutine(CheckpointOpenRoutine(crate));
    }

    private IEnumerator CheckpointOpenRoutine(GameObject crate)
    {
        Collider trigger = crate.GetComponent<Collider>();
        if (trigger != null)
            trigger.enabled = false;

        Transform intact = crate.transform.Find("CheckpointCrate original model");
        if (intact != null)
            intact.gameObject.SetActive(false);

        // Animation 15 drives the six joints of checkpoint OGI 245 at 25 FPS.
        TextAsset animation = Resources.Load<TextAsset>("BeachItems/CheckpointOpen");
        if (animation == null)
            throw new FileNotFoundException("Missing original checkpoint opening animation.");

        Vector3[,] positions;
        Quaternion[,] rotations;
        int frames;
        int fps;
        using (BinaryReader reader = new BinaryReader(new MemoryStream(animation.bytes)))
        {
            if (new string(reader.ReadChars(4)) != "TLCA")
                throw new InvalidDataException("Invalid checkpoint opening animation.");

            int joints = reader.ReadInt32();
            frames = reader.ReadInt32();
            fps = reader.ReadInt32();
            if (joints != 6 || frames < 2 || fps <= 0)
                throw new InvalidDataException("Unexpected checkpoint opening animation dimensions.");

            positions = new Vector3[frames, joints];
            rotations = new Quaternion[frames, joints];
            for (int frame = 0; frame < frames; frame++)
                for (int joint = 0; joint < joints; joint++)
                {
                    positions[frame, joint] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    rotations[frame, joint] = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
        }

        GameObject opened = BuildItemModel("CheckpointOpeningModel", crate.transform);
        opened.name = "Checkpoint opening - original OGI 245 and animation 15";
        opened.transform.localScale = Vector3.one * 1.25f;
        MeshFilter[] fragmentMeshes = opened.GetComponentsInChildren<MeshFilter>();
        Transform[] panels = new Transform[6];

        for (int i = 0; i < panels.Length; i++)
        {
            GameObject pivot = new GameObject("Checkpoint panel " + i);
            pivot.transform.SetParent(opened.transform, false);
            panels[i] = pivot.transform;

            // Adjacent meshes are the outside and inside material of the same joint.
            MeshFilter first = fragmentMeshes[i * 2];
            MeshFilter second = fragmentMeshes[i * 2 + 1];
            Vector3 rest = positions[0, i];
            pivot.transform.localPosition = rest;
            first.transform.SetParent(pivot.transform, false);
            second.transform.SetParent(pivot.transform, false);
            first.transform.localPosition = -rest;
            second.transform.localPosition = -rest;
        }

        float elapsed = 0f;
        float duration = (frames - 1f) / fps;
        while (elapsed < duration)
        {
            float sample = Mathf.Min(elapsed * fps, frames - 1f);
            int from = Mathf.FloorToInt(sample);
            int to = Mathf.Min(from + 1, frames - 1);
            float blend = sample - from;
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].localPosition = Vector3.Lerp(positions[from, i], positions[to, i], blend);
                panels[i].localRotation = Quaternion.Slerp(rotations[from, i], rotations[to, i], blend);
            }

            opened.transform.localRotation = Quaternion.Euler(0f, 360f * Mathf.Clamp01(elapsed / duration), 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            panels[i].localPosition = positions[frames - 1, i];
            panels[i].localRotation = rotations[frames - 1, i];
        }
        opened.transform.localRotation = Quaternion.identity;
    }

    public bool ConsumeMask()
    {
        if (masks <= 0)
            return false;

        masks--;

        if (akuFollower != null)
            akuFollower.SetLevel(masks);

        return true;
    }

    private void BuildAkuMaskVisual(Transform parent, float scale)
    {
        CreateMaskPart("Wooden mask", parent, new Vector3(0, 0, 0), new Vector3(0.72f, 0.88f, 0.18f) * scale, new Color(0.47f, 0.20f, 0.06f));

        CreateMaskPart("Left eye", parent, new Vector3(-0.19f, 0.16f, 0.11f) * scale, new Vector3(0.20f, 0.20f, 0.06f) * scale, Color.white);
        CreateMaskPart("Right eye", parent, new Vector3(0.19f, 0.16f, 0.11f) * scale, new Vector3(0.20f, 0.20f, 0.06f) * scale, Color.white);

        CreateMaskPart("Left pupil", parent, new Vector3(-0.19f, 0.16f, 0.15f) * scale, new Vector3(0.075f, 0.09f, 0.035f) * scale, new Color(0.08f, 0.05f, 0.02f));
        CreateMaskPart("Right pupil", parent, new Vector3(0.19f, 0.16f, 0.15f) * scale, new Vector3(0.075f, 0.09f, 0.035f) * scale, new Color(0.08f, 0.05f, 0.02f));

        CreateMaskPart("Mouth", parent, new Vector3(0, -0.23f, 0.11f) * scale, new Vector3(0.38f, 0.08f, 0.05f) * scale, new Color(0.12f, 0.04f, 0.02f));

        CreateMaskPart("Red feather", parent, new Vector3(-0.28f, 0.75f, 0), new Vector3(0.13f, 0.70f, 0.10f) * scale, new Color(0.90f, 0.12f, 0.06f), Quaternion.Euler(0, 0, -18f));
        CreateMaskPart("Orange feather", parent, new Vector3(-0.13f, 0.82f, 0), new Vector3(0.13f, 0.76f, 0.10f) * scale, new Color(1f, 0.45f, 0.03f), Quaternion.Euler(0, 0, -8f));
        CreateMaskPart("Yellow feather", parent, new Vector3(0, 0.86f, 0), new Vector3(0.13f, 0.82f, 0.10f) * scale, new Color(1f, 0.83f, 0.04f));
        CreateMaskPart("Green feather", parent, new Vector3(0.14f, 0.82f, 0), new Vector3(0.13f, 0.76f, 0.10f) * scale, new Color(0.16f, 0.73f, 0.18f), Quaternion.Euler(0, 0, 8f));
        CreateMaskPart("Blue feather", parent, new Vector3(0.29f, 0.75f, 0), new Vector3(0.13f, 0.70f, 0.10f) * scale, new Color(0.05f, 0.44f, 0.90f), Quaternion.Euler(0, 0, 18f));
    }

    private void CreateMaskPart(string name, Transform parent, Vector3 position, Vector3 scale, Color color, Quaternion? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation ?? Quaternion.identity;
        part.transform.localScale = scale;

        Collider collider = part.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        Shader shader = Shader.Find("Unlit/Color");

        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = color;

        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void OnGUI()
    {
        if (hud == null)
            hud = new GUIStyle(GUI.skin.label);

        hud.font = crashFont;
        hud.fontSize = Mathf.Max(20, Screen.height / 30);
        hud.normal.textColor = new Color(1f, 0.73f, 0.13f);

        GUI.Label(new Rect(20, 15, 750, 45),
            "WUMPA " + wumpa +
            "      LIVES " + lives +
            "      GEMS " + gems +
            "      CRYSTALS " + crystals +
            "      AKU " + masks, hud);

        hud.fontSize = Mathf.Max(15, Screen.height / 48);

        GUI.Label(
            new Rect(20, Screen.height - 35, Screen.width - 20, 30),
            "WASD move  •  Mouse camera  •  Space jump  •  Left click spin  •  Shift slide  •  Ctrl crouch  •  E body slam  •  Esc pause",
            hud);

        if (!IsPaused && Time.time < checkpointMessageUntil)
        {
            if (checkpointStyle == null)
            {
                checkpointStyle = new GUIStyle(GUI.skin.label);
                checkpointStyle.alignment = TextAnchor.MiddleCenter;
            }

            checkpointStyle.font = crashFont;
            checkpointStyle.fontSize = Mathf.Max(40, Screen.height / 12);
            Rect banner = new Rect(0, Screen.height * 0.16f, Screen.width, Screen.height * 0.16f);
            checkpointStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(banner.x + 3, banner.y + 3, banner.width, banner.height), "CHECKPOINT!", checkpointStyle);
            checkpointStyle.normal.textColor = new Color(1f, 0.73f, 0.13f);
            GUI.Label(banner, "CHECKPOINT!", checkpointStyle);
        }

        if (IsPaused)
            DrawPauseMenu();
    }

    private void DrawPauseMenu()
    {
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (pauseStyle == null)
        {
            pauseStyle = new GUIStyle(GUI.skin.label);
            pauseStyle.font = crashFont;
            pauseStyle.alignment = TextAnchor.MiddleCenter;
        }

        float width = Mathf.Min(520f, Screen.width * 0.8f);
        float left = (Screen.width - width) * 0.5f;
        float top = Screen.height * 0.24f;

        pauseStyle.fontSize = Mathf.Max(38, Screen.height / 13);
        pauseStyle.normal.textColor = new Color(1f, 0.68f, 0.12f);

        GUI.Label(new Rect(left, top, width, 80), "PAUSED", pauseStyle);

        for (int i = 0; i < pauseOptions.Length; i++)
        {
            Rect rect = new Rect(left, top + 100f + i * 58f, width, 48f);
            bool active = pauseSelected == i;

            pauseStyle.fontSize = Mathf.Max(24, Screen.height / 24);
            pauseStyle.normal.textColor = active ? new Color(1f, 0.78f, 0.18f) : Color.white;

            GUI.Label(rect, active ? "> " + pauseOptions[i] + " <" : pauseOptions[i], pauseStyle);

            Event input = Event.current;

            if (rect.Contains(input.mousePosition))
                pauseSelected = i;

            if (input.type == EventType.MouseUp && input.button == 0 && rect.Contains(input.mousePosition))
                SelectPauseOption();
        }

        pauseStyle.fontSize = Mathf.Max(16, Screen.height / 42);
        pauseStyle.normal.textColor = Color.white;

        GUI.Label(
            new Rect(left, top + 290f, width, 40f),
            "ESC  RESUME     ENTER  SELECT",
            pauseStyle);
    }
}

public class BeachItem : MonoBehaviour
{
    public string kind;

    private Vector3 origin;
    private bool consumed;
    private int hits;
    private float fuse;
    private float nextHit;

    private void Start()
    {
        origin = transform.position;
    }

    private void Update()
    {
        if (kind == "CheckpointCrate" && !consumed && BeachLevelRuntime.Current != null)
        {
            Transform player = BeachLevelRuntime.Current.PlayerTransform;
            if (player != null && Mathf.Abs(player.position.y - transform.position.y) < 2f)
            {
                Vector3 delta = player.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < 4f)
                    Hit();
            }
        }

        if (fuse > 0f)
        {
            fuse -= Time.deltaTime;

            if (fuse <= 0f)
                Explode();
        }

        if (!kind.Contains("Crate"))
        {
            transform.Rotate(0, Time.deltaTime * 70f, 0, Space.World);
            transform.position = origin + Vector3.up * (0.13f * Mathf.Sin(Time.time * 3f));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        BeachPlayerController player = other.GetComponent<BeachPlayerController>();

        if (consumed || player == null)
            return;

        if (kind == "NitroCrate")
        {
            Explode();
            player.TakeDamage();
            return;
        }

        if (kind.Contains("Crate"))
        {
            if (player.attacking)
                Hit();

            return;
        }

        consumed = true;

        if (kind != "AkuMask")
            BeachLevelRuntime.Current.PlayEffect("pickup", transform.position);
        BeachLevelRuntime.Current.Collect(kind, transform.position);

        Destroy(gameObject);
    }

    public void Hit()
    {
        if (!kind.Contains("Crate") || consumed || kind == "IronCrate")
            return;

        if (kind == "TntCrate")
        {
            if (fuse <= 0f)
            {
                fuse = 3f;
                BeachLevelRuntime.Current.PlayEffect("crate", transform.position);
            }

            return;
        }

        if (kind == "MultipleHitCrate")
        {
            if (Time.time < nextHit)
                return;

            nextHit = Time.time + 0.34f;

            if (++hits < 5)
                return;
        }

        if (kind == "NitroCrate")
        {
            Explode();
            return;
        }

        consumed = true;

        if (kind != "CheckpointCrate")
            BeachLevelRuntime.Current.PlayEffect("crate", transform.position);
        BeachLevelRuntime.Current.Collect(kind, transform.position);

        if (kind == "CheckpointCrate")
        {
            BeachLevelRuntime.Current.AnimateCheckpointOpen(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    private void Explode()
    {
        if (consumed)
            return;

        consumed = true;

        BeachLevelRuntime.Current.PlayEffect("explosion", transform.position);

        Collider[] hitsNearby = Physics.OverlapSphere(transform.position, 2.2f, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider nearby in hitsNearby)
        {
            BeachPlayerController player = nearby.GetComponent<BeachPlayerController>();

            if (player != null)
                player.TakeDamage();

            BeachItem item = nearby.GetComponent<BeachItem>();

            if (item != null && item != this && item.kind != "IronCrate")
                item.Hit();
        }

        Destroy(gameObject);
    }
}

public class AkuAkuFollower : MonoBehaviour
{
    public Transform target;

    private Vector3 velocity;
    private int level;
    private float hoverTime;

    public void SetLevel(int value)
    {
        level = value;
        gameObject.SetActive(level > 0);

        if (level > 0)
            transform.localScale = Vector3.one * (level == 2 ? 1.12f : 1f);
    }

    private void Update()
    {
        if (target == null || level <= 0)
            return;

        hoverTime += Time.deltaTime;

        Vector3 desired =
            target.position +
            target.right * 0.95f -
            target.forward * 0.35f +
            Vector3.up * (1.55f + Mathf.Sin(hoverTime * 3.2f) * 0.11f);

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.14f);

        Vector3 direction = target.position + Vector3.up * 1.15f - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 8f);
        }
    }
}
