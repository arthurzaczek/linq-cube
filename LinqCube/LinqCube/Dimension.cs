using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public interface IDimensionParent<TDimension>
        where TDimension : IComparable
    {
        IDimensionParent<TDimension> Parent { get; }
        IDimension Root { get; }
        List<DimensionEntry<TDimension>> Children { get; }
    }

    public interface IDimension
    {
        string Name { get; }
        IEnumerable<IDimensionEntry> Children { get; }
    }

    public interface IDimensionEntry
    {
        string Label { get; }
        IEnumerable<IDimensionEntry> Children { get; }
        IDimensionEntry Parent { get; }
        IDimension Root { get; }
    }

    /// <summary>
    /// Dimension descriptor, 
    /// </summary>
    public class Dimension<TDimension, TFact> : IDimensionParent<TDimension>, IDimension
        where TDimension : IComparable
    {
        public Dimension(string name, Func<TFact, TDimension> selector)
            : this(name, selector, null)
        {
        }

        public Dimension(string name, Func<TFact, TDimension> startSelector, Func<TFact, TDimension> endSelector)
        {
            this.Name = name;
            Children = new List<DimensionEntry<TDimension>>();
            this.Selector = startSelector;
            this.EndSelector = endSelector;
        }

        public string Name { get; private set; }
        public Func<TFact, TDimension> Selector { get; private set; }
        public Func<TFact, TDimension> EndSelector { get; private set; }

        public IDimensionParent<TDimension> Parent { get { return null; } }
        public IDimension Root { get { return this; } }
        public List<DimensionEntry<TDimension>> Children { get; private set; }
        IEnumerable<IDimensionEntry> IDimension.Children { get { return Children.Cast<IDimensionEntry>(); } }

        public override string ToString()
        {
            return "Dim: " + Name;
        }
    }

    public class DimensionEntry<TDimension> : IDimensionParent<TDimension>, IDimensionEntry
        where TDimension : IComparable
    {
        public DimensionEntry(string label, IDimensionParent<TDimension> parent)
        {
            this.Parent = parent;
            this.Label = label;
            Children = new List<DimensionEntry<TDimension>>();
        }

        public string Label { get; set; }

        /// <summary>
        /// Min Value, incl.
        /// </summary>
        public TDimension Min { get; set; }

        /// <summary>
        /// Max Value, excl.
        /// </summary>
        public TDimension Max { get; set; }

        private TDimension _value;
        private bool hasValue = false;
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
            }
        }

        public bool InRange(TDimension value)
        {
            if (hasValue)
            {
                return Value.CompareTo(value) == 0;
            }
            else
            {
                return Min.CompareTo(value) <= 0 && Max.CompareTo(value) > 0;
            }
        }

        internal bool InRange(TDimension lower, TDimension upper)
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

        public IDimensionParent<TDimension> Parent { get; private set; }
        public IDimension Root { get { return Parent.Root; } }
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
    }
}
