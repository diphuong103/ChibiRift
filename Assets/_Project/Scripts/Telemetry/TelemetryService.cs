using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using ChibiRift.Core;

namespace ChibiRift.Telemetry
{
    /// <summary>
    /// Local-only balance logging (SRS 23.2). Writes JSON Lines, one object per line, so a run
    /// can be appended to without rewriting the file and so the log survives a crash mid-session.
    /// </summary>
    /// <remarks>
    /// TEL-004: the file stays on disk under <see cref="Application.persistentDataPath"/>.
    /// Nothing is ever sent over the network, and the whole service can be switched off from
    /// Settings.
    /// TEL-005: records are buffered in memory and written in one batch, normally at Run end, so
    /// telemetry cannot show up in the frame time budget of NFR-001 and NFR-002.
    /// </remarks>
    public sealed class TelemetryService : ITelemetryService, IDisposable
    {
        private const string LogCategory = "Telemetry";
        private const string LogFileName = "telemetry.jsonl";

        /// <summary>Buffer ceiling. Reaching it forces an early flush so memory stays bounded.</summary>
        private const int MaxBufferedRecords = 512;

        private readonly List<string> _buffer = new List<string>();
        private readonly string _logPath;

        /// <inheritdoc />
        public bool IsEnabled { get; set; } = true;

        /// <summary>Full path of the log file, surfaced for QA and for the tests of TC-TEL.</summary>
        public string LogPath => _logPath;

        /// <summary>Records waiting to be written.</summary>
        public int BufferedCount => _buffer.Count;

        /// <param name="directory">Defaults to persistentDataPath; overridable so tests write to a temp folder.</param>
        public TelemetryService(string directory = null)
        {
            string root = string.IsNullOrWhiteSpace(directory) ? Application.persistentDataPath : directory;
            _logPath = Path.Combine(root, LogFileName);
        }

        /// <inheritdoc />
        public void Record(string eventType, string jsonPayload)
        {
            if (!IsEnabled) return; // TEL-004
            if (string.IsNullOrWhiteSpace(eventType)) return;

            // Buffer only. No disk access on the calling frame (TEL-005).
            var line = new StringBuilder(128);
            line.Append("{\"type\":\"").Append(eventType).Append("\",");
            line.Append("\"utcTicks\":").Append(DateTime.UtcNow.Ticks).Append(',');
            line.Append("\"data\":").Append(string.IsNullOrWhiteSpace(jsonPayload) ? "{}" : jsonPayload);
            line.Append('}');

            _buffer.Add(line.ToString());

            if (_buffer.Count >= MaxBufferedRecords) Flush();
        }

        /// <summary>Buffers a serializable record, the usual entry point for the typed logs.</summary>
        public void Record<T>(string eventType, T payload) where T : class
            => Record(eventType, payload == null ? "{}" : JsonUtility.ToJson(payload));

        /// <inheritdoc />
        public void Flush()
        {
            if (_buffer.Count == 0) return;

            if (!IsEnabled)
            {
                // Toggled off while records were pending: drop them rather than write (TEL-004).
                _buffer.Clear();
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(_logPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                File.AppendAllLines(_logPath, _buffer);
                GameLog.Info(LogCategory, $"Flushed {_buffer.Count} record(s) to {_logPath}.");
            }
            catch (Exception exception)
            {
                // Telemetry is diagnostic only: a failure here must never disturb gameplay.
                GameLog.Warn(LogCategory, $"Flush failed: {exception.Message}");
            }
            finally
            {
                _buffer.Clear();
            }
        }

        /// <summary>Flushes anything still buffered when the game shuts down.</summary>
        public void Dispose() => Flush();
    }
}
