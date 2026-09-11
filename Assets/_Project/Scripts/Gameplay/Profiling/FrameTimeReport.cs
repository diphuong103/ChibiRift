using System;
using UnityEngine;

namespace ChibiRift.Gameplay
{
    /// <summary>One named call site's share of a run's allocations (NFR-002).</summary>
    [Serializable]
    public struct AllocationSourceEntry
    {
        /// <summary>The label passed to <c>AllocationProfiler.BeginSample</c>/<c>EndSample</c>.</summary>
        public string Label;

        /// <summary>Bytes this label allocated over the whole sample, in kilobytes.</summary>
        public float Kilobytes;

        /// <summary>
        /// Individual per-call samples for this label that were dropped because a garbage collection
        /// ran inside them (see <c>AllocationProfiler</c>). A large count relative to the run's total
        /// frame count means this label's figure is a bigger underestimate than usual.
        /// </summary>
        public int DroppedSamples;
    }

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

        /// <summary>
        /// Growth in the managed heap's live size over the run, in kilobytes.
        /// </summary>
        /// <remarks>
        /// <para>From <c>GC.GetTotalMemory</c>. The textbook-correct API for this,
        /// <c>GC.GetAllocatedBytesForCurrentThread</c>, measured a verified 0 bytes for a direct
        /// test that allocated and held 10MB of real arrays in this project's Unity/Mono runtime —
        /// it is not implemented here, however standard it is elsewhere. <c>GetTotalMemory</c> and
        /// <c>Profiler.GetMonoUsedSizeLong</c> both registered the same 10MB correctly, so this
        /// value comes from <c>GetTotalMemory</c> instead.</para>
        ///
        /// <para><b>This is a floor, not an exact figure, when <see cref="GcCollectionsDuringSample"/>
        /// is above zero.</b> Heap size can shrink mid-sample if a collection reclaims garbage, which
        /// makes a before/after delta undercount by however much was just freed. Unlike the smaller
        /// per-callsite samples in <see cref="TopAllocationSources"/>, this single ten-second window
        /// is not dropped when a collection lands inside it — discarding the one measurement that
        /// exists would defeat the point — so read this figure alongside
        /// <see cref="GcCollectionsDuringSample"/> rather than in isolation.</para>
        /// </remarks>
        public float AllocatedKilobytesDelta;

        /// <summary>
        /// Generation-0 collections that ran during the sample. Zero means
        /// <see cref="AllocatedKilobytesDelta"/> is exact; above zero means it is a floor (see that
        /// field's remarks) and is itself worth noting, since collections are a direct cause of the
        /// frame-time spikes <see cref="P99Ms"/> exists to catch.
        /// </summary>
        public int GcCollectionsDuringSample;

        /// <summary>
        /// True when the sample stopped because the number of frames hit a defensive safety limit
        /// rather than because <see cref="DurationSeconds"/> elapsed. Should never be true in normal
        /// use; if it is, <see cref="DurationSeconds"/> is shorter than the configured duration and
        /// the run is not comparable to one that completed normally.
        /// </summary>
        public bool StoppedBySafetyCap;

        /// <summary>
        /// The five call sites that allocated the most during the run, most bytes first. Empty when
        /// nothing was instrumented for this run.
        /// </summary>
        public AllocationSourceEntry[] TopAllocationSources;

        /// <summary>
        /// What this measurement does not cover. Written into the report itself so a reader cannot
        /// see the numbers without seeing the caveat.
        /// </summary>
        public string Caveats;
    }
}
