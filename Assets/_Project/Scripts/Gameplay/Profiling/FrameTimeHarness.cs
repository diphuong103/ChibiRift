using System.Collections;
using System.IO;
using UnityEngine;
using ChibiRift.Core;
using ChibiRift.Data;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// Spawns the NFR-001 crowd, samples every frame for a fixed stretch, and writes the
    /// distribution to disk (NFR-001, NFR-002).
    /// </summary>
    /// <remarks>
    /// <para><b>What it measures.</b> <c>Time.unscaledDeltaTime</c> per frame, with
    /// <see cref="BalanceConfig.StressEnemyCount"/> enemies alive and chasing, for
    /// <see cref="BalanceConfig.StressDurationSeconds"/>. Unscaled, so a hit stop is not counted as
    /// a slow frame — it is a deliberate freeze, not a performance problem.</para>
    ///
    /// <para><b>What it does not measure, and this matters.</b> The harness does not cast skills.
    /// The ultimate's cooldown is 15s and the sample is 10s, so a run would see one cast at most
    /// and the result would depend on whether it happened to land inside the window. Rather than
    /// produce a number that means something different every run, no skill is cast at all — so
    /// <b>the p99 here does not include the cost of an area skill sweeping a 3.5u radius</b>. That
    /// caveat is written into the report file itself, not only here.</para>
    ///
    /// <para><b>A headless run proves the harness works, not that the game is fast.</b> There is no
    /// renderer in batch mode, so the frame times are not the frame times a player would see. The
    /// test asserts the report is complete and never asserts a threshold. Running it in the editor
    /// on a real machine is the measurement that counts; README section 15 says how.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FrameTimeHarness : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the enemy count and the sample duration (NFR-001, NFR-002).")]
        [SerializeField] private BalanceConfig _balanceConfig;

        [Header("Wiring")]
        [Tooltip("Pool the stress enemies come from.")]
        [SerializeField] private EnemySpawner _spawner;

        [Tooltip("Enemies are scattered around this. Defaults to the hero.")]
        [SerializeField] private Transform _origin;

        /// <summary>Frame time above which a frame counts as a dropped one (NFR-002).</summary>
        public const float SlowFrameMs = 33f;

        /// <summary>Where reports are written, relative to the project folder.</summary>
        public const string ReportDirectory = "Logs";

        /// <summary>The last run's numbers, or null if none has finished.</summary>
        public FrameTimeReport LastReport { get; private set; }

        /// <summary>Path the last report was written to.</summary>
        public string LastReportPath { get; private set; }

        /// <summary>True while a run is sampling.</summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Runs one measurement end to end: spawn, sample, tidy up, write.
        /// </summary>
        public IEnumerator Run()
        {
            if (_balanceConfig == null || _spawner == null)
            {
                GameLog.Error("Profiling", "FrameTimeHarness needs a BalanceConfig and an EnemySpawner.");
                yield break;
            }

            IsRunning = true;

            int enemyCount = _balanceConfig.StressEnemyCount;
            float duration = _balanceConfig.StressDurationSeconds;

            SpawnCrowd(enemyCount);

            // One settled frame before sampling: the spawn frame itself is dominated by the
            // activation cost of the whole crowd and is not representative of anything.
            yield return null;

            long allocatedBefore = System.GC.GetTotalMemory(false);
            var samples = new float[Mathf.CeilToInt(duration * MaxPlausibleFps)];
            int frames = 0;
            float elapsed = 0f;

            while (elapsed < duration && frames < samples.Length)
            {
                yield return null;

                float frameMs = Time.unscaledDeltaTime * MillisecondsPerSecond;
                samples[frames++] = frameMs;
                elapsed += Time.unscaledDeltaTime;
            }

            long allocatedAfter = System.GC.GetTotalMemory(false);

            _spawner.DespawnAll();

            LastReport = Summarise(samples, frames, enemyCount, elapsed, allocatedAfter - allocatedBefore);
            LastReportPath = Write(LastReport);
            IsRunning = false;
        }

        /// <summary>Milliseconds in a second. Named so the conversion is not a bare literal.</summary>
        private const float MillisecondsPerSecond = 1000f;

        /// <summary>
        /// Upper bound on frames per second used only to size the sample buffer. Generous: an
        /// uncapped batch-mode run with no renderer goes far above any real frame rate, and a
        /// buffer too small would silently truncate the sample.
        /// </summary>
        private const int MaxPlausibleFps = 2000;

        private void SpawnCrowd(int count)
        {
            Vector2 centre = _origin != null ? (Vector2)_origin.position : Vector2.zero;
            float radius = _balanceConfig.DebugSpawnRadius;

            for (int i = 0; i < count; i++)
            {
                var offset = new Vector2(Random.Range(-radius, radius), 1f);
                _spawner.Spawn(centre + offset);
            }
        }

        private static FrameTimeReport Summarise(
            float[] samples, int frames, int enemyCount, float elapsed, long allocatedDelta)
        {
            var ordered = new float[frames];
            System.Array.Copy(samples, ordered, frames);
            System.Array.Sort(ordered);

            float total = 0f;
            int slow = 0;
            for (int i = 0; i < frames; i++)
            {
                total += ordered[i];
                if (ordered[i] > SlowFrameMs) slow++;
            }

            float mean = frames > 0 ? total / frames : 0f;
            float p99 = Percentile(ordered, frames, 0.99f);

            return new FrameTimeReport
            {
                CompletedUtc = System.DateTime.UtcNow.ToString("o"),
                EnemyCount = enemyCount,
                DurationSeconds = elapsed,
                FrameCount = frames,
                MeanMs = mean,
                MedianMs = frames > 0 ? ordered[frames / 2] : 0f,
                P95Ms = Percentile(ordered, frames, 0.95f),
                P99Ms = p99,
                MaxMs = frames > 0 ? ordered[frames - 1] : 0f,
                FramesOver33Ms = slow,
                MeanFps = mean > 0f ? MillisecondsPerSecond / mean : 0f,
                OnePercentLowFps = p99 > 0f ? MillisecondsPerSecond / p99 : 0f,
                AllocatedKilobytesDelta = allocatedDelta / 1024f,
                Caveats =
                    "No skill is cast during the run: the ultimate's 15s cooldown is longer than " +
                    "the 10s sample, so including it would make the result depend on whether a " +
                    "cast happened to land in the window. These figures therefore exclude the " +
                    "cost of an area skill sweeping its radius. A batch-mode run also has no " +
                    "renderer, so its frame times are not a player's frame times; only an editor " +
                    "run on real hardware measures NFR-001 and NFR-002."
            };
        }

        /// <summary>
        /// Nearest-rank percentile over an already sorted array. Nearest-rank rather than an
        /// interpolating variant because a frame time is an observed frame, and reporting a value
        /// halfway between two frames that never happened would be less honest, not more precise.
        /// </summary>
        private static float Percentile(float[] sorted, int count, float fraction)
        {
            if (count <= 0) return 0f;

            int rank = Mathf.Clamp(Mathf.CeilToInt(fraction * count) - 1, 0, count - 1);
            return sorted[rank];
        }

        private static string Write(FrameTimeReport report)
        {
            string directory = Path.Combine(Directory.GetCurrentDirectory(), ReportDirectory);
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, "frametime-report.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));

            GameLog.Info("Profiling",
                $"Frame time over {report.FrameCount} frames with {report.EnemyCount} enemies: " +
                $"mean {report.MeanMs:F2}ms ({report.MeanFps:F0} FPS), median {report.MedianMs:F2}ms, " +
                $"p95 {report.P95Ms:F2}ms, p99 {report.P99Ms:F2}ms ({report.OnePercentLowFps:F0} FPS 1% low), " +
                $"max {report.MaxMs:F2}ms, {report.FramesOver33Ms} frames over {SlowFrameMs}ms, " +
                $"heap +{report.AllocatedKilobytesDelta:F1} KB. Written to {path}");

            return path;
        }
    }
}
