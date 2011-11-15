using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public static class DimensionBuilder
    {
        public static Dimension<TDimension, TFact> Build<TDimension, TFact>(this List<DimensionEntry<TDimension>> lst)
            where TDimension : IComparable
        {
            return (Dimension<TDimension, TFact>)lst.First().Root;
        }

        public static List<DimensionEntry<DateTime>> BuildYear(this IDimensionParent<DateTime> parent, int fromYear, int thruYear)
        {
            for (int year = fromYear; year <= thruYear; year++)
            {
                var dtFrom = new DateTime(year, 1, 1);
                var dtUntil = new DateTime(year + 1, 1, 1);

                parent.Children.Add(new DimensionEntry<DateTime>(year.ToString(), parent)
                {
                    Min = dtFrom,
                    Max = dtUntil
                });
            }

            return parent.Children;
        }

        public static List<DimensionEntry<DateTime>> BuildYearRange(this IDimensionParent<DateTime> parent, DateTime from, DateTime thruDay)
        {
            if (from != from.Date) throw new ArgumentOutOfRangeException("from", "contains time component");
            if (thruDay != thruDay.Date) throw new ArgumentOutOfRangeException("thruDay", "contains time component");

            var children = BuildYear(parent, from.Year, thruDay.Year);

            children.First().Min = from;
            children.Last().Max = thruDay.AddDays(1);

            return parent.Children;
        }

        public static List<DimensionEntry<DateTime>> BuildYearSlice(this IDimensionParent<DateTime> parent, int fromYear, int thruYear, int sliceFromMonth, int? sliceFromDay, int sliceThruMonth, int? sliceThruDay)
        {
            for (int year = fromYear; year <= thruYear; year++)
            {
                var dtFrom = new DateTime(year, sliceFromMonth, sliceFromDay ?? 1);
                var dtUntil = new DateTime(year, sliceThruMonth, 1);
                if (sliceThruDay.HasValue)
                {
                    dtUntil = dtUntil.AddDays(sliceThruDay.Value - 1 + 1); // offset for starting on the first and adding one day for "thru" -> "max" transformation
                }
                else
                {
                    dtUntil = dtUntil.AddMonths(1);
                }

                parent.Children.Add(new DimensionEntry<DateTime>(year.ToString(), parent)
                {
                    Min = dtFrom,
                    Max = dtUntil
                });
            }

            return parent.Children;
        }

        public static List<DimensionEntry<DateTime>> BuildYear(this List<DimensionEntry<DateTime>> lst)
        {
            foreach (var parent in lst)
            {
                BuildYear(parent, parent.Min.Year, parent.Max.Year);
            }
            return lst.SelectMany(i => i.Children).ToList();
        }

        public static List<DimensionEntry<DateTime>> BuildQuater(this List<DimensionEntry<DateTime>> lst)
        {
            foreach (var parent in lst)
            {
                for (int quater = 1; quater <= 4; quater++)
                {
                    var dtFrom = new DateTime(parent.Min.Year, ((quater - 1) * 3) + 1, 1);
                    var dtUntil = dtFrom.AddMonths(3);
                    if (dtFrom < parent.Min) dtFrom = parent.Min;
                    if (dtUntil > parent.Max) dtUntil = parent.Max;

                    parent.Children.Add(new DimensionEntry<DateTime>(quater.ToString(), parent)
                    {
                        Min = dtFrom,
                        Max = dtUntil
                    });
                }
            }

            return lst.SelectMany(i => i.Children).ToList();
        }

        public static List<DimensionEntry<DateTime>> BuildMonths(this List<DimensionEntry<DateTime>> lst)
        {
            foreach (var parent in lst)
            {
                for (int month = parent.Min.Month; month <= parent.Max.Month; month++)
                {
                    var dtFrom = new DateTime(parent.Min.Year, month, 1);
                    var dtUntil = dtFrom.AddMonths(1);
                    if (dtFrom < parent.Min) dtFrom = parent.Min;
                    if (dtUntil > parent.Max) dtUntil = parent.Max;

                    if (dtUntil != dtFrom)
                    {
                        parent.Children.Add(new DimensionEntry<DateTime>(month.ToString(), parent)
                        {
                            Min = dtFrom,
                            Max = dtUntil
                        });
                    }
                }
            }

            return lst.SelectMany(i => i.Children).ToList();
        }

        public static List<DimensionEntry<string>> BuildEnum(this IDimensionParent<string> parent, params string[] entries)
        {
            foreach (var e in entries)
            {
                parent.Children.Add(new DimensionEntry<string>(e, parent)
                {
                    Value = e
                });
            }

            return parent.Children;
        }

        public static List<DimensionEntry<string>> BuildEnum(this List<DimensionEntry<string>> lst)
        {
            foreach (var parent in lst)
            {
                BuildEnum(parent, parent.Value);
            }
            return lst.SelectMany(i => i.Children).ToList();
        }

        public static List<DimensionEntry<decimal>> BuildPartition(this IDimensionParent<decimal> parent, decimal stepSize, decimal lowerLimit, decimal upperLimit)
        {
            if (upperLimit <= lowerLimit) throw new ArgumentOutOfRangeException("upperLimit", "Upper limit must be greater then lower limit");
            if (stepSize <= 0) throw new ArgumentOutOfRangeException("stepSize", "Stepsize must be > 0");

            var prev = decimal.MinValue;
            for (var limit = lowerLimit; limit <= upperLimit; limit += stepSize)
            {
                parent.Children.Add(new DimensionEntry<decimal>(string.Format("{0} - {1}", prev != decimal.MinValue ? prev.ToString() : string.Empty, limit), parent)
                {
                    Min = prev,
                    Max = limit
                });
                prev = limit;
            }
            parent.Children.Add(new DimensionEntry<decimal>(string.Format("{0} - ", prev), parent)
            {
                Min = prev,
                Max = decimal.MaxValue
            });

            return parent.Children;
        }

        public static List<DimensionEntry<decimal>> BuildPartition(this List<DimensionEntry<decimal>> lst, decimal stepSize)
        {
            foreach (var parent in lst)
            {
                if (parent.Min != decimal.MinValue && parent.Max != decimal.MaxValue)
                {
                    var prev = parent.Min;
                    for (var limit = prev + stepSize; limit <= parent.Max; limit += stepSize)
                    {
                        parent.Children.Add(new DimensionEntry<decimal>(string.Format("{0} - {1}", prev, limit), parent)
                        {
                            Min = prev,
                            Max = limit
                        });
                        prev = limit;
                    }
                }
                else
                {
                    parent.Children.Add(new DimensionEntry<decimal>(parent.Label, parent)
                    {
                        Min = parent.Min,
                        Max = parent.Max
                    });
                }

            }
            return lst.SelectMany(i => i.Children).ToList();
        }
    }
}
