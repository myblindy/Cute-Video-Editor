using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuteVideoEditor.Core.Models;
using System.Collections.ObjectModel;
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.Security.Cryptography.Certificates;

namespace CuteVideoEditor.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    [ObservableProperty]
    string? mediaFileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCropRect))]
    TimeSpan mediaDuration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCropRect))]
    double mediaFrameRate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCropRect))]
    TimeSpan mediaPosition;

    [ObservableProperty]
    SizeModel mediaPixelSize;

    public ObservableCollection<CropFrameEntry> CropFrames = [];

    public RectModel CurrentCropRect
    {
        get
        {
            if (CropFrames.Count == 0 || MediaFrameRate == 0 || MediaDuration == default)
                return default;
            else
            {
                var frameNumber = (int)(MediaPosition.TotalSeconds * MediaFrameRate);
                if (CropFrames.FirstOrDefault(x => x.FrameNumber == frameNumber) is { } cropFrame)
                    return cropFrame.Rect;
                else
                {
                    // todo interpolate
                    return default;
                }
            }
        }
    }

    partial void OnMediaPixelSizeChanged(SizeModel value)
    {
        if (CropFrames.Count == 0)
        {
            // default a crop frame on frame 0
            CropFrames.Add(new(0, new(
                MediaPixelSize.Width / 2, MediaPixelSize.Height / 2,
                MediaPixelSize.Width / 2, MediaPixelSize.Height / 2)));
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(PauseCommand))]
    MediaPlaybackState mediaPlaybackState;

    [RelayCommand(CanExecute = nameof(CanPause))]
    void Pause() => MediaPlaybackState = MediaPlaybackState.Paused;
    bool CanPause() => MediaPlaybackState == MediaPlaybackState.Playing;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    void Play() => MediaPlaybackState = MediaPlaybackState.Playing;
    bool CanPlay() => MediaPlaybackState is not MediaPlaybackState.Playing;
}

public readonly struct CropFrameEntry(int frameNumber, RectModel rect)
{
    public int FrameNumber { get; } = frameNumber;
    public RectModel Rect { get; } = rect;
}