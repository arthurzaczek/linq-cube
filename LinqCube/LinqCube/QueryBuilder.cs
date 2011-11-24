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
            if (q == null) throw new ArgumentNullException("q");
            if (dim == null) throw new ArgumentNullException("dim");
            if (q.QueryDimensions.FirstOrDefault(i => i.Dimension == (IDimension)dim) != null) throw new InvalidOperationException("Dimension already added");
            q.QueryDimensions.Add(new QueryDimension<TDimension, TFact>(dim));
            return q;
        }

        public static Query<TFact> WithMeasure<TIntermediate, TFact>(this Query<TFact> q, Measure<TFact, TIntermediate> measure)
        {
            if (q == null) throw new ArgumentNullException("q");
            if (measure == null) throw new ArgumentNullException("measure");
            if (q.Measures.Contains(measure))
                throw new InvalidOperationException("Measure already added");
            q.Measures.Add(measure);
            return q;
        }
    }
}
