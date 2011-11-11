using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace dasz.LinqCube
{
    public interface IDimensionParent<T>
        where T : IComparable
    {
        IDimensionParent<T> Parent { get; }
        IDimension Root { get; }
        List<DimensionEntry<T>> Children { get; }
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
    public class Dimension<T, Q> : IDimensionParent<T>, IDimension
        where T : IComparable
    {
        public Dimension(string name, Func<Q, T> selector)
        {
            this.Name = name;
            Children = new List<DimensionEntry<T>>();
            this.Selector = selector;
        }

        public string Name { get; private set; }
        public Func<Q, T> Selector { get; private set; }

        public IDimensionParent<T> Parent { get { return null; } }
        public IDimension Root { get { return this; } }
        public List<DimensionEntry<T>> Children { get; private set; }
        IEnumerable<IDimensionEntry> IDimension.Children { get { return Children.Cast<IDimensionEntry>(); } }

        public override string ToString()
        {
            return "Dim: " + Name;
        }
    }

    public class DimensionEntry<T> : IDimensionParent<T>, IDimensionEntry
        where T : IComparable
    {
        public DimensionEntry(string label, IDimensionParent<T> parent)
        {
            this.Parent = parent;
            this.Label = label;
            Children = new List<DimensionEntry<T>>();
        }

        public string Label { get; set; }

        /// <summary>
        /// Min Value, incl.
        /// </summary>
        public T Min { get; set; }

        /// <summary>
        /// Max Value, excl.
        /// </summary>
        public T Max { get; set; }

        private T _value;
        private bool hasValue = false;
        /// <summary>
        /// Distinct value
        /// </summary>
        public T Value
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

        public bool InRange(T value)
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

        public IDimensionParent<T> Parent { get; private set; }
        public IDimension Root { get { return Parent.Root; } }
        public List<DimensionEntry<T>> Children { get; private set; }
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
