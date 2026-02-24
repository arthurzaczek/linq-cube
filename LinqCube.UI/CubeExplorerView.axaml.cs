using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;

namespace dasz.LinqCube.UI;

/// <summary>
/// Holds the information needed to display a query node in the tree.
/// </summary>
public class QueryTreeNode
{
    public string Title { get; set; } = string.Empty;
    public IQuery? Query { get; set; }
    public IDimensionEntry? DimensionEntry { get; set; }
    public IMeasure? Measure { get; set; }
    public ObservableCollection<QueryTreeNode> Children { get; set; } = new();

    /// <summary>
    /// Tag to identify node type: "query", "dimension", "dimensionentry", "measure"
    /// </summary>
    public string NodeType { get; set; } = string.Empty;

    public override string ToString() => Title;
}

public partial class CubeExplorerView : UserControl
{
    private CubeResult? _cubeResult;
    private readonly List<QueryInfo> _queries = new();

    /// <summary>
    /// Holds query metadata for tree building.
    /// </summary>
    public class QueryInfo
    {
        public required IQuery Query { get; init; }
        public required List<IDimension> Dimensions { get; init; }
        public required List<IMeasure> Measures { get; init; }
    }

    public CubeExplorerView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Load cube data into the explorer view.
    /// </summary>
    public void LoadCubeResult(CubeResult result, IEnumerable<QueryInfo> queries)
    {
        _cubeResult = result;
        _queries.Clear();
        _queries.AddRange(queries);

        BuildTree();
    }

    private void BuildTree()
    {
        var treeItems = new ObservableCollection<QueryTreeNode>();

        foreach (var qi in _queries)
        {
            var queryNode = new QueryTreeNode
            {
                Title = qi.Query.Name,
                Query = qi.Query,
                NodeType = "query"
            };

            // Add dimension nodes
            var dimsNode = new QueryTreeNode { Title = "Dimensions", NodeType = "folder" };
            foreach (var dim in qi.Dimensions)
            {
                var dimNode = new QueryTreeNode
                {
                    Title = dim.Name,
                    DimensionEntry = dim,
                    Query = qi.Query,
                    NodeType = "dimension"
                };
                AddDimensionEntryChildren(dimNode, dim, qi.Query);
                dimsNode.Children.Add(dimNode);
            }
            queryNode.Children.Add(dimsNode);

            // Add measure nodes
            var measuresNode = new QueryTreeNode { Title = "Measures", NodeType = "folder" };
            foreach (var measure in qi.Measures)
            {
                measuresNode.Children.Add(new QueryTreeNode
                {
                    Title = measure.Name,
                    Measure = measure,
                    Query = qi.Query,
                    NodeType = "measure"
                });
            }
            queryNode.Children.Add(measuresNode);

            treeItems.Add(queryNode);
        }

        QueryTreeView.ItemsSource = treeItems;
    }

    private void AddDimensionEntryChildren(QueryTreeNode parentNode, IDimensionEntry entry, IQuery query)
    {
        foreach (var child in entry.Children)
        {
            var childNode = new QueryTreeNode
            {
                Title = child.Label,
                DimensionEntry = child,
                Query = query,
                NodeType = "dimensionentry"
            };
            AddDimensionEntryChildren(childNode, child, query);
            parentNode.Children.Add(childNode);
        }
    }

