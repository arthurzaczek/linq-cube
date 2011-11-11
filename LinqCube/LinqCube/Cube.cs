using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public class Cube
    {
        public static CubeResult Execute<TFact>(IQueryable<TFact> source, params Query<TFact>[] queries)
        {
            var result = new CubeResult();

            foreach (var query in queries)
            {
                query.Initialize();
            }

            foreach (var item in source)
            {
                foreach (var query in queries)
                {
                    query.Apply(item);
                }
            }

            foreach (var query in queries)
            {
                result.Add(query.Result);
            }

            return result;
        }
    }

    public class CubeResult : List<QueryResult>
    {
        public CubeResult()
        {
        }
    }
}
