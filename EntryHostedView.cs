using System;
using System.Windows;
using System.Windows.Controls;

namespace PassNotes;

public sealed class EntryHostedView : UserControl, IHostedDialogCloseRequestHandler
{
    private readonly EntryEditorView _entryEditorView;
    private bool _loadedNotified;

    public event Action<VaultEntry>? Saved;
    public event Action? Cancelled;

    public bool IsDirty => _entryEditorView.IsDirty;

    public EntryHostedView(EntryEditorView entryEditorView)
    {
        _entryEditorView = entryEditorView ?? throw new ArgumentNullException(nameof(entryEditorView));
        _entryEditorView.HostedSaved += OnHostedSaved;
        _entryEditorView.HostedCancelled += OnHostedCancelled;
        Content = _entryEditorView;
        Loaded += EntryHostedView_Loaded;
    }

    public EntryEditorDraft CaptureDraft()
        => _entryEditorView.CaptureDraft();

    public void PrepareForLockClose()
        => _entryEditorView.PrepareHostedCloseForLock();

    public bool TrySaveAndCloseForGate()
        => _entryEditorView.TrySaveAndCloseForGate();

    public void ForceCloseDiscardChangesForGate()
        => _entryEditorView.ForceCloseDiscardChangesForGate();

    public void RequestPrimaryAction()
        => _entryEditorView.RequestHostedPrimaryAction();

    public void RequestSecondaryAction()
        => _entryEditorView.RequestHostedSecondaryAction();

    public bool TryHandleHostedDialogCloseRequest()
        => _entryEditorView.TryHandleHostedCloseRequest();

    public void NotifyHostedDialogClosed()
    {
        _entryEditorView.HostedSaved -= OnHostedSaved;
        _entryEditorView.HostedCancelled -= OnHostedCancelled;
        _entryEditorView.NotifyHostedDialogClosed();
    }

    private void EntryHostedView_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadedNotified)
            return;

        _loadedNotified = true;
        _entryEditorView.NotifyHostedLoaded();
    }

    private void OnHostedSaved(VaultEntry entry)
        => Saved?.Invoke(entry);

    private void OnHostedCancelled()
        => Cancelled?.Invoke();
}
