using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.Helpers;
using FFmpegInteropX;
using ReactiveUI;

namespace CuteVideoEditor.ViewModels.Dialogs;

public partial class ExportVideoViewModel : ObservableObject
{
    public OutputType[] OutputFileTypes { get; } = [.. Enum.GetValues<OutputType>()];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    string? fileName;

    [ObservableProperty]
    OutputType type;

    [ObservableProperty]
    uint crf = 15;

    [ObservableProperty]
    double frameRateMultiplier = 1;

    [ObservableProperty]
    double pixelSizeMultiplier = 1;

    public bool IsValid => !string.IsNullOrWhiteSpace(FileName);

    public ExportVideoViewModel()
    {
        this.WhenAnyValue(x => x.FileName).WhereNotNull().Subscribe(fn =>
            Type = Path.GetExtension(fn) switch
            {
                ".webm" => OutputType.Vp9,
                _ => OutputType.Mp4
            });
        this.WhenAnyValue(x => x.Type).WhereNotNull().Subscribe(ft =>
            FileName = FileName is null ? null : ft switch
            {
                OutputType.Vp9 or OutputType.Vp8 => Path.ChangeExtension(FileName, ".webm"),
                OutputType.Mp4 => Path.ChangeExtension(FileName, ".mp4"),
                _ => throw new NotImplementedException()
            });
    }

    public FFmpegTranscodeOutput BuildTranscodeOutputProperties(MainViewModel mainViewModel) => new(
        FileName, Type, Crf, FrameRateMultiplier, (mainViewModel.LargestOutputPixelSize * PixelSizeMultiplier).ToWFSize(),
        OutputPresetType.Medium);
}
