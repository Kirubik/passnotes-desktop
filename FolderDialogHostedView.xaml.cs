using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PassNotes;

public partial class FolderDialogHostedView : UserControl, IHostedDialogCloseRequestHandler
{
    public event Action<string>? Accepted;
    public event Action? Cancelled;

    public string FolderName => (NameBox.Text ?? string.Empty).Trim();

    public FolderDialogHostedView(string label, string initial = "")
    {
        InitializeComponent();

        LabelText.Text = label;
        NameBox.Text = initial ?? string.Empty;

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
        PreviewKeyDown += FolderDialogHostedView_PreviewKeyDown;
    }

    public void RequestPrimaryAction()
        => TryAccept();

    public void RequestSecondaryAction()
        => Cancelled?.Invoke();

    public bool TryHandleHostedDialogCloseRequest()
    {
        Cancelled?.Invoke();
        return true;
    }

    private void FolderDialogHostedView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (Keyboard.FocusedElement is ButtonBase)
            return;

        e.Handled = true;
        TryAccept();
    }

    private void TryAccept()
    {
        if (string.IsNullOrWhiteSpace(FolderName))
        {
            ErrorTextBlock.Text = Loc.Instance["FolderNameEmpty"];
            ErrorTextBlock.Visibility = Visibility.Visible;
            NameBox.Focus();
            return;
        }

        ErrorTextBlock.Visibility = Visibility.Collapsed;
        Accepted?.Invoke(FolderName);
    }
}
