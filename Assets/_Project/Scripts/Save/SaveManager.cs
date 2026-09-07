using System;
using System.IO;
using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Save
{
    /// <summary>
    /// Reads and writes <see cref="MetaSave"/> as JSON under
    /// <see cref="Application.persistentDataPath"/> (SAVE-001, SAVE-002).
    /// </summary>
    /// <remarks>
    /// Two rules from SRS 23.1 shape this class:
    /// SAVE-003, a bad state must never overwrite a good file, so the payload is validated
    /// before writing and the write is atomic: temp file first, then an OS-level replace that
    /// rolls the previous file into a backup.
    /// SAVE-004, a corrupt file must not crash the game, so loading falls back to the backup
    /// and then to a fresh default profile.
    /// </remarks>
    public sealed class SaveManager : ISaveService
    {
        private const string LogCategory = "Save";
        private const string SaveFileName = "meta_save.json";
        private const string BackupFileName = "meta_save.backup.json";
        private const string TempFileName = "meta_save.tmp";

        private readonly string _saveDirectory;

        /// <summary>The live profile. Never null: a failed load leaves the default in place.</summary>
        public MetaSave Data { get; private set; } = MetaSave.CreateDefault();

        /// <inheritdoc />
        public bool LoadedCleanly { get; private set; } = true;

        /// <summary>Full path of the save file, surfaced for diagnostics and tests.</summary>
        public string SavePath => Path.Combine(_saveDirectory, SaveFileName);

        /// <summary>Full path of the backup written by the last successful save (SAVE-004).</summary>
        public string BackupPath => Path.Combine(_saveDirectory, BackupFileName);

        private string TempPath => Path.Combine(_saveDirectory, TempFileName);

        /// <param name="saveDirectory">Defaults to persistentDataPath; overridable so tests write to a temp folder.</param>
        public SaveManager(string saveDirectory = null)
        {
            _saveDirectory = string.IsNullOrWhiteSpace(saveDirectory)
                ? Application.persistentDataPath
                : saveDirectory;
        }

        /// <inheritdoc />
        public void Load()
        {
            if (TryLoadFrom(SavePath, out MetaSave loaded))
            {
                Data = loaded;
                LoadedCleanly = true;
                return;
            }

            // SAVE-004: the main file was missing or unreadable, so try the backup.
            if (TryLoadFrom(BackupPath, out MetaSave backup))
            {
                GameLog.Warn(LogCategory, "Main save unreadable; recovered from backup (SAVE-004).");
                Data = backup;
                LoadedCleanly = false;
                return;
            }

            // SAVE-004: nothing usable on disk. Start clean rather than crash.
            GameLog.Warn(LogCategory, "No readable save found; starting from defaults (SAVE-004).");
            Data = MetaSave.CreateDefault();
            LoadedCleanly = File.Exists(SavePath) == false;
        }

        /// <inheritdoc />
        public void Save()
        {
            // SAVE-003: refuse to persist a payload that fails validation, so a bug in run
            // state cannot destroy a good profile.
            if (Data == null || !Data.IsValid())
            {
                GameLog.Error(LogCategory, "Refusing to save: the profile failed validation (SAVE-003, NFR-009).");
                return;
            }

            try
            {
                Directory.CreateDirectory(_saveDirectory);
                Data.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

                string json = JsonUtility.ToJson(Data, prettyPrint: true);

                // Atomic write: fill the temp file completely before touching the real one, so a
                // crash mid-write leaves the previous save intact (SAVE-003).
                File.WriteAllText(TempPath, json);

                if (File.Exists(SavePath))
                {
                    // Replace swaps the file in one operation and rolls the old one into the backup.
                    File.Replace(TempPath, SavePath, BackupPath);
                }
                else
                {
                    File.Move(TempPath, SavePath);
                }

                GameLog.Info(LogCategory, $"Saved to {SavePath}.");
            }
            catch (Exception exception)
            {
                GameLog.Error(LogCategory, $"Save failed: {exception.Message}");
                TryDeleteTemp();
            }
        }

        /// <inheritdoc />
        public void ResetToDefaults()
        {
            // SAVE-005: the confirmation prompt is the caller's responsibility; by the time this
            // runs the player has already agreed.
            Data = MetaSave.CreateDefault();
            LoadedCleanly = true;
            Save();
        }

        private bool TryLoadFrom(string path, out MetaSave result)
        {
            result = null;

            try
            {
                if (!File.Exists(path)) return false;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return false;

                MetaSave parsed = JsonUtility.FromJson<MetaSave>(json);
                if (parsed == null || !parsed.IsValid())
                {
                    GameLog.Warn(LogCategory, $"Save at '{path}' failed validation.");
                    return false;
                }

                result = parsed;
                return true;
            }
            catch (Exception exception)
            {
                GameLog.Warn(LogCategory, $"Could not read save at '{path}': {exception.Message}");
                return false;
            }
        }

        private void TryDeleteTemp()
        {
            try
            {
                if (File.Exists(TempPath)) File.Delete(TempPath);
            }
            catch (Exception exception)
            {
                GameLog.Warn(LogCategory, $"Could not clean up the temp save: {exception.Message}");
            }
        }
    }
}
