using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuteVideoEditor.Core.Models;
using Microsoft.UI.Xaml;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Linq;
using Windows.Media.Playback;
using SuperLinq;
using CuteVideoEditor.Services;
using System.Text.Json;
using DynamicData;
using AutoMapper;
using Windows.System;
using FFmpegInteropX;

namespace CuteVideoEditor.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    private readonly DialogService dialogService;
    private readonly IMapper mapper;

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

    public event Action<TimeSpan>? UpdateMediaPosition;

    public Thickness VideoOverlayMargins { get; private set; }
    public double VideoOverlayScale { get; private set; }

    public ObservableCollection<CropFrameEntryModel> CropFrames = [];

    public CropRectModel CurrentCropRect
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

    [RelayCommand]
    async Task SaveProjectAsync()
    {
        if (await dialogService.SelectSaveProjectFileAsync() is { } projectFileName)
        {
            using var outputFile = File.Create(projectFileName);
            await JsonSerializer.SerializeAsync(outputFile, new SerializationModel
            {
                MediaFileName = MediaFileName!,
                CropFrames = mapper.Map<List<CropFrameEntrySerializationModel>>(CropFrames)
            });
        }
    }

    [RelayCommand]
    async Task ExportVideoAsync()
    {
        using FFmpegTranscode transcode = new();
        transcode.Run(new(MediaFileName, 0),
            new("output.mp4"));
    }

    public MainViewModel(DialogService dialogService, IMapper mapper)
    {
        this.dialogService = dialogService;
        this.mapper = mapper;
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

    public void LoadProjectFile(string projectFileName)
    {
        CropFrames.Clear();
        using (var inputFile = File.OpenRead(projectFileName))
            try
            {
                if (JsonSerializer.Deserialize<SerializationModel>(inputFile) is { } model)
                {
                    MediaFileName = model.MediaFileName;
                    CropFrames.AddRange(mapper.Map<List<CropFrameEntryModel>>(model.CropFrames));
                    return;
                }
            }
            catch (JsonException) { }

        // if we couldn't parse it as a project file, load it as a video file
        MediaFileName = projectFileName;
    }

    void TogglePlayPause()
    {
        if (MediaPlaybackState is MediaPlaybackState.Playing)
            MediaPlaybackState = MediaPlaybackState.Paused;
        else
            MediaPlaybackState = MediaPlaybackState.Playing;
    }

    internal bool ProcessKey(VirtualKey key, bool up)
    {
        switch ((key, up))
        {
            case (VirtualKey.Space, true):
                TogglePlayPause();
                return true;
            case (VirtualKey.Left, false):
                UpdateMediaPosition?.Invoke(MediaPosition - TimeSpan.FromSeconds(1 / MediaFrameRate));
                return true;
            case (VirtualKey.Right, false):
                UpdateMediaPosition?.Invoke(MediaPosition + TimeSpan.FromSeconds(1 / MediaFrameRate));
                return true;
        }

        return false;
    }
}
