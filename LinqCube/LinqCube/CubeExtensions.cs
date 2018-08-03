using System;
using System.Collections.Generic;
using System.Text;

namespace dasz.LinqCube
{
    public static class CubeExtensions
    {
        public static TValue Min<TValue>(this IDimensionEntry e) where TValue : IComparable
        {
            return ((DimensionEntry<TValue>)e).Min;
        }
        public static TValue Max<TValue>(this IDimensionEntry e) where TValue : IComparable
        {
            return ((DimensionEntry<TValue>)e).Max;
        }
        public static TValue Value<TValue>(this IDimensionEntry e) where TValue : IComparable
        {
            return ((DimensionEntry<TValue>)e).Value;
        }
    }
}
