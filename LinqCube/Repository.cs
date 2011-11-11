using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinqCube
{
    public class Person
    {
        public int ID { get; set; }
        public string Gender { get; set; }
        public DateTime Birthday { get; set; }
        public DateTime EmploymentStart { get; set; }
        public decimal Salary { get; set; }
    }

    public class Repository : IDisposable
    {
        public static readonly int DATA_COUNT = 50000;
        private static List<Person> _persons;

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
                _persons.Add(new Person()
                {
                    ID = i + 1,
                    Gender = rnd.Next(2) == 0 ? "F" : "M",
                    Salary = (decimal)(rnd.NextDouble() * 2500.0 + 500.0),
                    Birthday = DateTime.Today.AddYears(-18).AddDays(-rnd.Next(14600)),
                    EmploymentStart = DateTime.Today.AddDays(-rnd.Next(3650)),
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
