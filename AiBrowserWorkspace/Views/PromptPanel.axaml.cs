using Avalonia.Controls;

namespace AiBrowserWorkspace.Views;

public partial class PromptPanel : UserControl
{
    public PromptPanel()
    {
        InitializeComponent();
    }

    public void FocusEditor()
    {
        PromptTextBox.Focus();
    }
}
