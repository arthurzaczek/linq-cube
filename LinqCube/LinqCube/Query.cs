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
        /// The list of primary dimensions. These dimensions can only be accessed in the order of their definition.
        /// </summary>
        /// <remarks>
        /// The runtime of a query is in the order of the product of all entry-counts.
        /// </remarks>
        internal List<IQueryDimension> PrimaryQueryDimensions { get; private set; }

        /// <summary>
        /// The list of secondary dimensions. These dimensions can only be accessed in any order after all primary dimensions were walked.
        /// </summary>
        /// <remarks>
        /// The runtime of a query is O(n^d), where d is the number of secondary query dimensions.
        /// </remarks>
        internal List<IQueryDimension> SecondaryQueryDimensions { get; private set; }

        /// <summary>
        /// The list of top-level query dimensions. This is initialised together with the QueryResult and is used to decouple the executor from the primary/secondary distinction.
        /// </summary>
        private List<IQueryDimension> TopLevelQueryDimensions;

        public List<IMeasure> Measures { get; private set; }

        public Query(string name)
        {
            Name = name;
            PrimaryQueryDimensions = new List<IQueryDimension>();
            SecondaryQueryDimensions = new List<IQueryDimension>();
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

            if (PrimaryQueryDimensions.Count > 0)
            {
                // we have a primary dimension.
                // Create result and recurse initialisation
                var qDim = PrimaryQueryDimensions.First();
                TopLevelQueryDimensions.Add(qDim);
                qDim.AddMeasures(Measures);

                var dimResult = new DimensionResult<TFact>(qDim, Measures);
                ((IDictionary<IDimension, IDimensionEntryResult>)Result)[qDim.Dimension] = dimResult;
                dimResult.Initialize(PrimaryQueryDimensions.Skip(1), SecondaryQueryDimensions, null);
            }
            else
            {
                // no primary dimensions set
                // generate all secondary permutations
                foreach (var qDim in SecondaryQueryDimensions)
                {
                    TopLevelQueryDimensions.Add(qDim);
                    qDim.AddMeasures(Measures);

                    var dimResult = new DimensionResult<TFact>(qDim, Measures);
                    ((IDictionary<IDimension, IDimensionEntryResult>)Result)[qDim.Dimension] = dimResult;
                    dimResult.Initialize(null, SecondaryQueryDimensions.Where(i => i != qDim), null);
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
