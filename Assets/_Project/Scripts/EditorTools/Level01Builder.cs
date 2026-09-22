using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using ChibiRift.Gameplay;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Replaces the placeholder tilemap and hero sprite in Run_01 with real Dark Forest art and Hero spritesheet.
    /// </summary>
    public static class Level01Builder
    {
        private const string HeroSheetPath  = "Assets/_Project/Art/Characters/Hero/Hero_Spritesheet.png";
        private const string HeroControllerPath = "Assets/_Project/Art/Characters/Hero/Animations/Hero.controller";
        private const string GroundSheetPath = "Assets/_Project/Art/Tileset/Ground/env_ground.png";
        private const string TileFolder     = "Assets/_Project/Art/Tileset/Ground/Tiles";
        private const string ScenePath      = "Assets/_Project/Scenes/Run_01.unity";
        private const string BgFolder       = "Assets/_Project/Art/Background";

        private const int GrassTopIndex = 67;  // row=2,col=7 (Grass top)
        private const int DirtFillIndex = 126; // row=4,col=6 (Solid dirt fill)

        [MenuItem("ChibiRift/Setup/9. Build Level 01 with Real Art")]
        public static void Build()
        {
            Debug.Log("[Level01Builder] Starting Level 01 build with real art...");

            // 1. Force slicing of all spritesheets first
            SpritesheetSlicer.ReimportKnownSpritesheets();

            // 2. Build Hero Animator clips, controller & update Hero prefab
            HeroAnimatorBuilder.Build();

            // 3. Ensure tile asset folder exists
            Directory.CreateDirectory(TileFolder);
            AssetDatabase.Refresh();

            // 4. Create Grass & Dirt tiles
            Tile grassTile = CreateTileAsset(GrassTopIndex, "Tile_GrassTop");
            Tile dirtTile  = CreateTileAsset(DirtFillIndex, "Tile_Dirt");

            if (grassTile == null || dirtTile == null)
            {
                Debug.LogError("[Level01Builder] Ground tile sprites not found. Make sure env_ground.png exists in Assets/_Project/Art/Tileset/Ground/");
                return;
            }

            // 5. Open Run_01 scene
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("[Level01Builder] Cannot open Run_01.unity");
                return;
            }

            // 6. Update Ground Tilemap & Physics Colliders
            Tilemap tilemap = FindTilemap();
            if (tilemap != null)
            {
                ReplaceTiles(tilemap, grassTile, dirtTile);
                TilemapRenderer tr = tilemap.GetComponent<TilemapRenderer>();
                if (tr != null)
                {
                    tr.sortingOrder = 0;
                    EditorUtility.SetDirty(tr);
                }
                tilemap.color = Color.white;
                tilemap.RefreshAllTiles();

                var tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
                if (tilemapCollider != null)
                {
                    tilemapCollider.ProcessTilemapChanges();
                    EditorUtility.SetDirty(tilemapCollider);
                }

                var composite = tilemap.GetComponent<CompositeCollider2D>();
                if (composite != null)
                {
                    composite.GenerateGeometry();
                    EditorUtility.SetDirty(composite);
                }

                EditorUtility.SetDirty(tilemap);
                if (tilemap.layoutGrid != null) EditorUtility.SetDirty(tilemap.layoutGrid.gameObject);
            }
            else
            {
                Debug.LogError("[Level01Builder] No Tilemap found in scene!");
            }

            // 7. Add Background layers
            AddBackgrounds();

            // 8. Fix Hero in scene (reset scale, position, collider, sprite & animator)
            FixSceneHero();

            // 9. Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Level01Builder] Level 01 successfully built with real Dark Forest art and Hero character!");
        }

        private static Tile CreateTileAsset(int spriteIndex, string name)
        {
            string spriteName = $"Ground_{spriteIndex}";
            Sprite sprite = null;
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(GroundSheetPath))
            {
                if (obj is Sprite s && s.name == spriteName) { sprite = s; break; }
            }

            if (sprite == null)
            {
                foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(GroundSheetPath))
                {
                    if (obj is Sprite s) { sprite = s; break; }
                }
            }

            if (sprite == null)
            {
                Debug.LogWarning($"[Level01Builder] '{spriteName}' not found in {GroundSheetPath}");
                return null;
            }

            string path = $"{TileFolder}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color  = Color.white;
            tile.colliderType = Tile.ColliderType.Grid;
            AssetDatabase.CreateAsset(tile, path);
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static Tilemap FindTilemap()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "Ground")
                {
                    Tilemap tm = root.GetComponentInChildren<Tilemap>();
                    if (tm != null) return tm;
                }
            }
            return Object.FindFirstObjectByType<Tilemap>();
        }

        private static void ReplaceTiles(Tilemap tilemap, Tile grassTile, Tile dirtTile)
        {
            tilemap.ClearAllTiles();

            const float AHW      = 20f;   // arena half-width
            const float GroundY  = -0.5f;
            const float HoleCX   = -8f;
            const float HoleW    = 3f;
            const float T        = 0.5f;  // tile world size (16px at 32 PPU = 0.5u)

            float holeLeft  = HoleCX - HoleW / 2f;
            float holeRight = HoleCX + HoleW / 2f;

            // Main floor — left slab (fill 4 rows deep so ground is thick and solid)
            PaintRect(tilemap, grassTile, dirtTile,
                new Vector2(-AHW, GroundY - 1.5f),
                new Vector2(holeLeft - (-AHW), 2f), T);

            // Main floor — right slab
            PaintRect(tilemap, grassTile, dirtTile,
                new Vector2(holeRight, GroundY - 1.5f),
                new Vector2(AHW - holeRight, 2f), T);

            // Centre platform
            PaintRect(tilemap, grassTile, dirtTile,
                new Vector2(3f, 3f), new Vector2(6f, 0.5f), T);

            // Left elevated ledge
            PaintRect(tilemap, grassTile, dirtTile,
                new Vector2(-14f, 1.5f), new Vector2(4f, 0.5f), T);

            // Right elevated ledge
            PaintRect(tilemap, grassTile, dirtTile,
                new Vector2(14f, 1.5f), new Vector2(4f, 0.5f), T);

            // High platform over the hole area
            PaintRect(tilemap, grassTile, dirtTile,
                new Vector2(-10f, 4.5f), new Vector2(3f, 0.5f), T);
        }

        private static void PaintRect(Tilemap tilemap, Tile grassTile, Tile dirtTile,
            Vector2 worldMin, Vector2 worldSize, float tileSize)
        {
            Grid grid = tilemap.layoutGrid;
            int cols = Mathf.Max(1, Mathf.RoundToInt(worldSize.x / tileSize));
            int rows = Mathf.Max(1, Mathf.RoundToInt(worldSize.y / tileSize));
            Vector3Int origin = grid.WorldToCell(new Vector3(worldMin.x, worldMin.y, 0f));

            for (int x = 0; x < cols; x++)
                for (int y = 0; y < rows; y++)
                    tilemap.SetTile(new Vector3Int(origin.x + x, origin.y + y, 0),
                        y == rows - 1 ? grassTile : dirtTile);
        }

        private static void AddBackgrounds()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "Backgrounds") { Object.DestroyImmediate(root); break; }
            }

            var bgRoot = new GameObject("Backgrounds");

            var layers = new (string file, int order, float yPos, float scaleX, float scaleY)[]
            {
                ("main_background.png", -10, 2.5f, 20f, 12f),
                ("top_far_bgrnd.png",    -9, 5.5f, 10f,  4f),
                ("bgrd_tree5.png",       -8, 0.5f,  4f,  4f),
                ("bgrd_tree4.png",       -7, 0.2f,  4f,  3.5f),
                ("bgrd_tree3.png",       -6, 0f,    4f,  3f),
                ("bgrd_tree2.png",       -5, -0.2f, 4f,  3f),
                ("bgrd_tree1.png",       -4, -0.2f, 4f,  3f),
            };

            foreach (var (file, order, yPos, scaleX, scaleY) in layers)
            {
                string path = $"{BgFolder}/{file}";
                EnsureSpriteImport(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                var go = new GameObject(Path.GetFileNameWithoutExtension(file));
                go.transform.SetParent(bgRoot.transform, false);
                go.transform.position = new Vector3(0f, yPos, 10f);
                go.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;
                sr.drawMode = SpriteDrawMode.Simple;
                EditorUtility.SetDirty(go);
            }

            EditorUtility.SetDirty(bgRoot);
        }

        private static void EnsureSpriteImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.textureType == TextureImporterType.Sprite) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void FixSceneHero()
        {
            GameObject hero = GameObject.Find("Hero");
            if (hero == null) return;

            // Reset transform scale and spawn position
            hero.transform.localScale = Vector3.one;
            GameObject spawnPoint = GameObject.Find("SpawnPoint");
            if (spawnPoint != null)
            {
                hero.transform.position = spawnPoint.transform.position;
            }
            else
            {
                hero.transform.position = new Vector3(0f, 1f, 0f);
            }

            // Load Hero_0 sprite
            Sprite heroSprite = null;
            foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(HeroSheetPath))
            {
                if (obj is Sprite s && s.name == "Hero_0") { heroSprite = s; break; }
            }

            // Update SpriteRenderer
            var sr = hero.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (heroSprite != null) sr.sprite = heroSprite;
                sr.drawMode = SpriteDrawMode.Simple;
                sr.color = Color.white;
                sr.sortingOrder = 5;
                EditorUtility.SetDirty(sr);
            }

            // Fix CapsuleCollider2D alignment to hero sprite
            var capsule = hero.GetComponent<CapsuleCollider2D>();
            if (capsule != null)
            {
                capsule.direction = CapsuleDirection2D.Vertical;
                capsule.size = new Vector2(0.8f, 1.6f);
                capsule.offset = new Vector2(0f, 0.8f);
                EditorUtility.SetDirty(capsule);
            }

            // Ensure Animator is configured
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HeroControllerPath);
            if (controller != null)
            {
                var animator = hero.GetComponent<Animator>();
                if (animator == null) animator = hero.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
            }

            // Ensure PlayerAnimatorDriver component exists
            if (hero.GetComponent<PlayerAnimatorDriver>() == null)
            {
                hero.AddComponent<PlayerAnimatorDriver>();
            }

            EditorUtility.SetDirty(hero);
        }
    }
}
