using System.IO;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ChibiRift.Core;
using ChibiRift.Data;
using ChibiRift.Gameplay;
using ChibiRift.UI;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Builds the Run_01 test arena for P1 slice 1: colliders, spawn point, camera rig and the
    /// hero prefab. Geometry only, in flat-colour sprites. No art, tilemap or level design.
    /// </summary>
    public static class RunSceneBuilder
    {
        private const string SceneRoot = "Assets/_Project/Scenes";
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const string DataRoot = "Assets/_Project/Data";

        // Technical constants from the P1 slice 1 brief.
        private const int PixelsPerUnit = 32;
        private const int ReferenceWidth = 640;
        private const int ReferenceHeight = 360;
        private const float OrthographicSize = 5.625f;

        // Arena layout from the brief.
        private const float ArenaHalfWidth = 20f;
        private const float GroundY = -0.5f;
        private const float HoleCentreX = -8f;
        private const float HoleWidth = 3f;

        [MenuItem("ChibiRift/Setup/5. Build Run_01 Arena")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabRoot);

            GameObject heroPrefab = BuildHeroPrefab();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var context = new GameObject("SceneContext").AddComponent<SceneContext>();
            Transform spawn = new GameObject("SpawnPoint").transform;
            spawn.position = new Vector3(0f, 1f, 0f);

            BuildGeometry();
            Transform hero = BuildHero(heroPrefab, context, spawn);
            BuildCamera(hero);

            new GameObject("DebugOverlay").AddComponent<DebugOverlay>();
            BuildTrainingDummies();
            BuildDamageNumberCanvas();
            BuildPostRunLink();

            var contextSo = new SerializedObject(context);
            contextSo.FindProperty("_worldHalfWidth").floatValue = ArenaHalfWidth;
            contextSo.FindProperty("_fallLimitY").floatValue = -10f;
            contextSo.FindProperty("_spawnPoint").objectReferenceValue = spawn;
            contextSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, $"{SceneRoot}/{SceneNames.Run01}.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Run_01 arena built.");
        }

        /// <summary>
        /// Keeps the Run to PostRun leg walkable. Rebuilding this scene from scratch would
        /// otherwise drop the navigation the foundation slice put here.
        /// </summary>
        private static void BuildPostRunLink()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var buttonObject = new GameObject("EndRunButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);

            var rect = (RectTransform)buttonObject.transform;
            rect.sizeDelta = new Vector2(200f, 32f);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            buttonObject.GetComponent<Image>().color = new Color(0.22f, 0.24f, 0.32f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<Text>();
            label.text = "End Run (placeholder)";
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var link = new GameObject("EndRunButton_Link").AddComponent<SceneNavigationButton>();
            var linkSo = new SerializedObject(link);
            linkSo.FindProperty("_button").objectReferenceValue = buttonObject.GetComponent<Button>();
            linkSo.FindProperty("_targetScene").stringValue = SceneNames.PostRun;
            linkSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Ground split around a 3u hole at x = -8, two walls, and one platform.</summary>
        private static void BuildGeometry()
        {
            int ground = LayerMask.NameToLayer(GameLayers.Ground);
            int boundary = LayerMask.NameToLayer(GameLayers.Boundary);

            // The hole is cut by building the floor as two slabs rather than one.
            float holeLeft = HoleCentreX - HoleWidth / 2f;
            float holeRight = HoleCentreX + HoleWidth / 2f;

            float leftWidth = holeLeft - (-ArenaHalfWidth);
            CreateBox("Ground_Left", new Vector2(-ArenaHalfWidth + leftWidth / 2f, GroundY),
                new Vector2(leftWidth, 1f), ground, new Color(0.30f, 0.34f, 0.40f));

            float rightWidth = ArenaHalfWidth - holeRight;
            CreateBox("Ground_Right", new Vector2(holeRight + rightWidth / 2f, GroundY),
                new Vector2(rightWidth, 1f), ground, new Color(0.30f, 0.34f, 0.40f));

            CreateBox("Wall_Left", new Vector2(-ArenaHalfWidth, 0f), new Vector2(1f, 20f),
                boundary, new Color(0.22f, 0.24f, 0.30f));
            CreateBox("Wall_Right", new Vector2(ArenaHalfWidth, 0f), new Vector2(1f, 20f),
                boundary, new Color(0.22f, 0.24f, 0.30f));

            CreateBox("Platform", new Vector2(6f, 3f), new Vector2(6f, 0.5f),
                ground, new Color(0.36f, 0.42f, 0.34f));
        }

        private static Transform BuildHero(GameObject prefab, SceneContext context, Transform spawn)
        {
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            hero.name = "Hero";
            hero.transform.position = spawn.position;

            var motorSo = new SerializedObject(hero.GetComponent<PlayerMotor>());
            motorSo.FindProperty("_sceneContext").objectReferenceValue = context;
            motorSo.ApplyModifiedPropertiesWithoutUndo();

            return hero.transform;
        }

        /// <summary>Main Camera with Pixel Perfect, plus a Cinemachine rig confined to the arena.</summary>
        private static void BuildCamera(Transform target)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.09f, 0.12f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
            pixelPerfect.assetsPPU = PixelsPerUnit;
            pixelPerfect.refResolutionX = ReferenceWidth;
            pixelPerfect.refResolutionY = ReferenceHeight;
            // 'Upscale Render Texture' from the brief; upscaleRT itself is deprecated.
            pixelPerfect.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;

            cameraObject.AddComponent<CinemachineBrain>();

            var vcamObject = new GameObject("CM_Follow");
            var vcam = vcamObject.AddComponent<CinemachineCamera>();
            vcam.Lens.OrthographicSize = OrthographicSize;

            var composer = vcamObject.AddComponent<CinemachinePositionComposer>();
            var confiner = vcamObject.AddComponent<CinemachineConfiner2D>();

            // CAM-002: the bounding shape lives on its own object, never on hero or ground.
            var confinerObject = new GameObject("CameraConfiner");
            var polygon = confinerObject.AddComponent<PolygonCollider2D>();
            polygon.isTrigger = true;
            polygon.points = new[]
            {
                new Vector2(-ArenaHalfWidth, 0f),
                new Vector2(ArenaHalfWidth, 0f),
                new Vector2(ArenaHalfWidth, 12f),
                new Vector2(-ArenaHalfWidth, 12f)
            };
            confiner.BoundingShape2D = polygon;

            var rig = vcamObject.AddComponent<CameraRig>();
            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("_camera").objectReferenceValue = vcam;
            rigSo.FindProperty("_composer").objectReferenceValue = composer;
            rigSo.FindProperty("_confiner").objectReferenceValue = confiner;
            rigSo.FindProperty("_cameraConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<CameraConfig>($"{DataRoot}/CameraConfig.asset");
            rigSo.ApplyModifiedPropertiesWithoutUndo();

            rig.SetFollowTarget(target);
        }

        /// <summary>Hero prefab: capsule collider ~0.8 x 1.8u, flat sprite, motor and controller.</summary>
        private static GameObject BuildHeroPrefab()
        {
            var hero = new GameObject("Hero");
            hero.layer = LayerMask.NameToLayer(GameLayers.Player);

            var renderer = hero.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateFlatSprite(new Color(0.85f, 0.72f, 0.35f));
            // Hero is 64px tall at 32 PPU, i.e. 2 units.
            renderer.transform.localScale = new Vector3(1f, 2f, 1f);

            var body = hero.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var capsule = hero.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.8f, 1.8f);

            HeroData heroData = AssetDatabase.LoadAssetAtPath<HeroData>($"{DataRoot}/HERO_Knight.asset");
            BalanceConfig balance = AssetDatabase.LoadAssetAtPath<BalanceConfig>($"{DataRoot}/BalanceConfig.asset");

            var motor = hero.AddComponent<PlayerMotor>();
            hero.AddComponent<PlayerController>();

            // Health before stats: PlayerStats requires it and seeds it on Awake.
            var health = hero.AddComponent<HealthComponent>();
            var stats = hero.AddComponent<PlayerStats>();
            var combat = hero.AddComponent<PlayerCombat>();

            var motorSo = new SerializedObject(motor);
            motorSo.FindProperty("_heroData").objectReferenceValue = heroData;
            motorSo.ApplyModifiedPropertiesWithoutUndo();

            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("_isPlayer").boolValue = true;
            healthSo.FindProperty("_bodyCollider").objectReferenceValue = capsule;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var statsSo = new SerializedObject(stats);
            statsSo.FindProperty("_heroData").objectReferenceValue = heroData;
            statsSo.FindProperty("_balanceConfig").objectReferenceValue = balance;
            statsSo.ApplyModifiedPropertiesWithoutUndo();

            var combatSo = new SerializedObject(combat);
            combatSo.FindProperty("_heroData").objectReferenceValue = heroData;
            // COM-009: PlayerCombat is the only writer of flipX from slice 2 on (OI-20).
            combatSo.FindProperty("_spriteRenderer").objectReferenceValue = renderer;
            combatSo.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PrefabRoot}/Hero.prefab";
            AssetDatabase.DeleteAsset(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(hero, path);
            Object.DestroyImmediate(hero);

            return prefab;
        }

        /// <summary>
        /// Three inert targets at x = 3, 8 and 12 (P1 slice 2). Enemy AI is slice 3, but combat
        /// needs something to hit before then. The middle one is fragile so death, the corpse
        /// timer and the once-only EntityDiedEvent can be exercised by hand.
        /// </summary>
        private static void BuildTrainingDummies()
        {
            EnemyData sturdy =
                AssetDatabase.LoadAssetAtPath<EnemyData>($"{DataRoot}/ENM_TrainingDummy.asset");
            EnemyData fragile =
                AssetDatabase.LoadAssetAtPath<EnemyData>($"{DataRoot}/ENM_TrainingDummyFragile.asset");

            CreateDummy("Dummy_A", 3f, sturdy, new Color(0.55f, 0.30f, 0.32f));
            CreateDummy("Dummy_B_Fragile", 8f, fragile, new Color(0.72f, 0.40f, 0.30f));
            CreateDummy("Dummy_C", 12f, sturdy, new Color(0.55f, 0.30f, 0.32f));
        }

        private static void CreateDummy(string name, float x, EnemyData data, Color colour)
        {
            var dummy = new GameObject(name) { layer = LayerMask.NameToLayer(GameLayers.Enemy) };
            dummy.transform.position = new Vector3(x, 1f, 0f);

            var renderer = dummy.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateFlatSprite(colour);
            renderer.transform.localScale = new Vector3(1f, 2f, 1f);

            // Static: no Rigidbody2D, no AI, no retaliation. It exists to be hit.
            var box = dummy.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;

            var health = dummy.AddComponent<HealthComponent>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("_sourceData").objectReferenceValue = data;
            healthSo.FindProperty("_isPlayer").boolValue = false;
            healthSo.FindProperty("_bodyCollider").objectReferenceValue = box;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Screen-space canvas hosting the floating damage numbers (HPS-008).</summary>
        private static void BuildDamageNumberCanvas()
        {
            var root = new GameObject("DamageNumberCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);

            GameObject prefab = BuildDamageNumberPrefab();

            var spawner = root.AddComponent<DamageNumberSpawner>();
            var spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("_prefab").objectReferenceValue = prefab.GetComponent<DamageNumber>();
            spawnerSo.FindProperty("_container").objectReferenceValue = root.GetComponent<RectTransform>();
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject BuildDamageNumberPrefab()
        {
            var number = new GameObject("DamageNumber", typeof(RectTransform));

            var label = number.AddComponent<Text>();
            label.font = AssetDatabase.GetBuiltinExtraResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "0";

            var view = number.AddComponent<DamageNumber>();
            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_label").objectReferenceValue = label;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PrefabRoot}/DamageNumber.prefab";
            AssetDatabase.DeleteAsset(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(number, path);
            Object.DestroyImmediate(number);

            return prefab;
        }

        private static void CreateBox(string name, Vector2 centre, Vector2 size, int layer, Color colour)
        {
            var box = new GameObject(name) { layer = layer };
            box.transform.position = centre;

            var renderer = box.AddComponent<SpriteRenderer>();
            renderer.sprite = CreateFlatSprite(colour);
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);

            box.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        /// <summary>A 1x1 unit white sprite tinted by the renderer. Placeholder geometry only.</summary>
        private static Sprite CreateFlatSprite(Color colour)
        {
            var texture = new Texture2D(PixelsPerUnit, PixelsPerUnit)
            {
                filterMode = FilterMode.Point
            };

            var pixels = new Color[PixelsPerUnit * PixelsPerUnit];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = colour;
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, PixelsPerUnit, PixelsPerUnit),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
        }
    }
}
