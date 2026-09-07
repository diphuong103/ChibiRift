using UnityEngine;

namespace ChibiRift.Core
{
    /// <summary>
    /// Names and cached indices of the Physics2D layers required by the collision matrix.
    /// Kept here so no gameplay script ever passes a raw layer index. The matrix itself and the
    /// rationale for each pairing are documented in README.md.
    /// </summary>
    public static class GameLayers
    {
        public const string Player = "Player";
        public const string PlayerHitbox = "PlayerHitbox";
        public const string Enemy = "Enemy";
        public const string EnemyHitbox = "EnemyHitbox";
        public const string ProjectilePlayer = "Projectile_Player";
        public const string ProjectileEnemy = "Projectile_Enemy";
        public const string Ground = "Ground";
        public const string Boundary = "Boundary";
        public const string Pickup = "Pickup";

        /// <summary>Every project-defined layer, in the order they occupy slots 6..14.</summary>
        public static readonly string[] All =
        {
            Player, PlayerHitbox, Enemy, EnemyHitbox,
            ProjectilePlayer, ProjectileEnemy, Ground, Boundary, Pickup
        };

        /// <summary>Layer index, or -1 when the layer is missing from TagManager.</summary>
        public static int IndexOf(string layerName) => LayerMask.NameToLayer(layerName);

        /// <summary>Single-layer mask for <paramref name="layerName"/>.</summary>
        public static int MaskOf(string layerName) => 1 << LayerMask.NameToLayer(layerName);

        /// <summary>Mask covering everything a character treats as solid ground or a wall (MOV-004, MOV-007).</summary>
        public static int SolidWorldMask => LayerMask.GetMask(Ground, Boundary);
    }
}
