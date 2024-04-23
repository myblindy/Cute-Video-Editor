using CommunityToolkit.Mvvm.ComponentModel;
using ReactiveUI;
using System.ComponentModel;

namespace CuteVideoEditor.Core.Models;

public partial class TrimmingMarkerModel : ObservableObject, IReactiveObject
{
    public long FrameNumber { get; }

    [ObservableProperty]
    bool trimAfter;

    public TrimmingMarkerModel(long frameNumber, bool trimAfter = false)
    {
        FrameNumber = frameNumber;
        TrimAfter = trimAfter;
    }

    public void RaisePropertyChanging(PropertyChangingEventArgs args) => OnPropertyChanging(args);

    public void RaisePropertyChanged(PropertyChangedEventArgs args) => OnPropertyChanged(args);
}
