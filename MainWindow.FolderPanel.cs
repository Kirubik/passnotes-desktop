using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PassNotes;

public partial class MainWindow
{
    private DispatcherTimer? _folderSearchDebounceTimer;
    private const int FolderSearchDebounceMs = 200;
    private string _pendingFolderSearchText = string.Empty;
    private bool _foldersCollapsed;
    private double _foldersLastWidth = FolderExpandedDefaultWidth;
    private const double FolderExpandedDefaultWidth = 320;
    private const double FolderExpandedMinWidth = 160;
    private const double FolderCollapsedWidth = 0;
    private const int FolderAnimDurationMs = 160;
    private DispatcherTimer? _folderAnimTimer;
    private DateTime _folderAnimStart;
    private double _folderAnimFrom;
    private double _folderAnimTo;
    private string _folderSearchText = string.Empty;
    private Dictionary<string, bool>? _folderExpandedSnapshot;

    public bool IsFolderMultiSelectUiVisible => IsFolderMultiSelectMode && !_foldersCollapsed;

    private void ToggleFolders_Click(object sender, RoutedEventArgs e)
    {
        if (!_foldersCollapsed)
        {
            var current = FoldersColumn.ActualWidth;
            if (!double.IsNaN(current) && current > 0)
                _foldersLastWidth = Math.Max(FolderExpandedDefaultWidth, current);
        }

        _foldersCollapsed = !_foldersCollapsed;
        UpdateFolderHandleArrow();
        OnPropertyChanged(nameof(IsFolderMultiSelectUiVisible));

        FolderPanel.ClearValue(FrameworkElement.WidthProperty);

        if (_foldersCollapsed)
        {
            FolderTree.Visibility = Visibility.Collapsed;
            if (FolderActionsPanel != null)
                FolderActionsPanel.Visibility = Visibility.Collapsed;

            FolderSplitter.Visibility = Visibility.Collapsed;
            FoldersSplitterColumn.Width = new GridLength(0);

            FoldersColumn.MinWidth = 0;
            FoldersColumn.MaxWidth = double.PositiveInfinity;

            AnimateFoldersColumnWidth(FoldersColumn.ActualWidth, FolderCollapsedWidth, () =>
            {
                SetFoldersColumnWidth(FolderCollapsedWidth);
                FoldersColumn.MinWidth = FolderCollapsedWidth;
                FoldersColumn.MaxWidth = FolderCollapsedWidth;
                FolderPanel.Visibility = Visibility.Collapsed;
            });
        }
        else
        {
            FolderPanel.Visibility = Visibility.Visible;
            FolderTree.Visibility = Visibility.Visible;
            if (FolderActionsPanel != null)
                FolderActionsPanel.Visibility = Visibility.Visible;

            FolderSplitter.Visibility = Visibility.Visible;
            FoldersSplitterColumn.Width = new GridLength(2);

            FoldersColumn.MinWidth = 0;
            FoldersColumn.MaxWidth = double.PositiveInfinity;

            var target = ClampFolderWidth(_foldersLastWidth);
            AnimateFoldersColumnWidth(FoldersColumn.ActualWidth, target, () =>
            {
                SetFoldersColumnWidth(target);
                FoldersColumn.MinWidth = FolderExpandedMinWidth;
                FoldersColumn.MaxWidth = double.PositiveInfinity;
            });
        }

        UpdateFolderActionButtons();
    }

    private void UpdateFolderHandleArrow()
    {
        if (FolderHandle?.Template?.FindName("PART_ChevronGlyph", FolderHandle) is not System.Windows.Shapes.Path arrow)
            return;

        RotateTransform rotate;

        if (arrow.RenderTransform is RotateTransform existing)
        {
            rotate = existing.IsFrozen ? existing.CloneCurrentValue() : existing;

            if (!ReferenceEquals(rotate, existing))
                arrow.RenderTransform = rotate;
        }
        else
        {
            rotate = new RotateTransform();
            arrow.RenderTransform = rotate;
            arrow.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        rotate.Angle = _foldersCollapsed ? 0 : 180;
    }

    private void FolderSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_foldersCollapsed)
            return;

