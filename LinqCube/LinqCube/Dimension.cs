using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public interface IDimensionEntry : IEnumerable<IDimensionEntry>
    {
        string Label { get; }
        IEnumerable<IDimensionEntry> Children { get; }
        IDimensionEntry Parent { get; }
        IDimension Root { get; }
    }

    public interface IDimension : IDimensionEntry
    {
        string Name { get; }
    }

    public class DimensionEntry<TDimension> : IDimensionEntry
        where TDimension : IComparable
    {
        public DimensionEntry(string label, DimensionEntry<TDimension> parent)
        {
            this._parent = parent;
            this.Label = label;
            Children = new List<DimensionEntry<TDimension>>();
        }

        public string Label { get; set; }

        /// <summary>
        /// Min Value, incl.
        /// </summary>
        public virtual TDimension Min { get; set; }

        /// <summary>
        /// Max Value, excl.
        /// </summary>
        public virtual TDimension Max { get; set; }

        private TDimension _value;
        protected bool hasValue { get; private set; }

        /// <summary>
        /// Distinct value
        /// </summary>
        public TDimension Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
                hasValue = true;
                if (_parent != null) _parent.hasValue = true;
            }
        }

        public virtual bool InRange(TDimension value)
        {
            if (hasValue)
            {
                if (Value == null && value == null)
                    return true;
                else if (Value == null)
                    return false;
                else
                    return Value.CompareTo(value) == 0;
            }
            else
            {
                return Min.CompareTo(value) <= 0 && Max.CompareTo(value) > 0;
            }
        }

        public bool InRange(TDimension lower, TDimension upper)
        {
            if (hasValue)
            {
                throw new InvalidOperationException("tried filtering a range on a discrete dimension");
            }
            else
            {
                return (lower.CompareTo(Min) >= 0 && lower.CompareTo(Max) < 0)
                    || (lower.CompareTo(Min) < 0 && upper.CompareTo(Min) >= 0);
            }
        }

        private DimensionEntry<TDimension> _parent;
        public IDimensionEntry Parent { get { return _parent; } }
        public virtual IDimension Root { get { return Parent.Root; } }
        public List<DimensionEntry<TDimension>> Children { get; private set; }
        IEnumerable<IDimensionEntry> IDimensionEntry.Children { get { return Children.Cast<IDimensionEntry>(); } }

        public string DebugOut()
        {
            return DebugOut(0);
        }
        public string DebugOut(int level)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0}{1}: {2} - {3}\n", "".PadLeft(level), Label, Min, Max);
            foreach (var child in Children)
            {
                sb.AppendLine(child.DebugOut(level + 1));
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return Label;
        }

        IDimensionEntry IDimensionEntry.Parent
        {
            get { return Parent as IDimensionEntry; }
        }

        public IEnumerator<IDimensionEntry> GetEnumerator()
        {
            return Children.Cast<IDimensionEntry>().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Children.GetEnumerator();
        }
    }

    /// <summary>
    /// Dimension descriptor, 
    /// </summary>
    public class Dimension<TDimension, TFact> : DimensionEntry<TDimension>, IDimension
        where TDimension : IComparable
    {
        public Dimension(string name, Func<TFact, TDimension> selector)
            : this(name, selector, null, null)
        {
        }

        public Dimension(string name, Func<TFact, TDimension> startSelector, Func<TFact, TDimension> endSelector)
            : this(name, startSelector, endSelector, null)
        {
        }

        public Dimension(string name, Func<TFact, TDimension> selector, Func<TFact, bool> filter)
            : this(name, selector, null, filter)
        {
        }

        public Dimension(string name, Func<TFact, TDimension> startSelector, Func<TFact, TDimension> endSelector, Func<TFact, bool> filter)
            : base(name, null)
        {
            this.Name = name;
            this.Selector = startSelector;
            this.EndSelector = endSelector;
            this.Filter = filter;
        }

        public string Name { get; private set; }
        public Func<TFact, TDimension> Selector { get; private set; }
        public Func<TFact, TDimension> EndSelector { get; private set; }
        public Func<TFact, bool> Filter { get; private set; }

        public override string ToString()
        {
            return "Dim: " + Name;
        }

        public override IDimension Root { get { return this; } }

        public override TDimension Min
        {
            get
            {
                return Children.First().Min;
            }
            set
            {
                throw new NotSupportedException("Cannot set lower boundary value on basic dimension");
            }
        }

        public override TDimension Max
        {
            get
            {
                return Children.Last().Max;
            }
            set
            {
                throw new NotSupportedException("Cannot set upper boundary value on basic dimension");
            }
        }

        public override bool InRange(TDimension value)
        {
            if (hasValue)
                return true;
            else
                return base.InRange(value);
        }
    }
}
