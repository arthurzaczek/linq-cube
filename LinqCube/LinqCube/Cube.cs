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
            return Execute(source, false, queries);
        }

        /// <summary>
        /// Executes a cube and builds all results, optionally in <paramref name="sparse"/> mode.
        /// </summary>
        /// <typeparam name="TFact">Type of the underlying fact.</typeparam>
        /// <param name="source">Source</param>
        /// <param name="sparse">
        /// When <see langword="false"/> (default, backwards-compatible) the full dimension cross-product is
        /// materialised up front (every coordinate exists, zero-filled). When <see langword="true"/> result
        /// nodes are created lazily — only coordinates that actually receive a matching fact exist — so the
        /// memory footprint scales with the data, not with the product of the dimension cardinalities.
        /// Additive measures and date-range slicing are unaffected (empty coordinates simply don't exist);
        /// consumers that want a value for an <em>absent</em> coordinate must tolerate it being missing
        /// (iterate the domain + treat missing as zero) rather than assuming dense zero-fill.
        /// </param>
        /// <param name="queries">list of cube queries</param>
        /// <returns>a cube result</returns>
        public static CubeResult Execute<TFact>(IQueryable<TFact> source, bool sparse, params Query<TFact>[] queries)
        {
            var result = new CubeResult();

            foreach (var query in queries)
            {
                query.Initialize(sparse);
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

        public CubeResult Add(CubeResult other)
        {
            foreach (var kv in other)
            {
                this.Add(kv.Key, kv.Value);
            }
            return this;
        }
    }
}