    private void OnQueryTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_cubeResult == null) return;
        if (QueryTreeView.SelectedItem is not QueryTreeNode node) return;

        switch (node.NodeType)
        {
            case "query":
                ShowQueryOverview(node);
                break;
            case "dimension":
            case "dimensionentry":
                ShowDimensionData(node);
                break;
        }
    }

    private void ShowQueryOverview(QueryTreeNode node)
    {
        if (node.Query == null || _cubeResult == null) return;

        var qi = _queries.FirstOrDefault(q => q.Query == node.Query);
        if (qi == null) return;

        DataViewTitle.Text = node.Query.Name;
        var queryResult = _cubeResult[node.Query];

        // Show top-level dimension entries with all measures
        var firstDim = qi.Dimensions.FirstOrDefault();
        if (firstDim == null) return;

        ShowDimensionEntries(queryResult, firstDim, qi);
    }

    private void ShowDimensionData(QueryTreeNode node)
    {
        if (node.Query == null || _cubeResult == null || node.DimensionEntry == null) return;

        var qi = _queries.FirstOrDefault(q => q.Query == node.Query);
        if (qi == null) return;

        var queryResult = _cubeResult[node.Query];
        var entry = node.DimensionEntry;

        DataViewTitle.Text = $"{node.Query.Name} — {entry.Label}";

        // Navigate to the result for this dimension entry
        IDimensionEntryResult entryResult;
        try
        {
            entryResult = queryResult[entry];
        }
        catch
        {
            return;
        }

        // If this entry has children, show them
        if (entry.Children.Any())
        {
            ShowChildEntries(entryResult, entry, qi);
        }
        else
        {
            // Leaf node: show measures for this entry
            ShowLeafMeasures(entryResult, entry, qi);
        }
    }

    private void ShowDimensionEntries(QueryResult queryResult, IDimension dim, QueryInfo qi)
    {
        var rows = new List<IDictionary<string, object>>();

        foreach (var child in dim.Children)
        {
            var row = new Dictionary<string, object>();
            row[dim.Name] = child.Label;

            try
            {
                var entryResult = queryResult[child];
                foreach (var measure in qi.Measures)
                {
                    try
                    {
                        var val = entryResult[measure];
                        row[measure.Name] = FormatMeasureValue(val);
                    }
                    catch
                    {
                        row[measure.Name] = "-";
                    }
                }
            }
            catch
            {
                foreach (var measure in qi.Measures)
                {
                    row[measure.Name] = "-";
                }
            }

            rows.Add(row);
        }

        PopulateDataGrid(rows, new[] { dim.Name }.Concat(qi.Measures.Select(m => m.Name)).ToArray());
    }

    private void ShowChildEntries(IDimensionEntryResult parentResult, IDimensionEntry parentEntry, QueryInfo qi)
    {
        var rows = new List<IDictionary<string, object>>();

        foreach (var child in parentEntry.Children)
        {
            var row = new Dictionary<string, object>();
            row[parentEntry.Root.Name] = child.Label;

            try
            {
                var childResult = parentResult[child];
                foreach (var measure in qi.Measures)
                {
                    try
                    {
                        var val = childResult[measure];
                        row[measure.Name] = FormatMeasureValue(val);
                    }
                    catch
                    {
                        row[measure.Name] = "-";
                    }
                }
            }
            catch
            {
                foreach (var measure in qi.Measures)
                {
                    row[measure.Name] = "-";
                }
            }

            rows.Add(row);
        }

        var columns = new[] { parentEntry.Root.Name }.Concat(qi.Measures.Select(m => m.Name)).ToArray();
        PopulateDataGrid(rows, columns);
    }

    private void ShowLeafMeasures(IDimensionEntryResult entryResult, IDimensionEntry entry, QueryInfo qi)
    {
        var rows = new List<IDictionary<string, object>>();

        var row = new Dictionary<string, object>();
        row["Entry"] = entry.Label;
        foreach (var measure in qi.Measures)
        {
            try
            {
                var val = entryResult[measure];
                row[measure.Name] = FormatMeasureValue(val);
            }
            catch
            {
                row[measure.Name] = "-";
            }
        }
        rows.Add(row);

        var columns = new[] { "Entry" }.Concat(qi.Measures.Select(m => m.Name)).ToArray();
        PopulateDataGrid(rows, columns);
    }

    private static object FormatMeasureValue(IMeasureResult val)
    {
        if (val is DecimalMeasureResult)
            return val.DecimalValue.ToString("N2");
        if (val is DoubleMeasureResult)
            return val.DoubleValue.ToString("N2");
        return val.IntValue;
    }

    private void PopulateDataGrid(List<IDictionary<string, object>> rows, string[] columnNames)
    {
        ResultDataGrid.Columns.Clear();
        ResultDataGrid.AutoGenerateColumns = false;

        // Convert rows to list of string arrays for simple index-based binding
        var items = new List<RowItem>();
        foreach (var row in rows)
        {
            var values = new string[columnNames.Length];
            for (int i = 0; i < columnNames.Length; i++)
            {
                values[i] = row.ContainsKey(columnNames[i]) ? row[columnNames[i]]?.ToString() ?? "" : "";
            }
            items.Add(new RowItem(values));
        }

        // Create columns using indexed property binding
        for (int i = 0; i < columnNames.Length; i++)
        {
            ResultDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = columnNames[i],
                Binding = new Avalonia.Data.Binding($"Values[{i}]")
            });
        }

        ResultDataGrid.ItemsSource = items;
    }
}

/// <summary>
/// Simple row wrapper that exposes values by index for DataGrid binding.
/// </summary>
public class RowItem
{
    public string[] Values { get; }

    public RowItem(string[] values)
    {
        Values = values;
    }
}


