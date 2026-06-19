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

            FinalizeResults(result);

            return result;
        }

        // After the single ingestion pass, give every freezable measure result a one-time callback so it can
        // release build-only scratch and compact its long-lived state (e.g. a distinct-count set becomes a
        // packed array). A cheap O(nodes) walk run once per build; results that do not opt in — the additive
        // built-ins — are simply skipped, so existing cubes are unaffected.
        private static void FinalizeResults(CubeResult result)
        {
            foreach (var queryResult in result.Values)
            {
                foreach (var root in queryResult.Values)
                {
                    FinalizeNode(root);
                }
            }
        }

        // Visits a node and its whole sub-tree once. Hierarchy children live in Entries (e.g. year→month→day)
        // and chained/crossing sub-dimensions in OtherDimensions; the two are disjoint, so no node is visited
        // twice.
        private static void FinalizeNode(IDimensionEntryResult node)
        {
            foreach (var measureResult in node.Values.Values)
            {
                var freezable = measureResult as IFreezableMeasureResult;
                if (freezable != null)
                {
                    freezable.Freeze(node);
                }
            }

            foreach (var other in node.OtherDimensions)
            {
                FinalizeNode(other.Value);
            }

            foreach (var child in node.Entries.Values)
            {
                FinalizeNode(child);
            }
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
