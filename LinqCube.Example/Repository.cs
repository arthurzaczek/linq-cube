using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube.Example
{
    public class Person
    {
        public int ID { get; set; }
        public string Gender { get; set; }
        public DateTime Birthday { get; set; }
        public DateTime EmploymentStart { get; set; }
        public DateTime? EmploymentEnd { get; set; }
        public decimal Salary { get; set; }
        public string Office { get; set; }
    }

    public class Repository : IDisposable
    {
        public static readonly int DATA_COUNT = 50000;
        public static readonly DateTime MIN_DATE = new DateTime(DateTime.Today.Year - 10, 1, 1);
        public static readonly DateTime MAX_DATE = new DateTime(DateTime.Today.Year + 1, 1, 1);
        public static int CURRENT_YEAR { get { return MAX_DATE.Year - 1; } }

        private static List<Person> _persons;
        public static readonly string[] OFFICES = new[]
        {
            "New York",
            "Vienna",
            "Moscow",
            "Bejing",
            "Sydney",
            "Rio",
        };

        public IQueryable<Person> Persons
        {
            get
            {
                if (_persons == null)
                {
                    CreateTestData();
                }
                return _persons.AsQueryable();
            }
        }

        private void CreateTestData()
        {
            Console.WriteLine("Initializing repository");

            Random rnd = new Random();

            _persons = new List<Person>(DATA_COUNT);
            for (int i = 0; i < DATA_COUNT; i++)
            {
                var empStart = MIN_DATE.AddDays(rnd.Next(3650));
                DateTime? empEnd = empStart.AddDays(rnd.Next(3650 * 2));
                if (empEnd > DateTime.Today)
                {
                    empEnd = null;
                }

                _persons.Add(new Person()
                {
                    ID = i + 1,
                    Gender = rnd.Next(2) == 0 ? "F" : "M",
                    Salary = (decimal)(rnd.NextDouble() * 2500.0 + 500.0),
                    Birthday = MAX_DATE.AddYears(-18).AddDays(-rnd.Next(14600)),
                    EmploymentStart = empStart,
                    EmploymentEnd = empEnd,
                    Office = OFFICES[rnd.Next(OFFICES.Length)],
                });
            }

            Console.WriteLine("Initializing repository finished");
        }

        public void Dispose()
        {
            // Close your database connection here
        }
    }
}
