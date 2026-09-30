using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// A Unity-built recreation guided by the supplied 30 fps reference frames.
public class TwinsanityNativeIntro : MonoBehaviour
{
    public string newGameScene = "";
    public Font crashFont;
    public AudioClip themeMusic;
    public AudioClip startSound;
    public AudioClip moveSound;
    public AudioClip selectSound;

    private Camera sceneCamera;
    private Transform iceberg;
    private Transform portal;
    private Transform seagull;
    private Transform logoRoot;
    private Camera logoCamera;
    private RenderTexture logoTexture;
    private Texture2D skyTexture;
    private Texture2D menuDisc;
    private Texture2D crashIcon;
    private AudioSource music;
    private AudioSource effects;
    private GUIStyle textStyle;
    private float sequenceTime;
    private bool menuOpen;
    private bool optionsOpen;
    private bool musicStarted;
    private int selected;
    private string message = "";
    private readonly string[] choices = { "NEW GAME", "LOAD GAME", "OPTIONS" };
    private readonly List<Material> ownedMaterials = new List<Material>();
    private readonly List<Mesh> ownedMeshes = new List<Mesh>();

    private void Awake()
    {
        if (string.IsNullOrEmpty(newGameScene)) newGameScene = "BeachLevel";
        if (crashFont == null) crashFont = Resources.Load<Font>("Crash-Euro");
        if (themeMusic == null) themeMusic = Resources.Load<AudioClip>("TwinsanityTheme");
        if (startSound == null) startSound = Resources.Load<AudioClip>("Frontend_8000");
        if (moveSound == null) moveSound = Resources.Load<AudioClip>("Frontend_8001");
        if (selectSound == null) selectSound = Resources.Load<AudioClip>("Frontend_8002");
        crashIcon = Resources.Load<Texture2D>("CrashIcon");
        menuDisc = MakeDisc(256);
        music = gameObject.AddComponent<AudioSource>();
        music.clip = themeMusic;
        music.loop = true;
        music.playOnAwake = false;
        music.volume = 0.7f;
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;

        sceneCamera = Camera.main;
        if (sceneCamera == null) sceneCamera = new GameObject("Main Camera").AddComponent<Camera>();
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = new Color(0.75f, 0.38f, 0.46f);
        sceneCamera.fieldOfView = 52f;
        sceneCamera.nearClipPlane = 0.1f;
        sceneCamera.farClipPlane = 500f;

        sequenceTime = 12f; // The supplied shot sequence begins at the empty ocean.
        BuildSky();
        BuildWater();
        BuildIceberg();
        BuildBird();
        BuildIsland();
        BuildPortal();
        logoTexture = TwinsanityLogoRuntime.Create(transform, out logoRoot, out logoCamera);
        UpdateCamera(12f);
    }

    private void OnDestroy()
    {
        if (logoTexture != null) { logoTexture.Release(); Destroy(logoTexture); }
        if (skyTexture != null) Destroy(skyTexture);
        if (menuDisc != null) Destroy(menuDisc);
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        foreach (Material material in ownedMaterials) if (material != null) Destroy(material);
    }

