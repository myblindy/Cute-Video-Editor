using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuteVideoEditor.Core.Models;
using Microsoft.UI.Xaml;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Windows.Media.Playback;
using SuperLinq;
using System.Text.Json;
using DynamicData;
using AutoMapper;
using Windows.System;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.Core.Helpers;

namespace CuteVideoEditor.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    private readonly IDialogService dialogService;
    private readonly IMapper mapper;

    [ObservableProperty]
    string? mediaFileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCropRect), nameof(OutputMediaDuration), nameof(OutputMediaPosition),
        nameof(CurrentInputFrameNumber), nameof(CurrentOutputFrameNumber))]
    TimeSpan mediaDuration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCropRect), nameof(OutputMediaDuration), nameof(OutputMediaPosition),
        nameof(CurrentInputFrameNumber), nameof(CurrentOutputFrameNumber))]
    double mediaFrameRate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCropRect), nameof(CurrentInputFrameNumber), nameof(OutputMediaPosition),
        nameof(CurrentOutputFrameNumber))]
    TimeSpan inputMediaPosition;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoOverlayMargins))]
    SizeModel mediaPixelSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoOverlayMargins))]
    SizeModel videoPlayerPixelSize;

    public event Action<TimeSpan>? UpdateMediaPosition;

    public Thickness VideoOverlayMargins { get; private set; }
    public double VideoOverlayScale { get; private set; }

    public ObservableCollection<CropFrameEntryModel> CropFrames { get; } = [];
    public ObservableCollection<TrimmingMarkerModel> TrimmingMarkers { get; } = [new(0)];

    long CurrentInputFrameNumber => (long)(InputMediaPosition.TotalSeconds * MediaFrameRate);
    long CurrentOutputFrameNumber => GetOutputFrameNumberFromInputFrameNumber(CurrentInputFrameNumber);

    public TimeSpan OutputMediaDuration
    {
        get
        {
            if (MediaFrameRate is 0) return default;

            // calculate the frame count ignoring all the active trimming markers
            var totalFrameCount = (long)(MediaDuration.TotalSeconds * MediaFrameRate);
            var frameCount = totalFrameCount;
            for (var i = 0; i < TrimmingMarkers.Count; ++i)
                if (TrimmingMarkers[i].TrimAfter)
                {
                    var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                        i == TrimmingMarkers.Count - 1 ? totalFrameCount : TrimmingMarkers[i + 1].FrameNumber);
                    frameCount -= frameEnd - frameStart;
                }
            return TimeSpan.FromSeconds(frameCount / MediaFrameRate);
        }
    }

    public TimeSpan OutputMediaPosition
    {
        get => MediaFrameRate is 0 ? default : TimeSpan.FromSeconds(CurrentOutputFrameNumber / MediaFrameRate);
        set
        {
            // convert the output position to input position
            var outputFrameNumber = (long)(value.Clamp(TimeSpan.Zero, OutputMediaDuration).TotalSeconds * MediaFrameRate);
            var inputFrameNumber = 0L;
            for (var i = 0; i < TrimmingMarkers.Count; ++i)
            {
                var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                    (long)(i == TrimmingMarkers.Count - 1 ? MediaDuration.TotalSeconds * MediaFrameRate : TrimmingMarkers[i + 1].FrameNumber));

                var framesNeeded = Math.Min(frameEnd - frameStart, outputFrameNumber);
                inputFrameNumber += framesNeeded;
                if (!TrimmingMarkers[i].TrimAfter)
                    outputFrameNumber -= framesNeeded;

                if (outputFrameNumber == 0)
                    break;
            }

            // if we have frames left over, we don't have enough output media duration
            if (outputFrameNumber > 0)
                throw new InvalidOperationException();

            UpdateMediaPosition?.Invoke(TimeSpan.FromSeconds(inputFrameNumber / MediaFrameRate));
        }
    }

    public CropRectModel CurrentCropRect
    {
        get
        {
            if (CropFrames.Count == 0 || MediaFrameRate == 0 || MediaDuration == default)
                return new(default, CropRectType.None);
            else
            {
                var frameNumber = CurrentOutputFrameNumber;
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

            var frameNumber = CurrentOutputFrameNumber;
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

    public ObservableCollection<DisjunctTrimmingMarkerEntry> DisjunctOutputTrims { get; } = [];
    public ObservableCollection<TimeSpan> NonDisjunctOutputMarkers { get; } = [];

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
    void AddMarker()
    {
        var frameNumber = CurrentInputFrameNumber;
        var insertIndex = TrimmingMarkers.FindLastIndex(x => x.FrameNumber <= frameNumber);
        if (TrimmingMarkers[insertIndex].FrameNumber == frameNumber) return;
        TrimmingMarkers.Insert(insertIndex + 1, new(frameNumber));
        OnPropertyChanged(nameof(OutputMediaDuration));
        OnPropertyChanged(nameof(OutputMediaPosition));
    }

    [RelayCommand]
    void PositionPercentageUpdateRequest(double percentage) =>
        UpdateMediaPosition?.Invoke(GetPositionFromFrameNumber(
            GetInputFrameNumberFromOutputFrameNumber(GetFrameNumberFromPosition(TimeSpan.FromSeconds(percentage * OutputMediaDuration.TotalSeconds)))));

    [RelayCommand]
    void AddTrim()
    {
        var outputFrameNumber = CurrentOutputFrameNumber;
        if (TrimmingMarkers.LastOrDefault(w => w.FrameNumber < outputFrameNumber) is { } marker)
        {
            marker.TrimAfter = true;
            OnPropertyChanged(nameof(OutputMediaDuration));
            OnPropertyChanged(nameof(OutputMediaPosition));
        }
    }

    [RelayCommand]
    async Task ExportVideoAsync()
    {
    }

    public MainViewModel(IDialogService dialogService, IMapper mapper)
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

        // trimming marker updates
        TrimmingMarkers.ActOnEveryObject((s, e) => RebuildTrimmingMarkers());
        this.WhenAnyValue(x => x.MediaDuration, x => x.MediaFrameRate).Subscribe(_ => RebuildTrimmingMarkers());
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
                UpdateMediaPosition?.Invoke(InputMediaPosition - TimeSpan.FromSeconds(1 / MediaFrameRate));
                return true;
            case (VirtualKey.Right, false):
                UpdateMediaPosition?.Invoke(InputMediaPosition + TimeSpan.FromSeconds(1 / MediaFrameRate));
                return true;
            case (VirtualKey.M, true):
                AddMarker();
                return true;
        }

        return false;
    }

    public long GetOutputFrameNumberFromInputFrameNumber(long inputFrameNumber)
    {
        var frameNumber = 0L;
        for (var i = 0; i < TrimmingMarkers.Count; ++i)
            if (TrimmingMarkers[i].FrameNumber <= inputFrameNumber)
            {
                var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                    i == TrimmingMarkers.Count - 1 ? MediaDuration.TotalSeconds * MediaFrameRate : TrimmingMarkers[i + 1].FrameNumber);
                frameNumber += !TrimmingMarkers[i].TrimAfter
                    ? (long)Math.Min(inputFrameNumber - frameStart, frameEnd - frameStart)
                    : 0;
            }
            else
                break;

        return frameNumber;
    }

    public TimeSpan GetPositionFromFrameNumber(long outputFrameNumber) =>
        MediaFrameRate is 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(outputFrameNumber / MediaFrameRate);

    public long GetFrameNumberFromPosition(TimeSpan position) =>
        (long)(position.TotalSeconds * MediaFrameRate);

    public long GetInputFrameNumberFromOutputFrameNumber(long outputFrameNumber)
    {
        var frameNumber = 0L;
        for (var i = 0; i < TrimmingMarkers.Count; ++i)
        {
            var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                (long)(i == TrimmingMarkers.Count - 1 ? MediaDuration.TotalSeconds * MediaFrameRate : TrimmingMarkers[i + 1].FrameNumber));

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

    void RebuildTrimmingMarkers()
    {
        TimeSpan lastStart = TimeSpan.MinValue, lastEnd = TimeSpan.MinValue,
            lastMarkerPosition = TimeSpan.MinValue;

        DisjunctOutputTrims.Clear();
        NonDisjunctOutputMarkers.Clear();

        foreach (var marker in TrimmingMarkers)
        {
            var markerPosition = GetPositionFromFrameNumber(GetOutputFrameNumberFromInputFrameNumber(marker.FrameNumber));
            if (!marker.TrimAfter)
            {
                if (lastStart == TimeSpan.MinValue)
                    lastStart = markerPosition;
                else if (lastEnd == lastMarkerPosition || lastEnd == TimeSpan.MinValue)
                {
                    NonDisjunctOutputMarkers.Add(markerPosition);
                    lastEnd = markerPosition;
                }
                else
                {
                    DisjunctOutputTrims.Add(new(lastStart, lastEnd));
                    lastStart = markerPosition;
                    lastEnd = TimeSpan.MinValue;
                }
            }
            else if (lastStart != TimeSpan.MinValue)
            {
                DisjunctOutputTrims.Add(new(lastStart, markerPosition));
                lastStart = TimeSpan.MinValue;
            }

            lastMarkerPosition = markerPosition;
        }

        // last trim
        if (lastStart != TimeSpan.MinValue && !TrimmingMarkers[^1].TrimAfter)
            DisjunctOutputTrims.Add(new(lastStart, OutputMediaDuration));
        else if (lastStart == TimeSpan.MinValue && lastEnd == TimeSpan.MinValue && DisjunctOutputTrims.Count == 0)
            DisjunctOutputTrims.Add(new(TimeSpan.Zero, TimeSpan.Zero));
    }
}

public readonly struct DisjunctTrimmingMarkerEntry
{
    public readonly TimeSpan From, To;

    public DisjunctTrimmingMarkerEntry(TimeSpan from, TimeSpan to) =>
        (From, To) = (from, to);
}