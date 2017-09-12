using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    /// <summary>
    /// Static helper class for building dimensions
    /// </summary>
    public static class DimensionBuilder
    {
        /// <summary>
        /// Finally builds a dimension
        /// </summary>
        /// <typeparam name="TDimension"></typeparam>
        /// <typeparam name="TFact"></typeparam>
        /// <param name="lst"></param>
        /// <returns></returns>
        public static Dimension<TDimension, TFact> Build<TDimension, TFact>(this List<DimensionEntry<TDimension>> lst)
            where TDimension : IComparable
        {
            return (Dimension<TDimension, TFact>)lst.First().Root;
        }

        /// <summary>
        /// Builds a dimension representing Years
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="fromYear"></param>
        /// <param name="thruYear"></param>
        /// <returns></returns>
        public static List<DimensionEntry<DateTime>> BuildYear(this DimensionEntry<DateTime> parent, int fromYear, int thruYear)
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

        /// <summary>
        /// Builds a dimension representing all Years in the the given range
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="from"></param>
        /// <param name="thruDay"></param>
        /// <returns></returns>
        public static List<DimensionEntry<DateTime>> BuildYearRange(this DimensionEntry<DateTime> parent, DateTime from, DateTime thruDay)
        {
            if (from != from.Date) throw new ArgumentOutOfRangeException("from", "contains time component");
            if (thruDay != thruDay.Date) throw new ArgumentOutOfRangeException("thruDay", "contains time component");

            var children = BuildYear(parent, from.Year, thruDay.Year);

            children.First().Min = from;
            children.Last().Max = thruDay.AddDays(1);

            return parent.Children;
        }

        /// <summary>
        /// Builds a dimension representing Years in the given range. This method limits the years individual ends, e.g. 1.1. - 1.3. 
        /// This makes part of years comparable.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="fromYear"></param>
        /// <param name="thruYear"></param>
        /// <param name="sliceFromMonth"></param>
        /// <param name="sliceFromDay"></param>
        /// <param name="sliceThruMonth"></param>
        /// <param name="sliceThruDay"></param>
        /// <returns></returns>
        public static List<DimensionEntry<DateTime>> BuildYearSlice(this DimensionEntry<DateTime> parent, int fromYear, int thruYear, int sliceFromMonth, int? sliceFromDay, int sliceThruMonth, int? sliceThruDay)
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

        /// <summary>
        /// Build years from the given time dimensions
        /// </summary>
        /// <param name="lst"></param>
        /// <returns></returns>
        public static List<DimensionEntry<DateTime>> BuildYear(this List<DimensionEntry<DateTime>> lst)
        {
            foreach (var parent in lst)
            {
                BuildYear(parent, parent.Min.Year, parent.Max.Year);
            }
            return lst.SelectMany(i => i.Children).ToList();
        }

        /// <summary>
        /// Build quaters from the given time dimensions
        /// </summary>
        /// <param name="lst"></param>
        /// <returns></returns>
        public static List<DimensionEntry<DateTime>> BuildQuarter(this List<DimensionEntry<DateTime>> lst)
        {
            foreach (var parent in lst)
            {
                for (int quarter = 1; quarter <= 4; quarter++)
                {
                    var dtFrom = new DateTime(parent.Min.Year, ((quarter - 1) * 3) + 1, 1);
                    var dtUntil = dtFrom.AddMonths(3);
                    if (dtFrom < parent.Min) dtFrom = parent.Min;
                    if (dtUntil > parent.Max) dtUntil = parent.Max;

                    parent.Children.Add(new DimensionEntry<DateTime>(quarter.ToString(), parent)
                    {
                        Min = dtFrom,
                        Max = dtUntil
                    });
                }
            }

            return lst.SelectMany(i => i.Children).ToList();
        }

        /// <summary>
        /// Build months from the given time dimensions
        /// </summary>
        /// <param name="lst"></param>
        /// <returns></returns>
        public static List<DimensionEntry<DateTime>> BuildMonths(this List<DimensionEntry<DateTime>> lst)
        {
            foreach (var parent in lst)
            {
                for (DateTime month = new DateTime(parent.Min.Year, parent.Min.Month, 1); month <= parent.Max; month = month.AddMonths(1))
                {
                    var dtFrom = month;
                    var dtUntil = dtFrom.AddMonths(1);
                    if (dtFrom < parent.Min) dtFrom = parent.Min;
                    if (dtUntil > parent.Max) dtUntil = parent.Max;

                    if (dtUntil != dtFrom)
                    {
                        parent.Children.Add(new DimensionEntry<DateTime>(month.ToString("MM"), parent)
                        {
                            Min = dtFrom,
                            Max = dtUntil
                        });
                    }
                }
            }

            return lst.SelectMany(i => i.Children).ToList();
        }

        /// <summary>
        /// Builds a simple string bases enumeration dimension
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="entries"></param>
        /// <returns></returns>
        public static List<DimensionEntry<string>> BuildEnum(this DimensionEntry<string> parent, params string[] entries)
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

        /// <summary>
        /// Builds a enumeration dimension from the given Enum Type.
        /// </summary>
        /// <typeparam name="TEnum"></typeparam>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static List<DimensionEntry<TEnum>> BuildEnum<TEnum>(this DimensionEntry<TEnum> parent)
            where TEnum : struct, IComparable
        {
            var enumType = typeof(TEnum);
            // TODO: Re-Support this
            // if (!enumType.IsEnum) throw new ArgumentOutOfRangeException("TEnum", "is no enumeration");

            foreach (var e in Enum.GetValues(enumType))
            {
                TEnum value = (TEnum)e;
                parent.Children.Add(new DimensionEntry<TEnum>(e.ToString(), parent)
                {
                    Value = value
                });
            }

            return parent.Children;
        }

        /// <summary>
        /// Build a enum dimension from the given string dimensions
        /// </summary>
        /// <param name="lst"></param>
        /// <returns></returns>
        public static List<DimensionEntry<string>> BuildEnum(this List<DimensionEntry<string>> lst)
        {
            foreach (var parent in lst)
            {
                BuildEnum(parent, parent.Value);
            }
            return lst.SelectMany(i => i.Children).ToList();
        }

        private static List<DimensionEntry<T>> BuildPartition<T>(DimensionEntry<T> parent, T stepSize, T lowerLimit, T upperLimit, Func<T, T, T> add, T minValue, T maxValue, string lowerLabelFormat, string defaultLabelFormat, string upperLabelFormat)
            where T : IComparable
        {
            if (upperLimit.CompareTo(lowerLimit) <= 0) throw new ArgumentOutOfRangeException("upperLimit", "Upper limit must be greater then lower limit");
            if (stepSize.CompareTo(default(T)) <= 0) throw new ArgumentOutOfRangeException("stepSize", "Stepsize must be > 0");

            var prev = minValue;
            for (var limit = lowerLimit; limit.CompareTo(upperLimit) <= 0; limit = add(limit, stepSize))
            {
                var label = prev.CompareTo(minValue) == 0
                    ? string.Format(lowerLabelFormat, limit)
                    : string.Format(defaultLabelFormat, prev, limit);

                parent.Children.Add(new DimensionEntry<T>(label, parent)
                {
                    Min = prev,
                    Max = limit
                });
                prev = limit;
            }
            parent.Children.Add(new DimensionEntry<T>(string.Format(upperLabelFormat, prev), parent)
            {
                Min = prev,
                Max = maxValue
            });

            return parent.Children;
        }


        /// <summary>
        /// Builds a partition dimension.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="stepSize"></param>
        /// <param name="lowerLimit"></param>
        /// <param name="upperLimit"></param>
        /// <returns></returns>
        public static List<DimensionEntry<decimal>> BuildPartition(this DimensionEntry<decimal> parent, decimal stepSize, decimal lowerLimit, decimal upperLimit)
        {
            return BuildPartition(parent, stepSize, lowerLimit, upperLimit, (a, b) => a + b, decimal.MinValue, decimal.MaxValue, "- {0}", "{0} - {1}", "{0} -");
        }

        /// <summary>
        /// Builds a partition dimension
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="stepSize"></param>
        /// <param name="lowerLimit"></param>
        /// <param name="upperLimit"></param>
        /// <returns></returns>
        public static List<DimensionEntry<int>> BuildPartition(this DimensionEntry<int> parent, int stepSize, int lowerLimit, int upperLimit)
        {
            return BuildPartition(parent, stepSize, lowerLimit, upperLimit, (a, b) => a + b, int.MinValue, int.MaxValue, "- {0}", "{0} - {1}", "{0} -");
        }

        /// <summary>
        /// Builds a partition dimension
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="stepSize"></param>
        /// <param name="lowerLimit"></param>
        /// <param name="upperLimit"></param>
        /// <param name="lowerLabelFormat"></param>
        /// <param name="defaultLabelFormat"></param>
        /// <param name="upperLabelFormat"></param>
        /// <returns></returns>
        public static List<DimensionEntry<decimal>> BuildPartition(this DimensionEntry<decimal> parent, decimal stepSize, decimal lowerLimit, decimal upperLimit, string lowerLabelFormat, string defaultLabelFormat, string upperLabelFormat)
        {
            return BuildPartition(parent, stepSize, lowerLimit, upperLimit, (a, b) => a + b, decimal.MinValue, decimal.MaxValue, lowerLabelFormat, defaultLabelFormat, upperLabelFormat);
        }

        /// <summary>
        /// Builds a partition dimension
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="stepSize"></param>
        /// <param name="lowerLimit"></param>
        /// <param name="upperLimit"></param>
        /// <param name="lowerLabelFormat"></param>
        /// <param name="defaultLabelFormat"></param>
        /// <param name="upperLabelFormat"></param>
        /// <returns></returns>
        public static List<DimensionEntry<int>> BuildPartition(this DimensionEntry<int> parent, int stepSize, int lowerLimit, int upperLimit, string lowerLabelFormat, string defaultLabelFormat, string upperLabelFormat)
        {
            return BuildPartition(parent, stepSize, lowerLimit, upperLimit, (a, b) => a + b, int.MinValue, int.MaxValue, lowerLabelFormat, defaultLabelFormat, upperLabelFormat);
        }

        /// <summary>
        /// Builds a partition dimension.
        /// TODO: refactor the methods below like the BuildPartitions above
        /// </summary>
        /// <param name="lst"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Builds a partition dimension.
        /// </summary>
        /// <param name="lst"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public static List<DimensionEntry<int>> BuildPartition(this List<DimensionEntry<int>> lst, int stepSize)
        {
            foreach (var parent in lst)
            {
                if (parent.Min != int.MinValue && parent.Max != int.MaxValue)
                {
                    var prev = parent.Min;
                    for (var limit = prev + stepSize; limit <= parent.Max; limit += stepSize)
                    {
                        parent.Children.Add(new DimensionEntry<int>(string.Format("{0} - {1}", prev, limit), parent)
                        {
                            Min = prev,
                            Max = limit
                        });
                        prev = limit;
                    }
                }
                else
                {
                    parent.Children.Add(new DimensionEntry<int>(parent.Label, parent)
                    {
                        Min = parent.Min,
                        Max = parent.Max
                    });
                }

            }
            return lst.SelectMany(i => i.Children).ToList();
        }

        /// <summary>
        /// Builds a bool dimension.
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public static List<DimensionEntry<bool>> BuildBool(this DimensionEntry<bool> parent)
        {
            parent.Children.Add(new DimensionEntry<bool>(false.ToString(), parent)
            {
                Value = false,
                Min = false,
                Max = false
            });

            parent.Children.Add(new DimensionEntry<bool>(true.ToString(), parent)
            {
                Value = true,
                Min = true,
                Max = true
            });

            return parent.Children;
        }
    }
}
