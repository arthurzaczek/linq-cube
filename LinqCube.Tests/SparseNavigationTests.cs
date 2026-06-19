using System;
using System.Linq;
using Xunit;

namespace dasz.LinqCube.Tests
{
    /// <summary>
    /// Deep, multi-dimension navigation over a <b>sparse</b> cube (Date → Product → Store). The point of
    /// these tests is the case that used to throw: reaching a coordinate <em>through</em> a parent that was
    /// never materialised — e.g. "Apple sales in Vienna in Feb 2026" when February 2026 had no sales at all.
    /// Every such path must read 0 and stay navigable into the next chained dimension, exactly as a dense
    /// cube would. Mirrors the user's access shape <c>result[date][2026][2][product]["Apple"][store]["Vienna"]</c>.
    /// </summary>
    public class SparseNavigationTests
    {
        private sealed class Sale
        {
            public DateTime Date { get; init; }

            public string Product { get; init; } = "";

            public string Store { get; init; } = "";

            public decimal Amount { get; init; }
        }

        // 2025 has sales (only in Jan and Mar); 2026 has NONE. Apple sells only in Jan 2025; Banana only in
        // Vienna; Cherry only in Graz in Mar. So plenty of valid-but-unmaterialised coordinates to probe.
        private static readonly Sale[] Sales =
        [
            new Sale { Date = new DateTime(2025, 1, 10), Product = "Apple",  Store = "Vienna", Amount = 100m },
            new Sale { Date = new DateTime(2025, 1, 20), Product = "Apple",  Store = "Graz",   Amount = 50m },
            new Sale { Date = new DateTime(2025, 1, 25), Product = "Banana", Store = "Vienna", Amount = 30m },
            new Sale { Date = new DateTime(2025, 3, 5),  Product = "Cherry", Store = "Graz",   Amount = 10m },
        ];

        private sealed record Cube(
            QueryResult Result,
            Dimension<DateTime, Sale> Date,
            Dimension<string, Sale> Product,
            Dimension<string, Sale> Store);

        private static Cube Build(bool sparse)
        {
            var date = new Dimension<DateTime, Sale>("Date", s => s.Date).BuildYear(2025, 2026).BuildMonths().Build<DateTime, Sale>();
            var product = new Dimension<string, Sale>("Product", s => s.Product).BuildEnum("Apple", "Banana", "Cherry").Build<string, Sale>();
            var store = new Dimension<string, Sale>("Store", s => s.Store).BuildEnum("Vienna", "Graz").Build<string, Sale>();

            var query = new Query<Sale>("sales")
                .WithChainedDimension(date)
                .WithChainedDimension(product)
                .WithChainedDimension(store)
                .WithMeasure(new DecimalSumMeasure<Sale>("Amount", s => s.Amount))
                .WithMeasure(new CountMeasure<Sale>("Count", _ => true));

            var result = dasz.LinqCube.Cube.Execute(Sales.AsQueryable(), sparse, query)[query];
            return new Cube(result, date, product, store);
        }

        private static decimal Amount(IDimensionEntryResult n) => n.Values.Values.Single(m => m.Name == "Amount").DecimalValue;

        private static int Count(IDimensionEntryResult n) => n.Values.Values.Single(m => m.Name == "Count").IntValue;

        private static IDimensionEntry Year(Cube c, string label) => c.Date.Children.Single(e => e.Label.Contains(label));

        private static IDimensionEntry Month(IDimensionEntry year, int oneBased) => year.Children.ElementAt(oneBased - 1);

        // ---- materialised paths (sanity: the harness + crossing navigation work) -------------------

        [Fact]
        public void Sparse_FullyMaterialisedLeaf_ReadsItsValue()
        {
            var c = Build(sparse: true);
            var jan = Month(Year(c, "2025"), 1);

            var vienna = c.Result[c.Date][jan][c.Product]["Apple"][c.Store]["Vienna"];
            Assert.Equal(100m, Amount(vienna));
            Assert.Equal(1, Count(vienna));

            var graz = c.Result[c.Date][jan][c.Product]["Apple"][c.Store]["Graz"];
            Assert.Equal(50m, Amount(graz));
        }

        [Fact]
        public void Sparse_IntermediateAggregates_AreCorrect()
        {
            var c = Build(sparse: true);
            var jan = Month(Year(c, "2025"), 1);

            // Whole month: 100 + 50 + 30 = 180 over 3 sales.
            Assert.Equal(180m, Amount(c.Result[c.Date][jan]));
            Assert.Equal(3, Count(c.Result[c.Date][jan]));

            // Apple across both stores in Jan: 100 + 50 over 2 sales.
            var apple = c.Result[c.Date][jan][c.Product]["Apple"];
            Assert.Equal(150m, Amount(apple));
            Assert.Equal(2, Count(apple));
        }

        // ---- empty coordinates reached through materialised parents ---------------------------------

        [Fact]
        public void Sparse_EmptyStore_UnderASoldProduct_ReadsZero()
        {
            var c = Build(sparse: true);
            var jan = Month(Year(c, "2025"), 1);

            // Banana sold only in Vienna in Jan → Graz is empty.
            Assert.Equal(0m, Amount(c.Result[c.Date][jan][c.Product]["Banana"][c.Store]["Graz"]));
        }

