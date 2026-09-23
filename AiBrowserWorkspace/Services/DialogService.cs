using AiBrowserWorkspace.Models;
using AiBrowserWorkspace.ViewModels;
using AiBrowserWorkspace.Views;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace AiBrowserWorkspace.Services;

public sealed record AddBrowserResult(AiPlatform Platform, string? SessionName, ProxyEndpoint? Proxy);

public interface IDialogService
{
    Task<AddBrowserResult?> ShowAddBrowserDialogAsync();
    Task<bool> ConfirmAsync(string title, string message);
}

public sealed class DialogService : IDialogService
{
    public async Task<AddBrowserResult?> ShowAddBrowserDialogAsync()
    {
        var owner = GetOwnerWindow();
        var viewModel = new AddBrowserViewModel();
        var dialog = new AddBrowserDialog { DataContext = viewModel };

        var accepted = owner is not null
            ? await dialog.ShowDialog<bool>(owner)
            : false;

        // The dialog only closes "accepted" after Validate() has already succeeded
        // (see AddBrowserDialog.axaml.cs), so ResolvedProxy is already up to date here.
        return accepted ? new AddBrowserResult(viewModel.SelectedPlatform, viewModel.SessionName, viewModel.ResolvedProxy) : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var owner = GetOwnerWindow();
        var dialog = new ConfirmDialog(title, message);

        return owner is not null && await dialog.ShowDialog<bool>(owner);
    }

    private static Window? GetOwnerWindow()
        => Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
