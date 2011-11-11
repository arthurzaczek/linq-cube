using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinqCube.LinqCube
{
    public class Query<Q>
    {
        public List<IQueryDimension> Dimensions { get; private set; }
        public QueryResult Result { get; private set; }

        public Query()
        {
            Dimensions = new List<IQueryDimension>();
        }

        public void Apply(Q item)
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
                var dimResult = new DimensionResult<Q>(dim);
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

    public class QueryDimension<T, Q> : IQueryDimension
        where T : IComparable
    {
        public Dimension<T, Q> Dimension { get; private set; }
        IDimension IQueryDimension.Dimension { get { return Dimension; } }

        public QueryDimension(Dimension<T, Q> dim)
        {
            this.Dimension = dim;
        }

        public void Apply(object item, IDimensionResult dimResult)
        {
            Apply((Q)item, Dimension, dimResult);
        }

        private void Apply(Q item, IDimensionParent<T> parent, IDimensionResult dimResult)
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
