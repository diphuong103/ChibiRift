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

            var overlay = new GameObject("DebugOverlay");
            overlay.AddComponent<DebugOverlay>();

            // Feeds the enemy half of the overlay. Lives in Gameplay because it has to see enemies,
            // which ChibiRift.UI cannot.
            overlay.AddComponent<EnemyDebugCensus>();
            BuildEnemies();
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

            // AI-002: EnemyAI locates its target by this tag. Without it every enemy stays Idle
            // forever and nothing in the arena reacts to the hero.
            hero.tag = "Player";

            // 1 x 2 units, the 64px-at-32-PPU figure from README section 2. Sized through the
            // renderer, never through Transform scale: this SpriteRenderer sits on the hero root,
            // so scaling its transform scaled the capsule and every offset measured from
            // transform.position along with it.
            SpriteRenderer renderer = AddPlaceholderSprite(
                hero, new Vector2(1f, 2f), new Color(0.85f, 0.72f, 0.35f));

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

            // HPS-005: the visible half of the i-frame window.
            var flash = hero.AddComponent<HurtFlash>();

            var motorSo = new SerializedObject(motor);
            motorSo.FindProperty("_heroData").objectReferenceValue = heroData;
            motorSo.ApplyModifiedPropertiesWithoutUndo();

            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("_isPlayer").boolValue = true;
            healthSo.FindProperty("_bodyCollider").objectReferenceValue = capsule;

            // HPS-005. Without this the shipped hero had no post-hit invulnerability at all: the
            // component supports it, but nothing had ever assigned the duration outside of tests.
            healthSo.FindProperty("_hurtIFrameDuration").floatValue =
                heroData != null ? heroData.HurtIFrameDuration : 0f;

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

            SetReferences(flash, ("_balanceConfig", balance), ("_spriteRenderer", renderer));

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

            AddPlaceholderSprite(dummy, new Vector2(1f, 2f), colour);

            // Static: no Rigidbody2D, no AI, no retaliation. It exists to be hit.
            var box = dummy.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 2f);

            // A trigger, not a solid body. Dummies sit on the Enemy layer, and Enemy collides with
            // Enemy, so solid dummies became walls: an enemy chasing the hero wedged itself behind
            // the dummy at x = 3 and crawled 0.6u in six seconds. Attack sweeps include triggers,
            // so the dummy still takes hits — it just stops being terrain.
            box.isTrigger = true;

            var health = dummy.AddComponent<HealthComponent>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("_sourceData").objectReferenceValue = data;
            healthSo.FindProperty("_isPlayer").boolValue = false;
            healthSo.FindProperty("_bodyCollider").objectReferenceValue = box;
            healthSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Three melee enemies at x = 5, 10 and 15 (P1 slice 3), from a prefab built entirely out
        /// of the EnemyData asset (NFR-007). They are placed above the ground and settle onto it.
        /// </summary>
        private static void BuildEnemies()
        {
            GameObject prefab = BuildEnemyPrefab();
            if (prefab == null) return;

            float[] positions = { 5f, 10f, 15f };
            for (int i = 0; i < positions.Length; i++)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                enemy.name = $"Enemy_{i + 1}";

                // Half the 2u body above the ground surface, so the ground probe finds ground on
                // the first fixed step instead of the enemy starting inside the floor.
                enemy.transform.position = new Vector3(positions[i], 1f, 0f);
            }
        }

        /// <summary>
        /// ENM_MeleeGrunt.prefab. Every number reaches the components through EnemyData, so
        /// retuning the enemy is an asset edit and never a script edit (AI-001, NFR-007).
        /// </summary>
        private static GameObject BuildEnemyPrefab()
        {
            EnemyData data =
                AssetDatabase.LoadAssetAtPath<EnemyData>($"{DataRoot}/ENM_MeleeGrunt.asset");
            BalanceConfig balance =
                AssetDatabase.LoadAssetAtPath<BalanceConfig>($"{DataRoot}/BalanceConfig.asset");

            if (data == null)
            {
                Debug.LogError("[Setup] ENM_MeleeGrunt.asset is missing; run the data generator first.");
                return null;
            }

            var enemy = new GameObject("ENM_MeleeGrunt")
            {
                layer = LayerMask.NameToLayer(GameLayers.Enemy)
            };

            AddPlaceholderSprite(enemy, new Vector2(1f, 2f), new Color(0.62f, 0.28f, 0.34f));

            var body = enemy.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var box = enemy.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.8f, 1.8f);

            // Ensure, not Add: these components declare RequireComponent on each other, so Unity
            // adds some of them for us. A second AddComponent of a DisallowMultipleComponent type
            // returns null, which then fails at the SerializedObject rather than at the cause.
            var health = Ensure<HealthComponent>(enemy);
            var motor = Ensure<EnemyMotor>(enemy);
            var attack = Ensure<EnemyAttack>(enemy);
            var ai = Ensure<EnemyAI>(enemy);
            var controller = Ensure<EnemyController>(enemy);

            SetReferences(health, ("_sourceData", data), ("_bodyCollider", box));
            SetReferences(motor, ("_enemyData", data));
            SetReferences(attack, ("_enemyData", data));
            SetReferences(ai, ("_enemyData", data), ("_balanceConfig", balance));
            SetReferences(controller, ("_enemyData", data));

            BuildEnemyHealthBar(enemy, balance);

            string path = $"{PrefabRoot}/ENM_MeleeGrunt.prefab";
            AssetDatabase.DeleteAsset(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(enemy, path);
            Object.DestroyImmediate(enemy);

            return prefab;
        }

        /// <summary>
        /// World-space health bar, added as a CHILD of the enemy. That parenting is what lets a
        /// component in ChibiRift.UI identify its owner by the parent's instance id without ever
        /// referencing ChibiRift.Gameplay, and makes it follow the enemy with no code.
        /// </summary>
        private static void BuildEnemyHealthBar(GameObject enemy, BalanceConfig balance)
        {
            var barRoot = new GameObject("HealthBar", typeof(Canvas));
            barRoot.transform.SetParent(enemy.transform, false);
            barRoot.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            var canvas = barRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var canvasRect = barRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1.2f, 0.16f);
            canvasRect.localScale = Vector3.one;

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(barRoot.transform, false);
            StretchToParent(background.GetComponent<RectTransform>());
            background.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 0.85f);

            var fillObject = new GameObject("Fill", typeof(Image));
            fillObject.transform.SetParent(barRoot.transform, false);
            StretchToParent(fillObject.GetComponent<RectTransform>());

            var fill = fillObject.GetComponent<Image>();
            fill.color = new Color(0.78f, 0.22f, 0.24f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;

            var bar = barRoot.AddComponent<EnemyHealthBar>();
            SetReferences(bar, ("_balanceConfig", balance), ("_canvas", canvas), ("_fill", fill));
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Returns the component, adding it only when it is not already there.</summary>
        private static T Ensure<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        /// <summary>Assigns several private serialized object references in one call.</summary>
        private static void SetReferences(Object target, params (string Field, Object Value)[] fields)
        {
            var so = new SerializedObject(target);
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = so.FindProperty(fields[i].Field);
                if (property == null)
                {
                    Debug.LogError($"[Setup] {target.GetType().Name} has no field '{fields[i].Field}'.");
                    continue;
                }

                property.objectReferenceValue = fields[i].Value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
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

            AddPlaceholderSprite(box, size, colour);
            box.AddComponent<BoxCollider2D>().size = size;
        }

        /// <summary>
        /// Adds a placeholder sprite of exactly <paramref name="size"/> world units, tinted to
        /// <paramref name="colour"/>.
        /// </summary>
        /// <remarks>
        /// Two rules are enforced here rather than left to each caller, because breaking either one
        /// produced a bug that shipped:
        /// <list type="bullet">
        ///   <item>The sprite is a real asset on disk. A sprite built in memory cannot be
        ///   serialised into a prefab and silently becomes None.</item>
        ///   <item>Size comes from <c>SpriteRenderer.size</c>, never from Transform scale. These
        ///   renderers sit on the actor root, so scaling the transform also scales the collider and
        ///   desynchronises every offset measured from <c>transform.position</c> — which is what
        ///   left the hero permanently airborne with its ground probe 0.9u inside its own body.</item>
        /// </list>
        /// </remarks>
        private static SpriteRenderer AddPlaceholderSprite(GameObject target, Vector2 size, Color colour)
        {
            var renderer = target.AddComponent<SpriteRenderer>();

            renderer.sprite = PlaceholderArt.LoadOrCreateWhiteSprite();
            renderer.color = colour;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;

            return renderer;
        }
    }
}
