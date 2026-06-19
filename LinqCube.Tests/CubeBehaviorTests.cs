using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace dasz.LinqCube.Tests
{
    /// <summary>
    /// Behavioural parity between a <b>dense</b> and a <b>sparse</b> cube, exercised over the README's
    /// feature set (crossing dimensions with order-independent browsing, a year→month hierarchy, and
    /// additive + distinct-count measures). The two concrete subclasses run this whole set against each
    /// mode, so every assertion here is a *parity* assertion: dense and sparse must agree.
    /// <para>
    /// The single documented difference (README "Sparse cubes"): in sparse mode an empty coordinate is not
    /// materialised, so <b>walking a result's <c>Entries</c> skips the empty ones</b> — while indexing that
    /// same empty coordinate still reads 0 in both modes. That one divergence is asserted in
    /// <see cref="WalkingResultEntries_IncludesEmptyCoordinatesOnlyInDense"/>.
    /// </para>
    /// </summary>
    public abstract class CubeBehaviorTests
    {
        /// <summary>Dense (<c>false</c>) vs sparse (<c>true</c>) — set by the concrete subclass.</summary>
        protected abstract bool Sparse { get; }

        private sealed class Person
        {
            public int Id { get; init; }

            public string Gender { get; init; } = "";

            public string Office { get; init; } = "";

            public DateTime Hired { get; init; }

            public decimal Salary { get; init; }
        }

        // Id 1 appears twice (two facts, one person) → distinct ≠ count. Office "Graz" is in the domain but
        // never occurs → an empty coordinate. June 2025 is the only 2025 activity.
        private static readonly Person[] People =
        [
            new() { Id = 1, Gender = "M", Office = "Vienna", Hired = new DateTime(2024, 1, 10), Salary = 1000m },
            new() { Id = 1, Gender = "M", Office = "Vienna", Hired = new DateTime(2024, 2, 10), Salary = 1000m },
            new() { Id = 2, Gender = "F", Office = "Vienna", Hired = new DateTime(2024, 3, 10), Salary = 2000m },
            new() { Id = 3, Gender = "M", Office = "Linz",   Hired = new DateTime(2025, 6, 10), Salary = 1500m },
        ];

        private sealed record Fixture(
            QueryResult Result,
            Dimension<DateTime, Person> Time,
            Dimension<string, Person> Gender,
            Dimension<string, Person> Office);

        private Fixture Build()
        {
            var time = new Dimension<DateTime, Person>("Time", p => p.Hired).BuildYear(2024, 2025).BuildMonths().Build<DateTime, Person>();
            var gender = new Dimension<string, Person>("Gender", p => p.Gender).BuildEnum("M", "F").Build<string, Person>();
            var office = new Dimension<string, Person>("Office", p => p.Office).BuildEnum("Vienna", "Graz", "Linz").Build<string, Person>();

            var query = new Query<Person>("people")
                .WithCrossingDimension(time)
                .WithCrossingDimension(gender)
                .WithCrossingDimension(office)
                .WithMeasure(new CountMeasure<Person>("Count", _ => true))
                .WithMeasure(new DecimalSumMeasure<Person>("Salary", p => p.Salary))
                .WithMeasure(new DistinctCountMeasure<Person, int>("Persons", p => p.Id));

            var result = Cube.Execute(People.AsQueryable(), Sparse, query)[query];
            return new Fixture(result, time, gender, office);
        }

        private static int Count(IDimensionEntryResult n) => n.Values.Values.Single(m => m.Name == "Count").IntValue;

        private static decimal Salary(IDimensionEntryResult n) => n.Values.Values.Single(m => m.Name == "Salary").DecimalValue;

        private static int Persons(IDimensionEntryResult n) => n.Values.Values.Single(m => m.Name == "Persons").IntValue;

        private static IDimensionEntry Year(Fixture f, string label) => f.Time.Children.Single(y => y.Label.Contains(label));

        // ---- grand totals at a dimension root -------------------------------------------------------

        [Fact]
        public void DimensionRoot_HoldsTheGrandTotal()
        {
            var f = Build();
            var root = f.Result[f.Gender];   // every fact matches the root

            Assert.Equal(4, Count(root));       // 4 rows
            Assert.Equal(5500m, Salary(root));  // 1000+1000+2000+1500
            Assert.Equal(3, Persons(root));     // distinct ids {1,2,3}
        }

        // ---- single coordinate, additive + distinct ------------------------------------------------

        [Fact]
        public void SingleCoordinate_AggregatesCorrectly()
        {
            var f = Build();

            Assert.Equal(3, Count(f.Result[f.Office]["Vienna"]));      // rows 1,2,3
            Assert.Equal(4000m, Salary(f.Result[f.Office]["Vienna"]));
            Assert.Equal(2, Persons(f.Result[f.Office]["Vienna"]));    // distinct ids {1,2}
            Assert.Equal(1, Count(f.Result[f.Office]["Linz"]));
        }

        [Fact]
        public void DistinctCount_IsNotAdditive()
        {
            var f = Build();
            var mVienna = f.Result[f.Gender]["M"][f.Office]["Vienna"];

            Assert.Equal(2, Count(mVienna));     // two rows (id 1 twice)
            Assert.Equal(1, Persons(mVienna));   // …but one distinct person
        }

        // ---- empty (valid) coordinates read 0 ------------------------------------------------------

        [Fact]
        public void EmptyCoordinate_ReadsZero()
        {
            var f = Build();
            var graz = f.Result[f.Office]["Graz"];   // in the domain, never occurs

            Assert.Equal(0, Count(graz));
            Assert.Equal(0m, Salary(graz));
            Assert.Equal(0, Persons(graz));
        }

        [Fact]
        public void EmptyCrossingCoordinate_ReadsZero()
        {
            var f = Build();

            Assert.Equal(0, Count(f.Result[f.Gender]["F"][f.Office]["Linz"]));   // no female in Linz
            Assert.Equal(0, Count(f.Result[f.Office]["Graz"][f.Gender]["M"]));   // through an empty office
        }

        // ---- order-independent crossing navigation -------------------------------------------------

        [Fact]
        public void CrossingNavigation_IsOrderIndependent()
        {
            var f = Build();

            var genderThenOffice = f.Result[f.Gender]["M"][f.Office]["Vienna"];
            var officeThenGender = f.Result[f.Office]["Vienna"][f.Gender]["M"];

            Assert.Equal(2, Count(genderThenOffice));
            Assert.Equal(Count(genderThenOffice), Count(officeThenGender));
            Assert.Equal(Salary(genderThenOffice), Salary(officeThenGender));
            Assert.Equal(Persons(genderThenOffice), Persons(officeThenGender));
        }

        // ---- hierarchy: a year totals its months, empty months read 0 ------------------------------

        [Fact]
        public void Hierarchy_YearTotalsItsMonths_AndEmptyMonthsReadZero()
        {
            var f = Build();
            var y2024 = Year(f, "2024");

            Assert.Equal(3, Count(f.Result[f.Time][y2024]));   // Jan + Feb + Mar
            Assert.Equal(1, Count(f.Result[f.Time][Year(f, "2025")]));

            // The year total equals the sum over its month children.
            var monthSum = y2024.Children.Sum(m => Count(f.Result[f.Time][y2024][m]));
            Assert.Equal(Count(f.Result[f.Time][y2024]), monthSum);

            // April 2024 (4th month) had no hires.
            Assert.Equal(0, Count(f.Result[f.Time][y2024][y2024.Children.ElementAt(3)]));
        }

        // ---- THE one documented divergence ---------------------------------------------------------

        [Fact]
        public void WalkingResultEntries_IncludesEmptyCoordinatesOnlyInDense()
        {
            var f = Build();
            var walked = f.Result[f.Office].Entries.Values
                .Select(e => e.DimensionEntry.Label)
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToArray();

            if (Sparse)
            {
                // Sparse: the empty "Graz" coordinate was never materialised, so it is absent from the walk.
                Assert.Equal(["Linz", "Vienna"], walked);
            }
            else
            {
                // Dense: every domain coordinate exists (zero-filled), so the walk includes empty "Graz".
                Assert.Equal(["Graz", "Linz", "Vienna"], walked);
            }

            // …yet in BOTH modes, indexing the empty coordinate reads 0 (the access contract is identical).
            Assert.Equal(0, Count(f.Result[f.Office]["Graz"]));
        }
    }

    /// <summary>Runs <see cref="CubeBehaviorTests"/> against a <b>dense</b> cube.</summary>
    public sealed class DenseCubeBehaviorTests : CubeBehaviorTests
    {
        protected override bool Sparse => false;
    }

    /// <summary>Runs <see cref="CubeBehaviorTests"/> against a <b>sparse</b> cube.</summary>
    public sealed class SparseCubeBehaviorTests : CubeBehaviorTests
    {
        protected override bool Sparse => true;
    }
}
