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
            Result = new QueryResult();

            if (ChainedQueryDimensions.Count > 0)
            {
                // we have a chained dimension.
                // Create result and recurse initialisation
                var qDim = ChainedQueryDimensions.First();
                TopLevelQueryDimensions.Add(qDim);
                qDim.AddMeasures(Measures);

                var dimResult = new DimensionResult<TFact>(qDim, Measures);
                ((IDictionary<IDimension, IDimensionEntryResult>)Result)[qDim.Dimension] = dimResult;
                dimResult.Initialize(ChainedQueryDimensions.Skip(1), CrossingQueryDimensions, null);
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
                    dimResult.Initialize(null, CrossingQueryDimensions.Where(i => i != qDim), null);
                }
            }
        }
    }

    public interface IQueryDimension
    {
        void Apply(object item, IDimensionEntryResult dimResult);
        IDimension Dimension { get; }

        void AddMeasures(List<IMeasure> measures);
    }

    public class QueryDimension<TDimension, TFact> : IQueryDimension
        where TDimension : IComparable
    {
        public Dimension<TDimension, TFact> Dimension { get; private set; }
        IDimension IQueryDimension.Dimension { get { return Dimension; } }
        public List<IMeasure> Measures { get; private set; }

        public QueryDimension(Dimension<TDimension, TFact> dim)
        {
            this.Dimension = dim;
        }

        public void AddMeasures(List<IMeasure> measures)
        {
            this.Measures = measures;
        }

        public void Apply(object item, IDimensionEntryResult dimResult)
        {
            Apply((TFact)item, Dimension, dimResult);
        }

        private void Apply(TFact item, DimensionEntry<TDimension> entry, IDimensionEntryResult result)
        {
            var match = false;
            if (Dimension.Filter == null || Dimension.Filter(item))
            {
                if (Dimension.EndSelector == null)
                {
                    match = entry.InRange(Dimension.Selector(item));
                }
                else
                {
                    match = entry.InRange(Dimension.Selector(item), Dimension.EndSelector(item));
                }
            }

            if (match)
            {
                // Do something
                foreach (var kvp in result.Values)
                {
                    kvp.Key.Apply(kvp.Value, result, item);
                }
                // All other
                foreach (var otherDim in result.OtherDimensions)
                {
                    otherDim.Key.Apply(item, otherDim.Value);
                }
                foreach (DimensionEntry<TDimension> child in entry.Children)
                {
                    Apply(item, child, result.Entries[child]);
                }
            }
        }
    }
}
