using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Data
{
    /// <summary>
    /// Base for every content asset. Guarantees the unique-ID rule that UPG-001 states for
    /// upgrades and that SRS 23 implies for all entities, and gives SRS 34 ("stat/skill quan
    /// trọng phải có min/max hoặc validation") one place to hang validation.
    /// </summary>
    public abstract class GameDataAsset : ScriptableObject
    {
        protected const string LogCategory = "Data";

        [Header("Identity")]
        [Tooltip("Stable unique id. Referenced by save files and telemetry, so never rename it after release.")]
        [SerializeField] private string _id = string.Empty;

        [Tooltip("Player-facing name (UPG-002).")]
        [SerializeField] private string _displayName = string.Empty;

        [TextArea(2, 5)]
        [Tooltip("Player-facing description (UPG-002, UPG-007).")]
        [SerializeField] private string _description = string.Empty;

        /// <summary>Stable unique id (UPG-001).</summary>
        public string Id => _id;

        /// <summary>Player-facing name (UPG-002).</summary>
        public string DisplayName => _displayName;

        /// <summary>Player-facing description (UPG-002).</summary>
        public string Description => _description;

        /// <summary>False when the id is missing, which must block release (SRS 30).</summary>
        public bool HasValidId => !string.IsNullOrWhiteSpace(_id);

        /// <summary>
        /// Editor-time validation. Derived assets override this and call base first.
        /// Warns on an empty id and on any id already used by another asset of the same type.
        /// </summary>
        protected virtual void OnValidate()
        {
#if UNITY_EDITOR
            if (!HasValidId)
            {
                GameLog.Warn(LogCategory, $"{GetType().Name} '{name}' has an empty Id (UPG-001).");
                return;
            }

            WarnOnDuplicateId();
#endif
        }

#if UNITY_EDITOR
        private void WarnOnDuplicateId()
        {
            string thisPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{GetType().Name}");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path == thisPath) continue;

                var other = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataAsset>(path);
                if (other == null || other.Id != _id) continue;

                GameLog.Warn(LogCategory,
                    $"Duplicate Id '{_id}' on {GetType().Name}: '{name}' and '{other.name}' (UPG-001).");
                return;
            }
        }
#endif
    }
}
