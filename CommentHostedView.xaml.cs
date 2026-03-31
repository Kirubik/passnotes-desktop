using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PassNotes;

public partial class CommentHostedView : UserControl, IHostedDialogCloseRequestHandler
{
    public event Action<string>? Applied;
    public event Action? Cancelled;

    public CommentHostedView(string initialText)
    {
        InitializeComponent();

        CommentTextBox.Text = initialText ?? string.Empty;
        Loaded += CommentHostedView_Loaded;
    }

    public void RequestPrimaryAction()
        => Applied?.Invoke(CommentTextBox.Text ?? string.Empty);

    public void RequestSecondaryAction()
        => Cancelled?.Invoke();

    public bool TryHandleHostedDialogCloseRequest()
    {
        Cancelled?.Invoke();
        return true;
    }

    private void CommentHostedView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            CommentTextBox.Focus();
            CommentTextBox.CaretIndex = CommentTextBox.Text?.Length ?? 0;
        }
        catch
        {
            // ignore
        }
    }

    private void CommentTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (e.Key == Key.Enter || e.Key == Key.Return))
        {
            e.Handled = true;
            RequestPrimaryAction();
        }
    }
}
