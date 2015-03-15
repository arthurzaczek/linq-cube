# Summary #
LinqCube is a small utility library to define measures and dimensions in code an do in-memory cubes against LINQ queries.

Read [the example program](http://code.google.com/p/linq-cube/source/browse/LinqCube.Example/Program.cs) for an overview of the possibilities and syntax.

# Why #
Often you have the requirement to build some reports. For example all open cases over time, all open cases over time and status etc. Your queries are complicated enough to end up in large SQL Views or dozen's of single SQL Queries. Implementing a real OLAP Cube would be an overkill, because you only have 10.000s of rows.

LinqCube was build to reduce SQL Statements and complexity and to have most of the features a real OLAP Cube provides.

# Source, Fact #
The Source is your fact table. It's a simple LinqQuery. It is recommended not to use any aggregations as LinqCube is doing this job. It is also recommended to apply only simple preconditions for data selection. Also, a projection is very helpful, so only the columns you need will be selected from your database.

```
ctx.Persons.Where(x => x.EmploymentStart >= dtReportStart)
```

LinqCube is looping (streaming) your source only once, regardless of the count of LinqCube queries you defined.

# Dimensions #
A Dimension is a definition of how to cluster your facts (source). For example you want to cluster by time (year, quarter, month) or by status (open, resolved, closed; male, female).

A Dimension is a hierarchy. For a time dimension you can define:

```
2010
  Q1
    Jan
    Feb
    Mar
  Q2
    Apr
    May
    Jun
  Q3
    Jul
    Aug
    Sep
  Q4
    Okt
    Nov
    Dec
2011
  Q1
    Jan
    Feb
...
```

Same for decimal ranges
```
      - 1.000
1.000 - 2.000
  1.000 - 1.100
  1.100 - 1.200
  ...
2.000 - 3.000
  ...
3.000 - 4.000
  ...
4.000 - 5.000
  ...
5.000 - 
```

A DimensionEntry and row in your fact table will be linked by a Lambda.

```
var time = new Dimension<DateTime, Person>("Time", k => k.Birthday)
```

It is also possible to link ranges, for example if you want your time dimension to reflect if a person was employed in the given time range

```
new Dimension<DateTime, Person>("Time employment", 
     k => k.EmploymentStart.Value, 
     k => k.EmploymentEnd ?? DateTime.MaxValue)
```

To build a dimension you use extension methods. The most important extension methods are already defined but feel free, to implement your own.

```
// Dimension year - months
var time = new Dimension<DateTime, Person>("Time", k => k.Birthday)
		.BuildYear(1978, Repository.CURRENT_YEAR)
		.BuildMonths()
		.Build<DateTime, Person>();
// a period dimension, only look at jan-sept. very use full for comparing time periods during a year
var time_empstart = new Dimension<DateTime, Person>("Time employment start", k => k.EmploymentStart.Value, k => k.EmploymentStart.HasValue)
		.BuildYearSlice(Repository.MIN_DATE.Year, Repository.CURRENT_YEAR, 1, null, 9, null) 
		.BuildMonths()
		.Build<DateTime, Person>();

// Year only time dimension
var time_employment = new Dimension<DateTime, Person>("Time employment", k => k.EmploymentStart.Value, k => k.EmploymentEnd ?? DateTime.MaxValue, k => k.EmploymentStart.HasValue)
		.BuildYear(Repository.MIN_DATE.Year, Repository.CURRENT_YEAR)
		.Build<DateTime, Person>();

// "Enum" dimension with strings
var gender = new Dimension<string, Person>("Gender", k => k.Gender)
		.BuildEnum("M", "F")
		.Build<string, Person>();

// A partition dimension. Step size 500, from 1000 up to 2500, lower hierarchy has a step size of 100
var salary = new Dimension<decimal, Person>("Salary", k => k.Salary)
		.BuildPartition(500, 1000, 2500, "up to {0}", "{0} up to {1}", "{0} and more")
		.BuildPartition(100)
		.Build<decimal, Person>();

// "Enum" dimension with strings
Dimension<string, Person> offices = new Dimension<string, Person>("Office", k => k.Office)
	.BuildEnum(Repository.OFFICES)
	.Build<string, Person>();
```

# Measures #
A measure is the fact you want to
**count** average
**sum** etc.

You can define as many measures as you like.

Measures are linked to the fact table with a lambda.

```
var sumSalary = new DecimalSumMeasure<Person>("Sum of Salaries", k => k.Salary);
```

If you just want to count rows, return a true.

```
var countAll = new CountMeasure<Person>("Count", k => true);
```

# Query #
When all definitions are done (dimensions and measures) you can define queries. A query is the link between your fact table, dimensions and measures.

```
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
```

LinqCube will select your source (fact table) only one and will apply each row to every query.

```
result = Cube.Execute(ctx.Persons.Where(x => x.EmploymentStart >= dtReportStart),
                genderAgeQuery,
                salaryQuery,
                countByOfficeQuery
);
```

Dimensions, Measures and Queries are not tied to a Cube or it's result. So you can define your dimensions, measures and queries once and reuse them as often needed.

# Results #
You can browse cube using the dimensions as keys and for dimension entries their label:
```
result[salaryQuery][gender]["M"][countAll].IntValue
```

You can also loop dimensions and use dimension entries:
```
foreach (var year in time_empstart)
{
	result[salaryQuery][year][countAll].IntValue;
	result[salaryQuery][year][gender]["M"][countAll].IntValue;
}
```

Note: the order does not count:
```
foreach (var year in time_empstart)
{
	result[salaryQuery][year][gender]["M"][countAll].IntValue;
	result[salaryQuery][gender]["M"][year][countAll].IntValue;
}
```

# Performance #
Each dimension will be cross applied to all other dimensions. Also for each hierarchy. Each dimension entry will be cross applied to all other dimensions (O(n!)).

That means: as more dimensions you link to a query as longer the calculation will take. It is better to define more queries with less dimensions when you only need some combinations.

Good:

```
var genderAgeQuery = new Query<Person>("gender over birthday")
			.WithDimension(time)
			.WithDimension(gender);

var salaryQuery = new Query<Person>("salary over gender and date of employment")
			.WithDimension(time_empstart)
			.WithDimension(gender)
			.WithDimension(salary);

var countByOfficeQuery = new Query<Person>("count currently employed by office")
			.WithDimension(time_employment)
			.WithDimension(offices);
```

Not so good:

```
var allInOnceQuery = new Query<Person>("full cube")
			.WithDimension(time)
			.WithDimension(time_empstart)
			.WithDimension(time_employment)
			.WithDimension(gender)
			.WithDimension(salary)
			.WithDimension(offices);
```

The size and depth of a dimension counts. As deeper as more cross applies (O(n!)). Double length, double cross applies (O(n)).

Of course the amount of selected fact rows also counts. The count of measures does not count that much as they are relative cheap operations compared to cross applying a row to all dimensions. This will change when distinct sum/count measures are introduced.