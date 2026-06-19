using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    /// <summary>
    /// The result of a cube query
    /// </summary>
    public class QueryResult : Dictionary<IDimension, IDimensionEntryResult>
    {
        /// <summary>
        /// creates a new query result
        /// </summary>
        public QueryResult()
        {
        }

        /// <summary>
        /// Indexer for accessing a specific dimension
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IDimensionEntryResult this[IDimensionEntry key]
        {
            get
            {
                return ((IDictionary<IDimension, IDimensionEntryResult>)this)[key.Root][key];
            }
        }
    }

    /// <summary>
    /// Represents the result of a dimension entry with all sub dimensions and measures.
    /// </summary>
    public interface IDimensionEntryResult
    {
        /// <summary>
        /// Returns the associated dimension entry
        /// </summary>
        IDimensionEntry DimensionEntry { get; }

        /// <summary>
        /// Returns all measures
        /// </summary>
        MeasureResultDictionary Values { get; }
        /// <summary>
        /// Access a measure result by measure
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        IMeasureResult this[IMeasure key] { get; }

        /// <summary>
        /// Returns the parent dimension entry result
        /// </summary>
        IDimensionEntryResult ParentCoordinate { get; }
        /// <summary>
        /// 
        /// </summary>
        IEnumerable<IDimensionEntryResult> CubeCoordinates { get; }

        /// <summary>
        /// 
        /// </summary>
        DimensionResultOtherDimensionsDictionary OtherDimensions { get; }
        /// <summary>
        /// Returns all children dimension entry results
        /// </summary>
        DimensionResultEntriesDictionary Entries { get; }

        /// <summary>
        /// Return a dimension entry result by the given dimension entry
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        IDimensionEntryResult this[IDimensionEntry key] { get; }
        /// <summary>
        /// Return a dimension entry result by the given dimension entry
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        IDimensionEntryResult this[string key] { get; }
    }

    /// <summary>
    /// Represents the result of a dimension with all sub dimensions and measures.
    /// </summary>
    public interface IDimensionResult : IDimensionEntryResult
    {
        /// <summary>
        /// Returns the associated dimension
        /// </summary>
        IDimension Dimension { get; }
    }

    /// <summary>
    /// Implementation of a dimension entry result
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    public class DimensionEntryResult<TFact> : IDimensionEntryResult
    {
        /// <summary>
        /// Creates a dimension entry result
        /// </summary>
        /// <param name="e"></param>
        /// <param name="measures"></param>
        public DimensionEntryResult(IDimensionEntry e, IEnumerable<IMeasure> measures)
        {
            DimensionEntry = e;
            Entries = new DimensionResultEntriesDictionary();
            OtherDimensions = new DimensionResultOtherDimensionsDictionary();
            Measures = measures;
            Values = new MeasureResultDictionary();
        }

        /// <summary>
        /// Returns the associated dimension
        /// </summary>
        public IDimension Dimension { get { return this.DimensionEntry.Root; } }
        /// <summary>
        /// Returns the associated dimension entry
        /// </summary>
        public IDimensionEntry DimensionEntry { get; private set; }
        /// <summary>
        /// Returns all children dimension entry results
        /// </summary>
        public DimensionResultEntriesDictionary Entries { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public DimensionResultOtherDimensionsDictionary OtherDimensions { get; private set; }
        /// <summary>
        /// Returns all measure results
        /// </summary>
        public MeasureResultDictionary Values { get; private set; }
        /// <summary>
        /// Returns all associated measures
        /// </summary>
        public IEnumerable<IMeasure> Measures { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public IDimensionEntryResult ParentCoordinate { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<IDimensionEntryResult> CubeCoordinates
        {
            get
            {
                IDimensionEntryResult self = this;
                while (self != null)
                {
                    yield return self;
                    self = self.ParentCoordinate;
                }
            }
        }

        // The dimension context this node would expand into (the remaining chained dimensions + the
        // crossing dimensions). Kept so a sparse node can build its children / other-dimensions lazily.
        private IEnumerable<IQueryDimension> _chainedDimensions;
        private IEnumerable<IQueryDimension> _crossingDimensions;
        private bool _sparse;
        private bool _otherDimensionsBuilt;

        /// <summary>
        /// Initialize the entry result (dense — backwards compatible).
        /// </summary>
        public void Initialize(IEnumerable<IQueryDimension> chainedDimensions, IEnumerable<IQueryDimension> crossingDimensions, IDimensionEntryResult parentCoordinate)
        {
            Initialize(chainedDimensions, crossingDimensions, parentCoordinate, false);
        }

        /// <summary>
        /// Initialize the entry result.
        /// </summary>
        /// <param name="chainedDimensions"></param>
        /// <param name="crossingDimensions"></param>
        /// <param name="parentCoordinate"></param>
        /// <param name="sparse">
        /// When <see langword="false"/> the full child / other-dimension sub-tree is materialised eagerly
        /// (every coordinate exists). When <see langword="true"/> only this node + its measure results are
        /// created; children (<see cref="GetOrCreateChild"/>) and other-dimensions
        /// (<see cref="EnsureOtherDimensions"/>) are materialised lazily as matching facts arrive.
        /// </param>
        public void Initialize(IEnumerable<IQueryDimension> chainedDimensions, IEnumerable<IQueryDimension> crossingDimensions, IDimensionEntryResult parentCoordinate, bool sparse)
        {
            ParentCoordinate = parentCoordinate;
            _chainedDimensions = chainedDimensions;
            _crossingDimensions = crossingDimensions;
            _sparse = sparse;

            if (!sparse)
            {
                foreach (var child in DimensionEntry.Children)
                {
                    var result = new DimensionEntryResult<TFact>(child, Measures);
                    Entries[child] = result;
                    result.Initialize(chainedDimensions, crossingDimensions, parentCoordinate, sparse);
                }

                BuildOtherDimensions();
                _otherDimensionsBuilt = true;
            }

            foreach (var measure in Measures)
            {
                Values[measure] = measure.CreateResult();
            }
        }

        // Builds the chained-next / crossing other-dimension roots under this node (each recursing with the
        // same sparse flag). Shared by the eager Initialize and the lazy EnsureOtherDimensions.
        private void BuildOtherDimensions()
        {
            var nextDim = _chainedDimensions == null ? null : _chainedDimensions.FirstOrDefault();
            if (nextDim != null)
            {
                // we have a "next" chained dimension.
                var nextResult = new DimensionResult<TFact>(nextDim, Measures);
                OtherDimensions[nextDim] = nextResult;
                nextResult.Initialize(_chainedDimensions.Skip(1), _crossingDimensions, this, _sparse);
            }
            else if (_crossingDimensions != null)
            {
                // no chained dimensions left — generate all crossing permutations
                foreach (var other in _crossingDimensions)
                {
                    var otherResult = new DimensionResult<TFact>(other, Measures);
                    OtherDimensions[other] = otherResult;
                    otherResult.Initialize(null, _crossingDimensions.Where(i => i != other), this, _sparse);
                }
            }
        }

        /// <summary>
        /// Lazily materialises this node's other-dimension roots (chained-next / crossing) on first touch.
        /// A no-op in dense mode (already built) and after the first call. The cube executor calls this
        /// before descending into <see cref="OtherDimensions"/> in sparse mode.
        /// </summary>
        public void EnsureOtherDimensions()
        {
            if (_otherDimensionsBuilt) return;
            _otherDimensionsBuilt = true;
            BuildOtherDimensions();
        }

        /// <summary>
        /// Returns the child entry result for <paramref name="child"/>, creating it lazily (sparse mode) if
        /// it does not exist yet. The new node shares this node's cross-dimension parent coordinate.
        /// </summary>
        public IDimensionEntryResult GetOrCreateChild(IDimensionEntry child)
        {
            IDimensionEntryResult existing;
            if (Entries.TryGetValue(child, out existing))
            {
                return existing;
            }

            var result = new DimensionEntryResult<TFact>(child, Measures);
            Entries[child] = result;
            result.Initialize(_chainedDimensions, _crossingDimensions, ParentCoordinate, _sparse);
            return result;
        }

        /// <summary>
        /// Return a dimension entry result by the given dimension entry
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IDimensionEntryResult this[string key]
        {
            get
            {
                // Resolve by the dimension's defined child entries (the full domain), not only the
                // materialised ones, so a valid-but-empty coordinate reads 0 in a sparse cube instead of
                // throwing (the dense contract). Delegates to the IDimensionEntry indexer below.
                foreach (var child in DimensionEntry.Children)
                {
                    if (child.Label == key)
                    {
                        return this[child];
                    }
                }

                throw new ArgumentOutOfRangeException("key", string.Format("No child entry labelled '{0}'.", key));
            }
        }

        /// <summary>
        /// Return a dimension entry result by the given dimension entry
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IDimensionEntryResult this[IDimensionEntry key]
        {
            get
            {
                if (this.DimensionEntry == key)
                {
                    return this;
                }

                IDimensionEntryResult result;
                if (Entries.TryGetValue(key, out result))
                {
                    return result;
                }

                if (key.Parent != null)
                {
                    // `key` is a descendant of this node. When we are its direct parent, it is a valid child
                    // that simply was not materialised (sparse cube) — return a zero-valued node rather than
                    // recursing onto ourselves (which would loop) or throwing. This restores the dense
                    // contract: every valid coordinate is addressable and reads 0 when it received no facts.
                    if (key.Parent == this.DimensionEntry)
                    {
                        return CreateEmptyChild(key);
                    }

                    return this[key.Parent][key];
                }

                // `key` is a dimension root — it must be one of the chained / crossing other-dimensions.
                EnsureOtherDimensions();
                foreach (var dim in OtherDimensions)
                {
                    if (dim.Key.Dimension == key.Root)
                    {
                        return dim.Value[key];
                    }
                }

                throw new ArgumentOutOfRangeException("key", "key does not match dimension");
            }
        }

        // Builds a transient, zero-valued result node for a valid child coordinate that was not materialised
        // (sparse cube). Mirrors GetOrCreateChild but deliberately does NOT store the node in Entries: a built
        // cube is cached and read concurrently, so a read-path indexer must not mutate shared state. The node
        // carries fresh (zero) measure results and the same dimension context, so it reads 0 and is itself
        // navigable — its own children resolve the same way.
        private IDimensionEntryResult CreateEmptyChild(IDimensionEntry child)
        {
            var result = new DimensionEntryResult<TFact>(child, Measures);
            result.Initialize(_chainedDimensions, _crossingDimensions, ParentCoordinate, _sparse);

            // Populate the chained/crossing other-dimension roots so the node behaves like a dense empty
            // node for BOTH access styles — the indexer (`node[innerDim]`) and a direct read of
            // `OtherDimensions`. Their entries are themselves empty (built lazily), so everything still
            // reads 0. The node is transient (never stored), so this mutation touches no shared state.
            result.EnsureOtherDimensions();
            return result;
        }

        /// <summary>
        /// Return a dimension entry result by the given measure entry
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IMeasureResult this[IMeasure key]
        {
            get
            {
                return Values[key];
            }
        }
    }

    /// <summary>
    /// Implementation of a dimension result
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    public class DimensionResult<TFact> : DimensionEntryResult<TFact>, IDimensionResult
    {
        /// <summary>
        /// Creates a new dimension result
        /// </summary>
        /// <param name="dim"></param>
        /// <param name="measures"></param>
        public DimensionResult(IQueryDimension dim, IEnumerable<IMeasure> measures)
            : base(dim.Dimension, measures)
        {
            QueryDimension = dim;
        }

        /// <summary>
        /// Returns the dimension associated to the query
        /// </summary>
        public IQueryDimension QueryDimension { get; private set; }
    }

    public static class DimensionEntryResultExtensions
    {
        public static bool Count<TDimension>(this IDimensionEntryResult current, IDimension dim, Func<DimensionEntry<TDimension>, bool> selector)
            where TDimension : IComparable
        {
            if (current == null) return false;
            var dimEntryResult = current.CubeCoordinates.FirstOrDefault(c => c.DimensionEntry.Root == dim);
            if (dimEntryResult != null)
            {
                var dimEntry = (DimensionEntry<TDimension>)dimEntryResult.DimensionEntry;
                if (dimEntry != null)
                {
                    return selector(dimEntry);
                }
            }
            return false;
        }

        /// <summary>
        /// Retrieves the inner-most parent date coordinate of this entry.
        /// </summary>
        /// <param name="self"></param>
        /// <returns>the inner-most parent date coordinate of this entry or null</returns>
        public static DimensionEntry<DateTime> GetDateTimeEntry(this IDimensionEntryResult self)
        {
            while (self != null)
            {
                var entry = self.DimensionEntry as DimensionEntry<DateTime>;
                if (entry != null)
                    return entry;
                self = self.ParentCoordinate;
            }
            return null;
        }

        /// <summary>
        /// Flattens a Dimensions hierarchie
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dim"></param>
        /// <returns></returns>
        public static IEnumerable<DimensionEntry<TDimension>> FlattenHierarchy<TDimension>(this DimensionEntry<TDimension> dim)
            where TDimension : IComparable
        {
            var result = new List<DimensionEntry<TDimension>>();

            foreach (DimensionEntry<TDimension> c in dim)
            {
                result.Add(c);
                result.AddRange(FlattenHierarchy(c));
            }
            return result;
        }
    }
}
