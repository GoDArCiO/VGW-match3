using System.Collections.Generic;
using Match3.Core;

namespace Proto.Tests
{
    /// <summary>
    /// Returns an ever-increasing, never-repeating color id (starting high, outside any real palette). Refills
    /// from this source can NEVER form a match, which isolates a cascade to its gravity-driven matches — the
    /// clean way to assert exact cascade depth without refill interference.
    /// </summary>
    internal sealed class UniqueGemSource : IGemSource
    {
        private int _next;
        public UniqueGemSource(int start = 100) { _next = start; }
        public int Next(int column) => _next++;
    }

    /// <summary>
    /// Per-column scripted refill (the determinism contract): <c>Next(column)</c> dequeues that column's
    /// queue. Throws if a column runs dry, so a test fails loudly rather than silently drawing a default.
    /// Refill consumes column-major, bottom-row first.
    /// </summary>
    internal sealed class ScriptedGemSource : IGemSource
    {
        private readonly Dictionary<int, Queue<int>> _byColumn = new Dictionary<int, Queue<int>>();

        public ScriptedGemSource(Dictionary<int, int[]> perColumn)
        {
            foreach (var kv in perColumn)
                _byColumn[kv.Key] = new Queue<int>(kv.Value);
        }

        public int Next(int column)
        {
            if (!_byColumn.TryGetValue(column, out var q) || q.Count == 0)
                throw new System.InvalidOperationException($"ScriptedGemSource: column {column} exhausted.");
            return q.Dequeue();
        }
    }

    /// <summary>Records the exact (column) order in which refill draws, to pin the refill contract.</summary>
    internal sealed class RecordingGemSource : IGemSource
    {
        public readonly List<int> Calls = new List<int>();
        private readonly IGemSource _inner;
        public RecordingGemSource(IGemSource inner) { _inner = inner; }
        public int Next(int column) { Calls.Add(column); return _inner.Next(column); }
    }

    /// <summary>Returns a scripted sequence of die faces for deterministic meta tests.</summary>
    internal sealed class ScriptedDieRoller : IDieRoller
    {
        private readonly Queue<int> _faces;
        public ScriptedDieRoller(params int[] faces) { _faces = new Queue<int>(faces); }
        public int Roll() => _faces.Dequeue();
    }

    /// <summary>In-memory <see cref="IMetaStore"/> fake: a save/load round-trip with no PlayerPrefs.</summary>
    internal sealed class InMemoryMetaStore : IMetaStore
    {
        private MetaState? _saved;
        public int SaveCount { get; private set; }
        public int ClearCount { get; private set; }
        public MetaState Load() => _saved ?? MetaState.Defaults;
        public void Save(MetaState state) { _saved = state; SaveCount++; }
        public void Clear() { _saved = null; ClearCount++; }
    }
}
