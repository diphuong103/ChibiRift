using System.Collections;
using System.Collections.Generic;
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
    /// <para>It also disables the F1 debug overlay and its enemy census for the duration of the
    /// run (restoring whatever state they were in afterwards). Both are development-only tools with
    /// no place in the shipped game (OI-29). Neither turned out to be a measurable allocator in
    /// headless testing — <c>OnGUI</c> never dispatches at all in <c>-nographics</c> batch mode,
    /// which was verified directly rather than assumed — but excluding them is still correct: this
    /// run is answering "how expensive is the game", and a diagnostic overlay is not the game
    /// (OI-32).</para>
    ///
    /// <para><b>A headless run proves the harness works, not that the game is fast.</b> There is no
    /// renderer in batch mode, so the frame times are not the frame times a player would see, and
    /// batch mode cannot exercise anything tied to actual rendering — <c>OnGUI</c>, Canvas rebuilds,
    /// sprite batching — at all. The test asserts the report is complete and never asserts an FPS
    /// threshold. It does assert an allocation budget, because <see cref="FrameTimeReport.AllocatedKilobytesDelta"/>
    /// does not depend on rendering to be meaningful, only on <see cref="FrameTimeReport.GcCollectionsDuringSample"/>
    /// staying at zero for the figure to be exact rather than a floor. Running the FPS side of this
    /// in the editor on a real machine, with the debug overlay actually visible, is the only way to
    /// find allocation sources this harness cannot see from here; README section 14 says how.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class FrameTimeHarness : MonoBehaviour
    {
        [Header("Data")]
        [Tooltip("Supplies the enemy count, sample duration and allocation budget (NFR-001, NFR-002).")]
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

        /// <summary>
        /// Absolute ceiling on frames sampled in one run. Purely a defensive stop against a runaway
        /// loop (a duration misconfigured to a huge value, or <c>Time.unscaledDeltaTime</c> reading
        /// zero and the loop never advancing <c>elapsed</c>); a normal 10s run at any plausible
        /// frame rate comes nowhere near it. This is not a substitute for reaching the configured
        /// duration — see <see cref="FrameTimeReport.StoppedBySafetyCap"/> and OI-32, which is
        /// exactly the bug this constant now guards against reintroducing: an earlier version used a
        /// much smaller cap as if it were a generous bound, and a headless run with no renderer blew
        /// past it, silently ending the sample at 6s instead of the configured 10.
        /// </summary>
        private const int SafetyFrameCap = 1_000_000;

        /// <summary>
        /// Starting capacity for the sample list, from a generous frames-per-second guess. Only
        /// avoids a few internal resizes early on; unlike <see cref="SafetyFrameCap"/> it is not a
        /// limit and the list grows past it freely.
        /// </summary>
        private const int InitialCapacityFpsHint = 2000;

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

            // Development-only diagnostics excluded from the measurement, not just gated out of
            // Release builds: this run is answering "how expensive is the game", and an overlay
            // nobody asked to see is not the game (OI-32). Restored in the finally below whatever
            // state they were found in, so a developer profiling with F1 already on gets it back.
            List<(Behaviour Component, bool WasEnabled)> suppressed = SuppressDevelopmentTools();

            AllocationProfiler.Reset();
            AllocationProfiler.Recording = true;

            try
            {
                int enemyCount = _balanceConfig.StressEnemyCount;
                float duration = _balanceConfig.StressDurationSeconds;

                SpawnCrowd(enemyCount);

                // One settled frame before sampling: the spawn frame itself is dominated by the
                // activation cost of the whole crowd and is not representative of anything.
                yield return null;

                // GetTotalMemory, not GetAllocatedBytesForCurrentThread — see the AllocationProfiler
                // remarks for why: the latter measured zero for a verified 10MB allocation in this
                // runtime. GetTotalMemory is the live heap size, so it undercounts by whatever a
                // mid-window collection reclaims; the collection count is captured alongside it so
                // that undercount is visible rather than silent.
                long allocatedBefore = System.GC.GetTotalMemory(false);
                int collectionsBefore = System.GC.CollectionCount(0);

                var samples = new List<float>(Mathf.CeilToInt(duration * InitialCapacityFpsHint));
                float elapsed = 0f;
                bool stoppedBySafetyCap = false;

                while (elapsed < duration)
                {
                    yield return null;

                    float frameMs = Time.unscaledDeltaTime * MillisecondsPerSecond;
                    samples.Add(frameMs);
                    elapsed += Time.unscaledDeltaTime;

                    if (samples.Count < SafetyFrameCap) continue;

                    stoppedBySafetyCap = true;
                    break;
                }

                long allocatedAfter = System.GC.GetTotalMemory(false);
                int collectionsDuring = System.GC.CollectionCount(0) - collectionsBefore;

                _spawner.DespawnAll();

                LastReport = Summarise(
                    samples,
                    enemyCount,
                    elapsed,
                    allocatedAfter - allocatedBefore,
                    collectionsDuring,
                    stoppedBySafetyCap);
                LastReportPath = Write(LastReport);
            }
            finally
            {
                AllocationProfiler.Recording = false;
                RestoreDevelopmentTools(suppressed);
                IsRunning = false;
            }
        }

        /// <summary>Milliseconds in a second. Named so the conversion is not a bare literal.</summary>
        private const float MillisecondsPerSecond = 1000f;

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

        /// <summary>
        /// Disables the F1 overlay and its census scan for the duration of the run. Looked up by
        /// name and by type respectively rather than wired in the inspector: this component lives in
        /// ChibiRift.Gameplay and the overlay lives in ChibiRift.UI, which Gameplay cannot reference
        /// (SRS 26) — a GameObject reference needs no such link.
        /// </summary>
        private static List<(Behaviour, bool)> SuppressDevelopmentTools()
        {
            var suppressed = new List<(Behaviour, bool)>();

            var census = Object.FindFirstObjectByType<EnemyDebugCensus>();
            if (census != null)
            {
                suppressed.Add((census, census.enabled));
                census.enabled = false;
            }

            GameObject overlay = GameObject.Find("DebugOverlay");
            if (overlay != null)
            {
                foreach (Behaviour behaviour in overlay.GetComponents<Behaviour>())
                {
                    suppressed.Add((behaviour, behaviour.enabled));
                    behaviour.enabled = false;
                }
            }

            return suppressed;
        }

        private static void RestoreDevelopmentTools(List<(Behaviour Component, bool WasEnabled)> suppressed)
        {
            if (suppressed == null) return;

            foreach ((Behaviour component, bool wasEnabled) in suppressed)
            {
                if (component != null) component.enabled = wasEnabled;
            }
        }

        private FrameTimeReport Summarise(
            List<float> samples,
            int enemyCount,
            float elapsed,
            long allocatedDelta,
            int gcCollectionsDuringSample,
            bool stoppedBySafetyCap)
        {
            int frames = samples.Count;
            var ordered = samples.ToArray();
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
                GcCollectionsDuringSample = gcCollectionsDuringSample,
                StoppedBySafetyCap = stoppedBySafetyCap,
                TopAllocationSources = TopAllocationSources(),
                Caveats =
                    "No skill is cast during the run: the ultimate's 15s cooldown is longer than " +
                    "the 10s sample, so including it would make the result depend on whether a " +
                    "cast happened to land in the window. These figures therefore exclude the " +
                    "cost of an area skill sweeping its radius. The F1 debug overlay and its enemy " +
                    "census are disabled for the run, so AllocatedKilobytesDelta is the game's own " +
                    "cost, not a diagnostic tool's (OI-32) — though neither measured as a meaningful " +
                    "allocator even when left enabled, since OnGUI never dispatches in batch mode. " +
                    "A batch-mode run also has no renderer, so its frame times are not a player's " +
                    "frame times, and rendering-tied costs (Canvas rebuilds, sprite batching, OnGUI " +
                    "itself) are invisible to this run no matter what; only an editor run on real " +
                    "hardware, with the overlay actually visible, can measure those or NFR-001/002 " +
                    "for frame time. AllocatedKilobytesDelta comes from GC.GetTotalMemory, not " +
                    "GC.GetAllocatedBytesForCurrentThread (unimplemented in this runtime — verified, " +
                    "not assumed), so it is exact only when GcCollectionsDuringSample is zero; " +
                    "otherwise it is a floor, undercounting by whatever those collections reclaimed."
            };
        }

        /// <summary>Every recorded label's total, most bytes first, trimmed to five.</summary>
        private static AllocationSourceEntry[] TopAllocationSources()
        {
            List<KeyValuePair<string, AllocationProfiler.Entry>> rows = AllocationProfiler.Snapshot();
            int count = Mathf.Min(5, rows.Count);
            var top = new AllocationSourceEntry[count];

            for (int i = 0; i < count; i++)
            {
                top[i] = new AllocationSourceEntry
                {
                    Label = rows[i].Key,
                    Kilobytes = rows[i].Value.Bytes / 1024f,
                    DroppedSamples = rows[i].Value.DroppedSamples
                };
            }

            return top;
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

            var topSources = new System.Text.StringBuilder();
            if (report.TopAllocationSources != null)
            {
                for (int i = 0; i < report.TopAllocationSources.Length; i++)
                {
                    AllocationSourceEntry entry = report.TopAllocationSources[i];
                    string dropNote = entry.DroppedSamples > 0 ? $" ({entry.DroppedSamples} dropped)" : "";
                    topSources.Append($"\n  {i + 1}. {entry.Label}: {entry.Kilobytes:F1} KB{dropNote}");
                }
            }

            string gcNote = report.GcCollectionsDuringSample > 0
                ? $" (floor: {report.GcCollectionsDuringSample} GC0 collection(s) ran during the sample)"
                : " (exact: no GC0 collection ran during the sample)";

            GameLog.Info("Profiling",
                $"Frame time over {report.FrameCount} frames with {report.EnemyCount} enemies: " +
                $"mean {report.MeanMs:F2}ms ({report.MeanFps:F0} FPS), median {report.MedianMs:F2}ms, " +
                $"p95 {report.P95Ms:F2}ms, p99 {report.P99Ms:F2}ms ({report.OnePercentLowFps:F0} FPS 1% low), " +
                $"max {report.MaxMs:F2}ms, {report.FramesOver33Ms} frames over {SlowFrameMs}ms, " +
                $"heap +{report.AllocatedKilobytesDelta:F1} KB{gcNote}. Top allocators:{topSources}. " +
                $"Written to {path}");

            return path;
        }
    }
}
