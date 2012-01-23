using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public static class QueryBuilder
    {
        public static Query<TFact> WithPrimaryDimension<TDimension, TFact>(this Query<TFact> q, Dimension<TDimension, TFact> dim)
            where TDimension : IComparable
        {
            if (q == null) throw new ArgumentNullException("q");
            if (q.SecondaryQueryDimensions.Count > 0) throw new InvalidOperationException("Already added secondary dimensions");
            if (q.PrimaryQueryDimensions.Any(i => i.Dimension == (IDimension)dim)) throw new InvalidOperationException("Dimension already added");
            if (dim == null) throw new ArgumentNullException("dim");

            q.PrimaryQueryDimensions.Add(new QueryDimension<TDimension, TFact>(dim));

            return q;
        }

        public static Query<TFact> WithSecondaryDimension<TDimension, TFact>(this Query<TFact> q, Dimension<TDimension, TFact> dim)
            where TDimension : IComparable
        {
            if (q == null) throw new ArgumentNullException("q");
            if (dim == null) throw new ArgumentNullException("dim");
            if (q.SecondaryQueryDimensions.Any(i => i.Dimension == (IDimension)dim)) throw new InvalidOperationException("Dimension already added");

            q.SecondaryQueryDimensions.Add(new QueryDimension<TDimension, TFact>(dim));

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
