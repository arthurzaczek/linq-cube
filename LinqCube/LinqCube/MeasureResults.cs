using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public interface IMeasureResult
    {
        string Name { get; }

        /// <summary>The number of records that were aggregated to create this result.</summary>
        int Count { get; }

        /// <summary>The aggregated value devided by the count of records.</summary>
        double Average { get; }

        int IntValue { get; }
        double DoubleValue { get; }
        decimal DecimalValue { get; }

        DateTime DateTimeValue { get; }
        TimeSpan TimeSpanValue { get; }
    }

    public class DecimalMeasureResult : IMeasureResult
    {
        private decimal _value;
        public DecimalMeasureResult(IMeasure measure, decimal init)
        {
            this._value = init;
            this.Measure = measure;
            this.Count = 0;
        }

        public IMeasure Measure { get; private set; }
        public string Name { get { return Measure.Name; } }

        public int Count { get; private set; }

        public double Average { get { return Count == 0 ? 0 : (double)_value / (double)Count; } }

        public int IntValue
        {
            get { return (int)_value; }
        }

        public double DoubleValue
        {
            get { return (double)_value; }
        }

        public decimal DecimalValue
        {
            get { return _value; }
        }

        public DateTime DateTimeValue
        {
            get { throw new NotSupportedException(); }
        }

        public TimeSpan TimeSpanValue
        {
            get { throw new NotSupportedException(); }
        }

        public void Set(decimal item)
        {
            _value = item;
            Count += 1;
        }

        public override string ToString()
        {
            return string.Format("res: {0} = {1}", Name, _value);
        }
    }

    public class DoubleMeasureResult : IMeasureResult
    {
        private double _value;
        public DoubleMeasureResult(IMeasure measure, double init)
        {
            this._value = init;
            this.Measure = measure;
            this.Count = 0;
        }

        public IMeasure Measure { get; private set; }
        public string Name { get { return Measure.Name; } }

        public int Count { get; private set; }

        public double Average { get { return Count == 0 ? 0 : (double)_value / (double)Count; } }

        public int IntValue
        {
            get { return (int)_value; }
        }

        public double DoubleValue
        {
            get { return _value; }
        }

        public decimal DecimalValue
        {
            get { return (decimal)_value; }
        }

        public DateTime DateTimeValue
        {
            get { throw new NotSupportedException(); }
        }

        public TimeSpan TimeSpanValue
        {
            get { throw new NotSupportedException(); }
        }

        public void Set(double item)
        {
            _value = item;
            Count += 1;
        }

        public override string ToString()
        {
            return string.Format("res: {0} = {1}", Name, _value);
        }
    }

    public class IntMeasureResult : IMeasureResult
    {
        private int _value;
        public IntMeasureResult(IMeasure measure, int init)
        {
            this._value = init;
            this.Measure = measure;
            this.Count = 0;
        }

        public IMeasure Measure { get; private set; }
        public string Name { get { return Measure.Name; } }

        public int Count { get; private set; }

        public double Average { get { return Count == 0 ? 0 : (double)_value / (double)Count; } }

        public int IntValue
        {
            get { return _value; }
        }

        public double DoubleValue
        {
            get { return _value; }
        }

        public decimal DecimalValue
        {
            get { return _value; }
        }

        public DateTime DateTimeValue
        {
            get { throw new NotSupportedException(); }
        }

        public TimeSpan TimeSpanValue
        {
            get { throw new NotSupportedException(); }
        }

        internal void Set(int item)
        {
            _value = item;
            Count += 1;
        }

        public override string ToString()
        {
            return string.Format("res: {0} = {1}", Name, _value);
        }
    }
}
