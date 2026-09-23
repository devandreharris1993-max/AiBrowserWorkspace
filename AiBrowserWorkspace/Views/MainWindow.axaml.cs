using Avalonia.Controls;
using AiBrowserWorkspace.ViewModels;

namespace AiBrowserWorkspace.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
