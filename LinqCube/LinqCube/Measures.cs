using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public interface IMeasure
    {
        string Name { get; }

        void Apply(IMeasureResult result, object item);

        IMeasureResult CreateResult();
    }

    public abstract class Measure<TFact, TIntermediate> : IMeasure
    {
        private readonly Func<TFact, TIntermediate> _selector;
        protected Func<TFact, TIntermediate> Selector { get { return _selector; } }

        public string Name { get; private set; }

        public Measure(string name, Func<TFact, TIntermediate> selector)
        {
            this._selector = selector;
            this.Name = name;
        }

        public abstract IMeasureResult CreateResult();
        public abstract void Apply(IMeasureResult result, object item);
    }

    public class DecimalSumMeasure<TFact> : Measure<TFact, decimal>
    {
        public DecimalSumMeasure(string name, Func<TFact, decimal> selector)
            : base(name, selector)
        {
        }

        public override IMeasureResult CreateResult()
        {
            return new DecimalMeasureResult(this, 0);
        }

        public override void Apply(IMeasureResult result, object item)
        {
            var myResult = (DecimalMeasureResult)result;
            myResult.Set(myResult.DecimalValue + Selector((TFact)item));
        }
    }

    public class CountMeasure<TFact> : Measure<TFact, object>
    {
        public CountMeasure(string name, Func<TFact, object> selector)
            : base(name, selector)
        {
        }

        public override IMeasureResult CreateResult()
        {
            return new IntMeasureResult(this, 0);
        }

        public override void Apply(IMeasureResult result, object item)
        {
            var myResult = (IntMeasureResult)result;
            if (Selector((TFact)item) != null)
            {
                myResult.Set(myResult.IntValue + 1);
            }
        }
    }
}
