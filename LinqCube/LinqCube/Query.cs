using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public interface IQuery
    {
        string Name { get; }
        QueryResult Result { get; }
    }

    public class Query<TFact> : IQuery
    {
        public string Name { get; private set; }
        public QueryResult Result { get; private set; }

        /// <summary>
        /// The list of chained dimensions. These dimensions can only be accessed in the order of their definition.
        /// </summary>
        /// <remarks>
        /// The runtime of a query is in the order of the product of all entry-counts.
        /// </remarks>
        internal List<IQueryDimension> ChainedQueryDimensions { get; private set; }

        /// <summary>
        /// The list of crossing dimensions. These dimensions can only be accessed in any order after all chained dimensions were walked.
        /// </summary>
        /// <remarks>
        /// The runtime of a query is O(n^d), where d is the number of crossing query dimensions.
        /// </remarks>
        internal List<IQueryDimension> CrossingQueryDimensions { get; private set; }

        /// <summary>
        /// The list of top-level query dimensions. This is initialised together with the QueryResult and is used to decouple the executor from the chained/crossing distinction.
        /// </summary>
        private List<IQueryDimension> TopLevelQueryDimensions;

        public List<IMeasure> Measures { get; private set; }

        public Query(string name)
        {
            Name = name;
            ChainedQueryDimensions = new List<IQueryDimension>();
            CrossingQueryDimensions = new List<IQueryDimension>();
            TopLevelQueryDimensions = new List<IQueryDimension>();
            Measures = new List<IMeasure>();
        }

        public void Apply(TFact item)
        {
            if (Measures.Count == 0) throw new InvalidOperationException("No measures added");
            if (Result == null) throw new InvalidOperationException("Not initialized yet: no result created");

            foreach (IQueryDimension qDim in TopLevelQueryDimensions)
            {
                var dimResult = Result[qDim.Dimension];
                qDim.Apply(item, dimResult);
            }
        }

        internal void Initialize()
        {
            Initialize(false);
        }

        internal void Initialize(bool sparse)
        {
            Result = new QueryResult();

            // Every query dimension (top, chained-sub and crossing) needs the sparse flag, since each one's
            // Apply decides whether to materialise children/other-dimensions lazily.
            foreach (var qDim in ChainedQueryDimensions) qDim.SetSparse(sparse);
            foreach (var qDim in CrossingQueryDimensions) qDim.SetSparse(sparse);

            if (ChainedQueryDimensions.Count > 0)
            {
                // we have a chained dimension.
                // Create result and recurse initialisation
                var qDim = ChainedQueryDimensions.First();
                TopLevelQueryDimensions.Add(qDim);
                qDim.AddMeasures(Measures);

                var dimResult = new DimensionResult<TFact>(qDim, Measures);
                ((IDictionary<IDimension, IDimensionEntryResult>)Result)[qDim.Dimension] = dimResult;
                dimResult.Initialize(ChainedQueryDimensions.Skip(1), CrossingQueryDimensions, null, sparse);
            }
            else
            {
                // no chained dimensions set
                // generate all crossing permutations
                foreach (var qDim in CrossingQueryDimensions)
                {
                    TopLevelQueryDimensions.Add(qDim);
                    qDim.AddMeasures(Measures);

                    var dimResult = new DimensionResult<TFact>(qDim, Measures);
                    ((IDictionary<IDimension, IDimensionEntryResult>)Result)[qDim.Dimension] = dimResult;
                    dimResult.Initialize(null, CrossingQueryDimensions.Where(i => i != qDim), null, sparse);
                }
            }
        }
    }

    public interface IQueryDimension
    {
        void Apply(object item, IDimensionEntryResult dimResult);
        IDimension Dimension { get; }

        void AddMeasures(List<IMeasure> measures);

        /// <summary>Sets whether result nodes are materialised lazily (sparse) or eagerly (dense).</summary>
        void SetSparse(bool sparse);
    }

    public class QueryDimension<TDimension, TFact> : IQueryDimension
        where TDimension : IComparable
    {
        public Dimension<TDimension, TFact> Dimension { get; private set; }
        IDimension IQueryDimension.Dimension { get { return Dimension; } }
        public List<IMeasure> Measures { get; private set; }

        /// <summary>Whether result nodes are materialised lazily (sparse) or eagerly (dense).</summary>
        public bool Sparse { get; private set; }

        public QueryDimension(Dimension<TDimension, TFact> dim)
        {
            this.Dimension = dim;
        }

        public void AddMeasures(List<IMeasure> measures)
        {
            this.Measures = measures;
        }

        public void SetSparse(bool sparse)
        {
            this.Sparse = sparse;
        }

        public void Apply(object item, IDimensionEntryResult dimResult)
        {
            Apply((TFact)item, Dimension, dimResult);
        }

        // Whether the fact matches this entry (dimension filter + the entry's range/value test).
        private bool Matches(TFact item, DimensionEntry<TDimension> entry)
        {
            if (Dimension.Filter != null && !Dimension.Filter(item))
            {
                return false;
            }

            if (Dimension.EndSelector == null)
            {
                return entry.InRange(Dimension.Selector(item));
            }

            return entry.InRange(Dimension.Selector(item), Dimension.EndSelector(item));
        }

        private void Apply(TFact item, DimensionEntry<TDimension> entry, IDimensionEntryResult result)
        {
            // Range/filter test for this entry, then apply. The root and the dense walk enter here; sparse
            // children are pre-matched by their parent and call ApplyMatched directly, so a matched
            // coordinate is range-checked exactly once on the hot path.
            if (Matches(item, entry))
            {
                ApplyMatched(item, entry, result);
            }
        }

        // Applies the fact at a coordinate already known to match, then recurses.
        private void ApplyMatched(TFact item, DimensionEntry<TDimension> entry, IDimensionEntryResult result)
        {
            // Apply measures at this coordinate.
            foreach (var kvp in result.Values)
            {
                kvp.Key.Apply(kvp.Value, result, item);
            }

            // Recurse into chained-next / crossing dimensions. In sparse mode they are materialised on the
            // first matching fact (a node that exists has always had this called, so reads stay consistent).
            if (Sparse)
            {
                ((DimensionEntryResult<TFact>)result).EnsureOtherDimensions();
            }
            foreach (var otherDim in result.OtherDimensions)
            {
                otherDim.Key.Apply(item, otherDim.Value);
            }

            // Recurse into the hierarchy children. Sparse mode only creates the children the fact matches
            // (so empty coordinates never exist) and recurses into the already-matched child directly; dense
            // mode walks the pre-built children (each re-tested at the top of Apply).
            if (Sparse)
            {
                foreach (DimensionEntry<TDimension> child in entry.Children)
                {
                    if (!Matches(item, child))
                    {
                        continue;
                    }

                    var childResult = ((DimensionEntryResult<TFact>)result).GetOrCreateChild(child);
                    ApplyMatched(item, child, childResult);
                }
            }
            else
            {
                foreach (DimensionEntry<TDimension> child in entry.Children)
                {
                    Apply(item, child, result.Entries[child]);
                }
            }
        }
    }
}
