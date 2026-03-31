using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PassNotes;

public partial class PasswordGeneratorHostedView : UserControl, IHostedDialogCloseRequestHandler
{
    private readonly MainWindow _owner;
    private readonly PopupToastController _toast = new(1200);

    public event Action? Cancelled;

    public PasswordGeneratorHostedView(MainWindow owner)
    {
        InitializeComponent();

        _owner = owner;

        Loaded += PasswordGeneratorHostedView_Loaded;
        Unloaded += PasswordGeneratorHostedView_Unloaded;
        PreviewKeyDown += PasswordGeneratorHostedView_PreviewKeyDown;

        GenerateAndShow();
    }

    public bool TryHandleHostedDialogCloseRequest()
    {
        Cancelled?.Invoke();
        return true;
    }

    private void PasswordGeneratorHostedView_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LenBox.Focus();
            LenBox.SelectAll();
        }
        catch
        {
            // ignore
        }
    }

    private void PasswordGeneratorHostedView_Unloaded(object sender, RoutedEventArgs e)
    {
        try { _toast.CloseCurrent(); } catch { }
        CloseCopyToastsSafe();
    }

    private void PasswordGeneratorHostedView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
        {
            HelpWindowManager.ShowOrActivate(_owner, null);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (Keyboard.FocusedElement is ButtonBase)
            return;

        e.Handled = true;
        GenerateAndShow();
    }

    private void LenBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        foreach (var ch in e.Text)
        {
            if (!char.IsDigit(ch))
            {
                e.Handled = true;
                return;
            }
        }

        e.Handled = false;
    }

    private int? GetLengthOrShowError()
    {
        if (!int.TryParse(LenBox.Text, out var n))
            return 16;

        if (n < PasswordGenerator.MinLength)
        {
            AppMessageDialogWindow.ShowOk(_owner, Loc.Instance["Info"], Loc.Instance["GeneratorMinLength3"]);
            LenBox.Text = PasswordGenerator.MinLength.ToString();
            LenBox.SelectAll();
            LenBox.Focus();
            return null;
        }

        if (n > PasswordGenerator.MaxLength)
        {
            AppMessageDialogWindow.ShowOk(_owner, Loc.Instance["Info"], Loc.Instance["GeneratorMaxLength32"]);
            LenBox.Text = PasswordGenerator.MaxLength.ToString();
            LenBox.SelectAll();
            LenBox.Focus();
            return null;
        }

        return n;
    }

    private void GenerateAndShow()
    {
        var len = GetLengthOrShowError();
        if (len is null)
            return;

        OutBox.Text = PasswordGenerator.Generate(len.Value);
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
        => GenerateAndShow();

    private void CloseCopyToastsSafe()
    {
        try { CopyToastPopup.IsOpen = false; } catch { }
        try { CopyFailedToastPopup.IsOpen = false; } catch { }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(OutBox.Text))
            return;

        var ok = ClipboardSecurity.TryCopySecret(OutBox.Text, out _);

        try
        {
            CloseCopyToastsSafe();
            var popup = ok ? CopyToastPopup : CopyFailedToastPopup;
            _toast.Show(popup, durationMs: 1200, onClose: CloseCopyToastsSafe);
        }
        catch
        {
            CloseCopyToastsSafe();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
        => Cancelled?.Invoke();
}
