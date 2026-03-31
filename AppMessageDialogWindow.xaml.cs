using System;
using System.ComponentModel;
using System.Windows;
using PassNotes.Behaviors;

namespace PassNotes;

public partial class AppMessageDialogWindow : Window
{
    private readonly MessageBoxButton _mode;
    private MessageBoxResult _result = MessageBoxResult.None;

    private AppMessageDialogWindow(string title, string message, MessageBoxButton mode)
    {
        InitializeComponent();

        _mode = mode;
        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = string.IsNullOrWhiteSpace(message) ? title : message;

        YesButton.Content = Loc.Instance["Yes"];
        NoButton.Content = Loc.Instance["No"];
        CancelButton.Content = Loc.Instance["Cancel"];
        OkButton.Content = Loc.Instance["Ok"];

        ConfigureButtons();

        Loaded += (_, _) =>
        {
            WindowGeometryHelper.ApplyResponsiveDialogConstraints(this, Owner);
            WindowGeometryHelper.CenterDialogInWorkArea(this, Owner);
            FocusDefaultButton();
        };
    }

    public static MessageBoxResult ShowOk(Window? owner, string title, string message)
        => Show(owner, title, message, MessageBoxButton.OK);

    public static MessageBoxResult ShowYesNo(Window? owner, string title, string message)
        => Show(owner, title, message, MessageBoxButton.YesNo);

    public static MessageBoxResult ShowYesNoCancel(Window? owner, string title, string message)
        => Show(owner, title, message, MessageBoxButton.YesNoCancel);

    private static MessageBoxResult Show(Window? owner, string title, string message, MessageBoxButton mode)
    {
        if (owner is MainWindow main)
            return main.ShowHostedAppMessageDialog(title, message, mode);

        var dialog = new AppMessageDialogWindow(title, message, mode);
        ApplyOwner(dialog, owner);
        AppOwnedModalDialogScope.ShowDialog(dialog);
        return dialog._result == MessageBoxResult.None ? GetFallbackResult(mode) : dialog._result;
    }

    private static MessageBoxResult GetFallbackResult(MessageBoxButton mode)
        => mode switch
        {
            MessageBoxButton.OK => MessageBoxResult.OK,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            _ => MessageBoxResult.Cancel
        };

    private static void ApplyOwner(Window dialog, Window? owner)
    {
        if (owner != null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    private void ConfigureButtons()
    {
        OkButton.Visibility = _mode == MessageBoxButton.OK ? Visibility.Visible : Visibility.Collapsed;
        YesButton.Visibility = _mode == MessageBoxButton.OK ? Visibility.Collapsed : Visibility.Visible;
        NoButton.Visibility = _mode == MessageBoxButton.OK ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.Visibility = _mode == MessageBoxButton.YesNoCancel ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FocusDefaultButton()
    {
        if (_mode == MessageBoxButton.OK)
            OkButton.Focus();
        else
            YesButton.Focus();
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.Yes;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.No;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.Cancel;
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _result = MessageBoxResult.OK;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_result == MessageBoxResult.None)
            _result = GetFallbackResult(_mode);

        base.OnClosing(e);
    }
}
