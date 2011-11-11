using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public class Query<TFact>
    {
        public List<IQueryDimension> Dimensions { get; private set; }
        public QueryResult Result { get; private set; }

        public Query()
        {
            Dimensions = new List<IQueryDimension>();
        }

        public void Apply(TFact item)
        {
            if (Result == null) throw new InvalidOperationException("Not initialized yet");

            foreach (IQueryDimension dim in Dimensions)
            {
                var dimResult = Result[dim.Dimension];
                dim.Apply(item, dimResult);
            }
        }

        internal void Initialize()
        {
            Result = new QueryResult();
            foreach (var dim in Dimensions)
            {
                var dimResult = new DimensionResult<TFact>(dim);
                Result[dim.Dimension] = dimResult;
                dimResult.Initialize(Dimensions.Where(i => i != dim));
            }
        }
    }

    public interface IQueryDimension
    {
        void Apply(object item, IDimensionResult dimResult);
        IDimension Dimension { get; }
    }

    public class QueryDimension<TDimension, TFact> : IQueryDimension
        where TDimension : IComparable
    {
        public Dimension<TDimension, TFact> Dimension { get; private set; }
        IDimension IQueryDimension.Dimension { get { return Dimension; } }

        public QueryDimension(Dimension<TDimension, TFact> dim)
        {
            this.Dimension = dim;
        }

        public void Apply(object item, IDimensionResult dimResult)
        {
            Apply((TFact)item, Dimension, dimResult);
        }

        private void Apply(TFact item, IDimensionParent<TDimension> parent, IDimensionResult dimResult)
        {
            foreach (var child in parent.Children)
            {
                var result = dimResult.Entries[child];                
                if (child.InRange(Dimension.Selector(item)))
                {
                    // Do something
                    result.Value++;
                    // All other
                    foreach (var otherDim in result.Dimensions)
                    {
                        otherDim.Key.Apply(item, otherDim.Value);
                    }
                    Apply(item, child, result);
                }
            }
        }
    }
}
