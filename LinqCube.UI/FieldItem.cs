using System.Collections.Generic;
using System.Linq;

namespace dasz.LinqCube.UI;

/// <summary>
/// Represents an available field (dimension or measure) that the user can assign to rows, columns, or values.
/// </summary>
public class FieldItem : System.ComponentModel.INotifyPropertyChanged
{
    private string _label = string.Empty;
    private int _expansionLevel;

    public string Label
    {
        get => _label;
        set { _label = value; PropertyChanged?.Invoke(this, new(nameof(Label))); }
    }

    public IDimension? Dimension { get; set; }
    public IMeasure? Measure { get; set; }

    /// <summary>"dimension" or "measure"</summary>
    public string FieldType { get; set; } = string.Empty;

    /// <summary>
    /// Current hierarchy expansion level (uniform). 0 = top-level children only.
    /// Each increment drills one level deeper into the dimension hierarchy.
    /// </summary>
    public int ExpansionLevel
    {
        get => _expansionLevel;
        set
        {
            _expansionLevel = value;
            // Clear per-entry expansions when uniform level changes
            _expandedEntries.Clear();
            UpdateLabel();
            PropertyChanged?.Invoke(this, new(nameof(ExpansionLevel)));
        }
    }

    /// <summary>
    /// The base name of the dimension (without level indicator).
    /// </summary>
    public string BaseName { get; set; } = string.Empty;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    // ── Per-entry (individual) expansion tracking ──

    /// <summary>
    /// Set of dimension entries that have been individually expanded by the user
    /// (e.g. by double-clicking a row in the data grid).
    /// </summary>
    private readonly HashSet<IDimensionEntry> _expandedEntries = new();

    /// <summary>
    /// Returns true if the given entry has been individually expanded.
    /// </summary>
    public bool IsEntryExpanded(IDimensionEntry entry) => _expandedEntries.Contains(entry);

    /// <summary>
    /// Toggle individual expansion of a specific entry. Returns true if now expanded.
    /// </summary>
    public bool ToggleEntryExpansion(IDimensionEntry entry)
    {
        if (_expandedEntries.Contains(entry))
        {
            // Collapse: also collapse any descendants
            CollapseEntryAndDescendants(entry);
            return false;
        }
        else
        {
            // Only expand if the entry has children
            if (entry.Children.Any())
            {
                _expandedEntries.Add(entry);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Collapse an entry and all of its descendant entries that were expanded.
    /// </summary>
    private void CollapseEntryAndDescendants(IDimensionEntry entry)
    {
        _expandedEntries.Remove(entry);
        foreach (var child in entry.Children)
        {
            if (_expandedEntries.Contains(child))
                CollapseEntryAndDescendants(child);
        }
    }

    /// <summary>
    /// Clear all per-entry expansions.
    /// </summary>
    public void ClearEntryExpansions() => _expandedEntries.Clear();

    /// <summary>
    /// Whether any per-entry expansions are active.
    /// </summary>
    public bool HasEntryExpansions => _expandedEntries.Count > 0;

    // ── Uniform depth helpers ──

    /// <summary>
    /// Returns the maximum hierarchy depth available for this dimension.
    /// 0 means only top-level entries (no children to expand into).
    /// </summary>
    public int MaxDepth
    {
        get
        {
            if (Dimension == null) return 0;
            return GetMaxDepth(Dimension, 0);
        }
    }

    private static int GetMaxDepth(IDimensionEntry entry, int currentDepth)
    {
        var children = entry.Children.ToList();
        if (children.Count == 0) return currentDepth;
        // Only need to check one branch — dimensions are uniform
        return GetMaxDepth(children[0], currentDepth + 1);
    }

    /// <summary>
    /// Updates the display label to reflect the current expansion level.
    /// </summary>
    public void UpdateLabel()
    {
        if (Dimension == null || MaxDepth == 0)
        {
            Label = BaseName;
            return;
        }

        if (ExpansionLevel == 0)
        {
            Label = BaseName;
        }
        else
        {
            // Show depth indicator: find the label of a representative entry at the target level
            var levelName = GetLevelName(Dimension, ExpansionLevel);
            Label = $"{BaseName} ▸ {levelName}";
        }
    }

    /// <summary>
    /// Gets a descriptive name for a given depth level by following the first child down.
    /// </summary>
    private static string GetLevelName(IDimensionEntry entry, int level)
    {
        var current = entry;
        for (int i = 0; i <= level; i++)
        {
            var children = current.Children.ToList();
            if (children.Count == 0) return $"Level {level}";
            current = children[0];
        }
        return $"Level {level} (e.g. {current.Label})";
    }

    public override string ToString() => Label;
}