using System;
using System.Windows.Threading;

namespace PassNotes;

public partial class MainWindow
{
    private HelpHostedView? _hostedHelpView;

    internal void ShowHostedHelpDialog(HelpNavState initialState)
    {
        if (_hostedHelpView != null && ReferenceEquals(HostedDialogHost.Content, _hostedHelpView))
        {
            _hostedHelpView.NavigateTo(initialState);
            TryUpdateHostedHelpDialogSize();
            return;
        }

        var view = new HelpHostedView();
        view.NavigateTo(initialState);
        view.CloseRequested += CloseHostedDialog;
        _hostedHelpView = view;

        var size = GetHostedHelpDialogSize();

        ShowHostedDialogModal(new HostedDialogRequest
        {
            Title = Loc.Instance["HelpTitle"],
            Content = view,
            SecondaryButtonText = Loc.Instance["Close"],
            SecondaryAction = view.RequestClose,
            Width = size.width,
            MinWidth = size.minWidth,
            MaxWidth = size.maxWidth,
            Height = size.height,
            MinHeight = size.minHeight,
            PreferContentFocus = true,
            OnClosed = () =>
            {
                if (ReferenceEquals(_hostedHelpView, view))
                    _hostedHelpView = null;
            }
        });

        Dispatcher.BeginInvoke(new Action(TryUpdateHostedHelpDialogSize), DispatcherPriority.Loaded);
    }

    private void TryUpdateHostedHelpDialogSize()
    {
        if (_hostedHelpView == null || !ReferenceEquals(HostedDialogHost.Content, _hostedHelpView))
            return;

        var size = GetHostedHelpDialogSize();
        HostedDialogHost.UpdateCurrentSize(size.width, size.minWidth, size.maxWidth, size.height, size.minHeight);
    }

    private (double width, double minWidth, double maxWidth, double height, double minHeight) GetHostedHelpDialogSize()
    {
        var hostWidth = HostedDialogLayer?.ActualWidth > 0 ? HostedDialogLayer.ActualWidth : ActualWidth;
        var hostHeight = HostedDialogLayer?.ActualHeight > 0 ? HostedDialogLayer.ActualHeight : ActualHeight;

        var availableWidth = Math.Max(320, hostWidth - 72);
        var availableHeight = Math.Max(280, hostHeight - 72);

        var width = Math.Min(980, availableWidth);
        var height = Math.Min(680, availableHeight);

        var minWidth = Math.Min(560, width);
        var minHeight = Math.Min(420, height);
        var maxWidth = width;

        return (width, minWidth, maxWidth, height, minHeight);
    }
}