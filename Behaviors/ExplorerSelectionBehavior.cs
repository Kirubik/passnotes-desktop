using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PassNotes.Behaviors;

/// <summary>
/// Explorer-like selection behavior:
/// - Right-click on an item selects that item (so context menu applies to it).
/// - Right-click on empty space clears selection.
///
/// Enabled globally via App.xaml styles to keep code-behind minimal.
/// </summary>
public static class ExplorerSelectionBehavior
{
    public static readonly DependencyProperty EnableProperty =
        DependencyProperty.RegisterAttached(
            "Enable",
            typeof(bool),
            typeof(ExplorerSelectionBehavior),
            new PropertyMetadata(false, OnEnableChanged));

    public static void SetEnable(DependencyObject element, bool value) => element.SetValue(EnableProperty, value);
    public static bool GetEnable(DependencyObject element) => (bool)element.GetValue(EnableProperty);


/// <summary>
/// Internal flag used by the app to prevent "activation"/navigation when selection changes due to
/// Explorer-like right-click selection or clearing selection on empty space.
/// </summary>
public static readonly DependencyProperty SuppressTreeActivateNextSelectionChangeProperty =
    DependencyProperty.RegisterAttached(
        "SuppressTreeActivateNextSelectionChange",
        typeof(bool),
        typeof(ExplorerSelectionBehavior),
        new PropertyMetadata(false));

public static void SetSuppressTreeActivateNextSelectionChange(DependencyObject element, bool value) =>
    element.SetValue(SuppressTreeActivateNextSelectionChangeProperty, value);

public static bool GetSuppressTreeActivateNextSelectionChange(DependencyObject element) =>
    (bool)element.GetValue(SuppressTreeActivateNextSelectionChangeProperty);

