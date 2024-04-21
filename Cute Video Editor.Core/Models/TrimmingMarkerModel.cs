using CommunityToolkit.Mvvm.ComponentModel;

namespace CuteVideoEditor.Core.Models;

public partial class TrimmingMarkerModel : ObservableObject
{
    public long FrameNumber { get; }

    [ObservableProperty]
    bool trimAfter;

    public TrimmingMarkerModel(long frameNumber, bool trimAfter = false)
    {
        FrameNumber = frameNumber;
        TrimAfter = trimAfter;
    }
}
