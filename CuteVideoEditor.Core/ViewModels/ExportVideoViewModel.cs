using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.Core.Models;
using CuteVideoEditor.Core.Services;
using ReactiveUI;

namespace CuteVideoEditor.ViewModels.Dialogs;

public partial class ExportVideoViewModel(SettingsService settingsService) : ObservableObject
{
    public VideoOutputType[] OutputFileTypes { get; } = [.. Enum.GetValues<VideoOutputType>()];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    string? fileName;

    [ObservableProperty]
    VideoOutputType type = settingsService.LastVideoOutputType;

    [ObservableProperty]
    uint crf = settingsService.LastCrf;

    [ObservableProperty]
    double frameRateMultiplier = 1;

    [ObservableProperty]
    double pixelSizeMultiplier = 1;

    [ObservableProperty]
    double originalFrameRate;

    public bool IsValid => !string.IsNullOrWhiteSpace(FileName);

    partial void OnFileNameChanged(string? value) =>
        Type = value is null ? Type : Path.GetExtension(value) switch
        {
            ".webm" => VideoOutputType.Vp9,
            _ => VideoOutputType.Mp4
        };

    partial void OnTypeChanged(VideoOutputType value) =>
        FileName = FileName is null ? null : value switch
        {
            VideoOutputType.Vp9 or VideoOutputType.Vp8 => Path.ChangeExtension(FileName, ".webm"),
            VideoOutputType.Mp4 => Path.ChangeExtension(FileName, ".mp4"),
            _ => throw new NotImplementedException()
        };

    public VideoTranscodeOutput BuildTranscodeOutputProperties(VideoEditorViewModel mainViewModel) => new()
    {
        FileName = FileName!,
        OutputType = Type,
        Crf = Crf,
        FrameRateMultiplier = FrameRateMultiplier,
        PixelWidth = (mainViewModel.LargestOutputPixelSize * PixelSizeMultiplier).Width,
        PixelHeight = (mainViewModel.LargestOutputPixelSize * PixelSizeMultiplier).Height,
    };

    public static string GetPrettyFrameRate(double frameRate, double multiplier = 1) =>
        (frameRate * multiplier).ToString("0.##\x00A0FPS");
}
