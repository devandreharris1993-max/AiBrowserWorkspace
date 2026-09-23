using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AiBrowserWorkspace.ViewModels;

namespace AiBrowserWorkspace.Views;

public partial class AddBrowserDialog : Window
{
    public AddBrowserDialog()
    {
        InitializeComponent();
    }

    private void OnAddClick(object? sender, RoutedEventArgs e) => TryAccept();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close(false);
                e.Handled = true;
                break;
            case Key.Enter:
                TryAccept();
                e.Handled = true;
                break;
        }
    }

    // Only closes once the proxy fields (if any) pass validation; otherwise
    // AddBrowserViewModel.ValidationError is now set and the dialog stays open.
    private void TryAccept()
    {
        if (DataContext is AddBrowserViewModel viewModel && !viewModel.Validate())
        {
            return;
        }

        Close(true);
    }
}
