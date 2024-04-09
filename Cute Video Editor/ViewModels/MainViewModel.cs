using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuteVideoEditor.Core.Models;
using Microsoft.UI.Xaml;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using Windows.Media.Playback;

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
    [NotifyPropertyChangedFor(nameof(VideoOverlayMargins))]
    SizeModel mediaPixelSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoOverlayMargins))]
    SizeModel videoPlayerPixelSize;

    public Thickness VideoOverlayMargins { get; private set; }
    public double VideoOverlayScale { get; private set; }

    public ObservableCollection<CropFrameEntry> CropFrames = [];

    public CropRect CurrentCropRect
    {
        get
        {
            if (CropFrames.Count == 0 || MediaFrameRate == 0 || MediaDuration == default)
                return new(default, CropRectType.None);
            else
            {
                var frameNumber = (int)(MediaPosition.TotalSeconds * MediaFrameRate);
                if (CropFrames.FirstOrDefault(x => x.FrameNumber == frameNumber) is { Rect: { Width: > 0, Height: > 0 } } cropFrame)
                    return new(cropFrame.Rect, CropRectType.Frozen);
                else
                {
                    // extrapolate
                    if (frameNumber > CropFrames[^1].FrameNumber && CropFrames.Count >= 2)
                        return new(RectModel.Extrapolate(CropFrames[^2].Rect, CropFrames[^1].Rect,
                            CropFrames[^1].FrameNumber - CropFrames[^2].FrameNumber,
                            frameNumber - CropFrames[^1].FrameNumber), CropRectType.Interpolated);
                    else if (frameNumber > CropFrames[^1].FrameNumber)
                        return new(CropFrames[^1].Rect, CropRectType.Interpolated);

                    // interpolate
                    if (CropFrames.TakeWhile(w => w.FrameNumber < frameNumber).Count() is { } idx
                        && idx >= 0 && idx < CropFrames.Count - 1)
                    {
                        return new(RectModel.Interpolate(CropFrames[idx].Rect, CropFrames[idx + 1].Rect,
                            (frameNumber - CropFrames[idx].FrameNumber) / (double)(CropFrames[idx + 1].FrameNumber - CropFrames[idx].FrameNumber)), CropRectType.Interpolated);
                    }
                }
            }

            throw new InvalidOperationException();
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

    public MainViewModel()
    {
        this.WhenAnyValue(x => x.MediaPixelSize, x => x.VideoPlayerPixelSize)
            .Subscribe(((SizeModel MediaPixelSize, SizeModel VideoPlayerPixelSize) w) =>
            {
                if (w.MediaPixelSize == default || w.VideoPlayerPixelSize == default) return;

                // calculate margins to center the overlay element on the video player
                var videoPlayerAspectRatio = VideoPlayerPixelSize.Width / (double)VideoPlayerPixelSize.Height;
                var mediaAspectRatio = MediaPixelSize.Width / (double)MediaPixelSize.Height;
                if (videoPlayerAspectRatio > mediaAspectRatio)
                {
                    var overlayWidth = VideoPlayerPixelSize.Height * mediaAspectRatio;
                    VideoOverlayMargins = new Thickness((VideoPlayerPixelSize.Width - overlayWidth) / 2, 0, (VideoPlayerPixelSize.Width - overlayWidth) / 2, 0);
                }
                else
                {
                    var overlayHeight = VideoPlayerPixelSize.Width / mediaAspectRatio;
                    VideoOverlayMargins = new Thickness(0, (VideoPlayerPixelSize.Height - overlayHeight) / 2, 0, (VideoPlayerPixelSize.Height - overlayHeight) / 2);
                }
                OnPropertyChanged(nameof(VideoOverlayMargins));

                // calculate scale to fit the overlay element on the video player
                if (videoPlayerAspectRatio > mediaAspectRatio)
                    VideoOverlayScale = VideoPlayerPixelSize.Height / (double)MediaPixelSize.Height;
                else
                    VideoOverlayScale = VideoPlayerPixelSize.Width / (double)MediaPixelSize.Width;
                OnPropertyChanged(nameof(VideoOverlayScale));
            });
    }
}

public readonly struct CropFrameEntry(int frameNumber, RectModel rect)
{
    public int FrameNumber { get; } = frameNumber;
    public RectModel Rect { get; } = rect;
}

public enum CropRectType { None, FirstFrameUnfrozen, Frozen, Interpolated }

public readonly struct CropRect(RectModel rect, CropRectType type)
{
    public RectModel Rect { get; } = rect;
    public CropRectType Type { get; } = type;
}