    private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el)
            return;

        bool enable = e.NewValue is bool b && b;

        // Detach first (safe even if not attached).
        el.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;

        // Also clear selection on left-click empty space for TreeView and DataGrid.
        // TreeView: matches Windows Explorer behavior.
        // DataGrid: preserves existing "Excel-like" UX in the app.
        el.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;

        if (!enable)
            return;

        el.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;

        if (d is TreeView || d is DataGrid)
            el.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView tree)
        {
            // Explorer-like: left-clicking empty space in the tree clears selection.
            if (FindVisualParent<ScrollBar>(e.OriginalSource as DependencyObject) != null)
                return;

            var tvi = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (tvi != null)
                return; // Normal selection handled by TreeView.

            var hadSelection = tree.SelectedItem != null;
            SetSuppressTreeActivateNextSelectionChange(tree, hadSelection);
            ClearTreeViewSelection(tree);
            tree.Focus();
            return;
        }

        if (sender is DataGrid grid)
        {
            // Excel-like: left-clicking empty area clears selection.
            DependencyObject? src = e.OriginalSource as DependencyObject;

            if (FindVisualParent<ScrollBar>(src) != null) return;
            if (FindVisualParent<DataGridColumnHeader>(src) != null) return;

            var row = FindDataGridRow(grid, src);
            var cell = FindDataGridCell(grid, src);
            if (row != null || cell != null)
                return;

            // IMPORTANT: for SelectionMode=Extended we must clear row selection (not only cells).
            grid.SelectedItem = null;
            grid.UnselectAll();
            grid.UnselectAllCells();
            grid.CurrentCell = new DataGridCellInfo();
            return;
        }
    }

    private static void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeView tree)
        {
            HandleTreeViewRightClick(tree, e);
            return;
        }

        if (sender is DataGrid grid)
        {
            HandleDataGridRightClick(grid, e);
            return;
        }

        if (sender is ListBox listBox)
        {
            HandleListBoxRightClick(listBox, e);
            return;
        }

        if (sender is ListView listView)
        {
            HandleListViewRightClick(listView, e);
            return;
        }
    }

    private static void HandleTreeViewRightClick(TreeView tree, MouseButtonEventArgs e)
    {
        // Ignore clicks on scrollbars.
        if (FindVisualParent<ScrollBar>(e.OriginalSource as DependencyObject) != null)
            return;

        // If clicked on an item -> select it.
        var tvi = FindVisualParent<TreeViewItem>(e.OriginalSource as DependencyObject);
        if (tvi != null)
        {
            SetSuppressTreeActivateNextSelectionChange(tree, !tvi.IsSelected);
            tvi.IsSelected = true;
            tvi.Focus();
            tree.Focus();
            return;
        }

        // Empty space -> clear selection.
        var hadSelection = tree.SelectedItem != null;
        SetSuppressTreeActivateNextSelectionChange(tree, hadSelection);
        ClearTreeViewSelection(tree);
        tree.Focus();
    }

    private static void HandleDataGridRightClick(DataGrid grid, MouseButtonEventArgs e)
    {
        DependencyObject? src = e.OriginalSource as DependencyObject;

        // Ignore clicks on scrollbars and column headers (let DataGrid handle those normally).
        if (FindVisualParent<ScrollBar>(src) != null) return;
        if (FindVisualParent<DataGridColumnHeader>(src) != null) return;

        var row = FindDataGridRow(grid, src);

        if (row != null)
        {
            // Explorer-like:
            // - Right-click on an already selected row keeps multi-selection.
            // - Right-click on an unselected row clears selection and selects only that row.
            bool alreadySelected = false;
            try
            {
                alreadySelected = grid.SelectedItems?.Contains(row.Item) == true;
            }
            catch { /* ignore */ }

            if (!alreadySelected)
            {
                grid.UnselectAll();
                row.IsSelected = true;
                grid.SelectedItem = row.Item;
            }

            row.Focus();
            grid.Focus();
            return;
        }

        // Empty area: clear selection.
        grid.SelectedItem = null;
        grid.UnselectAll();
        grid.UnselectAllCells();
        grid.CurrentCell = new DataGridCellInfo();
        grid.Focus();
    }

    private static DataGridRow? FindDataGridRow(DataGrid grid, DependencyObject? source)
    {
        if (source == null)
            return null;

        try
        {
            if (ItemsControl.ContainerFromElement(grid, source) is DataGridRow row)
                return row;

            if (ItemsControl.ContainerFromElement(grid, source) is DataGridCell cell)
                return FindVisualParent<DataGridRow>(cell);
        }
        catch
        {
            // Fall back to visual-parent walk below.
        }

        return FindVisualParent<DataGridRow>(source);
    }

    private static DataGridCell? FindDataGridCell(DataGrid grid, DependencyObject? source)
    {
        if (source == null)
            return null;

        try
        {
            if (ItemsControl.ContainerFromElement(grid, source) is DataGridCell cell)
                return cell;
        }
        catch
        {
            // Fall back to visual-parent walk below.
        }

        return FindVisualParent<DataGridCell>(source);
    }
    private static void HandleListBoxRightClick(ListBox listBox, MouseButtonEventArgs e)
    {
        if (FindVisualParent<ScrollBar>(e.OriginalSource as DependencyObject) != null)
            return;

        var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item != null)
        {
            item.IsSelected = true;
            item.Focus();
            listBox.Focus();
            return;
        }

        listBox.SelectedItem = null;
        listBox.Focus();
    }

    private static void HandleListViewRightClick(ListView listView, MouseButtonEventArgs e)
    {
        if (FindVisualParent<ScrollBar>(e.OriginalSource as DependencyObject) != null)
            return;

        var item = FindVisualParent<ListViewItem>(e.OriginalSource as DependencyObject);
        if (item != null)
        {
            item.IsSelected = true;
            item.Focus();
            listView.Focus();
            return;
        }

        listView.SelectedItem = null;
        listView.Focus();
    }

    private static void ClearTreeViewSelection(TreeView tree)
    {
        var selected = FindFirstSelectedTreeViewItem(tree);
        if (selected != null)
            selected.IsSelected = false;
    }

    private static TreeViewItem? FindFirstSelectedTreeViewItem(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TreeViewItem tvi)
            {
                if (tvi.IsSelected)
                    return tvi;

                var nested = FindFirstSelectedTreeViewItem(tvi);
                if (nested != null)
                    return nested;
            }
            else
            {
                var nested = FindFirstSelectedTreeViewItem(child);
                if (nested != null)
                    return nested;
            }
        }

        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T typed)
                return typed;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}

