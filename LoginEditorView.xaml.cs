using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PassNotes;

public partial class LoginEditorView : UserControl
{
    private bool _vaultExists;

    public event Action<string>? Accepted;
    public event Action? Cancelled;

    public LoginEditorView()
    {
        InitializeComponent();

        Loaded += LoginEditorView_Loaded;
        Unloaded += LoginEditorView_Unloaded;
        PreviewKeyDown += LoginEditorView_PreviewKeyDown;
    }

    public void InitializeForVaultState(bool vaultExists)
    {
        _vaultExists = vaultExists;
        ConfirmPanel.Visibility = vaultExists ? Visibility.Collapsed : Visibility.Visible;
        RefreshTexts();
    }

    public void FocusPrimaryPassword()
    {
        try
        {
            Pwd1.Focus();
            Keyboard.Focus(Pwd1);
        }
        catch
        {
            // ignore
        }
    }

    private void LoginEditorView_Loaded(object sender, RoutedEventArgs e)
    {
        Loc.Instance.PropertyChanged -= Loc_PropertyChanged;
        Loc.Instance.PropertyChanged += Loc_PropertyChanged;
        RefreshTexts();
        FocusPrimaryPassword();
    }

    private void LoginEditorView_Unloaded(object sender, RoutedEventArgs e)
        => Loc.Instance.PropertyChanged -= Loc_PropertyChanged;

    private void Loc_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => RefreshTexts();

    private void RefreshTexts()
        => ModeText.Text = _vaultExists ? Loc.Instance["UnlockVault"] : Loc.Instance["CreateVault"];

    private void LoginEditorView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancelled?.Invoke();
            return;
        }

        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (Keyboard.FocusedElement is ButtonBase)
            return;

        e.Handled = true;
        TryAccept();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => Cancelled?.Invoke();

    private void Ok_Click(object sender, RoutedEventArgs e)
        => TryAccept();

    private void PasswordChanged_ClearError(object sender, RoutedEventArgs e)
    {
        if (ErrorTextBlock.Visibility == Visibility.Visible)
            ErrorTextBlock.Visibility = Visibility.Collapsed;
    }

    private void TryAccept()
    {
        var p1 = Pwd1.Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(p1))
        {
            ShowError(Loc.Instance["EmptyPassword"]);
            return;
        }

        if (!_vaultExists)
        {
            var p2 = Pwd2.Password ?? string.Empty;
            if (p1 != p2)
            {
                ShowError(Loc.Instance["PasswordMismatch"]);
                return;
            }
        }

        ErrorTextBlock.Visibility = Visibility.Collapsed;
        Accepted?.Invoke(p1);
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
        FocusPrimaryPassword();
    }
}
