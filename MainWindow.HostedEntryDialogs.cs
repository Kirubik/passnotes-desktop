using System;

namespace PassNotes;

public partial class MainWindow
{
    private EntryHostedView? _hostedEntryView;

    private VaultEntry? ShowHostedEntryDialog(
        VaultEntry? existing,
        (string displayName, Guid? folderId, bool isMissing) location,
        EntryEditorDraft? draft = null,
        bool replaceCurrentModal = false,
        Action? afterShow = null)
    {
        VaultEntry? result = null;

        var entryEditorView = new EntryEditorView(this, existing);
        entryEditorView.SetFolderLocation(location.displayName, location.folderId, location.isMissing);
        if (draft != null)
            entryEditorView.ApplyDraft(draft);

        var view = new EntryHostedView(entryEditorView);
        _hostedEntryView = view;

        view.Saved += entry =>
        {
            result = entry;
            CloseHostedDialog();
        };
        view.Cancelled += CloseHostedDialog;

        var request = new HostedDialogRequest
        {
            Title = entryEditorView.DialogTitle,
            Content = view,
            PrimaryButtonText = Loc.Instance["Save"],
            PrimaryAction = view.RequestPrimaryAction,
            SecondaryButtonText = Loc.Instance["Cancel"],
            SecondaryAction = view.RequestSecondaryAction,
            AfterShown = afterShow,
            PreferContentFocus = true,
            Width = 760,
            MinWidth = 720,
            MaxWidth = 920,
            Height = 680,
            MinHeight = 620,
            OnClosed = () =>
            {
                view.NotifyHostedDialogClosed();
                if (ReferenceEquals(_hostedEntryView, view))
                    _hostedEntryView = null;
            }
        };

        if (replaceCurrentModal)
            ReplaceHostedDialogModal(request);
        else
            ShowHostedDialogModal(request);

        return result;
    }

    internal void ShowHostedEntryCommentDialog(EntryEditorView entryEditorView)
    {
        if (entryEditorView == null)
            return;

        var title = BuildHostedEntryCommentTitle(entryEditorView.Result?.Title ?? string.Empty);
        var view = new CommentHostedView(entryEditorView.CurrentCommentText);

        view.Applied += text =>
        {
            entryEditorView.CurrentCommentText = text;
            CloseHostedDialog();
        };
        view.Cancelled += CloseHostedDialog;

        ShowHostedDialogModal(new HostedDialogRequest
        {
            Title = title,
            Content = view,
            PrimaryButtonText = Loc.Instance["Ok"],
            PrimaryAction = view.RequestPrimaryAction,
            SecondaryButtonText = Loc.Instance["Cancel"],
            SecondaryAction = view.RequestSecondaryAction,
            Width = 760,
            MinWidth = 520,
            MaxWidth = 920,
            Height = 560,
            MinHeight = 420,
            PreferContentFocus = true
        });
    }

    internal void ShowHostedEntryPasswordGeneratorDialog(EntryEditorView entryEditorView)
    {
        var view = new PasswordGeneratorHostedView(this);
        view.Cancelled += CloseHostedDialog;

        ShowHostedDialogModal(new HostedDialogRequest
        {
            Title = Loc.Instance["GeneratorTitle"],
            Content = view,
            Width = 520,
            MinWidth = 520,
            MaxWidth = 520,
            PreferContentFocus = true
        });
    }

    private static string BuildHostedEntryCommentTitle(string entryTitleRaw)
    {
        var baseTitle = (Loc.Instance["FieldComment"] ?? string.Empty).Trim().TrimEnd(':').Trim();
        var entryTitle = (entryTitleRaw ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(entryTitle))
            return baseTitle;

        return string.IsNullOrWhiteSpace(baseTitle)
            ? entryTitle
            : $"{baseTitle} - {entryTitle}";
    }
}



