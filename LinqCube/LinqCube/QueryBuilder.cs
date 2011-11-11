using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public static class QueryBuilder
    {
        public static Query<Q> WithDimension<T, Q>(this Query<Q> q, Dimension<T, Q> dim)
            where T : IComparable
        {
            if (q.Dimensions.FirstOrDefault(i => i.Dimension == (IDimension)dim) != null) throw new InvalidOperationException("Dimension already added");
            q.Dimensions.Add(new QueryDimension<T, Q>(dim));
            return q;
        }

        public static Query<Q> Count<T, Q>(this Query<Q> q, Func<Q, T> selector)
            where T : IComparable
        {
            return q;
        }
    }
}
