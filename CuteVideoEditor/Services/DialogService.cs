using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.Core.Models;
using CuteVideoEditor.Core.Services;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;
using CuteVideoEditor.Views.Dialogs;
using CuteVideoEditor_Video;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace CuteVideoEditor.Services;

public class DialogService(IServiceProvider serviceProvider, SettingsService settingsService) : IDialogService
{
    public bool IsDialogOpen { get; private set; }

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

        try
        {
            IsDialogOpen = true;
            return await picker.PickSingleFileAsync() is { } file ? file.Path : null;
        }
        finally
        {
            IsDialogOpen = false;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Service")]
    public async Task<string?> SelectSaveProjectFileAsync(string? filename)
    {
        FileSavePicker picker = new()
        {
            FileTypeChoices =
            {
                ["CuteVideoEditor Project"] = [".cve"]
            },
            SuggestedFileName = filename
        };
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

        try
        {
            IsDialogOpen = true;
            return await picker.PickSaveFileAsync() is { } file ? file.Path : null;
        }
        finally
        {
            IsDialogOpen = false;
        }
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

        try
        {
            IsDialogOpen = true;
            return await picker.PickSaveFileAsync() is { } file ? file.Path : null;
        }
        finally
        {
            IsDialogOpen = false;
        }
    }

    public async Task<VideoTranscodeOutput?> SelectTranscodeOutputParameters(VideoEditorViewModel mainViewModel)
    {
        var dlg = serviceProvider.GetRequiredService<ExportVideoContentDialog>();
        dlg.XamlRoot = App.MainWindow.Content.XamlRoot;
        dlg.ViewModel.FileName = mainViewModel.ProjectFileName;
        dlg.ViewModel.Type = OutputType.Vp9;
        dlg.ViewModel.OriginalFrameRate = mainViewModel.VideoPlayerViewModel.MediaFrameRate;

        try
        {
            IsDialogOpen = true;
            if (await dlg.ShowAsync() is not ContentDialogResult.Primary)
                return null;

            var output = dlg.ViewModel.BuildTranscodeOutputProperties(mainViewModel);
            settingsService.LastCrf = output.Crf;
            settingsService.LastVideoOutputType = output.OutputType;
            return output;
        }
        finally
        {
            IsDialogOpen = false;
        }
    }

    public async Task<bool> ShowOperationProgressDialog(string? description, bool autoClose,
        Func<OperationProgressViewModel, Task> operation)
    {
        var dlg = serviceProvider.GetRequiredService<OperationProgressContentDialog>();
        dlg.XamlRoot = App.MainWindow.Content.XamlRoot;
        dlg.ViewModel.Description = description;

        try
        {
            IsDialogOpen = true;
            var showTask = dlg.ShowAsync();
            await operation(dlg.ViewModel);

            if (autoClose) dlg.Hide();
            await showTask;

            return dlg.ViewModel.Result;
        }
        finally
        {
            IsDialogOpen = false;
        }
    }

    public async Task<MessageDialogResult> ShowInformationMessageDialog(string title, string message, string? extraButtonText)
    {
        try
        {
            IsDialogOpen = true;
            return await MessageContentDialog.Information(App.MainWindow.Content.XamlRoot, title, message, extraButtonText);
        }
        finally
        {
            IsDialogOpen = false;
        }
    }

    public async Task ShowErrorMessageDialog(string content, string title)
    {
        try
        {
            IsDialogOpen = true;
            await MessageContentDialog.Error(App.MainWindow.Content.XamlRoot, title, content);
        }
        finally
        {
            IsDialogOpen = false;
        }
    }
}