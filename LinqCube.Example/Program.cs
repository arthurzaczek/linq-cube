using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dasz.LinqCube;

namespace dasz.LinqCube.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Building dimensions");
            var time = new Dimension<DateTime, Person>("Time", k => k.Birthday)
                    .BuildYear(1978, Repository.CURRENT_YEAR)
                    .BuildMonths()
                    .Build<DateTime, Person>();

            var time_empstart = new Dimension<DateTime, Person>("Time employment start", k => k.EmploymentStart.Value, k => k.EmploymentStart.HasValue)
                    .BuildYearSlice(Repository.MIN_DATE.Year, Repository.CURRENT_YEAR, 1, null, 9, null) // only look at jan-sept
                    .BuildMonths()
                    .Build<DateTime, Person>();

            var time_employment = new Dimension<DateTime, Person>("Time employment", k => k.EmploymentStart.Value, k => k.EmploymentEnd ?? DateTime.MaxValue, k => k.EmploymentStart.HasValue)
                    .BuildYear(Repository.MIN_DATE.Year, Repository.CURRENT_YEAR)
                    .Build<DateTime, Person>();

            var gender = new Dimension<string, Person>("Gender", k => k.Gender)
                    .BuildEnum("M", "F")
                    .Build<string, Person>();

            var salary = new Dimension<decimal, Person>("Salary", k => k.Salary)
                    .BuildPartition(500, 1000, 2500, "up to {0}", "{0} up to {1}", "{0} and more")
                    .BuildPartition(100)
                    .Build<decimal, Person>();

            Dimension<string, Person> offices = new Dimension<string, Person>("Office", k => k.Office)
                .BuildEnum(Repository.OFFICES)
                .Build<string, Person>();

            Console.WriteLine("Building measures");
            var countAll = new CountMeasure<Person>("Count", k => true);

            var countEmployedFullMonth = new FilteredMeasure<Person, bool>("Count full month", k => k.EmploymentStart.HasValue && k.EmploymentStart.Value.Day == 1, countAll);

            var countStartingEmployment = new CountMeasure<Person>("Count Starting Employment (whole year)", (k, entry) => entry.Count<DateTime>(time_employment, (e) => k.EmploymentStart.HasValue && e.Min.Year == k.EmploymentStart.Value.Year));

            var sumSalary = new DecimalSumMeasure<Person>("Sum of Salaries", k => k.Salary);

            Console.WriteLine("Building queries");
            var genderAgeQuery = new Query<Person>("gender over birthday")
                                    .WithDimension(time)
                                    .WithDimension(gender)
                                    .WithMeasure(countAll);

            var salaryQuery = new Query<Person>("salary over gender and date of employment")
                                    .WithDimension(time_empstart)
                                    .WithDimension(gender)
                                    .WithDimension(salary)
                                    .WithMeasure(countAll)
                                    .WithMeasure(countEmployedFullMonth)
                                    .WithMeasure(sumSalary);

            var countByOfficeQuery = new Query<Person>("count currently employed by office")
                                    .WithDimension(time_employment)
                                    .WithDimension(offices)
                                    .WithMeasure(countAll)
                                    .WithMeasure(countStartingEmployment);

            CubeResult result;
            using (var ctx = new Repository())
            {
                result = Cube.Execute(ctx.Persons.OrderBy(x => x.EmploymentStart),
                                genderAgeQuery,
                                salaryQuery,
                                countByOfficeQuery
                );
            }

            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////

            Console.WriteLine(salaryQuery.Name);
            Console.WriteLine("==================");
            Console.WriteLine();
            foreach (var year in time_empstart)
            {
                Console.WriteLine(year.Label);
                Console.WriteLine("==================");
                foreach (var gPart in salary)
                {
                    foreach (var gPart2 in gPart)
                    {
                        Console.WriteLine("{0}: {1,13}, M: {2,3} W: {3,3}, monthStart: {4,3}",
                            salary.Name,
                            gPart2.Label,
                            result[salaryQuery][year][gPart2][gender]["M"][countAll].IntValue,
                            result[salaryQuery][year][gPart2][gender]["F"][countAll].IntValue,
                            result[salaryQuery][year][gPart2][countEmployedFullMonth].IntValue
                            );
                    }
                }
                Console.WriteLine();
            }

            ////////////////////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////////////////////

            Console.WriteLine(countByOfficeQuery.Name);
            Console.WriteLine("==================");
            Console.WriteLine();
            Console.WriteLine("{0,10}|{1}",
                string.Empty,
                string.Join("|", time_employment.Children.Select(c => string.Format(" {0,6} ", c.Label)).ToArray())
            );
            Console.WriteLine("----------+--------+--------+--------+--------+--------+--------+--------+--------+--------+--------+--------");
            foreach (var officeEntry in offices)
            {
                var officeCounts = result[countByOfficeQuery][officeEntry];
                Console.WriteLine("{0,10}|{1}",
                    officeEntry.Label,
                    string.Join("|", time_employment.Children.Select(c => string.Format(" {0,6} ", officeCounts[c][countAll].IntValue)).ToArray())
                );
                Console.WriteLine("          |{0}",
                    string.Join("|", time_employment.Children.Select(c => string.Format(" {0,6} ", officeCounts[c][countStartingEmployment].IntValue)).ToArray())
                );
            }

            Console.WriteLine("Finished, hit the anykey to exit");
            Console.ReadKey();
        }
    }
}
