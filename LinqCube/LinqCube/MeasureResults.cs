using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    /// <summary>
    /// A result node in the cube of a measure.
    /// </summary>
    public interface IMeasureResult
    {
        /// <summary>
        /// Name of the measure
        /// </summary>
        string Name { get; }

        /// <summary>The number of records that were aggregated to create this result.</summary>
        int Count { get; }

        /// <summary>The aggregated value devided by the count of records.</summary>
        double Average { get; }

        /// <summary>
        /// result value as interger
        /// </summary>
        int IntValue { get; }
        /// <summary>
        /// result value as double
        /// </summary>
        double DoubleValue { get; }
        /// <summary>
        /// result value as decimal
        /// </summary>
        decimal DecimalValue { get; }

        /// <summary>
        /// result value as DateTme
        /// </summary>
        DateTime DateTimeValue { get; }
        /// <summary>
        /// result value as TimeSpan
        /// </summary>
        TimeSpan TimeSpanValue { get; }
    }

    /// <summary>
    /// A decimal measure result node
    /// </summary>
    public class DecimalMeasureResult : IMeasureResult
    {
        private decimal _value;
        /// <summary>
        /// Creates a new MeasureResult
        /// </summary>
        /// <param name="measure"></param>
        /// <param name="init"></param>
        public DecimalMeasureResult(IMeasure measure, decimal init)
        {
            this._value = init;
            this.Measure = measure;
            this.Count = 0;
        }

        /// <summary>
        /// Returns the unterlying measure
        /// </summary>
        public IMeasure Measure { get; private set; }
        /// <summary>
        /// Name of the measure
        /// </summary>
        public string Name { get { return Measure.Name; } }

        /// <summary>The number of records that were aggregated to create this result.</summary>
        public int Count { get; private set; }

        /// <summary>The aggregated value devided by the count of records.</summary>
        public double Average { get { return Count == 0 ? 0 : (double)_value / (double)Count; } }

        /// <summary>
        /// result value as interger
        /// </summary>
        public int IntValue
        {
            get { return (int)_value; }
        }

        /// <summary>
        /// result value as double
        /// </summary>
        public double DoubleValue
        {
            get { return (double)_value; }
        }

        /// <summary>
        /// result value as decimal
        /// </summary>
        public decimal DecimalValue
        {
            get { return _value; }
        }

        /// <summary>
        /// Not suppored by this measure result
        /// </summary>
        public DateTime DateTimeValue
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Not suppored by this measure result
        /// </summary>
        public TimeSpan TimeSpanValue
        {
            get { throw new NotSupportedException(); }
        }

        internal void Set(decimal item)
        {
            _value = item;
            Count += 1;
        }

        /// <summary>
        /// Returns a string represenation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("res: {0} = {1}", Name, _value);
        }
    }

    /// <summary>
    /// A double measure result node
    /// </summary>
    public class DoubleMeasureResult : IMeasureResult
    {
        private double _value;
        /// <summary>
        /// Creates a new MeasureResult
        /// </summary>
        /// <param name="measure"></param>
        /// <param name="init"></param>
        public DoubleMeasureResult(IMeasure measure, double init)
        {
            this._value = init;
            this.Measure = measure;
            this.Count = 0;
        }

        /// <summary>
        /// Returns the unterlying measure
        /// </summary>
        public IMeasure Measure { get; private set; }
        /// <summary>
        /// Name of the measure
        /// </summary>
        public string Name { get { return Measure.Name; } }

        /// <summary>The number of records that were aggregated to create this result.</summary>
        public int Count { get; private set; }

        /// <summary>The aggregated value devided by the count of records.</summary>
        public double Average { get { return Count == 0 ? 0 : (double)_value / (double)Count; } }

        /// <summary>
        /// result value as interger
        /// </summary>
        public int IntValue
        {
            get { return (int)_value; }
        }

        /// <summary>
        /// result value as double
        /// </summary>
        public double DoubleValue
        {
            get { return _value; }
        }

        /// <summary>
        /// result value as decimal
        /// </summary>
        public decimal DecimalValue
        {
            get { return (decimal)_value; }
        }

        /// <summary>
        /// Not suppored by this measure result
        /// </summary>
        public DateTime DateTimeValue
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Not suppored by this measure result
        /// </summary>
        public TimeSpan TimeSpanValue
        {
            get { throw new NotSupportedException(); }
        }

        internal void Set(double item)
        {
            _value = item;
            Count += 1;
        }

        /// <summary>
        /// Returns a string represenation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("res: {0} = {1}", Name, _value);
        }
    }

    /// <summary>
    /// A integer measure result node
    /// </summary>
    public class IntMeasureResult : IMeasureResult
    {
        private int _value;
        /// <summary>
        /// Creates a new MeasureResult
        /// </summary>
        /// <param name="measure"></param>
        /// <param name="init"></param>
        public IntMeasureResult(IMeasure measure, int init)
        {
            this._value = init;
            this.Measure = measure;
            this.Count = 0;
        }

        /// <summary>
        /// Returns the unterlying measure
        /// </summary>
        public IMeasure Measure { get; private set; }
        /// <summary>
        /// Name of the measure
        /// </summary>
        public string Name { get { return Measure.Name; } }

        /// <summary>The number of records that were aggregated to create this result.</summary>
        public int Count { get; private set; }

        /// <summary>The aggregated value devided by the count of records.</summary>
        public double Average { get { return Count == 0 ? 0 : (double)_value / (double)Count; } }

        /// <summary>
        /// result value as interger
        /// </summary>
        public int IntValue
        {
            get { return _value; }
        }

        /// <summary>
        /// result value as double
        /// </summary>
        public double DoubleValue
        {
            get { return _value; }
        }

        /// <summary>
        /// result value as decimal
        /// </summary>
        public decimal DecimalValue
        {
            get { return _value; }
        }

        /// <summary>
        /// Not suppored by this measure result
        /// </summary>
        public DateTime DateTimeValue
        {
            get { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Not suppored by this measure result
        /// </summary>
        public TimeSpan TimeSpanValue
        {
            get { throw new NotSupportedException(); }
        }

        internal void Set(int item)
        {
            _value = item;
            Count += 1;
        }

        /// <summary>
        /// Returns a string represenation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("res: {0} = {1}", Name, _value);
        }
    }

    /// <summary>
    /// A decimal measure result node
    /// </summary>
    public class TimeSpanMeasureResult : IMeasureResult
    {
        private TimeSpan _value;
        /// <summary>
        /// Creates a new MeasureResult
        /// </summary>
        /// <param name="measure"></param>
        /// <param name="init"></param>
        public TimeSpanMeasureResult(IMeasure measure, TimeSpan init)
        {
            this._value = init;
            this.Measure = measure;
            this.Count = 0;
        }

        /// <summary>
        /// Returns the unterlying measure
        /// </summary>
        public IMeasure Measure { get; private set; }
        /// <summary>
        /// Name of the measure
        /// </summary>
        public string Name { get { return Measure.Name; } }

        /// <summary>The number of records that were aggregated to create this result.</summary>
        public int Count { get; private set; }

        /// <summary>The aggregated value devided by the count of records.</summary>
        public double Average { get { throw new NotSupportedException(); } }

        /// <summary>
        /// result value as interger in milliseconds
        /// </summary>
        public int IntValue
        {
            get { return (int)_value.TotalMilliseconds; }
        }

        /// <summary>
        /// result value as double
        /// </summary>
        public double DoubleValue
        {
            get { return _value.TotalMilliseconds; }
        }

        /// <summary>
        /// result value as decimal
        /// </summary>
        public decimal DecimalValue
        {
            get { return (decimal)_value.TotalMilliseconds; }
        }

        /// <summary>
        /// Not suppored by this measure result
        /// </summary>
        public DateTime DateTimeValue
        {
            get { return DateTime.MinValue.Add(_value); }
        }

        /// <summary>
        /// Not suppored by this measure result
        /// </summary>
        public TimeSpan TimeSpanValue
        {
            get { return _value; }
        }

        internal void Set(TimeSpan item)
        {
            _value = item;
            Count += 1;
        }

        /// <summary>
        /// Returns a string represenation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("res: {0} = {1}", Name, _value);
        }
    }

}
