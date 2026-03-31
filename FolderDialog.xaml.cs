using System.Windows;

namespace PassNotes;

public partial class FolderDialog : Window
{
    public string FolderName => (NameBox.Text ?? "").Trim();

    public FolderDialog(string title, string label, string initial = "")
    {
        InitializeComponent();

        Title = title;
        LabelText.Text = label;

        NameBox.Text = initial ?? "";
        Loaded += (_, _) =>
        {
            WindowGeometryHelper.ApplyResponsiveDialogConstraints(this, Owner);
            WindowGeometryHelper.CenterDialogInWorkArea(this, Owner);
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FolderName))
        {
            AppMessageDialogWindow.ShowOk(this, Loc.Instance["Info"], Loc.Instance["FolderNameEmpty"]);
            return;
        }

        DialogResult = true;
    }
}
