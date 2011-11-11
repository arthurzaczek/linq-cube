using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public static class QueryBuilder
    {
        public static Query<TFact> WithDimension<TDimension, TFact>(this Query<TFact> q, Dimension<TDimension, TFact> dim)
            where TDimension : IComparable
        {
            if (q.Dimensions.FirstOrDefault(i => i.Dimension == (IDimension)dim) != null) throw new InvalidOperationException("Dimension already added");
            q.Dimensions.Add(new QueryDimension<TDimension, TFact>(dim));
            return q;
        }

        public static Query<TFact> Count<TDimension, TFact>(this Query<TFact> q, Func<TFact, TDimension> selector)
            where TDimension : IComparable
        {
            return q;
        }
    }
}
