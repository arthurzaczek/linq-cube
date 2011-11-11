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

        internal List<IQueryDimension> QueryDimensions { get; private set; }
        public List<IMeasure> Measures { get; private set; }

        public Query(string name)
        {
            Name = name;
            QueryDimensions = new List<IQueryDimension>();
            Measures = new List<IMeasure>();
        }

        public void Apply(TFact item)
        {
            if (Measures.Count == 0) throw new InvalidOperationException("No measures added");
            if (Result == null) throw new InvalidOperationException("Not initialized yet: no result created");

            foreach (IQueryDimension dim in QueryDimensions)
            {
                var dimResult = Result[dim.Dimension];
                dim.Apply(item, dimResult);
            }
        }

        internal void Initialize()
        {
            Result = new QueryResult();

            foreach (var qDim in QueryDimensions)
            {
                qDim.AddMeasures(Measures);

                var dimResult = new DimensionResult<TFact>(qDim, Measures);
                Result[qDim.Dimension] = dimResult;
                dimResult.Initialize(QueryDimensions.Where(i => i != qDim));
            }
        }
    }

    public interface IQueryDimension
    {
        void Apply(object item, IDimensionResult dimResult);
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

        public void Apply(object item, IDimensionResult dimResult)
        {
            Apply((TFact)item, Dimension, dimResult);
        }

        private void Apply(TFact item, IDimensionParent<TDimension> parent, IDimensionResult dimResult)
        {
            foreach (DimensionEntry<TDimension> child in parent.Children)
            {
                IDimensionEntryResult result = dimResult.Entries[child];
                if (child.InRange(Dimension.Selector(item)))
                {
                    // Do something
                    foreach (var measureResult in result.Values.Values)
                    {
                        measureResult.Measure.Apply(measureResult, item);
                    }
                    // All other
                    foreach (var otherDim in result.OtherDimensions)
                    {
                        otherDim.Key.Apply(item, otherDim.Value);
                    }
                    Apply(item, child, result);
                }
            }
        }
    }
}