    private void Update()
    {
        sequenceTime += Time.deltaTime;
        if (!musicStarted && sequenceTime >= 11.5f)
        {
            musicStarted = true;
            if (themeMusic != null) music.Play();
        }
        float sceneTime = Mathf.Max(12f, sequenceTime);
        UpdateCamera(sceneTime);
        float iceX = sceneTime < 24f ? Mathf.Lerp(47f, -7f, Mathf.Clamp01((sceneTime - 13f) / 11f)) : Mathf.Lerp(-7f, -55f, Mathf.Clamp01((sceneTime - 24f) / 5f));
        iceberg.position = new Vector3(iceX, Mathf.Sin(sceneTime * 1.6f) * 0.17f, 25f);
        iceberg.rotation = Quaternion.Euler(Mathf.Sin(sceneTime * 0.8f) * 3f, sceneTime * 2f, Mathf.Sin(sceneTime * 1.2f) * 4f);
        if (seagull != null)
        {
            seagull.gameObject.SetActive(sceneTime >= 17f && sceneTime < 28f);
            float landing = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sceneTime - 17f) / 3f));
            float takeoff = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sceneTime - 24f) / 3f));
            Vector3 approach = Vector3.Lerp(new Vector3(3.8f, 7.5f, 0.8f), new Vector3(0.3f, 4.8f, -0.2f), landing);
            seagull.position = iceberg.position + approach + new Vector3(takeoff * 7f, takeoff * takeoff * 7f, -takeoff * 2f);
            seagull.localRotation = Quaternion.Euler(0f, -35f + takeoff * 25f, 5f * takeoff);
            TwinsanityCharacterRuntime.AnimateSeagull(sceneTime, Mathf.Clamp01(1f - landing) + takeoff * 0.9f + 0.12f);
        }
        float opening = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sceneTime - 31f) / 2.2f));
        portal.localScale = Vector3.one * opening;
        portal.Rotate(0f, 0f, Time.deltaTime * 45f, Space.Self);
        if (logoRoot != null && logoCamera != null)
        {
            logoRoot.localRotation = Quaternion.Euler(2f * Mathf.Sin(sequenceTime * 0.8f), 180f + 4f * Mathf.Sin(sequenceTime * 0.65f), Mathf.Sin(sequenceTime) * 2f);
            logoRoot.localPosition = new Vector3(0, 0.1f * Mathf.Sin(sequenceTime * 1.2f), 1000);
            logoCamera.Render();
        }

        bool start = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton7);
        if (!menuOpen)
        {
            if (start)
            {
                if (sequenceTime < 33f) sequenceTime = 33f;
                else if (Application.CanStreamedLevelBeLoaded(newGameScene)) { Play(startSound); SceneManager.LoadScene(newGameScene); }
                else { menuOpen = true; Play(startSound); }
            }
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            if (optionsOpen) optionsOpen = false;
            else menuOpen = false;
            message = "";
        }
        if (optionsOpen) return;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.JoystickButton13))
        {
            selected = (selected + 1) % choices.Length;
            Play(moveSound);
        }
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.JoystickButton12))
        {
            selected = (selected + choices.Length - 1) % choices.Length;
            Play(moveSound);
        }
        if (start || Input.GetKeyDown(KeyCode.JoystickButton0)) SelectOption();
    }

    private void UpdateCamera(float t)
    {
        // Frame 365: open water. Frames 420-700: ice traverses right to left.
        // Frames 760-950: camera turns toward the beach hub island.
        float pan = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 24f) / 7.5f));
        sceneCamera.transform.position = Vector3.Lerp(new Vector3(0f, 4.4f, -16f), new Vector3(130f, 4.8f, 80f), pan);
        Vector3 target = Vector3.Lerp(new Vector3(0f, 4.4f, 70f), new Vector3(200f, 4.8f, 150f), pan);
        sceneCamera.transform.LookAt(target);
    }

    private void BuildSky()
    {
        skyTexture = new Texture2D(512, 256, TextureFormat.RGB24, false);
        skyTexture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[512 * 256];
        for (int y = 0; y < 256; y++)
        for (int x = 0; x < 512; x++)
        {
            float u = x / 511f;
            float v = y / 255f;
            Color color = Color.Lerp(new Color(1f, 0.72f, 0.38f), new Color(0.37f, 0.21f, 0.60f), Mathf.Clamp01(v * 1.1f));
            float cloud = Mathf.PerlinNoise(u * 5.3f, v * 8.2f) * 0.6f + Mathf.PerlinNoise(u * 11f, v * 12f) * 0.4f;
            float ribbon = Mathf.Sin(v * 40f + Mathf.Sin(u * 9f) * 2f);
            color = Color.Lerp(color, new Color(1f, 0.42f, 0.55f), Mathf.Clamp01((cloud - 0.47f) * 2.3f + ribbon * 0.12f) * 0.75f);
            float sun = Mathf.Exp(-Mathf.Pow((u - 0.75f) * 4.8f, 2f) - Mathf.Pow((v - 0.23f) * 3.3f, 2f));
            pixels[y * 512 + x] = Color.Lerp(color, new Color(1f, 0.97f, 0.58f), sun * 0.95f);
        }
        skyTexture.SetPixels(pixels);
        skyTexture.Apply();
        Mesh skyMesh = QuadMesh(600f, 180f);
        GameObject sky = MeshObject("Painted sunset and clouds", transform, skyMesh, TextureMaterial(skyTexture));
        sky.transform.position = new Vector3(0, 58f, 205f);
    }

    private void BuildWater()
    {
        int columns = 90;
        int rows = 70;
        Vector3[] vertices = new Vector3[(columns + 1) * (rows + 1)];
        int[] indices = new int[columns * rows * 6];
        for (int z = 0; z <= rows; z++)
        for (int x = 0; x <= columns; x++) vertices[z * (columns + 1) + x] = new Vector3(-500f + x * 1000f / columns, 0f, -200f + z * 700f / rows);
        int index = 0;
        for (int z = 0; z < rows; z++)
        for (int x = 0; x < columns; x++)
        {
            int a = z * (columns + 1) + x;
            int b = a + 1;
            int c = a + columns + 1;
            indices[index++] = a; indices[index++] = c; indices[index++] = b;
            indices[index++] = b; indices[index++] = c; indices[index++] = c + 1;
        }
        Mesh mesh = new Mesh { name = "Ocean mesh" };
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(new Vector3(0, 0, 150), new Vector3(1000, 8, 700));
        ownedMeshes.Add(mesh);
        Shader shader = Resources.Load<Shader>("WaterSurface");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material water = new Material(shader) { name = "Animated ocean" };
        ownedMaterials.Add(water);
        MeshObject("Animated ocean", transform, mesh, water);
    }

    private void BuildIceberg()
    {
        GameObject root = new GameObject("Floating original front-end iceberg");
        root.transform.SetParent(transform, false);
        iceberg = root.transform;
        GameObject flyby = TwinsanityFlybyRuntime.Create(root.transform);
        flyby.transform.localScale = Vector3.one * 1.7f;
        Mesh wake = RingMesh(2.8f, 0.12f, 64);
        GameObject foam = MeshObject("Waterline and wake", root.transform, wake, ColorMaterial(new Color(0.62f, 0.85f, 0.98f)));
        foam.transform.localPosition = new Vector3(0, 0.06f, 0);
        foam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void BuildBird()
    {
        GameObject bird = new GameObject("Seagull circling the iceberg");
        bird.transform.SetParent(transform, false);
        seagull = bird.transform;
        GameObject original = TwinsanityCharacterRuntime.Create("Seagull", bird.transform);
        original.transform.localScale = Vector3.one * 1.5f;
    }

    private void BuildIsland()
    {
        GameObject island = TwinsanitySceneryRuntime.Create(transform);
        island.transform.localPosition = new Vector3(210f, -0.6f, 160f);
        island.transform.localScale = Vector3.one * 0.5f;
    }

    private void BuildPortal()
    {
        GameObject root = new GameObject("Opening purple portal");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(200f, 9f, 155f);
        portal = root.transform;
        portal.localScale = Vector3.zero;
        Material[] colors = {
            ColorMaterial(new Color(0.48f, 0.09f, 0.78f)),
            ColorMaterial(new Color(0.18f, 0.48f, 1f)),
            ColorMaterial(new Color(0.77f, 0.28f, 0.92f))
        };
        for (int i = 0; i < 3; i++)
        {
            Mesh mesh = RingMesh(19f - i * 3f, 0.85f, 64);
            GameObject ring = MeshObject("Swirling portal ring " + i, root.transform, mesh, colors[i]);
            ring.transform.localRotation = Quaternion.Euler(0, i * 19f, i * 27f);
        }
        for (int i = 0; i < 15; i++)
        {
            float angle = i / 15f * Mathf.PI * 2f;
            Primitive("Portal spark", PrimitiveType.Sphere, root.transform, new Vector3(Mathf.Cos(angle) * 19f, Mathf.Sin(angle) * 19f, -0.3f), new Vector3(0.12f, 0.12f, 0.12f), colors[i % 3]);
        }
    }

    private Material ColorMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Unlit/Color"));
        material.color = color;
        ownedMaterials.Add(material);
        return material;
    }

    private Material TextureMaterial(Texture texture)
    {
        Material material = new Material(Shader.Find("Unlit/Texture"));
        material.mainTexture = texture;
        ownedMaterials.Add(material);
        return material;
    }

    private GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        return obj;
    }

    private GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        return obj;
    }

    private Mesh QuadMesh(float width, float height)
    {
        Mesh mesh = new Mesh { name = "Sky card" };
        mesh.vertices = new[] { new Vector3(-width / 2, -height / 2, 0), new Vector3(width / 2, -height / 2, 0), new Vector3(-width / 2, height / 2, 0), new Vector3(width / 2, height / 2, 0) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        ownedMeshes.Add(mesh);
        return mesh;
    }

    private Mesh RingMesh(float radius, float thickness, int sections)
    {
        Vector3[] vertices = new Vector3[sections * 2];
        int[] triangles = new int[sections * 6];
        for (int i = 0; i < sections; i++)
        {
            float a = i * Mathf.PI * 2f / sections;
            vertices[i * 2] = new Vector3(Mathf.Cos(a) * (radius - thickness), Mathf.Sin(a) * (radius - thickness), 0);
            vertices[i * 2 + 1] = new Vector3(Mathf.Cos(a) * (radius + thickness), Mathf.Sin(a) * (radius + thickness), 0);
            int next = (i + 1) % sections;
            int j = i * 6;
            triangles[j] = i * 2; triangles[j + 1] = next * 2; triangles[j + 2] = i * 2 + 1;
            triangles[j + 3] = i * 2 + 1; triangles[j + 4] = next * 2; triangles[j + 5] = next * 2 + 1;
        }
        Mesh mesh = new Mesh { name = "Portal energy ring" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        ownedMeshes.Add(mesh);
        return mesh;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null) effects.PlayOneShot(clip);
    }

    private void SelectOption()
    {
        Play(selectSound);
        message = "";
        if (selected == 0)
        {
            if (string.IsNullOrEmpty(newGameScene)) message = "ASSIGN A GAMEPLAY SCENE";
            else if (Application.CanStreamedLevelBeLoaded(newGameScene)) SceneManager.LoadScene(newGameScene);
            else message = "ADD THE GAMEPLAY SCENE TO BUILD SETTINGS";
        }
        else if (selected == 1) message = "CONNECT YOUR SAVE SYSTEM HERE";
        else optionsOpen = true;
    }

    private void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 640f, Screen.height / 360f);
        float x = (Screen.width - 640f * scale) * 0.5f;
        float y = (Screen.height - 360f * scale) * 0.5f;
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        GUI.matrix = Matrix4x4.TRS(new Vector3(x, y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
        GUI.BeginGroup(new Rect(0, 0, 640, 360));
        if (sequenceTime < 11.5f)
        {
            GUI.DrawTexture(new Rect(0, 0, 640, 360), Texture2D.blackTexture);
            if (crashIcon != null) GUI.DrawTexture(new Rect(236, 65, 168, 168), crashIcon, ScaleMode.ScaleToFit, true);
            Label("LOADING", new Rect(174, 267, 292, 40), 34, true);
        }
        else if (sequenceTime < 33f)
        {
            if (sequenceTime < 31.7f) Label("THREE YEARS AGO...", new Rect(135, 321, 370, 30), 22, true);
            else Label("PRESS START BUTTON", new Rect(120, 321, 400, 30), 25, true);
        }
        else
        {
            if (logoTexture != null)
            {
                float entry = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((sequenceTime - 33f) / 1.6f));
                float size = Mathf.Lerp(720f, menuOpen ? 400f : 620f, entry);
                GUI.color = new Color(1f, 1f, 1f, entry);
                Rect logoRect = new Rect(320 - size * 0.5f, menuOpen ? -70f : -135f, size, size);
                GUI.color = new Color(0.02f, 0.01f, 0.04f, entry * 0.7f);
                GUI.DrawTexture(new Rect(logoRect.x + 4f, logoRect.y + 5f, logoRect.width, logoRect.height), logoTexture);
                GUI.color = new Color(1f, 1f, 1f, entry);
                GUI.DrawTexture(logoRect, logoTexture);
                GUI.color = Color.white;
            }
            else
            {
                Label("CRASH", new Rect(98, 66, 444, 95), 75, true);
                Label("TWINSANITY", new Rect(102, 160, 436, 70), 50, true);
            }
            if (!menuOpen) Label("PRESS START BUTTON", new Rect(120, 321, 400, 30), 25, true);
        }
        if (menuOpen)
        {
            GUI.color = new Color(0.06f, 0.14f, 0.40f, 0.84f);
            GUI.DrawTexture(new Rect(142, 168, 356, 177), menuDisc);
            GUI.color = Color.white;
            if (optionsOpen)
            {
                Label("OPTIONS", new Rect(204, 219, 232, 33), 28, true);
                Label("ADD YOUR SETTINGS HERE", new Rect(180, 262, 280, 26), 17, false);
                if (Option("BACK", new Rect(240, 301, 160, 28), true)) optionsOpen = false;
            }
            else
            {
                for (int i = 0; i < choices.Length; i++)
                {
                    Rect rect = new Rect(200, 218 + i * 36, 240, 33);
                    if (Option(choices[i], rect, selected == i)) { selected = i; SelectOption(); }
                }
                Label("SELECT X       BACK ESC", new Rect(196, 326, 248, 22), 15, false);
            }
        }
        if (!string.IsNullOrEmpty(message)) Label(message, new Rect(50, 330, 540, 24), 15, true);
        GUI.EndGroup();
        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private bool Option(string value, Rect rect, bool highlighted)
    {
        Label(value, rect, highlighted ? 25 : 23, highlighted);
        Event input = Event.current;
        return input.type == EventType.MouseUp && input.button == 0 && rect.Contains(input.mousePosition);
    }

    private void Label(string value, Rect rect, int size, bool highlighted)
    {
        if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label);
        textStyle.font = crashFont;
        textStyle.fontSize = size;
        textStyle.alignment = TextAnchor.MiddleCenter;
        textStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(rect.x + 1, rect.y + 2, rect.width, rect.height), value, textStyle);
        textStyle.normal.textColor = highlighted ? new Color(1f, 0.64f, 0.15f) : Color.white;
        GUI.Label(rect, value, textStyle);
    }

    private static Texture2D MakeDisc(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)size * 2f - 1f;
            float v = y / (float)size * 2f - 1f;
            float r = Mathf.Sqrt(u * u + v * v);
            if (r < 0.98f)
            {
                Color color = Color.Lerp(new Color(0.01f, 0.08f, 0.31f), new Color(0.08f, 0.44f, 0.70f), v * 0.5f + 0.5f);
                if (r > 0.91f) color = new Color(0.61f, 0.76f, 0.83f);
                else if (r > 0.85f) color = new Color(0.01f, 0.06f, 0.21f);
                color.a = Mathf.Clamp01((0.98f - r) * 50f);
                pixels[y * size + x] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
