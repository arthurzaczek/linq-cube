using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinqCube.LinqCube
{
    public class QueryResult : Dictionary<IDimension, IDimensionResult>
    {
        public QueryResult()
        {
        }

        public IDimensionResult this[IDimensionEntry key]
        {
            get
            {
                return this[key.Root][key];
            }
        }
    }

    public interface IDimensionResult
    {
        DimensionResultEntriesDictionary Entries { get; }
        DimensionResultOtherDimensionsDictionary Dimensions { get; }

        IDimensionResult this[IDimension key] { get; }
        IDimensionEntryResult this[IDimensionEntry key] { get; }
        IDimensionEntryResult this[string key] { get; }
    }

    public interface IDimensionEntryResult : IDimensionResult
    {
        int Value { get; set; }
    }

    public class DimensionResult<Q> : IDimensionResult
    {
        public DimensionResult(IQueryDimension dim)
        {
            QueryDimension = dim;
            Entries = new DimensionResultEntriesDictionary();
            Dimensions = new DimensionResultOtherDimensionsDictionary();
        }

        public IQueryDimension QueryDimension { get; private set; }
        public DimensionResultEntriesDictionary Entries { get; private set; }
        public DimensionResultOtherDimensionsDictionary Dimensions { get; private set; }

        public void Initialize(IEnumerable<IQueryDimension> others)
        {
            foreach (var child in QueryDimension.Dimension.Children)
            {
                var result = new DimensionEntryResult<Q>(child);
                Entries[child] = result;
                result.Initialize(others);
            }
        }

        public IDimensionResult this[IDimension key]
        {
            get
            {
                return Dimensions[key];
            }
        }

        public IDimensionEntryResult this[string key]
        {
            get
            {
                return Entries[key];
            }
        }

        public IDimensionEntryResult this[IDimensionEntry key]
        {
            get
            {
                IDimensionEntryResult result;
                if (Entries.TryGetValue(key, out result))
                {
                    return result;
                }
                else
                {
                    if (key.Parent == null) throw new ArgumentOutOfRangeException("key", "key does not match dimension");
                    return this[key.Parent][key];
                }
            }
        }
    }

    public class DimensionEntryResult<Q> : IDimensionEntryResult
    {
        public DimensionEntryResult(IDimensionEntry e)
        {
            Entry = e;
            Entries = new DimensionResultEntriesDictionary();
            Dimensions = new DimensionResultOtherDimensionsDictionary();
        }

        public IDimensionEntry Entry { get; private set; }
        public DimensionResultEntriesDictionary Entries { get; private set; }
        public DimensionResultOtherDimensionsDictionary Dimensions { get; private set; }
        public int Value { get; set; }

        public void Initialize(IEnumerable<IQueryDimension> others)
        {
            foreach (var child in Entry.Children)
            {
                var result = new DimensionEntryResult<Q>(child);
                Entries[child] = result;
                result.Initialize(others);
            }

            foreach (var other in others)
            {
                var otherResult = new DimensionResult<Q>(other);
                Dimensions[other] = otherResult;
                otherResult.Initialize(others.Where(i => i != other));
            }
        }

        public IDimensionResult this[IDimension key]
        {
            get
            {
                return Dimensions[key];
            }
        }

        public IDimensionEntryResult this[string key]
        {
            get
            {
                return Entries[key];
            }
        }

        public IDimensionEntryResult this[IDimensionEntry key]
        {
            get
            {
                IDimensionEntryResult result;
                if (Entries.TryGetValue(key, out result))
                {
                    return result;
                }
                else
                {
                    if (key.Parent != null)
                    {
                        return this[key.Parent][key];
                    }
                    else
                    {
                        foreach (var dim in Dimensions)
                        {
                            if (dim.Key.Dimension == key.Root)
                            {
                                return dim.Value[key];
                            }
                        }
                    }

                    throw new ArgumentOutOfRangeException("key", "key does not match dimension");
                }
            }
        }
    }
}
