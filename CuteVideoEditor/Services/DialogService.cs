﻿using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.Core.Models;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;
using CuteVideoEditor.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

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
                ["CuteVideoEditor Project"] = [".cve"]
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

    public async Task<VideoTranscodeOutput?> SelectTranscodeOutputParameters(VideoEditorViewModel mainViewModel)
    {
        var dlg = serviceProvider.GetRequiredService<ExportVideoContentDialog>();
        dlg.XamlRoot = App.MainWindow.Content.XamlRoot;
        dlg.ViewModel.FileName = mainViewModel.ProjectFileName;
        dlg.ViewModel.Type = VideoOutputType.Vp9;
        dlg.ViewModel.OriginalFrameRate = mainViewModel.VideoPlayerViewModel.MediaFrameRate;

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

    public async Task ShowInformationMessageDialog(string title, string message) =>
        await MessageContentDialog.Information(App.MainWindow.Content.XamlRoot, title, message);
}