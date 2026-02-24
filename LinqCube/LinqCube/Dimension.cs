using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    /// <summary>
    /// An entry of a dimension
    /// </summary>
    public interface IDimensionEntry : IEnumerable<IDimensionEntry>
    {
        /// <summary>
        /// Returns the entry label.
        /// </summary>
        string Label { get; }
        /// <summary>
        /// Returns all children
        /// </summary>
        IEnumerable<IDimensionEntry> Children { get; }
        /// <summary>
        /// Returns the parent dimension entry
        /// </summary>
        IDimensionEntry Parent { get; }
        /// <summary>
        /// Return the root dimension
        /// </summary>
        IDimension Root { get; }
    }

    /// <summary>
    /// A dimension. A dimension may be the entry of another dimension.
    /// </summary>
    public interface IDimension : IDimensionEntry
    {
        /// <summary>
        /// Returns the name of the dimension
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Implements a dimension entry
    /// </summary>
    /// <typeparam name="TDimension"></typeparam>
    public class DimensionEntry<TDimension> : IDimensionEntry
        where TDimension : IComparable
    {
        /// <summary>
        /// Creats a new dimension entry
        /// </summary>
        /// <param name="label"></param>
        /// <param name="parent"></param>
        public DimensionEntry(string label, DimensionEntry<TDimension> parent)
        {
            this._parent = parent;
            this.Label = label;
            Children = new List<DimensionEntry<TDimension>>();
        }

        /// <summary>
        /// Returns the entry label.
        /// </summary>
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

        /// <summary>
        /// Signals, that the entry has a value
        /// </summary>
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

        /// <summary>
        /// checks if the given value is in range of this entry
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
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

        /// <summary>
        /// checks if this entry is in the given range
        /// </summary>
        /// <param name="lower"></param>
        /// <param name="upper"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Returns the parent dimension entry
        /// </summary>
        public IDimensionEntry Parent { get { return _parent; } }
        /// <summary>
        /// Return the root dimension
        /// </summary>
        public virtual IDimension Root { get { return Parent.Root; } }
        /// <summary>
        /// Returns all children
        /// </summary>
        public List<DimensionEntry<TDimension>> Children { get; private set; }
        IEnumerable<IDimensionEntry> IDimensionEntry.Children { get { return Children.Cast<IDimensionEntry>(); } }

        /// <summary>
        /// Returns a debug output
        /// </summary>
        /// <returns></returns>
        public string DebugOut()
        {
            return DebugOut(0);
        }
        /// <summary>
        /// Returns a debug output with a left padding
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Returns label
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Label;
        }

        IDimensionEntry IDimensionEntry.Parent
        {
            get { return Parent as IDimensionEntry; }
        }

        /// <summary>
        /// Returns all children
        /// </summary>
        /// <returns></returns>
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
    /// Dimension descriptor
    /// </summary>
    public class Dimension<TDimension, TFact> : DimensionEntry<TDimension>, IDimension
        where TDimension : IComparable
    {
        /// <summary>
        /// Creates a new dimension
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        public Dimension(string name, Func<TFact, TDimension> selector)
            : this(name, selector, null, null)
        {
        }

        /// <summary>
        /// Creates a new dimension
        /// </summary>
        /// <param name="name"></param>
        /// <param name="startSelector"></param>
        /// <param name="endSelector"></param>
        public Dimension(string name, Func<TFact, TDimension> startSelector, Func<TFact, TDimension> endSelector)
            : this(name, startSelector, endSelector, null)
        {
        }

        /// <summary>
        /// Creates a new dimension
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selector"></param>
        /// <param name="filter"></param>
        public Dimension(string name, Func<TFact, TDimension> selector, Func<TFact, bool> filter)
            : this(name, selector, null, filter)
        {
        }

        /// <summary>
        /// Creates a new dimension
        /// </summary>
        /// <param name="name"></param>
        /// <param name="startSelector"></param>
        /// <param name="endSelector"></param>
        /// <param name="filter"></param>
        public Dimension(string name, Func<TFact, TDimension> startSelector, Func<TFact, TDimension> endSelector, Func<TFact, bool> filter)
            : base(name, null)
        {
            this.Name = name;
            this.Selector = startSelector;
            this.EndSelector = endSelector;
            this.Filter = filter;
        }

        /// <summary>
        /// Name of the dimension
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Selects a fact from the query
        /// </summary>
        public Func<TFact, TDimension> Selector { get; private set; }
        /// <summary>
        /// Selects a fact from the query for range dimensions
        /// </summary>
        public Func<TFact, TDimension> EndSelector { get; private set; }
        /// <summary>
        /// Applies a filter
        /// </summary>
        public Func<TFact, bool> Filter { get; private set; }

        /// <summary>
        /// Returns a string representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Dim: " + Name;
        }

        /// <summary>
        /// Returns the root dimension of the current dimension
        /// </summary>
        public override IDimension Root { get { return this; } }

        /// <summary>
        /// Returns the lower boundary
        /// </summary>
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

        /// <summary>
        /// Returns the upper boundary
        /// </summary>
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

        /// <summary>
        /// Checks if a value is in range.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public override bool InRange(TDimension value)
        {
            if (hasValue)
                return true;
            else
                return base.InRange(value);
        }
    }
}
