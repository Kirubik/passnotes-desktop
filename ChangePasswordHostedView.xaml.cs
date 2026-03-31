using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PassNotes;

public partial class ChangePasswordHostedView : UserControl, IHostedDialogCloseRequestHandler
{
    public event Action<string, string>? Accepted;
    public event Action? Cancelled;

    public ChangePasswordHostedView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            OldPwd.Focus();
            Keyboard.Focus(OldPwd);
        };
        PreviewKeyDown += ChangePasswordHostedView_PreviewKeyDown;
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

    private void ChangePasswordHostedView_PreviewKeyDown(object sender, KeyEventArgs e)
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
        var oldPwd = OldPwd.Password ?? string.Empty;
        var p1 = NewPwd1.Password ?? string.Empty;
        var p2 = NewPwd2.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(oldPwd) || string.IsNullOrWhiteSpace(p1))
        {
            ShowError(Loc.Instance["EmptyPassword"]);
            return;
        }

        if (p1 != p2)
        {
            ShowError(Loc.Instance["PasswordMismatch"]);
            return;
        }

        if (oldPwd == p1)
        {
            ShowError(Loc.Instance["PasswordSame"]);
            return;
        }

        ErrorTextBlock.Visibility = Visibility.Collapsed;
        Accepted?.Invoke(oldPwd, p1);
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
