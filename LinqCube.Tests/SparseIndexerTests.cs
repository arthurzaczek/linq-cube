using System;
using System.Linq;
using Xunit;

namespace dasz.LinqCube.Tests
{
    /// <summary>
    /// The sparse-cube access contract: indexing a <em>valid</em> coordinate that received no facts must
    /// read <c>0</c> and stay navigable — exactly like a dense cube — never throw or loop. Sparse
    /// materialisation (the opt-in introduced in 10.0) only creates coordinates that receive a fact, so
    /// before the 10.1.2 fix the <see cref="QueryResults"/> indexers threw (string indexer) or recursed
    /// forever (entry indexer, top-level entry) on an empty coordinate. These tests pin the restored
    /// behaviour and guard the dense path against regression.
    /// </summary>
    public class SparseIndexerTests
    {
        private sealed class Fact
        {
            public string Cat { get; init; } = "";

            public DateTime Date { get; init; }
        }

        private const string CountMeasureName = "Count";

        // A, A, C — the "B" coordinate is valid (in the domain) but never receives a fact.
        private static readonly Fact[] CatFacts =
        [
            new Fact { Cat = "A" },
            new Fact { Cat = "A" },
            new Fact { Cat = "C" },
        ];

        private static Dimension<string, Fact> CatDim()
            => new Dimension<string, Fact>("Cat", f => f.Cat).BuildEnum("A", "B", "C").Build<string, Fact>();

        private static Dimension<DateTime, Fact> DateDim()
            => new Dimension<DateTime, Fact>("Date", f => f.Date).BuildYear(2025, 2026).BuildMonths().Build<DateTime, Fact>();

        private static int Count(IDimensionEntryResult node)
            => node.Values.Values.Single(m => m.Name == CountMeasureName).IntValue;

        // The single top-level dimension's root result for a one-chained-dimension query.
        private static IDimensionEntryResult RunCat(bool sparse, params Fact[] facts)
        {
            var query = new Query<Fact>("cat")
                .WithChainedDimension(CatDim())
                .WithMeasure(new CountMeasure<Fact>(CountMeasureName, _ => true));
            return Cube.Execute(facts.AsQueryable(), sparse, query)[query].Values.Single();
        }

        [Fact]
        public void Dense_EmptyCoordinate_ReadsZero()
        {
            // Baseline: in dense mode every coordinate is materialised with a zero measure.
            var cat = RunCat(sparse: false, CatFacts);
            Assert.Equal(2, Count(cat["A"]));
            Assert.Equal(0, Count(cat["B"]));
            Assert.Equal(1, Count(cat["C"]));
        }

        [Fact]
        public void Sparse_MaterialisedCoordinate_ReadsItsValue()
        {
            var cat = RunCat(sparse: true, CatFacts);
            Assert.Equal(2, Count(cat["A"]));
            Assert.Equal(1, Count(cat["C"]));
        }

        [Fact]
        public void Sparse_EmptyCoordinate_ByLabel_ReadsZero()
        {
            // Was: throws (the string indexer did Single() over materialised-only entries).
            var cat = RunCat(sparse: true, CatFacts);
            Assert.Equal(0, Count(cat["B"]));
        }

        [Fact]
        public void Sparse_EmptyCoordinate_ByEntry_ReadsZero_WithoutLooping()
        {
            // Was: infinite recursion — a top-level enum entry's Parent is the dimension root, so the
            // `this[key.Parent][key]` walk re-entered the same node forever.
            var cat = RunCat(sparse: true, CatFacts);
            var b = cat.DimensionEntry.Children.Single(e => e.Label == "B");
            Assert.Equal(0, Count(cat[b]));
        }

        [Fact]
        public void Sparse_IterateDomainAndIndex_YieldsFullOrderedSetWithZeros()
        {
            // The intended pattern: iterate the dimension's defined entries (full domain, in order) and
            // index each — empty coordinates read 0. (Iterating Entries alone would skip "B" under sparse.)
            var cat = RunCat(sparse: true, CatFacts);
            var counts = cat.DimensionEntry.Children.Select(e => Count(cat[e])).ToArray();
            Assert.Equal([2, 0, 1], counts);
        }

        [Fact]
        public void Sparse_InvalidLabel_StillThrows()
        {
            // A label outside the domain is a genuine error and must still surface as one.
            var cat = RunCat(sparse: true, CatFacts);
            Assert.Throws<ArgumentOutOfRangeException>(() => cat["Z"]);
        }

        [Fact]
        public void IndexingAnEmptyCoordinate_DoesNotMaterialiseIt()
        {
            // The fix returns a transient zero node; it must NOT be stored (the built cube is shared and
            // read concurrently). So an empty coordinate stays absent from Entries after being read.
            var cat = RunCat(sparse: true, CatFacts);
            Assert.Equal(0, Count(cat["B"]));
            Assert.DoesNotContain(cat.Entries.Values, e => e.DimensionEntry.Label == "B");
        }

        [Fact]
        public void Sparse_EmptyChildOfAMaterialisedParent_AndDeepEmptyCoordinate_ReadZero()
        {
            // One fact in Jan 2025; Feb 2025 (sibling) and all of 2026 are empty under sparse.
            var query = new Query<Fact>("date")
                .WithChainedDimension(DateDim())
                .WithMeasure(new CountMeasure<Fact>(CountMeasureName, _ => true));
            var date = Cube.Execute(new[] { new Fact { Date = new DateTime(2025, 1, 15) } }.AsQueryable(), true, query)[query].Values.Single();

            var years = date.DimensionEntry.Children.ToList();
            var y2025 = years.Single(e => e.Label.Contains("2025"));
            var y2026 = years.Single(e => e.Label.Contains("2026"));
            var months2025 = y2025.Children.ToList();

            // A live year with an empty sibling month → the month reads 0 (the common real-world case).
            Assert.Equal(1, Count(date[y2025][months2025[0]]));   // Jan 2025 (materialised)
            Assert.Equal(0, Count(date[y2025][months2025[1]]));   // Feb 2025 (empty)

            // An entirely empty year, then navigating a month within that zero node → still 0.
            Assert.Equal(0, Count(date[y2026]));
            Assert.Equal(0, Count(date[y2026][y2026.Children.First()]));
        }
    }
}
