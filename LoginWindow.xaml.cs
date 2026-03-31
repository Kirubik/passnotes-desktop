using System.Windows;

namespace PassNotes;

public partial class LoginWindow : Window
{
    public string MasterPassword { get; private set; } = string.Empty;

    public LoginWindow(bool vaultExists)
    {
        InitializeComponent();

        LoginView.InitializeForVaultState(vaultExists);
        Loaded += (_, _) =>
        {
            WindowGeometryHelper.ApplyResponsiveDialogConstraints(this);
            WindowGeometryHelper.CenterDialogInWorkArea(this);
        };
        LoginView.Accepted += password =>
        {
            MasterPassword = password;
            DialogResult = true;
        };
        LoginView.Cancelled += () => DialogResult = false;
    }
}
