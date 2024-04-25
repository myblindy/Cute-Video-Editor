using CommunityToolkit.Mvvm.ComponentModel;
using FFmpegInteropX;

namespace CuteVideoEditor.ViewModels.Dialogs;

public partial class ExportVideoViewModel : ObservableObject
{
    public OutputType[] OutputFileTypes { get; } = [.. Enum.GetValues<OutputType>()];

    [ObservableProperty]
    OutputType? selectedOutputFileType;

    public void SelectOutputFileType(string filename) => SelectedOutputFileType = Path.GetExtension(filename) switch
    {
        ".mp4" => OutputType.Mp4,
        ".webm" => OutputType.Vp9,
        _ => SelectedOutputFileType
    };
}
