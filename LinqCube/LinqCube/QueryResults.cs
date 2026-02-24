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

        /// <summary>
        /// Initialize the entry result
        /// </summary>
        /// <param name="chainedDimensions"></param>
        /// <param name="crossingDimensions"></param>
        /// <param name="parentCoordinate"></param>
        public void Initialize(IEnumerable<IQueryDimension> chainedDimensions, IEnumerable<IQueryDimension> crossingDimensions, IDimensionEntryResult parentCoordinate)
        {
            ParentCoordinate = parentCoordinate;
            foreach (var child in DimensionEntry.Children)
            {
                var result = new DimensionEntryResult<TFact>(child, Measures);
                Entries[child] = result;
                result.Initialize(chainedDimensions, crossingDimensions, parentCoordinate);
            }

            var nextDim = chainedDimensions == null ? null : chainedDimensions.FirstOrDefault();
            if (nextDim != null)
            {
                // we have a "next" chained dimension.
                // Create result and recurse initialisation
                var nextResult = new DimensionResult<TFact>(nextDim, Measures);
                OtherDimensions[nextDim] = nextResult;
                nextResult.Initialize(chainedDimensions.Skip(1), crossingDimensions, this);
            }
            else
            {
                // no chained dimensions left
                // generate all crossing permutations
                foreach (var other in crossingDimensions)
                {
                    var otherResult = new DimensionResult<TFact>(other, Measures);
                    OtherDimensions[other] = otherResult;
                    otherResult.Initialize(null, crossingDimensions.Where(i => i != other), this);
                }
            }

            foreach (var measure in Measures)
            {
                Values[measure] = measure.CreateResult();
            }
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
                return Entries[key];
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
                IDimensionEntryResult result;
                if (this.DimensionEntry == key)
                {
                    return this;
                }
                else if (Entries.TryGetValue(key, out result))
                {
                    return result;
                }
                else
                {
                    if (key.Parent != null)
                    {
                        return this[key.Parent][key];
                    }
                    else
                    {
                        foreach (var dim in OtherDimensions)
                        {
                            if (dim.Key.Dimension == key.Root)
                            {
                                return dim.Value[key];
                            }
                        }
                    }

                    throw new ArgumentOutOfRangeException("key", "key does not match dimension");
                }
            }
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
