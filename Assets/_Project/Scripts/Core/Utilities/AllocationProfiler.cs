using System;
using System.Collections.Generic;

namespace ChibiRift.Core
{
    /// <summary>
    /// Attributes managed heap allocations to a named call site, active only while something asks
    /// for it (NFR-002).
    /// </summary>
    /// <remarks>
    /// <para><b>Why <see cref="GC.GetTotalMemory"/> and not
    /// <see cref="GC.GetAllocatedBytesForCurrentThread"/>.</b> The latter is the textbook-correct
    /// API for exactly this job — it is what <c>com.unity.test-framework.performance</c> uses — and
    /// it is what the first version of this class used. It turned out to be stubbed in this
    /// project's Unity/Mono runtime: a direct test allocating and holding 10MB of real arrays
    /// between two calls measured a delta of exactly zero, both for the thread-local call and for a
    /// single boxed <c>int</c>. <see cref="GC.GetTotalMemory"/> and
    /// <see cref="UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong"/> both registered the same
    /// 10MB correctly in the same environment, so this class uses <c>GetTotalMemory</c>.</para>
    ///
    /// <para><b>The trade-off that comes back with it.</b> <c>GetTotalMemory</c> reports the size of
    /// the <i>live</i> heap, so a collection that happens to run inside a measured window makes that
    /// one window undercount by however much was just reclaimed — this was the original bug in the
    /// frame-time harness. The mitigation here is <see cref="GC.CollectionCount"/>: every sample
    /// records the generation-0 collection count at <c>BeginSample</c> and checks it again at
    /// <c>EndSample</c>. A sample that saw a collection run inside it is dropped rather than added,
    /// since its delta is not trustworthy. Because a single call site is sampled many times over a
    /// real run (once per frame or more), losing the rare one that overlaps a collection costs
    /// almost nothing; the alternative — keeping a known-wrong number — is worse. The count of
    /// dropped samples is tracked per label and exposed through <see cref="Snapshot"/> so this
    /// approximation stays visible rather than silently eating data.</para>
    ///
    /// <para><b>Cost when not recording.</b> One bool check per call site. Call sites keep the
    /// checks in production code deliberately: a system added in P2 that starts allocating in its
    /// hot path is invisible until someone profiles it, and the whole point of instrumenting here is
    /// that turning the lens back on costs nothing to wire up again.</para>
    /// </remarks>
    public static class AllocationProfiler
    {
        /// <summary>Whether Begin/EndSample currently record anything. False costs one bool read.</summary>
        public static bool Recording { get; set; }

        /// <summary>One label's accumulated numbers.</summary>
        public readonly struct Entry
        {
            /// <summary>Bytes attributed to this label, summed over every untainted sample.</summary>
            public readonly long Bytes;

            /// <summary>
            /// Samples for this label dropped because a garbage collection ran during them, and so
            /// were excluded rather than undercounted (see the class remarks).
            /// </summary>
            public readonly int DroppedSamples;

            public Entry(long bytes, int droppedSamples)
            {
                Bytes = bytes;
                DroppedSamples = droppedSamples;
            }
        }

        private static readonly Dictionary<string, long> Totals = new Dictionary<string, long>();
        private static readonly Dictionary<string, int> Dropped = new Dictionary<string, int>();
        private static readonly Dictionary<string, (long Bytes, int Collections)> Pending =
            new Dictionary<string, (long, int)>();

        /// <summary>Clears every accumulated total and any pending (unmatched) sample.</summary>
        public static void Reset()
        {
            Totals.Clear();
            Dropped.Clear();
            Pending.Clear();
        }

        /// <summary>
        /// Marks the start of one attributed region. Must be paired with <see cref="EndSample"/> on
        /// every exit path of the wrapped code — a <c>try/finally</c> at the call site, not a bare
        /// call before an early <c>return</c>, or the next call for the same label measures the idle
        /// time in between as well.
        /// </summary>
        public static void BeginSample(string label)
        {
            if (!Recording) return;
            Pending[label] = (GC.GetTotalMemory(false), GC.CollectionCount(0));
        }

        /// <summary>Closes the region opened by <see cref="BeginSample"/> and adds its cost.</summary>
        public static void EndSample(string label)
        {
            if (!Recording) return;
            if (!Pending.TryGetValue(label, out (long Bytes, int Collections) start)) return;

            // A collection inside the window would have shrunk the heap by whatever it reclaimed,
            // making a naive delta an undercount rather than the true allocation. Drop it instead of
            // reporting a number known to be wrong.
            if (GC.CollectionCount(0) != start.Collections)
            {
                Dropped.TryGetValue(label, out int droppedExisting);
                Dropped[label] = droppedExisting + 1;
                return;
            }

            long delta = GC.GetTotalMemory(false) - start.Bytes;
            if (delta < 0) delta = 0; // A concurrent collection outside our own count can still dip this.

            Totals.TryGetValue(label, out long existing);
            Totals[label] = existing + delta;
        }

        /// <summary>Every label recorded so far, most bytes first.</summary>
        public static List<KeyValuePair<string, Entry>> Snapshot()
        {
            var rows = new List<KeyValuePair<string, Entry>>(Totals.Count);

            foreach (KeyValuePair<string, long> total in Totals)
            {
                Dropped.TryGetValue(total.Key, out int droppedCount);
                rows.Add(new KeyValuePair<string, Entry>(total.Key, new Entry(total.Value, droppedCount)));
            }

            rows.Sort((a, b) => b.Value.Bytes.CompareTo(a.Value.Bytes));
            return rows;
        }
    }
}
