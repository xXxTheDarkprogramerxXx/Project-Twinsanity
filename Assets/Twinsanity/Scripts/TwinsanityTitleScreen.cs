using UnityEngine;
using UnityEngine.SceneManagement;

public class TwinsanityTitleScreen : MonoBehaviour
{
    public Font crashFont;
    public string newGameScene = "";
    public AudioClip startSound;
    public AudioClip moveSound;
    public AudioClip selectSound;
    public AudioClip themeMusic;
    public RenderTexture originalLogo;

    private Texture2D background;
    private Texture2D portal;
    private Texture2D iceCube;
    private Texture2D menuDisc;
    private GUIStyle center;
    private bool menuOpen;
    private bool optionsOpen;
    private int selected;
    private float time;
    private string message = "";
    private AudioSource audioSource;
    private bool ownsLogo;
    private AudioSource musicSource;
    private Transform logoRoot;
    private Camera logoCamera;
    private float backgroundRefresh;
    private float menuTransition;

    private readonly string[] options = { "NEW GAME", "LOAD GAME", "OPTIONS" };

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        if (crashFont == null) crashFont = Resources.Load<Font>("Crash-Euro");
        if (startSound == null) startSound = Resources.Load<AudioClip>("Frontend_8000");
        if (moveSound == null) moveSound = Resources.Load<AudioClip>("Frontend_8001");
        if (selectSound == null) selectSound = Resources.Load<AudioClip>("Frontend_8002");
        if (themeMusic == null) themeMusic = Resources.Load<AudioClip>("TwinsanityTheme");
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = themeMusic;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = 0.7f;
        if (themeMusic != null) musicSource.Play();
        if (originalLogo == null)
        {
            try
            {
                originalLogo = TwinsanityLogoRuntime.Create(transform, out logoRoot, out logoCamera);
                ownsLogo = originalLogo != null;
            }
            catch (System.Exception error)
            {
                Debug.LogError("Could not assemble the original GameLogo: " + error);
            }
        }
        background = MakeBackground(320, 224);
        portal = MakePortal(256);
        iceCube = MakeIceCube(176, 134);
        menuDisc = MakeMenuDisc(320);
    }

    private void OnDestroy()
    {
        Destroy(background);
        Destroy(portal);
        Destroy(iceCube);
        Destroy(menuDisc);
        if (ownsLogo && originalLogo != null) { originalLogo.Release(); Destroy(originalLogo); }
    }

    private void Update()
    {
        time += Time.deltaTime;
        menuTransition = Mathf.MoveTowards(menuTransition, menuOpen ? 1f : 0f, Time.deltaTime * 1.5f);
        if (time >= backgroundRefresh)
        {
            UpdateBackground(background, time);
            backgroundRefresh = time + 0.07f;
        }
        if (logoRoot != null && logoCamera != null)
        {
            logoRoot.localRotation = Quaternion.Euler(3f * Mathf.Sin(time * 0.8f), 5f * Mathf.Sin(time * 0.55f), 2f * Mathf.Sin(time * 0.7f));
            logoRoot.localPosition = new Vector3(0, 0.10f * Mathf.Sin(time * 1.3f), 1000);
            logoCamera.Render();
        }

        if (!menuOpen)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton7))
            {
                menuOpen = true;
                Play(startSound);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1))
        {
            if (optionsOpen)
                optionsOpen = false;
            else
                menuOpen = false;
            message = "";
        }

        if (optionsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.JoystickButton13))
        {
            selected = (selected + 1) % options.Length;
            Play(moveSound);
        }
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.JoystickButton12))
        {
            selected = (selected + options.Length - 1) % options.Length;
            Play(moveSound);
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0))
            SelectOption();
    }

    private void OnGUI()
    {
        float scale = Mathf.Min(Screen.width / 640f, Screen.height / 448f);
        Rect frame = new Rect((Screen.width - 640f * scale) * 0.5f, (Screen.height - 448f * scale) * 0.5f, 640f * scale, 448f * scale);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(frame.x, frame.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        GUI.BeginGroup(new Rect(0, 0, 640, 448));

        GUI.DrawTexture(new Rect(0, 0, 640, 448), background);
        float bob = Mathf.Sin(time * 1.5f) * 5f;
        float drift = Mathf.Sin(time * 0.42f) * 17f;
        GUI.color = new Color(0.35f, 0.85f, 1f, 0.28f);
        GUI.DrawTexture(new Rect(222 + drift, 310 + bob, 200, 19), iceCube);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(233 + drift, 220 + bob, 174, 132), iceCube);
        if (originalLogo != null)
        {
            float pulse = 1f + Mathf.Sin(time * 1.6f) * 0.018f;
            float size = Mathf.Lerp(350f, 295f, menuTransition) * pulse;
            float y = Mathf.Lerp(0f, -22f, menuTransition);
            GUI.DrawTexture(new Rect(320f - size * 0.5f, y + Mathf.Sin(time * 1.3f) * 4f, size, size), originalLogo);
        }
        else
        {
            float pulse = 1f + Mathf.Sin(time * 1.4f) * 0.035f;
            float whirl = 253f * pulse;
            GUI.color = new Color(1f, 1f, 1f, 0.83f);
            GUI.DrawTexture(new Rect(320f - whirl * 0.5f, 147f - whirl * 0.5f, whirl, whirl), portal);
            GUI.color = Color.white;
            DrawOutlined("CRASH", new Rect(94, 55, 452, 79), 75, new Color(1f, 0.72f, 0.12f), new Color(0.22f, 0.09f, 0.08f), 3);
            DrawOutlined("TWiNSANiTY", new Rect(112, 145, 416, 58), 45, new Color(0.95f, 0.21f, 0.12f), new Color(0.22f, 0.08f, 0.15f), 2);
        }

        if (!menuOpen)
        {
            float blink = 0.72f + Mathf.Sin(time * 4f) * 0.28f;
            DrawOutlined("PRESS START BUTTON", new Rect(123, 368, 394, 42), 29, new Color(1f, 0.64f, 0.17f, blink), Color.black, 1);
        }
        else if (optionsOpen)
        {
            DrawMenuDisc(new Rect(173, 183, 294, 252));
            DrawOutlined("OPTIONS", new Rect(150, 231, 340, 49), 35, new Color(1f, 0.75f, 0.28f), Color.black, 1);
            DrawOutlined("ADD YOUR GAME SETTINGS HERE", new Rect(151, 284, 338, 37), 20, Color.white, Color.black, 1);
            if (DrawButton("BACK", new Rect(248, 331, 144, 35), true))
                optionsOpen = false;
        }
        else
        {
            DrawMenuDisc(new Rect(169, 182, 302, 255));
            for (int i = 0; i < options.Length; i++)
            {
                Rect item = new Rect(205, 247 + i * 42, 230, 36);
                if (DrawButton(options[i], item, i == selected))
                {
                    selected = i;
                    SelectOption();
                }
            }
            DrawOutlined("SELECT  X       BACK  ESC", new Rect(183, 386, 274, 24), 17, Color.white, Color.black, 1);
        }

        if (!string.IsNullOrEmpty(message))
            DrawOutlined(message, new Rect(20, 412, 600, 28), 18, Color.white, Color.black, 1);

        GUI.EndGroup();
        GUI.matrix = previousMatrix;
    }

    private bool DrawButton(string label, Rect rect, bool active)
    {
        DrawOutlined(label, rect, active ? 29 : 27, active ? new Color(1f, 0.75f, 0.20f) : Color.white, new Color(0.04f, 0.07f, 0.23f), 1);
        Event input = Event.current;
        return input.type == EventType.MouseUp && input.button == 0 && rect.Contains(input.mousePosition);
    }

    private void DrawMenuDisc(Rect rect)
    {
        GUI.DrawTexture(rect, menuDisc);
    }

    private void DrawOutlined(string value, Rect rect, int size, Color color, Color outline, int thickness)
    {
        if (center == null)
            center = new GUIStyle(GUI.skin.label);
        center.font = crashFont;
        center.fontSize = size;
        center.alignment = TextAnchor.MiddleCenter;
        center.clipping = TextClipping.Overflow;

        center.normal.textColor = outline;
        for (int x = -thickness; x <= thickness; x += thickness)
            for (int y = -thickness; y <= thickness; y += thickness)
                GUI.Label(new Rect(rect.x + x, rect.y + y, rect.width, rect.height), value, center);
        center.normal.textColor = color;
        GUI.Label(rect, value, center);
    }

    private void SelectOption()
    {
        Play(selectSound);
        message = "";
        if (selected == 0)
        {
            if (string.IsNullOrEmpty(newGameScene))
                message = "ASSIGN A GAMEPLAY SCENE TO THE TITLE SCREEN";
            else if (Application.CanStreamedLevelBeLoaded(newGameScene))
                SceneManager.LoadScene(newGameScene);
            else
                message = "ADD THE GAMEPLAY SCENE TO BUILD SETTINGS";
        }
        else if (selected == 1)
            message = "CONNECT YOUR SAVE SYSTEM HERE";
        else
            optionsOpen = true;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private static Texture2D MakeBackground(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        UpdateBackground(texture, 0f);
        return texture;
    }

    private static void UpdateBackground(Texture2D texture, float time)
    {
        int width = texture.width;
        int height = texture.height;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                Color c;
                if (v > 0.34f)
                {
                    float t = Mathf.InverseLerp(0.34f, 1f, v);
                    c = Color.Lerp(new Color(1f, 0.68f, 0.28f), new Color(0.42f, 0.12f, 0.31f), t);
                    float sun = Mathf.Exp(-Mathf.Pow((u - 0.52f) * 3.2f, 2f) - Mathf.Pow((v - 0.4f) * 4.8f, 2f));
                    c = Color.Lerp(c, new Color(1f, 0.87f, 0.45f), sun * 0.7f);
                }
                else
                {
                    float depth = v / 0.34f;
                    float ripples = Mathf.Sin(u * (100f - depth * 65f) + v * 37f + time * 3f) * 0.045f;
                    ripples += Mathf.Sin(u * 62f - v * 91f - time * 2.2f) * 0.035f;
                    float reflection = Mathf.Exp(-Mathf.Pow((u - 0.52f + Mathf.Sin(time * 0.7f) * 0.015f) * (3f + v * 14f), 2f)) * 0.28f;
                    c = Color.Lerp(new Color(0.025f, 0.075f, 0.24f), new Color(0.12f, 0.45f, 0.69f), depth + ripples);
                    c += new Color(reflection, reflection * 0.7f, reflection * 0.26f);
                }
                pixels[y * width + x] = c;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false);
    }

    private static Texture2D MakeIceCube(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)width;
            float v = y / (float)height;
            float top = 0.18f + 0.07f * Mathf.Sin(u * 5f + 0.5f);
            float bottom = 0.92f - 0.09f * Mathf.Abs(u - 0.5f);
            if (u > 0.08f && u < 0.92f && v > top && v < bottom)
            {
                float edge = Mathf.Min(Mathf.Min(u - 0.08f, 0.92f - u), Mathf.Min(v - top, bottom - v));
                float facets = Mathf.Sin(u * 24f + v * 9f) * 0.08f + Mathf.Sin(u * 11f - v * 22f) * 0.08f;
                Color baseColor = u < 0.30f ? new Color(0.30f, 0.67f, 0.84f) : new Color(0.69f, 0.93f, 0.97f);
                if (v > 0.77f) baseColor = Color.Lerp(baseColor, new Color(0.05f, 0.30f, 0.53f), (v - 0.77f) * 4f);
                pixels[y * width + x] = baseColor * (0.86f + facets) + new Color(0f, 0f, 0f, Mathf.Clamp01(edge * 30f) * 0.78f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false);
        return texture;
    }

    private static Texture2D MakeMenuDisc(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r < 0.98f)
            {
                Color fill = Color.Lerp(new Color(0.025f, 0.12f, 0.38f), new Color(0.08f, 0.38f, 0.69f), Mathf.Clamp01(dy * 0.5f + 0.5f));
                if (r > 0.91f) fill = new Color(0.45f, 0.65f, 0.83f);
                else if (r > 0.84f) fill = new Color(0.015f, 0.05f, 0.19f);
                fill.a = Mathf.Clamp01((0.98f - r) * 65f) * 0.94f;
                pixels[y * size + x] = fill;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false);
        return texture;
    }

    private static Texture2D MakePortal(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float spiral = 0.5f + 0.5f * Mathf.Sin(angle * 4f + radius * 24f);
                float alpha = Mathf.Clamp01((1f - radius) * 3f) * 0.72f;
                Color c = Color.Lerp(new Color(0.16f, 0.025f, 0.34f), new Color(0.82f, 0.24f, 0.89f), spiral);
                c.a = alpha;
                pixels[y * size + x] = c;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