        var current = FoldersColumn.ActualWidth;
        if (!double.IsNaN(current) && current > 0)
            _foldersLastWidth = Math.Max(FolderExpandedDefaultWidth, current);
    }

    private void SetFoldersColumnWidth(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width))
            return;
        if (width < 0)
            width = 0;

        FoldersColumn.Width = new GridLength(width, GridUnitType.Pixel);
        AdjustFolderSearchWidth();
    }

    private void AdjustFolderSearchWidth()
    {
        if (FolderTopBar == null || FolderSearchBox == null)
            return;

        var topWidth = FolderTopBar.ActualWidth;
        if (double.IsNaN(topWidth) || topWidth <= 0)
            return;

        var buttonsWidth = FolderActionsPanel?.ActualWidth ?? 0;
        const double gap = 8;
        const double borderExtra = 2;
        const double safety = 6;

        var available = topWidth - buttonsWidth - gap - borderExtra - safety;
        if (available < 0)
            available = 0;

        var newWidth = Math.Min(200, available);

        if (!double.IsNaN(newWidth) && Math.Abs(FolderSearchBox.Width - newWidth) > 0.5)
            FolderSearchBox.Width = newWidth;
    }

    private void FolderTopBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        AdjustFolderSearchWidth();
    }

    private double ClampFolderWidth(double desired)
    {
        var max = Math.Max(FolderExpandedMinWidth, ActualWidth - 340);
        return Math.Max(FolderExpandedMinWidth, Math.Min(desired, max));
    }

    private void AnimateFoldersColumnWidth(double from, double to, Action? completed = null)
    {
        if (_folderAnimTimer != null)
        {
            _folderAnimTimer.Stop();
            _folderAnimTimer = null;
        }

        if (double.IsNaN(from) || from <= 0)
            from = FoldersColumn.ActualWidth;
        if (double.IsNaN(from) || from <= 0)
            from = _foldersCollapsed ? FolderCollapsedWidth : _foldersLastWidth;
        if (double.IsNaN(to))
            to = from;

        _folderAnimFrom = from;
        _folderAnimTo = to;
        _folderAnimStart = DateTime.UtcNow;

        _folderAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _folderAnimTimer.Tick += (s, e) =>
        {
            var t = (DateTime.UtcNow - _folderAnimStart).TotalMilliseconds / FolderAnimDurationMs;
            if (t >= 1)
            {
                _folderAnimTimer!.Stop();
                _folderAnimTimer = null;
                SetFoldersColumnWidth(_folderAnimTo);
                completed?.Invoke();
                return;
            }

            var tt = t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
            var w = _folderAnimFrom + (_folderAnimTo - _folderAnimFrom) * tt;
            SetFoldersColumnWidth(w);
        };
        _folderAnimTimer.Start();
    }

    private void FolderSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _pendingFolderSearchText = FolderSearchBox?.Text ?? string.Empty;
        if (_folderSearchDebounceTimer == null)
        {
            ApplyFolderSearchFilter(_pendingFolderSearchText);
            return;
        }

        _folderSearchDebounceTimer.Stop();
        _folderSearchDebounceTimer.Start();
    }

    private void FolderSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            FolderSearchBox.Clear();
            e.Handled = true;
        }
    }

    private void ClearFolderSearchBox_Click(object sender, RoutedEventArgs e)
    {
        if (FolderSearchBox == null)
            return;

        FolderSearchBox.Clear();
        FolderSearchBox.Focus();
        e.Handled = true;
    }

    private void ApplyFolderSearchFilter(string? text)
    {
        text = (text ?? string.Empty).Trim();

        if (string.Equals(text, _folderSearchText, StringComparison.Ordinal))
            return;

        bool wasEmpty = string.IsNullOrWhiteSpace(_folderSearchText);
        bool nowEmpty = string.IsNullOrWhiteSpace(text);

        _folderSearchText = text;

        if (!nowEmpty && wasEmpty)
            _folderExpandedSnapshot = CaptureExpandedStates();

        if (nowEmpty)
        {
            foreach (var root in _folderTreeRoots)
                SetVisibleRecursive(root, true);

            if (_folderExpandedSnapshot != null)
            {
                RestoreExpandedStates(_folderExpandedSnapshot);
                _folderExpandedSnapshot = null;
            }

            return;
        }

        var tokens = SplitSearchTokens(text);
        foreach (var root in _folderTreeRoots)
        {
            if (root.Kind == FolderNodeKind.Folder)
                ApplyFilterRecursive(root, tokens);
            else
                root.IsVisible = true;
        }
    }

    private bool ApplyFilterRecursive(FolderNode node, string[] tokens)
    {
        bool selfMatch = node.Kind == FolderNodeKind.Folder && tokens.Length > 0 && tokens.All(t => ContainsCI(node.Name, t));

        bool anyChildMatch = false;
        foreach (var child in node.Children)
        {
            if (ApplyFilterRecursive(child, tokens))
                anyChildMatch = true;
        }

        bool visible = selfMatch || anyChildMatch;
        node.IsVisible = visible;

        if (anyChildMatch)
            node.IsExpanded = true;

        return visible;
    }

    private void SetVisibleRecursive(FolderNode node, bool visible)
    {
        node.IsVisible = visible;
        foreach (var child in node.Children)
            SetVisibleRecursive(child, visible);
    }

    private static string FolderNodeKeyString(FolderNode node)
    {
        return node.Kind switch
        {
            FolderNodeKind.Folder => "F:" + node.Id.ToString("N"),
            FolderNodeKind.Favorites => "Fav",
            FolderNodeKind.NoFolder => "No",
            _ => node.Kind.ToString()
        };
    }

    private Dictionary<string, bool> CaptureExpandedStates()
    {
        var dict = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var root in _folderTreeRoots)
            CaptureExpandedStatesRecursive(root, dict);
        return dict;
    }

    private void CaptureExpandedStatesRecursive(FolderNode node, Dictionary<string, bool> dict)
    {
        dict[FolderNodeKeyString(node)] = node.IsExpanded;
        foreach (var child in node.Children)
            CaptureExpandedStatesRecursive(child, dict);
    }

    private void RestoreExpandedStates(Dictionary<string, bool> snapshot)
    {
        foreach (var root in _folderTreeRoots)
            RestoreExpandedStatesRecursive(root, snapshot);
    }

    private void RestoreExpandedStatesRecursive(FolderNode node, Dictionary<string, bool> snapshot)
    {
        if (snapshot.TryGetValue(FolderNodeKeyString(node), out var expanded))
            node.IsExpanded = expanded;
        foreach (var child in node.Children)
            RestoreExpandedStatesRecursive(child, snapshot);
    }
}
