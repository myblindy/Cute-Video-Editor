using CuteVideoEditor.Core.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CuteVideoEditor.Core.Contracts.ViewModels;
public interface IVideoPlayerViewModel : INotifyPropertyChanging, INotifyPropertyChanged, IDisposable
{
    string? MediaFileName { get; set; }
    TimeSpan InputMediaPosition { get; set; }
    long InputFrameNumber { get; set; }
    TimeSpan InputMediaDuration { get; }
    TimeSpan OutputMediaPosition { get; set; }
    long OutputFrameNumber { get; set; }
    TimeSpan OutputMediaDuration { get; }
    double MediaFrameRate { get; }
    MediaPlayerState MediaPlayerState { get; set; }

    ObservableCollection<TrimmingMarkerModel> TrimmingMarkers { get; }

    void FrameStep(bool forward);
    long GetFrameNumberFromPosition(TimeSpan position);
    long GetInputFrameNumberFromOutputFrameNumber(long outputFrameNumber);
    bool GetNextNonTrimmedInputFrameNumber(long inputFrameNumber, bool forward, out long nextNonTrimmedInputFrameNumber);
    long GetOutputFrameNumberFromInputFrameNumber(long inputFrameNumber);
    TimeSpan GetPositionFromFrameNumber(long outputFrameNumber);

    void TriggerFrameReady();
}

public enum MediaPlayerState
{

    Playing,
    Paused,
    Stopped
}