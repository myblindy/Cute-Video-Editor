using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.Views.Dialogs;
using FFmpegInteropX;
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
                ["H264 MP4 Files"] = [".mp4"],
                ["WebM VP8/9 Files"] = [".webm"]
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
        dlg.MainViewModel = mainViewModel;

        var largestOutputPixelSize = mainViewModel.LargestOutputPixelSize;
        dlg.OutputModel = new()
        {
            FileName = Path.ChangeExtension(mainViewModel.MediaFileName, "webm"),
            Type = OutputType.Vp9,
            CRF = 12,
            FrameRate = mainViewModel.MediaFrameRate,
            PixelSize = new(largestOutputPixelSize.Width, largestOutputPixelSize.Height),
            Preset = OutputPresetType.Medium
        };

        return await dlg.ShowAsync() is ContentDialogResult.Primary ? dlg.OutputModel : null;
    }
}