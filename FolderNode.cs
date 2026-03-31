using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PassNotes;

public enum FolderNodeKind
{
    Favorites,
    Trash,
    AllEntries,
    FolderRoot,
    Folder,
    NoFolder
}

public sealed class FolderNode : INotifyPropertyChanged
{
    public FolderNodeKind Kind { get; }
    public Guid Id { get; } // valid only for Kind == Folder
    public Guid? ParentId { get; }
    private string _name;

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public ObservableCollection<FolderNode> Children { get; } = new();

    // IMPORTANT (UX): expander visibility must be stable.
    // WPF TreeViewItem.HasItems / collection-based checks can temporarily flicker during container rebuilds
    // (e.g., when selecting special nodes like "Без папки" in multi-select mode), which can make the
    // expander arrow disappear for some folders.
    //
    // This hint is computed when building the tree (from the underlying folder hierarchy) and stays stable
    // until the tree is rebuilt.
    private bool _hasChildFoldersHint;
    public bool HasChildFoldersHint
    {
        get => _hasChildFoldersHint;
        set
        {
            if (_hasChildFoldersHint == value) return;
            _hasChildFoldersHint = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasChildFoldersHint)));
        }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    private bool _isVisible = true;
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value) return;
            _isVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    // Visual marker for the currently active folder context (right-side list).
    // IMPORTANT: this is NOT the same as selection in the TreeView.
    private bool _isActiveContext;
    public bool IsActiveContext
    {
        get => _isActiveContext;
        set
        {
            if (_isActiveContext == value) return;
            _isActiveContext = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActiveContext)));
        }
    }

    // Multi-select helper for folder operations (e.g., delete multiple folders).
    // IMPORTANT: This is independent from TreeView selection.
    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    
// Drag&Drop visual helper: highlights folder when it is a current drop target.
// This must not affect the active context selection logic.
private bool _isDropTarget;
public bool IsDropTarget
{
    get => _isDropTarget;
    set
    {
        if (_isDropTarget == value) return;
        _isDropTarget = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDropTarget)));
    }
}

public FolderNode(FolderNodeKind kind, string name, Guid id = default, Guid? parentId = null)
    {
        Kind = kind;
        Id = id;
        ParentId = parentId;
        _name = name;
    }

    public bool IsEditable => Kind == FolderNodeKind.Folder;
    public bool CanContainFolders => Kind is FolderNodeKind.FolderRoot or FolderNodeKind.Folder;

    public event PropertyChangedEventHandler? PropertyChanged;
}