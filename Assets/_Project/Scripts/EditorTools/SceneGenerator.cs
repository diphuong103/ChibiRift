using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ChibiRift.Core;
using ChibiRift.Gameplay;
using ChibiRift.Meta;
using ChibiRift.Save;
using ChibiRift.Telemetry;
using ChibiRift.UI;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Builds the five scenes of SRS 27 as empty shells: no art, no level design, just the
    /// wiring needed for Boot to MainMenu to Hub to Run_01 to PostRun to Hub to be walkable.
    /// </summary>
    public static class SceneGenerator
    {
        private const string SceneRoot = "Assets/_Project/Scenes";

        /// <summary>Creates all five scenes and registers them in Build Settings.</summary>
        [MenuItem("ChibiRift/Setup/4. Generate Scenes")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(SceneRoot);

            CreateBootScene();
            CreateMainMenuScene();
            CreateHubScene();
            CreateRunScene();
            CreatePostRunScene();

            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Setup] Five scenes generated and registered in Build Settings.");
        }

        /// <summary>
        /// Boot: the composition root. Holds GameBootstrap plus the installers that let Save,
        /// Telemetry and Meta register themselves without Core referencing them.
        /// </summary>
        private static void CreateBootScene()
        {
            Scene scene = NewScene();

            var bootstrap = new GameObject("GameBootstrap");
            var bootstrapComponent = bootstrap.AddComponent<GameBootstrap>();

            // P1-01: the Boot scene supplies the control asset; InputReader is not a singleton.
            var bootstrapSo = new SerializedObject(bootstrapComponent);
            bootstrapSo.FindProperty("_controls").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(
                    "Assets/_Project/Settings/ChibiRiftControls.inputactions");
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            // Discovered by GameBootstrap via GetComponentsInChildren and run in Order.
            bootstrap.AddComponent<TelemetryServiceInstaller>(); // Order 10
            bootstrap.AddComponent<SaveServiceInstaller>();      // Order 20
            bootstrap.AddComponent<MetaServiceInstaller>();      // Order 30

            // P1 slice 2: CombatSystem needs BalanceConfig, which Core cannot reference. The
            // installer is how ChibiRift.Gameplay reaches the composition root (SRS 26).
            var gameplayInstaller = bootstrap.AddComponent<GameplayServiceInstaller>();
            var installerSo = new SerializedObject(gameplayInstaller);
            installerSo.FindProperty("_balanceConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ChibiRift.Data.BalanceConfig>(
                    "Assets/_Project/Data/BalanceConfig.asset");
            installerSo.ApplyModifiedPropertiesWithoutUndo();

            CreateCamera();
            SaveScene(scene, SceneNames.Boot);
        }

        /// <summary>MainMenu: Play, Settings, How to Play, Quit. No Continue (SAVE-006).</summary>
        private static void CreateMainMenuScene()
        {
            Scene scene = NewScene();
            CreateCamera();
            Transform canvas = CreateCanvasWithEventSystem();

            var controllerObject = new GameObject("MainMenuController");
            var controller = controllerObject.AddComponent<MainMenuController>();

            Button play = CreateButton(canvas, "PlayButton", "Play", new Vector2(0f, 90f));
            Button settings = CreateButton(canvas, "SettingsButton", "Settings", new Vector2(0f, 30f));
            Button howToPlay = CreateButton(canvas, "HowToPlayButton", "How to Play", new Vector2(0f, -30f));
            Button quit = CreateButton(canvas, "QuitButton", "Quit", new Vector2(0f, -90f));

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_playButton").objectReferenceValue = play;
            serialized.FindProperty("_settingsButton").objectReferenceValue = settings;
            serialized.FindProperty("_howToPlayButton").objectReferenceValue = howToPlay;
            serialized.FindProperty("_quitButton").objectReferenceValue = quit;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SaveScene(scene, SceneNames.MainMenu);
        }

        /// <summary>Hub: wallet, hero selection and Start Run (SRS 17).</summary>
        private static void CreateHubScene()
        {
            Scene scene = NewScene();
            CreateCamera();
            Transform canvas = CreateCanvasWithEventSystem();

            var hubObject = new GameObject("HubController");
            hubObject.AddComponent<HubController>();
            var hubUi = hubObject.AddComponent<HubUiController>();

            Button startRun = CreateButton(canvas, "StartRunButton", "Start Run", new Vector2(0f, 30f));

            var serialized = new SerializedObject(hubUi);
            serialized.FindProperty("_startRunButton").objectReferenceValue = startRun;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AddSceneLink(canvas, "BackToMenuButton", "Back to Main Menu", new Vector2(0f, -40f), SceneNames.MainMenu);

            SaveScene(scene, SceneNames.Hub);
        }

        /// <summary>
        /// Run_01 placeholder. <see cref="RunSceneBuilder"/> replaces this with the real P1
        /// slice 1 arena; the link to PostRun is preserved there.
        /// </summary>
        private static void CreateRunScene()
        {
            Scene scene = NewScene();
            CreateCamera();
            Transform canvas = CreateCanvasWithEventSystem();

            AddSceneLink(canvas, "EndRunButton", "End Run (placeholder)", new Vector2(0f, 0f), SceneNames.PostRun);

            SaveScene(scene, SceneNames.Run01);
        }

        /// <summary>PostRun: summary and the single reward commit, then back to Hub (RUN-007).</summary>
        private static void CreatePostRunScene()
        {
            Scene scene = NewScene();
            CreateCamera();
            Transform canvas = CreateCanvasWithEventSystem();

            var panelObject = new GameObject("PostRunPanel");
            var panel = panelObject.AddComponent<PostRunPanel>();

            Button returnToHub = CreateButton(canvas, "ReturnToHubButton", "Return to Hub", new Vector2(0f, 0f));

            var serialized = new SerializedObject(panel);
            serialized.FindProperty("_returnToHubButton").objectReferenceValue = returnToHub;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            SaveScene(scene, SceneNames.PostRun);
        }

        private static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < SceneNames.All.Length; i++)
            {
                string path = $"{SceneRoot}/{SceneNames.All[i]}.unity";
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            // Boot must be index 0 so a player build starts at the composition root.
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Scene NewScene()
            => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static void SaveScene(Scene scene, string sceneName)
            => EditorSceneManager.SaveScene(scene, $"{SceneRoot}/{sceneName}.unity");

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.09f, 0.12f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            return camera;
        }

        private static Transform CreateCanvasWithEventSystem()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); // SRS 28.1 Target Spec

            // InputSystemUIInputModule, not StandaloneInputModule: the project uses the Input
            // System package and the legacy Input Manager is disabled.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            return canvasObject.transform;
        }

        private static void AddSceneLink(
            Transform canvas, string objectName, string label, Vector2 position, string targetScene)
        {
            Button button = CreateButton(canvas, objectName, label, position);

            var linkObject = new GameObject($"{objectName}_Link");
            var link = linkObject.AddComponent<SceneNavigationButton>();

            var serialized = new SerializedObject(link);
            serialized.FindProperty("_button").objectReferenceValue = button;
            serialized.FindProperty("_targetScene").stringValue = targetScene;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button CreateButton(Transform parent, string objectName, string label, Vector2 anchoredPosition)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = (RectTransform)buttonObject.transform;
            rect.sizeDelta = new Vector2(280f, 48f);
            rect.anchoredPosition = anchoredPosition;

            buttonObject.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.32f, 1f);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);

            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.font = GetBuiltinFont();

            return buttonObject.GetComponent<Button>();
        }

        private static Font GetBuiltinFont()
        {
            // Unity 6 renamed the built-in font; fall back for older editors.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
