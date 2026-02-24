using System.Collections.Generic;

namespace dasz.LinqCube.UI;

/// <summary>
/// Simple row wrapper that exposes values by index for DataGrid binding.
/// Carries the dimension entry path so the grid can identify which entry was clicked.
/// </summary>
public class RowItem
{
    public string[] Values { get; }

    /// <summary>
    /// The row dimension entries that produced this row (one per row dimension).
    /// Used to identify which entry was double-clicked for per-entry expansion.
    /// </summary>
    public List<IDimensionEntry> RowEntryPath { get; }

    public RowItem(string[] values, List<IDimensionEntry>? rowEntryPath = null)
    {
        Values = values;
        RowEntryPath = rowEntryPath ?? new List<IDimensionEntry>();
    }
}