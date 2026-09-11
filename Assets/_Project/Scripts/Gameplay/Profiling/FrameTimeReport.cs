using System;
using UnityEngine;

namespace ChibiRift.Gameplay
{
    /// <summary>
    /// The numbers one run of <see cref="FrameTimeHarness"/> produced (NFR-001, NFR-002).
    /// </summary>
    /// <remarks>
    /// Serialized with <c>JsonUtility</c>, so the fields are public and plainly named rather than
    /// properties. Times are milliseconds.
    /// </remarks>
    [Serializable]
    public sealed class FrameTimeReport
    {
        /// <summary>When the run finished, ISO-8601, so two reports can be told apart.</summary>
        public string CompletedUtc;

        /// <summary>Enemies alive for the whole sample (NFR-001 caps this at 30).</summary>
        public int EnemyCount;

        /// <summary>Seconds sampled.</summary>
        public float DurationSeconds;

        /// <summary>Frames sampled.</summary>
        public int FrameCount;

        /// <summary>Mean frame time in milliseconds.</summary>
        public float MeanMs;

        /// <summary>Median frame time. Less misleading than the mean when spikes are present.</summary>
        public float MedianMs;

        /// <summary>95th percentile frame time.</summary>
        public float P95Ms;

        /// <summary>99th percentile. NFR-002's ceiling is 33ms here.</summary>
        public float P99Ms;

        /// <summary>Worst single frame.</summary>
        public float MaxMs;

        /// <summary>Frames over 33ms, i.e. frames that fell below 30 FPS.</summary>
        public int FramesOver33Ms;

        /// <summary>Mean frames per second, derived from <see cref="MeanMs"/>.</summary>
        public float MeanFps;

        /// <summary>
        /// The 1% low of NFR-001: the FPS implied by <see cref="P99Ms"/>. This is the figure that
        /// catches stutter, which an average never does.
        /// </summary>
        public float OnePercentLowFps;

        /// <summary>Managed heap growth over the run, in kilobytes.</summary>
        public float AllocatedKilobytesDelta;

        /// <summary>
        /// What this measurement does not cover. Written into the report itself so a reader cannot
        /// see the numbers without seeing the caveat.
        /// </summary>
        public string Caveats;
    }
}
