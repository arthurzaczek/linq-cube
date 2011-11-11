using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinqCube.LinqCube
{
    public class DimensionResultEntriesDictionary : Dictionary<IDimensionEntry, IDimensionEntryResult>
    {
        public IDimensionEntryResult this[string key]
        {
            get
            {
                return base[Keys.Single(i => i.Label == key)];
            }
        }
    }

    public class DimensionResultOtherDimensionsDictionary : Dictionary<IQueryDimension, IDimensionResult>
    {
        public IDimensionResult this[IDimension key]
        {
            get
            {
                return base[Keys.Single(i => i.Dimension == key)];
            }
        }
    }
}
