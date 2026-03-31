using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PassNotes;

public partial class MasterPasswordPromptHostedView : UserControl, IHostedDialogCloseRequestHandler
{
    public event Action<string>? Accepted;
    public event Action? Cancelled;

    public MasterPasswordPromptHostedView(string prompt)
    {
        InitializeComponent();

        PromptTextBlock.Text = prompt;
        Loaded += (_, _) =>
        {
            try
            {
                Pwd.Focus();
                Keyboard.Focus(Pwd);
            }
            catch
            {
                // ignore
            }
        };
        PreviewKeyDown += MasterPasswordPromptHostedView_PreviewKeyDown;
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

    private void MasterPasswordPromptHostedView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (Keyboard.FocusedElement is ButtonBase)
            return;

        e.Handled = true;
        TryAccept();
    }

    private void Pwd_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ErrorTextBlock.Visibility == Visibility.Visible)
            ErrorTextBlock.Visibility = Visibility.Hidden;
    }

    public void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
        FocusPassword();
    }

    public void FocusPassword()
    {
        try
        {
            Pwd.Focus();
            Keyboard.Focus(Pwd);
        }
        catch
        {
            // ignore
        }
    }

    private void TryAccept()
    {
        var password = Pwd.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError(Loc.Instance["EmptyPassword"]);
            return;
        }

        ErrorTextBlock.Visibility = Visibility.Hidden;
        Accepted?.Invoke(password);
    }
}
