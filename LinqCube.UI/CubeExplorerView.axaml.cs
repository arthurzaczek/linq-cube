using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace dasz.LinqCube.UI;

public partial class CubeExplorerView : UserControl
{
    private CubeResult? _cubeResult;
    private readonly List<QueryInfo> _queries = new();
    private QueryInfo? _selectedQuery;

    // The four zone collections — bound to the ListBoxes
    private readonly ObservableCollection<FieldItem> _availableFields = new();
    private readonly ObservableCollection<FieldItem> _rowDimensions = new();
    private readonly ObservableCollection<FieldItem> _columnDimensions = new();
    private readonly ObservableCollection<FieldItem> _measures = new();

    // Drag state
    private Point _dragStartPoint;
    private bool _isDragging;
    private bool _isPointerPressedForDrag;
    private FieldItem? _dragItem;
    private const double DragThreshold = 5;

    // Custom data format for drag-drop
    private const string FieldItemFormat = "application/x-linqcube-field";

    /// <summary>
    /// Holds query metadata.
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

        AvailableFieldsList.ItemsSource = _availableFields;
        RowDimensionsList.ItemsSource = _rowDimensions;
        ColumnDimensionsList.ItemsSource = _columnDimensions;
        MeasuresList.ItemsSource = _measures;

        // Map ListBoxes to their parent zone Borders for drop resolution
        _listBoxToZone[AvailableFieldsList] = AvailableDropZone;
        _listBoxToZone[RowDimensionsList] = RowDropZone;
        _listBoxToZone[ColumnDimensionsList] = ColumnDropZone;
        _listBoxToZone[MeasuresList] = MeasuresDropZone;

        // Wire up drag initiation on all zone borders using Tunnel strategy
        // so we intercept pointer events before the ListBox handles them
        SetupDragSource(AvailableDropZone);
        SetupDragSource(RowDropZone);
        SetupDragSource(ColumnDropZone);
        SetupDragSource(MeasuresDropZone);

        // Wire up drop targets on the zone borders AND inner ListBoxes
        SetupDropTarget(AvailableDropZone);
        SetupDropTarget(RowDropZone);
        SetupDropTarget(ColumnDropZone);
        SetupDropTarget(MeasuresDropZone);
        SetupDropTarget(AvailableFieldsList);
        SetupDropTarget(RowDimensionsList);
        SetupDropTarget(ColumnDimensionsList);
        SetupDropTarget(MeasuresList);

        // Top-level drag-over handler to position the drag adorner
        AddHandler(DragDrop.DragOverEvent, OnTopLevelDragOver, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragLeaveEvent, OnTopLevelDragLeave, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DropEvent, OnTopLevelDrop, RoutingStrategies.Tunnel);

