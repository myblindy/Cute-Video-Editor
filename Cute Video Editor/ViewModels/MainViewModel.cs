﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuteVideoEditor.Core.Models;
using Microsoft.UI.Xaml;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using Windows.Media.Playback;
using SuperLinq;

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
                    return new(cropFrame.Rect, CropRectType.KeyFrame);
                else
                {
                    // extrapolate
                    if (frameNumber > CropFrames[^1].FrameNumber)
                        return new(CropFrames[^1].Rect, CropRectType.Interpolated);

                    // interpolate
                    if (CropFrames.TakeWhile(w => w.FrameNumber < frameNumber).Count() is { } idx
                        && idx >= 0 && idx < CropFrames.Count)
                    {
                        return new(RectModel.Interpolate(CropFrames[idx - 1].Rect, CropFrames[idx].Rect,
                            (frameNumber - CropFrames[idx - 1].FrameNumber) / (double)(CropFrames[idx].FrameNumber - CropFrames[idx - 1].FrameNumber)), CropRectType.Interpolated);
                    }
                }
            }

            throw new InvalidOperationException();
        }
        set
        {
            if (MediaFrameRate == 0 || MediaDuration == default)
                return;

            var frameNumber = (int)(MediaPosition.TotalSeconds * MediaFrameRate);
            var existingCropFrameIndex = CropFrames.FindIndex(x => x.FrameNumber == frameNumber);
            if (existingCropFrameIndex < 0)
            {
                // materialize the crop frame
                // Find the index to insert the new crop frame
                int insertIndex = CropFrames.FindLastIndex(x => x.FrameNumber <= frameNumber);
                if (insertIndex == -1)
                    CropFrames.Insert(0, new(frameNumber, value.Rect));
                else
                    CropFrames.Insert(insertIndex + 1, new(frameNumber, value.Rect));
                OnPropertyChanged(nameof(CurrentCropRect));
            }
            else if (CropFrames[existingCropFrameIndex].Rect != value.Rect)
            {
                CropFrames[existingCropFrameIndex] = new(frameNumber, value.Rect);
                OnPropertyChanged(nameof(CurrentCropRect));
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

public enum CropRectType { None, FirstFrameUnfrozen, KeyFrame, Interpolated }

public readonly struct CropRect(RectModel rect, CropRectType type)
{
    public RectModel Rect { get; } = rect;
    public CropRectType Type { get; } = type;
}