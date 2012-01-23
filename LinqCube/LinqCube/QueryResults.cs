using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public class QueryResult : Dictionary<IDimension, IDimensionEntryResult>
    {
        public QueryResult()
        {
        }

        public IDimensionEntryResult this[IDimensionEntry key]
        {
            get
            {
                return ((IDictionary<IDimension, IDimensionEntryResult>)this)[key.Root][key];
            }
        }
    }

    public interface IDimensionEntryResult
    {
        IDimensionEntry DimensionEntry { get; }

        MeasureResultDictionary Values { get; }
        IMeasureResult this[IMeasure key] { get; }

        IDimensionEntryResult ParentCoordinate { get; }
        IEnumerable<IDimensionEntryResult> CubeCoordinates { get; }

        DimensionResultOtherDimensionsDictionary OtherDimensions { get; }
        DimensionResultEntriesDictionary Entries { get; }

        IDimensionEntryResult this[IDimensionEntry key] { get; }
        IDimensionEntryResult this[string key] { get; }
    }

    public interface IDimensionResult : IDimensionEntryResult
    {
        IDimension Dimension { get; }
    }

    public class DimensionEntryResult<TFact> : IDimensionEntryResult
    {
        public DimensionEntryResult(IDimensionEntry e, IEnumerable<IMeasure> measures)
        {
            DimensionEntry = e;
            Entries = new DimensionResultEntriesDictionary();
            OtherDimensions = new DimensionResultOtherDimensionsDictionary();
            Measures = measures;
            Values = new MeasureResultDictionary();
        }

        public IDimension Dimension { get { return this.DimensionEntry.Root; } }
        public IDimensionEntry DimensionEntry { get; private set; }
        public DimensionResultEntriesDictionary Entries { get; private set; }
        public DimensionResultOtherDimensionsDictionary OtherDimensions { get; private set; }
        public MeasureResultDictionary Values { get; private set; }
        public IEnumerable<IMeasure> Measures { get; private set; }
        public IDimensionEntryResult ParentCoordinate { get; private set; }
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

        public void Initialize(IEnumerable<IQueryDimension> primaryDimensions, IEnumerable<IQueryDimension> secondaryDimensions, IDimensionEntryResult parentCoordinate)
        {
            ParentCoordinate = parentCoordinate;
            foreach (var child in DimensionEntry.Children)
            {
                var result = new DimensionEntryResult<TFact>(child, Measures);
                Entries[child] = result;
                result.Initialize(primaryDimensions, secondaryDimensions, parentCoordinate);
            }

            var nextDim = primaryDimensions == null ? null : primaryDimensions.FirstOrDefault();
            if (nextDim != null)
            {
                // we have a "next" primary dimension.
                // Create result and recurse initialisation
                var nextResult = new DimensionResult<TFact>(nextDim, Measures);
                OtherDimensions[nextDim] = nextResult;
                nextResult.Initialize(primaryDimensions.Skip(1), secondaryDimensions, this);
            }
            else
            {
                // no primary dimensions left
                // generate all secondary permutations
                foreach (var other in secondaryDimensions)
                {
                    var otherResult = new DimensionResult<TFact>(other, Measures);
                    OtherDimensions[other] = otherResult;
                    otherResult.Initialize(null, secondaryDimensions.Where(i => i != other), this);
                }
            }

            foreach (var measure in Measures)
            {
                Values[measure] = measure.CreateResult();
            }
        }

        public IDimensionEntryResult this[string key]
        {
            get
            {
                return Entries[key];
            }
        }

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

        public IMeasureResult this[IMeasure key]
        {
            get
            {
                return Values[key];
            }
        }
    }

    public class DimensionResult<TFact> : DimensionEntryResult<TFact>, IDimensionResult
    {
        public DimensionResult(IQueryDimension dim, IEnumerable<IMeasure> measures)
            : base(dim.Dimension, measures)
        {
            QueryDimension = dim;
        }

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
    }
}
