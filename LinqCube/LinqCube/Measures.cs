using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    /// <summary>
    /// A measure represents a result. e.g. the sum of all hours.
    /// </summary>
    public interface IMeasure
    {
        /// <summary>
        /// Name of the measure
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Applies a entry to a measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        void Apply(IMeasureResult result, IDimensionEntryResult entry, object item);

        /// <summary>
        /// Returns the result of the measure.
        /// </summary>
        /// <returns></returns>
        IMeasureResult CreateResult();
    }

    /// <summary>
    /// Abstract base class for mesaures.
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    /// <typeparam name="TIntermediate"></typeparam>
    public abstract class Measure<TFact, TIntermediate> : IMeasure
    {
        private readonly Func<TFact, IDimensionEntryResult, TIntermediate> _selector;

        /// <summary>
        /// Selector to extract the value from an entry
        /// </summary>
        protected Func<TFact, IDimensionEntryResult, TIntermediate> Selector { get { return _selector; } }

        /// <summary>
        /// Name of the measure.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public Measure(string name, Func<TFact, IDimensionEntryResult, TIntermediate> selector)
        {
            this._selector = selector;
            this.Name = name;
        }

        /// <summary>
        /// Derived classes should return the result of the measure.
        /// </summary>
        /// <returns></returns>
        public abstract IMeasureResult CreateResult();

        /// <summary>
        /// Derived classes should apply a entry to the measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        public abstract void Apply(IMeasureResult result, IDimensionEntryResult entry, object item);

        /// <summary>
        /// Returns "Measure: {Name}"
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("Measure: {0}", Name);
        }
    }

    /// <summary>
    /// Measure to sum a decimal value.
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    public class DecimalSumMeasure<TFact> : Measure<TFact, decimal>
    {
        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public DecimalSumMeasure(string name, Func<TFact, decimal> selector)
            : this(name, (fact, entry) => selector(fact))
        {
        }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public DecimalSumMeasure(string name, Func<TFact, IDimensionEntryResult, decimal> selector)
            : base(name, selector)
        {
        }

        /// <summary>
        /// Returns the result of the measure.
        /// </summary>
        /// <returns></returns>
        public override IMeasureResult CreateResult()
        {
            return new DecimalMeasureResult(this, 0);
        }

        /// <summary>
        /// Applies a entry to a measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (DecimalMeasureResult)result;
            myResult.Set(myResult.DecimalValue + Selector((TFact)item, entry));
        }
    }

    /// <summary>
    /// Measure to sum a double value.
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    public class DoubleSumMeasure<TFact> : Measure<TFact, double>
    {
        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public DoubleSumMeasure(string name, Func<TFact, double> selector)
            : base(name, (item, entry) => selector(item))
        {
        }

        /// <summary>
        /// Returns the result of the measure.
        /// </summary>
        /// <returns></returns>
        public override IMeasureResult CreateResult()
        {
            return new DoubleMeasureResult(this, 0);
        }

        /// <summary>
        /// Applies a entry to a measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (DoubleMeasureResult)result;
            myResult.Set(myResult.DoubleValue + Selector((TFact)item, entry));
        }
    }

    /// <summary>
    /// Measure to sum a integer value.
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    public class IntSumMeasure<TFact> : Measure<TFact, int>
    {
        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public IntSumMeasure(string name, Func<TFact, int> selector)
            : this(name, (fact, entry) => selector(fact))
        {
        }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public IntSumMeasure(string name, Func<TFact, IDimensionEntryResult, int> selector)
            : base(name, selector)
        {
        }

        /// <summary>
        /// Returns the result of the measure.
        /// </summary>
        /// <returns></returns>
        public override IMeasureResult CreateResult()
        {
            return new DecimalMeasureResult(this, 0);
        }

        /// <summary>
        /// Applies a entry to a measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (DecimalMeasureResult)result;
            myResult.Set(myResult.DecimalValue + Selector((TFact)item, entry));
        }
    }

    /// <summary>
    /// Measure to count items.
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    public class CountMeasure<TFact> : Measure<TFact, bool>
    {
        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public CountMeasure(string name, Func<TFact, bool> selector)
            : this(name, (item, entry) => selector(item))
        {
        }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public CountMeasure(string name, Func<TFact, IDimensionEntryResult, bool> selector)
            : base(name, selector)
        {
        }

        /// <summary>
        /// Returns the result of the measure.
        /// </summary>
        /// <returns></returns>
        public override IMeasureResult CreateResult()
        {
            return new IntMeasureResult(this, 0);
        }

        /// <summary>
        /// Applies a entry to a measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (IntMeasureResult)result;
            if (Selector((TFact)item, entry))
            {
                myResult.Set(myResult.IntValue + 1);
            }
        }
    }

    /// <summary>
    /// Measure, that wraps another measure and applies a filter operation.
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    /// <typeparam name="TIntermediate"></typeparam>
    public class FilteredMeasure<TFact, TIntermediate> : Measure<TFact, bool>
    {
        public Measure<TFact, TIntermediate> Measure { get; private set; }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="measure"></param>
        public FilteredMeasure(Func<TFact, bool> filter, Measure<TFact, TIntermediate> measure)
            : this(measure.Name, (fact, entry) => filter(fact), measure)
        {
        }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="filter"></param>
        /// <param name="measure"></param>
        public FilteredMeasure(string name, Func<TFact, bool> filter, Measure<TFact, TIntermediate> measure)
            : this(name, (fact, entry) => filter(fact), measure)
        {
        }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="measure"></param>
        public FilteredMeasure(Func<TFact, IDimensionEntryResult, bool> filter, Measure<TFact, TIntermediate> measure)
            : this(measure.Name, filter, measure)
        {
        }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="filter"></param>
        /// <param name="measure"></param>
        public FilteredMeasure(string name, Func<TFact, IDimensionEntryResult, bool> filter, Measure<TFact, TIntermediate> measure)
            : base(name, filter)
        {
            this.Measure = measure;
        }

        /// <summary>
        /// Returns the result of the measure.
        /// </summary>
        /// <returns></returns>
        public override IMeasureResult CreateResult()
        {
            return this.Measure.CreateResult();
        }

        /// <summary>
        /// Applies a entry to a measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            if (Selector((TFact)item, entry))
            {
                this.Measure.Apply(result, entry, item);
            }
        }
    }

    /// <summary>
    /// Measure to min a TimeSpan value.
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    public class TimeSpanMinMeasure<TFact> : Measure<TFact, TimeSpan>
    {
        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public TimeSpanMinMeasure(string name, Func<TFact, TimeSpan> selector)
            : this(name, (fact, entry) => selector(fact))
        {
        }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public TimeSpanMinMeasure(string name, Func<TFact, IDimensionEntryResult, TimeSpan> selector)
            : base(name, selector)
        {
        }

        /// <summary>
        /// Returns the result of the measure.
        /// </summary>
        /// <returns></returns>
        public override IMeasureResult CreateResult()
        {
            return new TimeSpanMeasureResult(this, TimeSpan.MaxValue);
        }

        /// <summary>
        /// Applies a entry to a measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (TimeSpanMeasureResult)result;
            var v = Selector((TFact)item, entry);
            if (v < myResult.TimeSpanValue)
                myResult.Set(v);
        }
    }

    /// <summary>
    /// Measure to max a TimeSpan value.
    /// </summary>
    /// <typeparam name="TFact"></typeparam>
    public class TimeSpanMaxMeasure<TFact> : Measure<TFact, TimeSpan>
    {
        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public TimeSpanMaxMeasure(string name, Func<TFact, TimeSpan> selector)
            : this(name, (fact, entry) => selector(fact))
        {
        }

        /// <summary>
        /// Constructs a new Measure
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public TimeSpanMaxMeasure(string name, Func<TFact, IDimensionEntryResult, TimeSpan> selector)
            : base(name, selector)
        {
        }

        /// <summary>
        /// Returns the result of the measure.
        /// </summary>
        /// <returns></returns>
        public override IMeasureResult CreateResult()
        {
            return new TimeSpanMeasureResult(this, TimeSpan.MinValue);
        }

        /// <summary>
        /// Applies a entry to a measure.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="entry"></param>
        /// <param name="item"></param>
        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (TimeSpanMeasureResult)result;
            var v = Selector((TFact)item, entry);
            if (v > myResult.TimeSpanValue)
                myResult.Set(v);
        }
    }
}
