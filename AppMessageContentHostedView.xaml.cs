using System.Windows.Controls;

namespace PassNotes;

public partial class AppMessageContentHostedView : UserControl
{
    public AppMessageContentHostedView(string message)
    {
        InitializeComponent();
        MessageTextBlock.Text = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
    }
}
