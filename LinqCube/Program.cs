using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqCube.LinqCube;

namespace LinqCube
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Building dimensions");
            var time = new Dimension<DateTime, Person>("Time", k => k.Birthday)
                    .BuildYear(new DateTime(1978, 1, 1), new DateTime(2012, 1, 1))
                    .BuildMonths()
                    .Build<DateTime, Person>();

            var time_empstart = new Dimension<DateTime, Person>("Time employment start", k => k.EmploymentStart)
                    .BuildYear(new DateTime(2001, 1, 1), new DateTime(2011, 1, 1))
                    .Build<DateTime, Person>();

            var gender = new Dimension<string, Person>("Gender", k => k.Gender)
                    .BuildEnum("M", "F")
                    .Build<string, Person>();

            var salary = new Dimension<decimal, Person>("Salary", k => k.Salary)
                    .BuildPartition(500, 1000, 2500)
                    .BuildPartition(100)
                    .Build<decimal, Person>();

            CubeResult result;
            using (var ctx = new Repository())
            {
                result = Cube.Execute(ctx.Persons,
                                new Query<Person>()
                                    .WithDimension(time)
                                    .WithDimension(gender)
                                    .Count(i => i.ID),
                                new Query<Person>()
                                    .WithDimension(time_empstart)
                                    .WithDimension(gender)
                                    .WithDimension(salary)
                                    .Count(i => i.ID),
                                new Query<Person>()
                                    .WithDimension(time_empstart)
                                    .Count(i => i.ID)
                );
            }

            foreach (var year in time_empstart.Children)
            {
                Console.WriteLine(year.Label);
                Console.WriteLine("==================");
                foreach (var gPart in salary.Children)
                {
                    foreach (var gPart2 in gPart.Children)
                    {
                        Console.WriteLine("{0}: {1,12}, M: {2,3} W: {3,3}",
                            salary.Name,
                            gPart2.Label,
                            result[1][year][gPart2][gender]["M"].Value,
                            result[1][year][gPart2][gender]["F"].Value);
                    }
                }
                Console.WriteLine();
            }


            Console.WriteLine("Finished, hit the anykey to exit");
            Console.ReadKey();
        }
    }
}
