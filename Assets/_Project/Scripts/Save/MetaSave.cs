using System;
using System.Collections.Generic;

namespace ChibiRift.Save
{
    /// <summary>One permanent upgrade and the level bought so far (META-004, META-006).</summary>
    [Serializable]
    public sealed class MetaUpgradeEntry
    {
        /// <summary>Upgrade id, matching the MetaUpgradeData asset.</summary>
        public string Id = string.Empty;

        /// <summary>Levels purchased. Capped by the cost curve (SRS 43 Q8).</summary>
        public int Level;
    }

    /// <summary>Player settings, persisted alongside progression (SAVE-002, SRS 19.4).</summary>
    [Serializable]
    public sealed class SettingsSave
    {
        /// <summary>Master bus volume, 0..1.</summary>
        public float MasterVolume = 1f;

        /// <summary>Music bus volume, 0..1.</summary>
        public float MusicVolume = 1f;

        /// <summary>SFX bus volume, 0..1.</summary>
        public float SfxVolume = 1f;

        /// <summary>Fullscreen or windowed.</summary>
        public bool Fullscreen = true;

        /// <summary>Horizontal resolution in pixels.</summary>
        public int ResolutionWidth = 1920;

        /// <summary>Vertical resolution in pixels.</summary>
        public int ResolutionHeight = 1080;

        /// <summary>Local telemetry toggle (TEL-004). SRS gives no default; on is assumed.</summary>
        public bool TelemetryEnabled = true;
    }

    /// <summary>
    /// Everything that survives between Runs (SRS 23, META-006).
    /// Serialized with <c>JsonUtility</c>, so every field is public and concrete.
    /// <see cref="Version"/> exists so a future schema change can migrate rather than discard.
    /// </summary>
    [Serializable]
    public sealed class MetaSave
    {
        /// <summary>Schema version of the file currently supported by the game.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Version the file was written with. Drives migration and the SAVE-004 fallback.</summary>
        public int Version = CurrentVersion;

        /// <summary>Gold in the wallet. Never lost on death (SRS 16).</summary>
        public int Gold;

        /// <summary>Gems in the wallet. Never lost on death (SRS 16).</summary>
        public int Gems;

        /// <summary>Heroes the player owns (HER-005).</summary>
        public List<string> UnlockedHeroIds = new List<string>();

        /// <summary>Permanent upgrade levels (META-001 to META-004, META-006).</summary>
        public List<MetaUpgradeEntry> MetaUpgrades = new List<MetaUpgradeEntry>();

        /// <summary>Settings (SAVE-002).</summary>
        public SettingsSave Settings = new SettingsSave();

        /// <summary>Unix timestamp of the last write, for diagnostics.</summary>
        public long LastSavedUtcTicks;

        /// <summary>A fresh profile. Also the SAVE-004 fallback when a file cannot be read.</summary>
        public static MetaSave CreateDefault() => new MetaSave
        {
            Version = CurrentVersion,
            Gold = 0,
            Gems = 0,
            UnlockedHeroIds = new List<string>(),
            MetaUpgrades = new List<MetaUpgradeEntry>(),
            Settings = new SettingsSave(),
            LastSavedUtcTicks = DateTime.UtcNow.Ticks
        };

        /// <summary>
        /// Basic sanity check (NFR-009: "không tin tuyệt đối dữ liệu runtime").
        /// A file failing this is treated as corrupt, so SAVE-003 never lets bad state overwrite good.
        /// </summary>
        public bool IsValid()
        {
            if (Version <= 0 || Version > CurrentVersion) return false;
            if (Gold < 0 || Gems < 0) return false;
            if (UnlockedHeroIds == null || MetaUpgrades == null || Settings == null) return false;

            for (int i = 0; i < MetaUpgrades.Count; i++)
            {
                MetaUpgradeEntry entry = MetaUpgrades[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Level < 0) return false;
            }

            return true;
        }

        /// <summary>Level owned of a permanent upgrade (META-004).</summary>
        public int GetUpgradeLevel(string id)
        {
            for (int i = 0; i < MetaUpgrades.Count; i++)
            {
                if (MetaUpgrades[i] != null && MetaUpgrades[i].Id == id) return MetaUpgrades[i].Level;
            }
            return 0;
        }

        /// <summary>Sets the level owned of a permanent upgrade (META-006).</summary>
        public void SetUpgradeLevel(string id, int level)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            for (int i = 0; i < MetaUpgrades.Count; i++)
            {
                if (MetaUpgrades[i] != null && MetaUpgrades[i].Id == id)
                {
                    MetaUpgrades[i].Level = level;
                    return;
                }
            }

            MetaUpgrades.Add(new MetaUpgradeEntry { Id = id, Level = level });
        }
    }
}
