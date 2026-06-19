using System;
using System.Collections.Generic;

namespace dasz.LinqCube
{
    /// <summary>
    /// A measure that counts the <em>distinct</em> keys among the facts at each dimension node. The engine's
    /// built-in measures are all additive (sum / count); a distinct count is not — the same key may appear
    /// under several leaves, so the distinct total over a slice is the <em>union</em> of the per-leaf key
    /// sets, not the sum of the per-leaf counts (which would over-count a key that occurs in two leaves).
    /// Each node therefore retains the keys it saw (see <see cref="DistinctMeasureResult{TKey}.Keys"/>) so a
    /// consumer can union them across an arbitrary set of coordinates and obtain an exact distinct count.
    /// <para>
    /// Keys equal to <c>default(TKey)</c> (e.g. <c>0</c> for <see cref="int"/>, <see langword="null"/> for a
    /// reference type) are ignored — they are the conventional "no key" sentinel.
    /// </para>
    /// </summary>
    /// <typeparam name="TFact">The cube's fact-row type.</typeparam>
    /// <typeparam name="TKey">The distinct key type (e.g. an entity id).</typeparam>
    public class DistinctCountMeasure<TFact, TKey> : Measure<TFact, TKey>
        where TKey : notnull
    {
        /// <summary>
        /// Constructs a new distinct-count measure.
        /// </summary>
        /// <param name="name">The measure name consumers read it by.</param>
        /// <param name="key">Extracts the distinct key from a fact (<c>default(TKey)</c> = ignore).</param>
        public DistinctCountMeasure(string name, Func<TFact, TKey> key)
            : base(name, (fact, entry) => key(fact))
        {
        }

        /// <summary>
        /// Returns a fresh, empty result for this measure.
        /// </summary>
        public override IMeasureResult CreateResult()
        {
            return new DistinctMeasureResult<TKey>(this);
        }

        /// <summary>
        /// Adds the fact's key to the node's distinct set (ignoring the <c>default(TKey)</c> sentinel).
        /// </summary>
        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var key = Selector((TFact)item, entry);
            if (!EqualityComparer<TKey>.Default.Equals(key, default))
            {
                ((DistinctMeasureResult<TKey>)result).Add(key);
            }
        }
    }

    /// <summary>
    /// The per-node result of a <see cref="DistinctCountMeasure{TFact, TKey}"/>: the set of distinct keys
    /// seen at this node. During the cube build keys accumulate in a <see cref="HashSet{T}"/> (O(1) dedup);
    /// when the build completes the engine calls <see cref="Freeze"/>, which compacts that set into a packed
    /// (and, where the key type is comparable, sorted) array. The packed array is far smaller than a
    /// long-lived hash set — one element per key with no bucket/slot overhead — and means fewer, flatter
    /// objects for the GC to track on a cached result. The numeric value (<see cref="IntValue"/> etc.) is the
    /// distinct count at this node; for a multi-coordinate slice union the <see cref="Keys"/> instead of
    /// summing the per-node counts.
    /// </summary>
    /// <typeparam name="TKey">The distinct key type.</typeparam>
    public sealed class DistinctMeasureResult<TKey> : IMeasureResult, IRetainedKeySet, IFreezableMeasureResult
        where TKey : notnull
    {
        // Whether TKey can be ordered. Sorting the frozen array is a pure optimisation (better locality and
        // a cheap merge-based union); correctness never depends on it, so non-comparable keys are simply
        // left in hash order. Computed once per closed generic type.
        private static readonly bool Sortable =
            typeof(IComparable<TKey>).IsAssignableFrom(typeof(TKey)) ||
            typeof(IComparable).IsAssignableFrom(typeof(TKey));

        // Exactly one of these is non-null at any time: _building during ingestion, _frozen after Freeze.
        private HashSet<TKey> _building;
        private TKey[] _frozen;

        /// <summary>
        /// Constructs a new, empty distinct result.
        /// </summary>
        /// <param name="measure">The owning measure (supplies <see cref="Name"/>).</param>
        public DistinctMeasureResult(IMeasure measure)
        {
            Measure = measure;
        }

        /// <summary>The owning measure.</summary>
        public IMeasure Measure { get; private set; }

        /// <summary>Name of the measure.</summary>
        public string Name { get { return Measure.Name; } }

        // Accumulates a key during the build. Only called before Freeze.
        internal void Add(TKey key)
        {
            if (_building == null)
            {
                _building = new HashSet<TKey>();
            }
            _building.Add(key);
        }

        /// <summary>
        /// The distinct keys retained at this node. After <see cref="Freeze"/> this is the packed array;
        /// during the build it is the live set. Union these across coordinates for an exact distinct count.
        /// </summary>
        public IReadOnlyCollection<TKey> Keys
        {
            get
            {
                if (_frozen != null) return _frozen;
                if (_building != null) return _building;
                return Array.Empty<TKey>();
            }
        }

        // The distinct count at this node, valid in both the building and frozen states.
        private int CountValue
        {
            get
            {
                if (_frozen != null) return _frozen.Length;
                if (_building != null) return _building.Count;
                return 0;
            }
        }

        /// <summary>
        /// Compacts the build hash set into a packed array, releasing the hash set. Idempotent, and a no-op
        /// for a node that never saw a key. Called once by the engine after the ingestion pass.
        /// </summary>
        public void Freeze(IDimensionEntryResult node)
        {
            if (_frozen != null) return;

            if (_building == null)
            {
                _frozen = Array.Empty<TKey>();
                return;
            }

            var arr = new TKey[_building.Count];
            _building.CopyTo(arr);
            _building = null;
            if (Sortable)
            {
                Array.Sort(arr);
            }
            _frozen = arr;
        }

        /// <summary>The number of distinct keys at this node.</summary>
        public int RetainedKeyCount { get { return CountValue; } }

        /// <summary>The number of distinct keys at this node.</summary>
        public int Count { get { return CountValue; } }

        /// <summary>Not meaningful for a distinct count; always 0.</summary>
        public double Average { get { return 0; } }

        /// <summary>The distinct count as an integer.</summary>
        public int IntValue { get { return CountValue; } }

        /// <summary>The distinct count as a double.</summary>
        public double DoubleValue { get { return CountValue; } }

        /// <summary>The distinct count as a decimal.</summary>
        public decimal DecimalValue { get { return CountValue; } }

        /// <summary>Not supported by this measure result.</summary>
        public DateTime DateTimeValue { get { throw new NotSupportedException(); } }

        /// <summary>Not supported by this measure result.</summary>
        public TimeSpan TimeSpanValue { get { throw new NotSupportedException(); } }

        /// <summary>Returns a string representation.</summary>
        public override string ToString()
        {
            return string.Format("res: {0} = {1}", Name, CountValue);
        }
    }
}
