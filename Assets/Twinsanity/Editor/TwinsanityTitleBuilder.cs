using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TwinsanityTitleBuilder
{
    [MenuItem("Tools/Twinsanity/Create Title Screen")]
    public static void CreateTitleScreen()
    {
        const string scenePath = "Assets/Twinsanity/Scenes/TitleScreen.unity";
        if (File.Exists(scenePath) && !EditorUtility.DisplayDialog("Replace title scene?", "This will replace the existing TitleScreen.unity scene.", "Replace", "Cancel"))
            return;

        Directory.CreateDirectory("Assets/Twinsanity/Scenes");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Camera camera = Camera.main;
        if (camera != null)
            camera.backgroundColor = Color.black;

        GameObject title = new GameObject("Twinsanity Title Screen");
        title.AddComponent<AudioSource>().playOnAwake = false;
        TwinsanityTitleScreen screen = title.AddComponent<TwinsanityTitleScreen>();
        screen.crashFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Twinsanity/Fonts/Crash-Euro.ttf");
        screen.startSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Twinsanity/Audio/Frontend_8000.wav");
        screen.moveSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Twinsanity/Audio/Frontend_8001.wav");
        screen.selectSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Twinsanity/Audio/Frontend_8002.wav");

        GameObject logo = TwinsanityLogoBuilder.BuildLogo();
        if (logo != null)
        {
            logo.transform.position = new Vector3(0, 0, 1000);
            foreach (MeshRenderer renderer in logo.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform.parent.name == "GameLogo_FlashLines" || renderer.transform.parent.name == "GameLogo_FlashBG")
                    renderer.enabled = false;
            }

            const string texturePath = "Assets/Twinsanity/Logo/Generated/LogoRenderTexture.asset";
            AssetDatabase.DeleteAsset(texturePath);
            RenderTexture logoTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) { name = "GameLogo Render" };
            AssetDatabase.CreateAsset(logoTexture, texturePath);

            GameObject logoCameraObject = new GameObject("GameLogo Camera");
            Camera logoCamera = logoCameraObject.AddComponent<Camera>();
            logoCamera.orthographic = true;
            logoCamera.orthographicSize = 6.2f;
            logoCamera.nearClipPlane = 0.1f;
            logoCamera.farClipPlane = 100f;
            logoCamera.transform.position = new Vector3(0, 0, 1020);
            logoCamera.transform.rotation = Quaternion.Euler(0, 180, 0);
            logoCamera.backgroundColor = new Color(0, 0, 0, 0);
            logoCamera.clearFlags = CameraClearFlags.SolidColor;
            logoCamera.targetTexture = logoTexture;
            screen.originalLogo = logoTexture;
        }

        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        Selection.activeGameObject = title;
        Debug.Log("Twinsanity title created. Open Game view and press Play; Enter opens the menu.");
    }
}
