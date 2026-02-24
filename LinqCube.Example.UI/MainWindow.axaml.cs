using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using dasz.LinqCube.UI;

namespace dasz.LinqCube.Example.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Load cube data after the window is shown
        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;
        LoadExampleCube();
    }

    private void LoadExampleCube()
    {
        var watch = new Stopwatch();
        watch.Start();

        // Build dimensions (same as console example)
        // Birthday range: MAX_DATE.AddYears(-18).AddDays(-rnd.Next(14600))
        //   oldest = Jan 1 2027 - 18y - 14599d ≈ 1969, youngest = Jan 1 2027 - 18y = 2009
        var birthdayStartYear = Repository.MAX_DATE.AddYears(-18).AddDays(-14599).Year;
        var birthdayEndYear   = Repository.MAX_DATE.AddYears(-18).Year; // exclusive upper bound year

        var time = new Dimension<DateTime, Person>("Time", k => k.Birthday)
            .BuildYear(birthdayStartYear, birthdayEndYear)
            .BuildMonths()
            .Build<DateTime, Person>();

        var time_weeks = new Dimension<DateTime, Person>("Time (Weeks)", k => k.Birthday)
            .BuildYear(birthdayStartYear, birthdayEndYear)
            .BuildWeeks()
            .Build<DateTime, Person>();

        var time_empstart = new Dimension<DateTime, Person>("Time employment start", k => k.EmploymentStart!.Value, k => k.EmploymentStart.HasValue)
            .BuildYearSlice(Repository.MIN_DATE.Year, Repository.CURRENT_YEAR, 1, null, 9, null)
            .BuildMonths()
            .Build<DateTime, Person>();

        var time_employment = new Dimension<DateTime, Person>("Time employment", k => k.EmploymentStart!.Value, k => k.EmploymentEnd ?? DateTime.MaxValue, k => k.EmploymentStart.HasValue)
            .BuildYear(Repository.MIN_DATE.Year, Repository.CURRENT_YEAR)
            .Build<DateTime, Person>();

        var gender = new Dimension<string, Person>("Gender", k => k.Gender)
            .BuildEnum("M", "F")
            .Build<string, Person>();

        var salary = new Dimension<decimal, Person>("Salary", k => k.Salary)
            .BuildPartition(500, 1000, 2500, "up to {0}", "{0} up to {1}", "{0} and more")
            .BuildPartition(100)
            .Build<decimal, Person>();

        var offices = new Dimension<string, Person>("Office", k => k.Office)
            .BuildEnum(Repository.OFFICES)
            .Build<string, Person>();

        var is_active = new Dimension<bool, Person>("Active", k => k.Active)
            .BuildBool()
            .Build<bool, Person>();

        // Build measures
        var countAll = new CountMeasure<Person>("Count", k => true);

        var countEmployedFullMonth = new FilteredMeasure<Person, bool>(
            "Count full month",
            k => k.EmploymentStart.HasValue && k.EmploymentStart.Value.Day == 1,
            countAll);

        var countStartingEmployment = new CountMeasure<Person>(
            "Count Starting Employment",
            (k, entry) => entry.Count<DateTime>(time_employment,
                e => k.EmploymentStart.HasValue && e.Min.Year == k.EmploymentStart.Value.Year));

        var sumSalary = new DecimalSumMeasure<Person>("Sum of Salaries", k => k.Salary);

        // Build queries
        var genderAgeQuery = new Query<Person>("Gender over Birthday (Weeks)")
            .WithChainedDimension(time_weeks)
            .WithChainedDimension(gender)
            .WithMeasure(countAll);

        var salaryQuery = new Query<Person>("Salary over Gender and Employment Start")
            .WithChainedDimension(time_empstart)
            .WithChainedDimension(salary)
            .WithChainedDimension(gender)
            .WithMeasure(countAll)
            .WithMeasure(countEmployedFullMonth)
            .WithMeasure(sumSalary);

        var countByOfficeQuery = new Query<Person>("Count by Office and Employment")
            .WithChainedDimension(offices)
            .WithChainedDimension(time_employment)
            .WithChainedDimension(is_active)
            .WithMeasure(countAll)
            .WithMeasure(countStartingEmployment);

        // Execute cube
        CubeResult result;
        using (var ctx = new Repository())
        {
            result = Cube.Execute(
                ctx.Persons.OrderBy(x => x.EmploymentStart),
                genderAgeQuery,
                salaryQuery,
                countByOfficeQuery
            );
        }

        watch.Stop();

        // Load result into UI
        var queryInfos = new List<CubeExplorerView.QueryInfo>
        {
            new()
            {
                Query = genderAgeQuery,
                Dimensions = new List<IDimension> { time_weeks, gender },
                Measures = new List<IMeasure> { countAll }
            },
            new()
            {
                Query = salaryQuery,
                Dimensions = new List<IDimension> { time_empstart, salary, gender },
                Measures = new List<IMeasure> { countAll, countEmployedFullMonth, sumSalary }
            },
            new()
            {
                Query = countByOfficeQuery,
                Dimensions = new List<IDimension> { offices, time_employment, is_active },
                Measures = new List<IMeasure> { countAll, countStartingEmployment }
            }
        };

        CubeExplorer.LoadCubeResult(result, queryInfos);
        StatusText.Text = $"Cube loaded — {Repository.DATA_COUNT:N0} records processed in {watch.Elapsed.TotalMilliseconds:N0}ms";
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        // Simple about message
        StatusText.Text = "LinqCube Explorer — Demo Application";
    }
}


