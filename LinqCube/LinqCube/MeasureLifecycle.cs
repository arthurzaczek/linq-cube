using System;

namespace dasz.LinqCube
{
    /// <summary>
    /// Implemented by a measure result that wants a one-time callback once the cube has aggregated every
    /// fact. <see cref="Cube"/> walks each query's result tree after the single ingestion pass and calls
    /// <see cref="Freeze"/> on every result node that implements this interface. A result uses it to release
    /// build-only scratch and compact its long-lived state — e.g. a distinct-count measure freezing its
    /// build <see cref="System.Collections.Generic.HashSet{T}"/> into a packed array. Results that do not
    /// implement it are left untouched, so the built-in additive measures (sum / count) are unaffected.
    /// </summary>
    public interface IFreezableMeasureResult
    {
        /// <summary>
        /// Called exactly once, after every fact has been aggregated, on the result node that owns this
        /// measure result. Implementations must be idempotent.
        /// </summary>
        /// <param name="node">
        /// The dimension-entry result this measure result belongs to. Its hierarchy children
        /// (<see cref="IDimensionEntryResult.Entries"/>) and chained/crossing sub-dimensions
        /// (<see cref="IDimensionEntryResult.OtherDimensions"/>) are fully built by the time this is called,
        /// so an implementation may inspect the tree shape (e.g. to treat leaf and interior nodes
        /// differently).
        /// </param>
        void Freeze(IDimensionEntryResult node);
    }

    /// <summary>
    /// Implemented by a measure result that retains a per-node set of keys (for example a distinct-count
    /// measure keeping the keys it has seen so a consumer can union them across coordinates for an exact
    /// distinct count). Lets diagnostics account for that retained memory without having to know the
    /// concrete — possibly generic — measure-result type.
    /// </summary>
    public interface IRetainedKeySet
    {
        /// <summary>The number of keys retained at this node.</summary>
        int RetainedKeyCount { get; }
    }
}