        // Style rows to indicate expandable entries
        ResultDataGrid.LoadingRow += OnDataGridLoadingRow;
    }

    // ListBox → parent zone Border mapping
    private readonly Dictionary<Control, Border> _listBoxToZone = new();

    // ════════════════════════════════════════════════════════════
    //  Drag source setup — attached to zone Borders with Tunnel
    // ════════════════════════════════════════════════════════════

    private void SetupDragSource(Border zone)
    {
        // Use Tunnel to intercept before the ListBox, plus Bubble to catch events
        // even when the ListBox has captured the pointer
        zone.AddHandler(InputElement.PointerPressedEvent, OnZonePointerPressed, RoutingStrategies.Tunnel);
        zone.AddHandler(InputElement.PointerMovedEvent, OnZonePointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        zone.AddHandler(InputElement.PointerReleasedEvent, OnZonePointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    private void OnZonePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border zone) return;
        var point = e.GetCurrentPoint(zone);
        if (!point.Properties.IsLeftButtonPressed) return;

        // Find the FieldItem under the pointer by walking the visual tree
        var hitElement = zone.InputHitTest(point.Position) as Visual;
        var fieldItem = FindFieldItemFromVisual(hitElement);
        if (fieldItem == null) return;

        _dragStartPoint = e.GetPosition(this);
        _dragItem = fieldItem;
        _isPointerPressedForDrag = true;
        _isDragging = false;
    }

    private async void OnZonePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerPressedForDrag || _dragItem == null || _isDragging) return;

        var currentPos = e.GetPosition(this);
        var diff = currentPos - _dragStartPoint;

        if (Math.Abs(diff.X) < DragThreshold && Math.Abs(diff.Y) < DragThreshold) return;

        _isDragging = true;

        // Release any pointer capture the ListBox may have taken so
        // the drag-drop system gets proper pointer events
        if (e.Pointer.Captured != null)
        {
            e.Pointer.Capture(null);
        }

        var data = new DataObject();
        data.Set(FieldItemFormat, _dragItem);

        // Mark the event as handled so the ListBox doesn't interfere
        e.Handled = true;

        // Show the drag adorner
        ShowDragAdorner(_dragItem, e.GetPosition(this));

        try
        {
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
        finally
        {
            HideDragAdorner();
            _isDragging = false;
            _dragItem = null;
            _isPointerPressedForDrag = false;
        }
    }

    private void OnZonePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragItem = null;
        _isDragging = false;
        _isPointerPressedForDrag = false;
    }

    /// <summary>
    /// Walk up the visual tree from the hit-tested element to find a FieldItem DataContext.
    /// </summary>
    private static FieldItem? FindFieldItemFromVisual(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Control control && control.DataContext is FieldItem fieldItem)
                return fieldItem;
            visual = visual.GetVisualParent();
        }
        return null;
    }

    // ════════════════════════════════════════════════════════════
    //  Drag adorner — floating label that follows the cursor
    // ════════════════════════════════════════════════════════════

    private void ShowDragAdorner(FieldItem item, Point position)
    {
        DragAdornerText.Text = item.Label;
        DragAdornerIcon.Text = item.FieldType == "measure" ? "∑" : "≡";
        Canvas.SetLeft(DragAdornerBorder, position.X + 12);
        Canvas.SetTop(DragAdornerBorder, position.Y + 8);
        DragAdornerLayer.IsVisible = true;
    }

    private void MoveDragAdorner(Point position)
    {
        Canvas.SetLeft(DragAdornerBorder, position.X + 12);
        Canvas.SetTop(DragAdornerBorder, position.Y + 8);
    }

    private void HideDragAdorner()
    {
        DragAdornerLayer.IsVisible = false;
    }

    /// <summary>
    /// Top-level DragOver on the entire UserControl — positions the drag adorner.
    /// Does NOT handle the event so it still reaches the zone drop targets.
    /// </summary>
    private void OnTopLevelDragOver(object? sender, DragEventArgs e)
    {
        if (!DragAdornerLayer.IsVisible) return;
        var pos = e.GetPosition(this);
        MoveDragAdorner(pos);
    }

    private void OnTopLevelDragLeave(object? sender, DragEventArgs e)
    {
        // Don't hide — might just be leaving a child element.
        // The adorner is hidden in the finally block of DoDragDrop.
    }

    private void OnTopLevelDrop(object? sender, DragEventArgs e)
    {
        HideDragAdorner();
    }

    // ════════════════════════════════════════════════════════════
    //  Drop target setup
    // ════════════════════════════════════════════════════════════

    private void SetupDropTarget(Control target)
    {
        target.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        target.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        target.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // Resolve to the zone Border (sender may be a ListBox inside the zone)
        var zone = ResolveZone(sender);
        if (zone == null) { e.DragEffects = DragDropEffects.None; return; }

        if (!e.Data.Contains(FieldItemFormat)) { e.DragEffects = DragDropEffects.None; return; }

        var item = e.Data.Get(FieldItemFormat) as FieldItem;
        if (item == null) { e.DragEffects = DragDropEffects.None; return; }

        if (!IsDropAllowed(item, zone))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;

        // Visual feedback: highlight border
        zone.BorderThickness = new Thickness(3);
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        var zone = ResolveZone(sender);
        if (zone != null) RestoreZoneBorder(zone);
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var zone = ResolveZone(sender);
        if (zone == null) return;

        RestoreZoneBorder(zone);

        if (!e.Data.Contains(FieldItemFormat)) return;
        var item = e.Data.Get(FieldItemFormat) as FieldItem;
        if (item == null) return;

        if (!IsDropAllowed(item, zone)) return;

        var targetCollection = GetCollectionForZone(zone);
        if (targetCollection == null) return;

        // Remove from whichever collection currently has it
        var sourceCollection = FindOwningCollection(item);
        if (sourceCollection == targetCollection) return; // dropped on same zone

        sourceCollection?.Remove(item);
        // Reset expansion level when moving back to available fields
        if (targetCollection == _availableFields)
            item.ExpansionLevel = 0;
        if (!targetCollection.Contains(item))
            targetCollection.Add(item);

        e.Handled = true;
        RefreshPivotTable();
    }

    /// <summary>
    /// Resolve the sender to a zone Border. The sender may be a Border itself
    /// or a ListBox inside a zone.
    /// </summary>
    private Border? ResolveZone(object? sender)
    {
        if (sender is Border border)
        {
            // Check if it's one of our zone borders
            if (border == AvailableDropZone || border == RowDropZone ||
                border == ColumnDropZone || border == MeasuresDropZone)
                return border;
        }

        if (sender is Control control && _listBoxToZone.TryGetValue(control, out var zone))
            return zone;

        return null;
    }

    private bool IsDropAllowed(FieldItem item, Border zone)
    {
        if (zone == MeasuresDropZone)
            return item.FieldType == "measure";
        if (zone == RowDropZone || zone == ColumnDropZone)
            return item.FieldType == "dimension";
        // Available zone accepts anything (it's "remove from assignment")
        return true;
    }

    private ObservableCollection<FieldItem>? GetCollectionForZone(Border zone)
    {
        if (zone == AvailableDropZone) return _availableFields;
        if (zone == RowDropZone) return _rowDimensions;
        if (zone == ColumnDropZone) return _columnDimensions;
        if (zone == MeasuresDropZone) return _measures;
        return null;
    }

    private ObservableCollection<FieldItem>? FindOwningCollection(FieldItem item)
    {
        if (_availableFields.Contains(item)) return _availableFields;
        if (_rowDimensions.Contains(item)) return _rowDimensions;
        if (_columnDimensions.Contains(item)) return _columnDimensions;
        if (_measures.Contains(item)) return _measures;
        return null;
    }

    private void RestoreZoneBorder(Border zone)
    {
        if (zone == AvailableDropZone)
            zone.BorderThickness = new Thickness(1);
        else
            zone.BorderThickness = new Thickness(2);
    }

    // ════════════════════════════════════════════════════════════
    //  Public API
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Load cube data into the explorer view.
    /// </summary>
    public void LoadCubeResult(CubeResult result, IEnumerable<QueryInfo> queries)
    {
        _cubeResult = result;
        _queries.Clear();
        _queries.AddRange(queries);

        QueryListBox.ItemsSource = _queries.Select(q => q.Query.Name).ToList();
    }

    // ════════════════════════════════════════════════════════════
    //  Query selection
    // ════════════════════════════════════════════════════════════

    private void OnQuerySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (QueryListBox.SelectedIndex < 0 || QueryListBox.SelectedIndex >= _queries.Count) return;

        _selectedQuery = _queries[QueryListBox.SelectedIndex];
        PopulateAvailableFields();
        ClearZones();
        ClearGrid();
        DataViewTitle.Text = $"{_selectedQuery.Query.Name} — drag dimensions to Rows / Columns, measures to Values";
    }

    private void PopulateAvailableFields()
    {
        _availableFields.Clear();
        if (_selectedQuery == null) return;

        foreach (var dim in _selectedQuery.Dimensions)
        {
            _availableFields.Add(new FieldItem
            {
                BaseName = dim.Name,
                Label = dim.Name,
                Dimension = dim,
                FieldType = "dimension"
            });
        }

        foreach (var measure in _selectedQuery.Measures)
        {
            _availableFields.Add(new FieldItem
            {
                BaseName = measure.Name,
                Label = measure.Name,
                Measure = measure,
                FieldType = "measure"
            });
        }
    }

    private void ClearZones()
    {
        _rowDimensions.Clear();
        _columnDimensions.Clear();
        _measures.Clear();
    }

    private void ClearGrid()
    {
        ResultDataGrid.Columns.Clear();
        ResultDataGrid.ItemsSource = null;
    }

    // ════════════════════════════════════════════════════════════
    //  Context menu + double-click (kept as fallback)
    // ════════════════════════════════════════════════════════════

    private void OnAvailableFieldDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (AvailableFieldsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType == "measure")
            MoveToZone(item, _availableFields, _measures);
        else
            MoveToZone(item, _availableFields, _rowDimensions);
    }

    private void OnAddToRows(object? sender, RoutedEventArgs e)
    {
        if (AvailableFieldsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "dimension") return;
        MoveToZone(item, _availableFields, _rowDimensions);
    }

    private void OnAddToColumns(object? sender, RoutedEventArgs e)
    {
        if (AvailableFieldsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "dimension") return;
        MoveToZone(item, _availableFields, _columnDimensions);
    }

    private void OnAddToValues(object? sender, RoutedEventArgs e)
    {
        if (AvailableFieldsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "measure") return;
        MoveToZone(item, _availableFields, _measures);
    }

    private void OnRemoveRowDimension(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FieldItem item)
            MoveToZone(item, _rowDimensions, _availableFields);
    }

    private void OnRemoveColumnDimension(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FieldItem item)
            MoveToZone(item, _columnDimensions, _availableFields);
    }

    private void OnRemoveMeasure(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FieldItem item)
            MoveToZone(item, _measures, _availableFields);
    }

    private void OnRemoveRowFromContext(object? sender, RoutedEventArgs e)
    {
        if (RowDimensionsList.SelectedItem is FieldItem item)
            MoveToZone(item, _rowDimensions, _availableFields);
    }

    private void OnRemoveColumnFromContext(object? sender, RoutedEventArgs e)
    {
        if (ColumnDimensionsList.SelectedItem is FieldItem item)
            MoveToZone(item, _columnDimensions, _availableFields);
    }

    private void OnRemoveMeasureFromContext(object? sender, RoutedEventArgs e)
    {
        if (MeasuresList.SelectedItem is FieldItem item)
            MoveToZone(item, _measures, _availableFields);
    }

    private void OnMoveRowToColumns(object? sender, RoutedEventArgs e)
    {
        if (RowDimensionsList.SelectedItem is FieldItem item)
            MoveToZone(item, _rowDimensions, _columnDimensions);
    }

    private void OnMoveColumnToRows(object? sender, RoutedEventArgs e)
    {
        if (ColumnDimensionsList.SelectedItem is FieldItem item)
            MoveToZone(item, _columnDimensions, _rowDimensions);
    }

    /// <summary>
    /// Double-tap on a row dimension: expand to next hierarchy level, or collapse back.
    /// </summary>
    private void OnRowDimensionDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (RowDimensionsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "dimension" || item.Dimension == null) return;
        ToggleExpansionLevel(item);
    }

    /// <summary>
    /// Double-tap on a column dimension: expand to next hierarchy level, or collapse back.
    /// </summary>
    private void OnColumnDimensionDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (ColumnDimensionsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "dimension" || item.Dimension == null) return;
        ToggleExpansionLevel(item);
    }

    /// <summary>
    /// Cycle the expansion level: 0 → 1 → ... → maxDepth-1 → 0 (wrap around).
    /// </summary>
    private void ToggleExpansionLevel(FieldItem item)
    {
        var maxDepth = item.MaxDepth;
        if (maxDepth <= 0) return; // no children to expand into

        // maxDepth is the number of available levels below root.
        // ExpansionLevel 0 = show top-level children (depth 1 entries)
        // ExpansionLevel 1 = show depth 2 entries (children of children), etc.
        // Max valid ExpansionLevel = maxDepth - 1
        if (item.ExpansionLevel < maxDepth - 1)
            item.ExpansionLevel++;
        else
            item.ExpansionLevel = 0;

        RefreshPivotTable();
    }

    private void OnExpandRowDimension(object? sender, RoutedEventArgs e)
    {
        if (RowDimensionsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "dimension" || item.Dimension == null) return;
        if (item.ExpansionLevel < item.MaxDepth - 1)
        {
            item.ExpansionLevel++;
            RefreshPivotTable();
        }
    }

    private void OnCollapseRowDimension(object? sender, RoutedEventArgs e)
    {
        if (RowDimensionsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "dimension" || item.Dimension == null) return;
        if (item.ExpansionLevel > 0)
        {
            item.ExpansionLevel--;
            RefreshPivotTable();
        }
    }

    private void OnExpandColumnDimension(object? sender, RoutedEventArgs e)
    {
        if (ColumnDimensionsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "dimension" || item.Dimension == null) return;
        if (item.ExpansionLevel < item.MaxDepth - 1)
        {
            item.ExpansionLevel++;
            RefreshPivotTable();
        }
    }

    private void OnCollapseColumnDimension(object? sender, RoutedEventArgs e)
    {
        if (ColumnDimensionsList.SelectedItem is not FieldItem item) return;
        if (item.FieldType != "dimension" || item.Dimension == null) return;
        if (item.ExpansionLevel > 0)
        {
            item.ExpansionLevel--;
            RefreshPivotTable();
        }
    }

    private void MoveToZone(FieldItem item, ObservableCollection<FieldItem> from, ObservableCollection<FieldItem> to)
    {
        from.Remove(item);
        // Reset expansion level when moving back to available fields
        if (to == _availableFields)
            item.ExpansionLevel = 0;
        if (!to.Contains(item))
            to.Add(item);
        RefreshPivotTable();
    }

    // ════════════════════════════════════════════════════════════
    //  Pivot table rendering
    // ════════════════════════════════════════════════════════════

    private void RefreshPivotTable()
    {
        if (_selectedQuery == null || _cubeResult == null)
        {
            ClearGrid();
            return;
        }

        var rowDims = _rowDimensions.Where(f => f.Dimension != null).Select(f => f.Dimension!).ToList();
        var colDims = _columnDimensions.Where(f => f.Dimension != null).Select(f => f.Dimension!).ToList();
        var measures = _measures.Where(f => f.Measure != null).Select(f => f.Measure!).ToList();

        if (measures.Count == 0)
        {
            DataViewTitle.Text = $"{_selectedQuery.Query.Name} — add at least one measure to Values";
            ClearGrid();
            return;
        }

        if (rowDims.Count == 0 && colDims.Count == 0)
        {
            DataViewTitle.Text = $"{_selectedQuery.Query.Name} — add at least one dimension to Rows or Columns";
            ClearGrid();
            return;
        }

        DataViewTitle.Text = $"{_selectedQuery.Query.Name} — double-click a row to expand/collapse";
        var queryResult = _cubeResult[_selectedQuery.Query];

        // Handle: only rows, only columns, or both
        if (rowDims.Count > 0 && colDims.Count > 0)
        {
            ShowPivotTable(queryResult, rowDims, colDims, measures);
        }
        else if (rowDims.Count > 0)
        {
            // Only row dimensions: columns are the measures
            ShowFlatTable(queryResult, rowDims, measures);
        }
        else
        {
            // Only column dimensions: transpose — single row per measure, columns are entries
            ShowTransposedTable(queryResult, colDims, measures);
        }
    }

    /// <summary>
    /// Full pivot: row dimensions × column dimensions × measures.
    /// </summary>
    private void ShowPivotTable(QueryResult queryResult, List<IDimension> rowDims, List<IDimension> colDims, List<IMeasure> measures)
    {
        var rowFieldItems = _rowDimensions.Where(f => f.Dimension != null).ToList();
        var colFieldItems = _columnDimensions.Where(f => f.Dimension != null).ToList();
        var rowPaths = GetEntryPaths(rowDims, rowFieldItems);
        var colPaths = GetEntryPaths(colDims, colFieldItems);

        // Build column headers
        var columnNames = new List<string>();
        foreach (var rd in rowDims)
            columnNames.Add(rd.Name);

        var colHeaders = new List<(List<IDimensionEntry> path, IMeasure measure)>();
        foreach (var colPath in colPaths)
        {
            foreach (var measure in measures)
            {
                var colLabel = GetColumnHeaderLabel(colPath, colFieldItems);
                if (measures.Count > 1) colLabel += " | " + measure.Name;
                columnNames.Add(colLabel);
                colHeaders.Add((colPath, measure));
            }
        }

        // Build column entry path map for header expansion
        var colEntryPaths = new Dictionary<int, List<IDimensionEntry>>();
        for (int c = 0; c < colHeaders.Count; c++)
        {
            colEntryPaths[c] = colHeaders[c].path;
        }

        // Build rows
        var rows = new List<RowItem>();
        foreach (var rowPath in rowPaths)
        {
            var values = new string[columnNames.Count];
            for (int r = 0; r < rowDims.Count; r++)
                values[r] = GetRowCellLabel(rowPath[r], rowFieldItems[r]);

            for (int c = 0; c < colHeaders.Count; c++)
            {
                var (colPath, measure) = colHeaders[c];
                values[rowDims.Count + c] = GetCellValue(queryResult, rowPath, colPath, measure);
            }
            rows.Add(new RowItem(values, new List<IDimensionEntry>(rowPath)));
        }

        PopulateDataGrid(rows, columnNames.ToArray(), rowDims.Count, colEntryPaths);
    }

    /// <summary>
    /// Flat table: only row dimensions, measures as columns.
    /// </summary>
    private void ShowFlatTable(QueryResult queryResult, List<IDimension> rowDims, List<IMeasure> measures)
    {
        var rowFieldItems = _rowDimensions.Where(f => f.Dimension != null).ToList();
        var rowPaths = GetEntryPaths(rowDims, rowFieldItems);

        var columnNames = rowDims.Select(d => d.Name).Concat(measures.Select(m => m.Name)).ToArray();

        var rows = new List<RowItem>();
        foreach (var rowPath in rowPaths)
        {
            var values = new string[columnNames.Length];
            for (int r = 0; r < rowDims.Count; r++)
                values[r] = GetRowCellLabel(rowPath[r], rowFieldItems[r]);

            for (int m = 0; m < measures.Count; m++)
            {
                values[rowDims.Count + m] = GetCellValue(queryResult, rowPath, new List<IDimensionEntry>(), measures[m]);
            }
            rows.Add(new RowItem(values, new List<IDimensionEntry>(rowPath)));
        }

        PopulateDataGrid(rows, columnNames, rowDims.Count);
    }

    /// <summary>
    /// Transposed table: only column dimensions, one row per measure.
    /// </summary>
    private void ShowTransposedTable(QueryResult queryResult, List<IDimension> colDims, List<IMeasure> measures)
    {
        var colFieldItems = _columnDimensions.Where(f => f.Dimension != null).ToList();
        var colPaths = GetEntryPaths(colDims, colFieldItems);

        var columnNames = new List<string> { "Measure" };
        var colEntryPaths = new Dictionary<int, List<IDimensionEntry>>();
        for (int c = 0; c < colPaths.Count; c++)
        {
            columnNames.Add(GetColumnHeaderLabel(colPaths[c], colFieldItems));
            colEntryPaths[c] = colPaths[c];
        }

        var rows = new List<RowItem>();
        foreach (var measure in measures)
        {
            var values = new string[columnNames.Count];
            values[0] = measure.Name;
            for (int c = 0; c < colPaths.Count; c++)
            {
                values[1 + c] = GetCellValue(queryResult, new List<IDimensionEntry>(), colPaths[c], measure);
            }
            rows.Add(new RowItem(values));
        }

        // Transposed: "Measure" column at index 0, then data columns at index 1+
        // The _rowDimColumnCount=0 but column headers start at index 1
        PopulateDataGrid(rows, columnNames.ToArray(), 0, colEntryPaths);
    }

    // ════════════════════════════════════════════════════════════
    //  Navigation helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Cartesian product of entries across multiple dimensions, respecting both uniform
    /// expansion levels and per-entry expansions.
    /// Each dimension's entries are expanded to the depth indicated by the corresponding FieldItem,
    /// then per-entry expansions are applied on top.
    /// </summary>
    private static List<List<IDimensionEntry>> GetEntryPaths(List<IDimension> dimensions, List<FieldItem> fieldItems)
    {
        var result = new List<List<IDimensionEntry>> { new() };

        for (int d = 0; d < dimensions.Count; d++)
        {
            var dim = dimensions[d];
            var fieldItem = d < fieldItems.Count ? fieldItems[d] : null;
            var expansionLevel = fieldItem?.ExpansionLevel ?? 0;

            // Get entries at the uniform expansion level
            var baseEntries = GetEntriesAtLevel(dim, expansionLevel);

            // Apply per-entry expansions: flatten expanded entries into parent + children
            var entries = fieldItem != null && fieldItem.HasEntryExpansions
                ? FlattenWithPerEntryExpansion(baseEntries, fieldItem)
                : baseEntries;

            var expanded = new List<List<IDimensionEntry>>();
            foreach (var existing in result)
            {
                foreach (var entry in entries)
                {
                    var path = new List<IDimensionEntry>(existing) { entry };
                    expanded.Add(path);
                }
            }
            result = expanded;
        }

        return result;
    }

    /// <summary>
    /// Gets all entries at the specified uniform expansion level.
    /// Level 0 = direct children of the dimension root.
    /// Level 1 = grandchildren (children of each child), etc.
    /// </summary>
    private static List<IDimensionEntry> GetEntriesAtLevel(IDimension dim, int level)
    {
        IEnumerable<IDimensionEntry> current = dim.Children;

        for (int i = 0; i < level; i++)
        {
            current = current.SelectMany(e => e.Children);
        }

        return current.ToList();
    }

    /// <summary>
    /// Takes a list of entries and recursively replaces any individually expanded entry
    /// with itself followed by its children (which may themselves be expanded).
    /// The parent entry is kept as a "group header" row.
    /// </summary>
    private static List<IDimensionEntry> FlattenWithPerEntryExpansion(List<IDimensionEntry> entries, FieldItem fieldItem)
    {
        var result = new List<IDimensionEntry>();
        foreach (var entry in entries)
        {
            result.Add(entry); // always include the entry itself
            if (fieldItem.IsEntryExpanded(entry))
            {
                // Recursively flatten children (they may also be expanded)
                var children = entry.Children.ToList();
                var expandedChildren = FlattenWithPerEntryExpansion(children, fieldItem);
                result.AddRange(expandedChildren);
            }
        }
        return result;
    }

    /// <summary>
    /// Builds a full hierarchical label for a dimension entry by walking up the parent chain
    /// to the dimension root. E.g., for a month entry "03" under year "2020", returns "2020 › 03".
    /// If the entry is a direct child of the root, returns just the label.
    /// </summary>
    private static string GetEntryFullLabel(IDimensionEntry entry)
    {
        var parts = new List<string>();
        var current = entry;
        while (current != null && current.Root != current)
        {
            parts.Add(current.Label);
            current = current.Parent;
            // Stop before the dimension root itself (its label is the dimension name)
            if (current != null && current == current.Root)
                break;
        }
        parts.Reverse();
        return parts.Count <= 1 ? entry.Label : string.Join(" › ", parts);
    }

    /// <summary>
    /// Builds a display label for a row dimension cell, including an expand/collapse indicator
    /// and indentation when per-entry expansion is active.
    /// </summary>
    private static string GetRowCellLabel(IDimensionEntry entry, FieldItem fieldItem)
    {
        var hasChildren = entry.Children.Any();
        var isExpanded = fieldItem.IsEntryExpanded(entry);

        // Calculate indent depth relative to the uniform expansion base level
        var depth = GetDepthRelativeToBase(entry, fieldItem);
        var indent = depth > 0 ? new string('\u2003', depth) : ""; // em-space for proportional font indentation

        var indicator = !hasChildren ? "  " : (isExpanded ? "▾ " : "▸ ");

        // When showing a child of an individually expanded parent, use just the entry's
        // own label — the parent context is already visible in the row above.
        var label = depth > 0 ? entry.Label : GetEntryFullLabel(entry);

        return indent + indicator + label;
    }

    /// <summary>
    /// Returns how many levels deep this entry is relative to the FieldItem's base expansion level.
    /// Entries at the base level return 0, their children return 1, etc.
    /// </summary>
    private static int GetDepthRelativeToBase(IDimensionEntry entry, FieldItem fieldItem)
    {
        // Walk up from entry, counting how many parents until we reach the base level
        // The base level entries are at depth (ExpansionLevel + 1) from the root dimension
        var baseDepthFromRoot = fieldItem.ExpansionLevel + 1;
        var entryDepthFromRoot = 0;
        var current = entry;
        while (current != null && current.Root != current)
        {
            entryDepthFromRoot++;
            current = current.Parent;
            if (current != null && current == current.Root)
                break;
        }
        return Math.Max(0, entryDepthFromRoot - baseDepthFromRoot);
    }

    /// <summary>
    /// Builds a column header label for a column entry path, including expand/collapse indicators.
    /// </summary>
    private static string GetColumnHeaderLabel(List<IDimensionEntry> colPath, List<FieldItem> colFieldItems)
    {
        var parts = new List<string>();
        for (int i = 0; i < colPath.Count; i++)
        {
            var entry = colPath[i];
            var fieldItem = i < colFieldItems.Count ? colFieldItems[i] : null;
            var hasChildren = entry.Children.Any();

            if (fieldItem != null && hasChildren)
            {
                var isExpanded = fieldItem.IsEntryExpanded(entry);
                var indicator = isExpanded ? "▾" : "▸";
                parts.Add($"{indicator} {GetEntryFullLabel(entry)}");
            }
            else
            {
                parts.Add(GetEntryFullLabel(entry));
            }
        }
        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Navigates the QueryResult tree by following the combined row+column entry path
    /// and returns the formatted measure value.
    /// The entries are reordered to match the chained dimension order of the query,
    /// and unselected dimensions are skipped (their root-level aggregate is used).
    /// </summary>
    private string GetCellValue(QueryResult queryResult, List<IDimensionEntry> rowPath, List<IDimensionEntry> colPath, IMeasure measure)
    {
        try
        {
            if (_selectedQuery == null) return "-";

            // Collect entries keyed by their root dimension
            var entryByDimension = new Dictionary<IDimension, IDimensionEntry>();
            foreach (var entry in rowPath.Concat(colPath))
            {
                entryByDimension[entry.Root] = entry;
            }

            // The query's dimension list is in chained order
            var chainedDimensions = _selectedQuery.Dimensions;
            if (chainedDimensions.Count == 0) return "-";

            // Start from the root dimension result of the first chained dimension
            var firstDim = chainedDimensions[0];
            IDimensionEntryResult current = ((IDictionary<IDimension, IDimensionEntryResult>)queryResult)[firstDim];

            // If the user selected the first dimension, navigate to the specific entry
            if (entryByDimension.TryGetValue(firstDim, out var firstEntry))
            {
                current = current[firstEntry];
            }

            // Navigate through remaining chained dimensions in order
            for (int i = 1; i < chainedDimensions.Count; i++)
            {
                var dim = chainedDimensions[i];
                // Navigate to this dimension's root via OtherDimensions (gives aggregate)
                var dimResult = current.OtherDimensions[dim];

                if (entryByDimension.TryGetValue(dim, out var entry))
                {
                    // User selected this dimension — drill into the specific entry
                    current = dimResult[entry];
                }
                else
                {
                    // User did not select this dimension — stay at root (aggregate)
                    current = dimResult;
                }
            }

            var val = current[measure];
            return FormatMeasureValue(val);
        }
        catch
        {
            return "-";
        }
    }

    private static string FormatMeasureValue(IMeasureResult val)
    {
        if (val is DecimalMeasureResult) return val.DecimalValue.ToString("N2");
        if (val is DoubleMeasureResult) return val.DoubleValue.ToString("N2");
        return val.IntValue.ToString();
    }

    private void PopulateDataGrid(List<RowItem> items, string[] columnNames,
        int rowDimColumnCount = 0,
        Dictionary<int, List<IDimensionEntry>>? columnEntryPaths = null)
    {
        ResultDataGrid.Columns.Clear();
        ResultDataGrid.AutoGenerateColumns = false;
        _columnEntryPaths.Clear();
        _rowDimColumnCount = rowDimColumnCount;

        if (columnEntryPaths != null)
        {
            foreach (var kvp in columnEntryPaths)
                _columnEntryPaths[kvp.Key] = kvp.Value;
        }

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

    // ════════════════════════════════════════════════════════════
    //  Row styling for expandable entries
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Called when a DataGrid row is being prepared. Sets cursor and tooltip
    /// on rows that contain expandable dimension entries.
    /// </summary>
    private void OnDataGridLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not RowItem rowItem) return;

        var rowFieldItems = _rowDimensions.Where(f => f.Dimension != null).ToList();
        var hasExpandable = false;

        for (int i = 0; i < rowFieldItems.Count && i < rowItem.RowEntryPath.Count; i++)
        {
            if (rowItem.RowEntryPath[i].Children.Any())
            {
                hasExpandable = true;
                break;
            }
        }

        if (hasExpandable)
        {
            e.Row.Cursor = new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(e.Row, "Double-click to expand/collapse");
        }
        else
        {
            e.Row.Cursor = Cursor.Default;
            ToolTip.SetTip(e.Row, null);
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Per-entry expansion in the DataGrid
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Maps data column index (after row dim columns) to the column entry path for column header expansion.
    /// </summary>
    private readonly Dictionary<int, List<IDimensionEntry>> _columnEntryPaths = new();

    /// <summary>
    /// The number of leading columns that represent row dimensions (not data columns).
    /// </summary>
    private int _rowDimColumnCount;

    /// <summary>
    /// Double-click on a DataGrid: if it's a row dimension cell, toggle expansion of
    /// the dimension entry in that cell. If it's a column header, toggle the column entry.
    /// </summary>
    private void OnDataGridDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (e.Source is not Visual sourceVisual) return;

        // Check if the double-click was on a column header
        var columnHeader = FindAncestor<DataGridColumnHeader>(sourceVisual);
        if (columnHeader != null)
        {
            HandleColumnHeaderDoubleClick(columnHeader);
            return;
        }

        // Try to determine column index from the clicked cell in the visual tree
        var clickedCell = FindAncestor<DataGridCell>(sourceVisual);
        int? cellColumnIndex = null;
        if (clickedCell != null)
        {
            // Walk siblings to find the cell's index
            var cellParent = clickedCell.GetVisualParent();
            if (cellParent != null)
            {
                int idx = 0;
                foreach (var child in cellParent.GetVisualChildren())
                {
                    if (child == clickedCell)
                    {
                        cellColumnIndex = idx;
                        break;
                    }
                    if (child is DataGridCell)
                        idx++;
                }
            }
        }

        // Otherwise check if it's a row dimension cell
        HandleRowCellDoubleClick(cellColumnIndex);
    }

    /// <summary>
    /// Handle double-click on a row dimension cell: toggle per-entry expansion.
    /// Works when clicking on any column — if a row dimension column is clicked,
    /// that dimension's entry is toggled; otherwise the first expandable row dimension
    /// entry is toggled.
    /// </summary>
    /// <param name="cellColumnIndex">Column index detected from visual tree hit-testing, if available.</param>
    private void HandleRowCellDoubleClick(int? cellColumnIndex = null)
    {
        var rowFieldItems = _rowDimensions.Where(f => f.Dimension != null).ToList();
        if (rowFieldItems.Count == 0) return;

        // Get the selected row
        if (ResultDataGrid.SelectedItem is not RowItem rowItem) return;
        if (rowItem.RowEntryPath.Count == 0) return;

        // Try to determine which column was clicked — prefer visual tree detection,
        // fall back to CurrentColumn property
        var colIndex = cellColumnIndex
            ?? (ResultDataGrid.CurrentColumn != null
                ? ResultDataGrid.Columns.IndexOf(ResultDataGrid.CurrentColumn)
                : -1);

        // If the click landed on a row dimension column, toggle that dimension's entry
        if (colIndex >= 0 && colIndex < rowFieldItems.Count && colIndex < rowItem.RowEntryPath.Count)
        {
            var fieldItem = rowFieldItems[colIndex];
            var entry = rowItem.RowEntryPath[colIndex];

            if (fieldItem.Dimension != null && entry.Children.Any())
            {
                fieldItem.ToggleEntryExpansion(entry);
                RefreshPivotTable();
                return;
            }
        }

        // Otherwise (click on a data/measure column or CurrentColumn not set):
        // Toggle the first row dimension entry that has expandable children.
        // This makes it intuitive to expand a row by double-clicking anywhere on it.
        for (int i = 0; i < rowFieldItems.Count && i < rowItem.RowEntryPath.Count; i++)
        {
            var fieldItem = rowFieldItems[i];
            var entry = rowItem.RowEntryPath[i];

            if (fieldItem.Dimension != null && entry.Children.Any())
            {
                fieldItem.ToggleEntryExpansion(entry);
                RefreshPivotTable();
                return;
            }
        }
    }

    /// <summary>
    /// Handle double-click on a column header: toggle per-entry expansion for the column dimension entry.
    /// </summary>
    private void HandleColumnHeaderDoubleClick(DataGridColumnHeader columnHeader)
    {
        // Find the column index by locating this header among its siblings
        var colIndex = -1;
        var parent = columnHeader.GetVisualParent();
        if (parent != null)
        {
            int idx = 0;
            foreach (var child in parent.GetVisualChildren())
            {
                if (child == columnHeader)
                {
                    colIndex = idx;
                    break;
                }
                if (child is DataGridColumnHeader)
                    idx++;
            }
        }

        // Fallback: match by header text
        if (colIndex < 0)
        {
            var headerText = columnHeader.Content?.ToString();
            if (string.IsNullOrEmpty(headerText)) return;
            for (int i = 0; i < ResultDataGrid.Columns.Count; i++)
            {
                if (ResultDataGrid.Columns[i].Header?.ToString() == headerText)
                {
                    colIndex = i;
                    break;
                }
            }
        }
        if (colIndex < 0) return;

        // Calculate the data column index (offset by row dimension columns)
        var dataColIndex = colIndex - _rowDimColumnCount;

        // For transposed tables, offset by the "Measure" label column
        if (_rowDimColumnCount == 0 && _columnEntryPaths.Count > 0)
            dataColIndex = colIndex - 1; // first column is "Measure"

        if (dataColIndex < 0) return;

        ToggleColumnEntryExpansion(dataColIndex);
    }

    /// <summary>
    /// Walk up the visual tree to find an ancestor of a specific type.
    /// </summary>
    private static T? FindAncestor<T>(Visual visual) where T : class
    {
        Visual? current = visual;
        while (current != null)
        {
            if (current is T found) return found;
            current = current.GetVisualParent();
        }
        return null;
    }

    /// <summary>
    /// Handles column header double-click for column dimension per-entry expansion.
    /// Called from the column header tap handler.
    /// </summary>
    private void ToggleColumnEntryExpansion(int dataColumnIndex)
    {
        if (!_columnEntryPaths.TryGetValue(dataColumnIndex, out var entryPath)) return;

        var colFieldItems = _columnDimensions.Where(f => f.Dimension != null).ToList();
        if (colFieldItems.Count == 0 || entryPath.Count == 0) return;

        // Find the first entry in the path that has children and can be toggled
        // We toggle the last entry in the column path that has expandable children
        for (int i = entryPath.Count - 1; i >= 0; i--)
        {
            var entry = entryPath[i];
            if (entry.Children.Any() && i < colFieldItems.Count)
            {
                colFieldItems[i].ToggleEntryExpansion(entry);
                RefreshPivotTable();
                return;
            }
        }
    }
}