using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public interface IMeasure
    {
        string Name { get; }

        void Apply(IMeasureResult result, IDimensionEntryResult entry, object item);

        IMeasureResult CreateResult();
    }

    public abstract class Measure<TFact, TIntermediate> : IMeasure
    {
        private readonly Func<TFact, IDimensionEntryResult, TIntermediate> _selector;
        protected Func<TFact, IDimensionEntryResult, TIntermediate> Selector { get { return _selector; } }

        public string Name { get; private set; }

        public Measure(string name, Func<TFact, IDimensionEntryResult, TIntermediate> selector)
        {
            this._selector = selector;
            this.Name = name;
        }

        public abstract IMeasureResult CreateResult();
        public abstract void Apply(IMeasureResult result, IDimensionEntryResult entry, object item);

        public override string ToString()
        {
            return string.Format("Measure: {0}", Name);
        }
    }

    public class DecimalSumMeasure<TFact> : Measure<TFact, decimal>
    {
        public DecimalSumMeasure(string name, Func<TFact, decimal> selector)
            : this(name, (fact, entry) => selector(fact))
        {
        }

        public DecimalSumMeasure(string name, Func<TFact, IDimensionEntryResult, decimal> selector)
            : base(name, selector)
        {
        }

        public override IMeasureResult CreateResult()
        {
            return new DecimalMeasureResult(this, 0);
        }

        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (DecimalMeasureResult)result;
            myResult.Set(myResult.DecimalValue + Selector((TFact)item, entry));
        }
    }

    public class DoubleSumMeasure<TFact> : Measure<TFact, double>
    {
        public DoubleSumMeasure(string name, Func<TFact, double> selector)
            : base(name, (item, entry) => selector(item))
        {
        }

        public override IMeasureResult CreateResult()
        {
            return new DoubleMeasureResult(this, 0);
        }

        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (DoubleMeasureResult)result;
            myResult.Set(myResult.DoubleValue + Selector((TFact)item, entry));
        }
    }

    public class CountMeasure<TFact> : Measure<TFact, bool>
    {
        public CountMeasure(string name, Func<TFact, bool> selector)
            : this(name, (item, entry) => selector(item))
        {
        }

        public CountMeasure(string name, Func<TFact, IDimensionEntryResult, bool> selector)
            : base(name, selector)
        {
        }

        public override IMeasureResult CreateResult()
        {
            return new IntMeasureResult(this, 0);
        }

        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            var myResult = (IntMeasureResult)result;
            if (Selector((TFact)item, entry))
            {
                myResult.Set(myResult.IntValue + 1);
            }
        }
    }

    public class FilteredMeasure<TFact, TIntermediate> : Measure<TFact, bool>
    {
        public Measure<TFact, TIntermediate> Measure { get; private set; }

        public FilteredMeasure(Func<TFact, bool> filter, Measure<TFact, TIntermediate> measure)
            : this(measure.Name, (fact, entry) => filter(fact), measure)
        {
        }

        public FilteredMeasure(string name, Func<TFact, bool> filter, Measure<TFact, TIntermediate> measure)
            : this(name, (fact, entry) => filter(fact), measure)
        {
        }

        public FilteredMeasure(Func<TFact, IDimensionEntryResult, bool> filter, Measure<TFact, TIntermediate> measure)
            : this(measure.Name, filter, measure)
        {
        }

        public FilteredMeasure(string name, Func<TFact, IDimensionEntryResult, bool> filter, Measure<TFact, TIntermediate> measure)
            : base(name, filter)
        {
            this.Measure = measure;
        }

        public override IMeasureResult CreateResult()
        {
            return this.Measure.CreateResult();
        }

        public override void Apply(IMeasureResult result, IDimensionEntryResult entry, object item)
        {
            if (Selector((TFact)item, entry))
            {
                this.Measure.Apply(result, entry, item);
            }
        }
    }
}
