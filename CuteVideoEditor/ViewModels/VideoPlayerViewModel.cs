using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.Core.Contracts.ViewModels;
using CuteVideoEditor.Core.Models;
using CuteVideoEditor_Video;
using System.Collections.ObjectModel;
using Windows.Graphics.Imaging;

namespace CuteVideoEditor.ViewModels;
partial class VideoPlayerViewModel : ObservableObject, IVideoPlayerViewModel, IDisposable
{
    private bool disposedValue;

    ImageReader? imageReader;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MediaFrameRate),
        nameof(InputMediaPosition), nameof(InputMediaDuration), nameof(InputFrameNumber),
        nameof(OutputMediaPosition), nameof(OutputMediaDuration), nameof(OutputFrameNumber))]
    string? mediaFileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MediaFrameRate),
        nameof(InputMediaDuration), nameof(InputFrameNumber),
        nameof(OutputMediaPosition), nameof(OutputMediaDuration), nameof(OutputFrameNumber))]
    TimeSpan inputMediaPosition;

    public TimeSpan InputMediaDuration { get; private set; }

    public double MediaFrameRate { get; private set; }

    [ObservableProperty]
    MediaPlayerState mediaPlayerState;

    public ObservableCollection<TrimmingMarkerModel> TrimmingMarkers { get; } = [new(0)];

    public event EventHandler<SoftwareBitmap?>? FrameReady;

    public long InputFrameNumber
    {
        get
        {
            if (MediaFrameRate is 0) return 0;
            return (long)(Math.Round(InputMediaPosition.TotalSeconds * MediaFrameRate));
        }
        set
        {
            if (MediaFrameRate is 0) throw new InvalidOperationException();
            InputMediaPosition = TimeSpan.FromSeconds(value / MediaFrameRate);
        }
    }

    public long OutputFrameNumber
    {
        get => GetOutputFrameNumberFromInputFrameNumber(InputFrameNumber);
        set => InputFrameNumber = GetInputFrameNumberFromOutputFrameNumber(value);
    }
    
    public TimeSpan OutputMediaPosition
    {
        get => GetPositionFromFrameNumber(OutputFrameNumber);
        set => OutputFrameNumber = GetFrameNumberFromPosition(value);
    }
    
    public TimeSpan OutputMediaDuration =>
        GetPositionFromFrameNumber(GetOutputFrameNumberFromInputFrameNumber((long)(InputMediaDuration.TotalSeconds * MediaFrameRate)));

    public bool GetNextNonTrimmedInputFrameNumber(long inputFrameNumber, bool forward, out long nextNonTrimmedInputFrameNumber)
    {
        nextNonTrimmedInputFrameNumber = 0;

        for (int i = 0; i < TrimmingMarkers.Count; ++i)
        {
            var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                i == TrimmingMarkers.Count - 1 ? InputMediaDuration.TotalSeconds * MediaFrameRate : TrimmingMarkers[i + 1].FrameNumber);
            if (frameStart <= inputFrameNumber && inputFrameNumber < frameEnd)
            {
                if (!TrimmingMarkers[i].TrimAfter)
                {
                    nextNonTrimmedInputFrameNumber = inputFrameNumber;
                    return true;
                }

                if (forward)
                {
                    for (; i < TrimmingMarkers.Count; ++i)
                        if (!TrimmingMarkers[i].TrimAfter)
                        {
                            nextNonTrimmedInputFrameNumber = TrimmingMarkers[i].FrameNumber;
                            return true;
                        }
                }
                else
                    for (; i >= 0; --i)
                        if (!TrimmingMarkers[i].TrimAfter)
                        {
                            nextNonTrimmedInputFrameNumber = TrimmingMarkers[i + 1].FrameNumber;
                            return true;
                        }

                return false;
            }
        }

        // what?
        return false;
    }

    public long GetFrameNumberFromPosition(TimeSpan position) =>
       (long)(position.TotalSeconds * MediaFrameRate);

    public long GetInputFrameNumberFromOutputFrameNumber(long outputFrameNumber)
    {
        var frameNumber = 0L;
        for (var i = 0; i < TrimmingMarkers.Count; ++i)
        {
            var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                (long)(i == TrimmingMarkers.Count - 1 ? InputMediaDuration.TotalSeconds * MediaFrameRate : TrimmingMarkers[i + 1].FrameNumber));

            if (TrimmingMarkers[i].TrimAfter)
                frameNumber += frameEnd - frameStart;
            else
            {
                var framesNeeded = Math.Min(frameEnd - frameStart, outputFrameNumber);
                frameNumber += framesNeeded;
                outputFrameNumber -= framesNeeded;

                if (outputFrameNumber == 0)
                    break;
            }
        }

        return frameNumber;
    }

    public TimeSpan GetPositionFromFrameNumber(long outputFrameNumber)
    {
        if (MediaFrameRate is 0) return TimeSpan.Zero;
        var newPositionTicks = TimeSpan.FromMilliseconds(Math.Round(outputFrameNumber / MediaFrameRate * 1000)).Ticks;
        var frameDurationTicks = TimeSpan.FromSeconds(1 / MediaFrameRate).Ticks;

        if (newPositionTicks % frameDurationTicks > frameDurationTicks / 2)
            newPositionTicks += frameDurationTicks - (newPositionTicks % frameDurationTicks);
        return TimeSpan.FromTicks(newPositionTicks);
    }

    public long GetOutputFrameNumberFromInputFrameNumber(long inputFrameNumber)
    {
        var frameNumber = 0L;
        for (var i = 0; i < TrimmingMarkers.Count; ++i)
            if (TrimmingMarkers[i].FrameNumber <= inputFrameNumber)
            {
                var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                    i == TrimmingMarkers.Count - 1 ? InputMediaDuration.TotalSeconds * MediaFrameRate : TrimmingMarkers[i + 1].FrameNumber);
                frameNumber += !TrimmingMarkers[i].TrimAfter
                    ? (long)Math.Min(inputFrameNumber - frameStart, frameEnd - frameStart)
                    : 0;
            }
            else
                break;

        return frameNumber;
    }

    public void TriggerFrameReady()
    {
        if (imageReader is not null)
            FrameReady?.Invoke(this, imageReader.CurrentFrameBitmap);
    }

    partial void OnMediaFileNameChanged(string? value)
    {
        imageReader?.Dispose();
        imageReader = value is null ? null : new(value);

        if (imageReader is not null)
        {
            MediaFrameRate = imageReader.FrameRate;
            InputMediaDuration = imageReader.MediaDuration;
        }

        TrimmingMarkers.Clear();
        TrimmingMarkers.Add(new(0));
        InputMediaPosition = TimeSpan.Zero;
        MediaPlayerState = MediaPlayerState.Stopped;

        TriggerFrameReady();
    }

    partial void OnInputMediaPositionChanged(TimeSpan value)
    {
        if (imageReader is not null)
        {
            imageReader.Position = value;
            TriggerFrameReady();
        }
    }

    public void FrameStep(bool forward)
    {
        if (imageReader is not null)
        {
            if (forward && OutputMediaPosition < OutputMediaDuration)
                ++OutputFrameNumber;
            else if (!forward && OutputFrameNumber > 0)
                --OutputFrameNumber;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // dispose managed state (managed objects)
            }

            // free unmanaged resources (unmanaged objects) and override finalizer
            FrameReady = null;
            imageReader?.Dispose();

            // set large fields to null
            imageReader = null;

            disposedValue = true;
        }
    }

    ~VideoPlayerViewModel()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