        [Fact]
        public void Sparse_EmptyProduct_UnderASoldMonth_StillCrossesIntoStore_AsZero()
        {
            var c = Build(sparse: true);
            var jan = Month(Year(c, "2025"), 1);

            // Cherry was not sold in Jan 2025 → the Product["Cherry"] node is unmaterialised, yet we must be
            // able to cross into Store under it and read 0.
            var cherry = c.Result[c.Date][jan][c.Product]["Cherry"];
            Assert.Equal(0m, Amount(cherry));
            Assert.Equal(0, Count(cherry));
            Assert.Equal(0m, Amount(cherry[c.Store]["Vienna"]));
        }

        // ---- THE headline case: an unmaterialised PARENT, then deeper access ------------------------

        [Fact]
        public void Sparse_EmptyMonth_DeepNavigationIntoProductAndStore_ReadsZero()
        {
            var c = Build(sparse: true);
            var feb2025 = Month(Year(c, "2025"), 2);   // no sales in Feb 2025 at all

            // result[date][2025][Feb][product]["Apple"][store]["Vienna"] — the parent (Feb) is not
            // materialised; the whole chain below it must still resolve to 0 without throwing.
            var node = c.Result[c.Date][feb2025][c.Product]["Apple"][c.Store]["Vienna"];
            Assert.Equal(0m, Amount(node));
            Assert.Equal(0, Count(node));
        }

        [Fact]
        public void Sparse_EmptyYear_DeepNavigation_ReadsZero()
        {
            var c = Build(sparse: true);
            var y2026 = Year(c, "2026");                // 2026 has no sales at all
            var feb2026 = Month(y2026, 2);

            Assert.Equal(0m, Amount(c.Result[c.Date][y2026]));                        // empty year
            Assert.Equal(0, Count(c.Result[c.Date][y2026][feb2026]));                 // empty month within it
            // …and all the way down across two more dimensions.
            var node = c.Result[c.Date][y2026][feb2026][c.Product]["Apple"][c.Store]["Vienna"];
            Assert.Equal(0m, Amount(node));
            Assert.Equal(0, Count(node));
        }

        [Fact]
        public void Sparse_EmptyCoordinate_ExposesItsSubDimensions_ForDirectOtherDimensionsAccess()
        {
            // A consumer may read `node.OtherDimensions` directly instead of indexing (e.g. to grab a
            // nested dimension by name). An empty coordinate must still expose its chained sub-dimension —
            // with empty entries — so that style of access returns 0 rather than throwing.
            var c = Build(sparse: true);
            var feb2025 = Month(Year(c, "2025"), 2);          // empty month
            var febNode = c.Result[c.Date][feb2025];

            var productSub = febNode.OtherDimensions.Single(kv => kv.Key.Dimension.Name == "Product").Value;
            Assert.Empty(productSub.Entries.Values);          // no products under an empty month
            Assert.Equal(0m, Amount(productSub["Apple"][c.Store]["Vienna"]));   // …and still navigable to 0
        }

        [Fact]
        public void Sparse_RepeatedDeepAccess_DoesNotMaterialiseThePath()
        {
            // Empty coordinates are served by transient nodes that must not be stored back into the shared,
            // cached cube. So after deep-reading an empty path, the parent's Entries stay free of it.
            var c = Build(sparse: true);
            var jan = Month(Year(c, "2025"), 1);
            var janResult = c.Result[c.Date][jan];

            _ = c.Result[c.Date][jan][c.Product]["Cherry"][c.Store]["Vienna"];   // touch an empty path
            _ = c.Result[c.Date][jan][c.Product]["Cherry"][c.Store]["Vienna"];   // again

            // Jan's product sub-dimension must not have gained a stored "Cherry" coordinate.
            var products = janResult[c.Product];
            Assert.DoesNotContain(products.Entries.Values, e => e.DimensionEntry.Label == "Cherry");
        }

        // ---- dense and sparse agree on every coordinate (empty or not) ------------------------------

        [Theory]
        [InlineData("2025", 1, "Apple", "Vienna", 100)]
        [InlineData("2025", 1, "Apple", "Graz", 50)]
        [InlineData("2025", 1, "Banana", "Graz", 0)]    // empty store
        [InlineData("2025", 1, "Cherry", "Vienna", 0)]  // empty product
        [InlineData("2025", 2, "Apple", "Vienna", 0)]   // empty month
        [InlineData("2026", 2, "Apple", "Vienna", 0)]   // empty year
        [InlineData("2025", 3, "Cherry", "Graz", 10)]   // materialised, different month
        public void DenseAndSparse_AgreeOnEveryCoordinate(string year, int month, string product, string store, int expected)
        {
            foreach (var sparse in new[] { false, true })
            {
                var c = Build(sparse);
                var m = Month(Year(c, year), month);
                var node = c.Result[c.Date][m][c.Product][product][c.Store][store];
                Assert.Equal(expected, (int)Amount(node));
            }
        }
    }
}
