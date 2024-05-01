using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;
using CuteVideoEditor.Views.Dialogs;
using FFmpegInteropX;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using Windows.UI.Popups;

namespace CuteVideoEditor.Services;

public class DialogService(IServiceProvider serviceProvider) : IDialogService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Service")]
    public async Task<string?> SelectVideoFileAsync()
    {
        FileOpenPicker picker = new()
        {
            FileTypeFilter =
            {
                ".cve",
                ".mp4",
                ".mkv",
                ".avi",
                ".webm"
            }
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        return await picker.PickSingleFileAsync() is { } file ? file.Path : null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Service")]
    public async Task<string?> SelectSaveProjectFileAsync(string? filename)
    {
        FileSavePicker picker = new()
        {
            FileTypeChoices =
            {
                ["Cute Video Editor Project"] = [".cve"]
            },
            SuggestedFileName = filename
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        return await picker.PickSaveFileAsync() is { } file ? file.Path : null;
    }

    public async Task<string?> SelectSaveVideoFileAsync(string? filename)
    {
        FileSavePicker picker = new()
        {
            FileTypeChoices =
            {
                ["Video Files"] = [".webm",".mp4"],
            },
            SuggestedFileName = filename
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        return await picker.PickSaveFileAsync() is { } file ? file.Path : null;
    }

    public async Task<FFmpegTranscodeOutput?> SelectTranscodeOutputParameters(MainViewModel mainViewModel)
    {
        var dlg = serviceProvider.GetRequiredService<ExportVideoContentDialog>();
        dlg.XamlRoot = App.MainWindow.Content.XamlRoot;
        dlg.ViewModel.FileName = mainViewModel.ProjectFileName;
        dlg.ViewModel.Type = OutputType.Vp9;

        return await dlg.ShowAsync() is ContentDialogResult.Primary
            ? dlg.ViewModel.BuildTranscodeOutputProperties(mainViewModel)
            : null;
    }

    public async Task<bool> ShowOperationProgressDialog(string? description, bool autoClose,
        Func<OperationProgressViewModel, Task> operation)
    {
        var dlg = serviceProvider.GetRequiredService<OperationProgressContentDialog>();
        dlg.XamlRoot = App.MainWindow.Content.XamlRoot;
        dlg.ViewModel.Description = description;

        var showTask = dlg.ShowAsync();
        await operation(dlg.ViewModel);

        if (autoClose) dlg.Hide();
        await showTask;

        return dlg.ViewModel.Result;
    }

    public async Task ShowMessageDialog(string content, string title)
    {
        var resultDlg = new MessageDialog(content, title);
        WinRT.Interop.InitializeWithWindow.Initialize(resultDlg, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

        await resultDlg.ShowAsync();
    }
}