using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    /// <summary>
    /// Static class for executing a cube
    /// </summary>
    public static class Cube
    {
        /// <summary>
        /// Executes a cube and build all results
        /// </summary>
        /// <typeparam name="TFact">Type of the underlying fact.</typeparam>
        /// <param name="source">Source</param>
        /// <param name="queries">list of cube queries</param>
        /// <returns>a cube result</returns>
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
                result[query] = query.Result;
            }

            return result;
        }
    }

    /// <summary>
    /// Represents the result of a cube
    /// </summary>
    public class CubeResult : Dictionary<IQuery, QueryResult>
    {
        /// <summary>
        /// Constructs a new cube result
        /// </summary>
        public CubeResult()
        {
        }
    }
}
