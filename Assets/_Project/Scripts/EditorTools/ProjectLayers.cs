using UnityEditor;
using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.EditorTools
{
    /// <summary>
    /// Writes the Physics2D layers and the collision matrix required by the project.
    /// Editor-only tooling: this assembly is excluded from every build.
    /// </summary>
    public static class ProjectLayers
    {
        /// <summary>First user layer slot. 0-5 are Unity built-ins, and 3, 6 and 7 are reserved blanks.</summary>
        public const int FirstUserLayer = 6;

        /// <summary>Creates the nine project layers at slots 6 to 14.</summary>
        [MenuItem("ChibiRift/Setup/1. Create Physics Layers")]
        public static void CreateLayers()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[Setup] TagManager.asset not found.");
                return;
            }

            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 0; i < GameLayers.All.Length; i++)
            {
                int slot = FirstUserLayer + i;
                if (slot >= layers.arraySize)
                {
                    Debug.LogError($"[Setup] Layer slot {slot} is out of range.");
                    break;
                }

                layers.GetArrayElementAtIndex(slot).stringValue = GameLayers.All[i];
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Setup] Created {GameLayers.All.Length} layers at slots {FirstUserLayer}-{FirstUserLayer + GameLayers.All.Length - 1}.");
        }

        /// <summary>
        /// Configures the Physics2D collision matrix. Pairs among the project layers are set
        /// explicitly; interactions with the Unity built-in layers 0-5 are left untouched so
        /// nothing outside the project's own model is silently disabled.
        /// The rationale for each pairing is documented in README.md.
        /// </summary>
        [MenuItem("ChibiRift/Setup/2. Configure Collision Matrix")]
        public static void ConfigureCollisionMatrix()
        {
            int player = LayerMask.NameToLayer(GameLayers.Player);
            int playerHitbox = LayerMask.NameToLayer(GameLayers.PlayerHitbox);
            int enemy = LayerMask.NameToLayer(GameLayers.Enemy);
            int enemyHitbox = LayerMask.NameToLayer(GameLayers.EnemyHitbox);
            int projectilePlayer = LayerMask.NameToLayer(GameLayers.ProjectilePlayer);
            int projectileEnemy = LayerMask.NameToLayer(GameLayers.ProjectileEnemy);
            int ground = LayerMask.NameToLayer(GameLayers.Ground);
            int boundary = LayerMask.NameToLayer(GameLayers.Boundary);
            int pickup = LayerMask.NameToLayer(GameLayers.Pickup);

            int[] all =
            {
                player, playerHitbox, enemy, enemyHitbox,
                projectilePlayer, projectileEnemy, ground, boundary, pickup
            };

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] < 0)
                {
                    Debug.LogError("[Setup] Run 'Create Physics Layers' before configuring the matrix.");
                    return;
                }
            }

            // Start from "nothing among our own layers collides", then switch on only the pairs
            // the design actually needs. Being explicit beats inheriting Unity's all-on default.
            for (int i = 0; i < all.Length; i++)
            {
                for (int j = i; j < all.Length; j++)
                {
                    Physics2D.IgnoreLayerCollision(all[i], all[j], true);
                }
            }

            // Bodies against the world (MOV-004, MOV-007).
            Enable(player, ground);
            Enable(player, boundary);
            Enable(enemy, ground);
            Enable(enemy, boundary);

            // Enemies push each other apart instead of stacking into one silhouette,
            // which serves the Readable Chaos pillar of SRS 5.
            Enable(enemy, enemy);

            // Attack volumes reach only the opposing body (COM-004). A hitbox never touches
            // the world, a projectile, a pickup, or another hitbox.
            Enable(playerHitbox, enemy);
            Enable(enemyHitbox, player);

            // Projectiles hit the opposing body and stop at the world, never their owner and
            // never each other.
            Enable(projectilePlayer, enemy);
            Enable(projectilePlayer, ground);
            Enable(projectilePlayer, boundary);
            Enable(projectileEnemy, player);
            Enable(projectileEnemy, ground);
            Enable(projectileEnemy, boundary);

            // Player bodies pass through enemy bodies: contact damage is delivered by
            // EnemyHitbox, so solid enemy bodies would only shove the hero around.
            // Pickups are collected by the player and rest on the world.
            Enable(pickup, player);
            Enable(pickup, ground);
            Enable(pickup, boundary);

            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Physics2D collision matrix configured.");
        }

        private static void Enable(int layerA, int layerB) => Physics2D.IgnoreLayerCollision(layerA, layerB, false);
    }
}
