using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CuteVideoEditor.Core.Models;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using SuperLinq;
using System.Text.Json;
using DynamicData;
using AutoMapper;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.Core.Helpers;
using System.Diagnostics;
using System.Reflection;
using CuteVideoEditor.Core.Contracts.Services;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using CuteVideoEditor.Core.Contracts.ViewModels;

namespace CuteVideoEditor.ViewModels;

public partial class VideoEditorViewModel : ObservableRecipient, IDisposable
{
    private bool disposedValue;
    private readonly IDialogService dialogService;
    private readonly IMapper mapper;
    private readonly IVideoTranscoderService videoTranscoderService;

    [ObservableProperty]
    string? projectFileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoOverlayMargins))]
    SizeModel mediaPixelSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoOverlayMargins))]
    SizeModel videoPlayerPixelSize;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCropRect))]
    bool freezeCropSizeMode;

    public IVideoPlayerViewModel VideoPlayerViewModel { get; }

    public event Action<TimeSpan>? UpdateMediaPosition;

    public ThicknessModel VideoOverlayMargins { get; private set; }
    public double VideoOverlayScale { get; private set; }

    public ObservableCollection<CropFrameEntryModel> CropFrames { get; } = [];

    partial void OnFreezeCropSizeModeChanged(bool value)
    {
        // if entering freeze crop size mode, delete all crop frames except the first one
        if (value)
            while (CropFrames.Count > 1)
                CropFrames.RemoveAt(1);
    }

    public CropRectModel CurrentCropRect
    {
        get => GetCropRectAt(VideoPlayerViewModel.OutputFrameNumber);
        set => MaterializeCropKeyFrame(VideoPlayerViewModel.OutputFrameNumber, value.Rect);
    }

    public CropRectModel GetCropRectAt(long outputFrameNumber)
    {
        if (CropFrames.Count == 0 || VideoPlayerViewModel.MediaFrameRate == 0 || VideoPlayerViewModel.InputMediaDuration == default)
            return new(default, CropRectType.None);
        else if (!FreezeCropSizeMode)
            return new(CropFrames[0].CropRectangle, CropRectType.FirstFrameUnfrozen);
        else
        {
            if (CropFrames.FirstOrDefault(x => x.FrameNumber == outputFrameNumber) is { CropRectangle: { Width: > 0, Height: > 0 } } cropFrame)
                return new(cropFrame.CropRectangle, CropRectType.KeyFrame);
            else
            {
                // extrapolate
                if (outputFrameNumber > CropFrames[^1].FrameNumber)
                    return new(CropFrames[^1].CropRectangle, CropRectType.Interpolated);

                // interpolate
                if (CropFrames.TakeWhile(w => w.FrameNumber < outputFrameNumber).Count() is { } idx
                    && idx >= 0 && idx < CropFrames.Count)
                {
                    return new(RectModel.Interpolate(CropFrames[idx - 1].CropRectangle, CropFrames[idx].CropRectangle,
                        (outputFrameNumber - CropFrames[idx - 1].FrameNumber) / (double)(CropFrames[idx].FrameNumber - CropFrames[idx - 1].FrameNumber)), CropRectType.Interpolated);
                }
            }
        }

        throw new InvalidOperationException();
    }

    public void MaterializeCropKeyFrame(long outputFrameNumber, RectModel? rect = null)
    {
        if (VideoPlayerViewModel.MediaFrameRate == 0 || VideoPlayerViewModel.InputMediaDuration == default)
            return;

        if (!FreezeCropSizeMode && rect.HasValue)
        {
            CropFrames[0] = new(0, rect.Value);
            OnPropertyChanged(nameof(CurrentCropRect));
            return;
        }

        var existingCropFrameIndex = CropFrames.FindIndex(x => x.FrameNumber == outputFrameNumber);
        if (existingCropFrameIndex < 0)
        {
            // materialize the crop frame
            // Find the index to insert the new crop frame
            int insertIndex = CropFrames.FindLastIndex(x => x.FrameNumber <= outputFrameNumber);
            rect ??= GetCropRectAt(outputFrameNumber).Rect;
            if (insertIndex == -1)
                CropFrames.Insert(0, new(outputFrameNumber, rect.Value));
            else
                CropFrames.Insert(insertIndex + 1, new(outputFrameNumber, rect.Value));
            OnPropertyChanged(nameof(CurrentCropRect));
        }
        else
        {
            rect ??= GetCropRectAt(outputFrameNumber).Rect;
            if (CropFrames[existingCropFrameIndex].CropRectangle != rect)
            {
                CropFrames[existingCropFrameIndex] = new(outputFrameNumber, rect.Value);
                OnPropertyChanged(nameof(CurrentCropRect));
            }
        }
    }

    void EnsureCropKeyFramesExistForTrimmedSegmentBorders()
    {
        var (lastTrimStart, lastTrimAfter) = (-1L, false);
        var totalOutputFrameCount = VideoPlayerViewModel.GetFrameNumberFromPosition(VideoPlayerViewModel.OutputMediaDuration);
        foreach (var trim in VideoPlayerViewModel.TrimmingMarkers)
        {
            var (trimFrameStart, trimFrameEnd) = (trim.FrameNumber,
                VideoPlayerViewModel.TrimmingMarkers.LastOrDefault(w => w.FrameNumber < trim.FrameNumber) is { } prevTrim
                    ? prevTrim.FrameNumber : VideoPlayerViewModel.InputMediaDuration.TotalSeconds * VideoPlayerViewModel.MediaFrameRate);

            if (lastTrimStart == -1 || trim.TrimAfter != lastTrimAfter)
            {
                // trim border, ensure crop key frames exist on the left side of a switch between non-trim to trim
                if (!lastTrimAfter)
                {
                    if (trimFrameStart > 0)
                        MaterializeCropKeyFrame(trimFrameStart - 1);
                    MaterializeCropKeyFrame(trimFrameStart);
                }

                lastTrimStart = trimFrameStart;
                lastTrimAfter = trim.TrimAfter;
            }
        }

        // remove all crop key frames outside of the output duration
        CropFrames.RemoveAll(w => w.FrameNumber < 0 || w.FrameNumber > totalOutputFrameCount);
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

    public ObservableCollection<DisjunctTrimmingMarkerEntry> DisjunctOutputTrims { get; } = [];
    public ObservableCollection<TimeSpan> NonDisjunctOutputMarkers { get; } = [];

    public SizeModel LargestOutputPixelSize =>
        new(CropFrames.Max(w => w.CropRectangle.Width), CropFrames.Max(w => w.CropRectangle.Height));

    [RelayCommand(CanExecute = nameof(CanPause))]
    void Pause() => VideoPlayerViewModel.MediaPlayerState = MediaPlayerState.Paused;
    bool CanPause() => VideoPlayerViewModel.MediaPlayerState == MediaPlayerState.Playing;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    void Play() => VideoPlayerViewModel.MediaPlayerState = MediaPlayerState.Playing;
    bool CanPlay() => VideoPlayerViewModel.MediaPlayerState is not MediaPlayerState.Playing;

    [RelayCommand]
    async Task SaveProjectAsync()
    {
        if ((ProjectFileName ??= await dialogService.SelectSaveProjectFileAsync(ProjectFileName)) is not null)
        {
            using var outputFile = File.Create(ProjectFileName!);
            await JsonSerializer.SerializeAsync(outputFile, new SerializationModel
            {
                MediaFileName = VideoPlayerViewModel.MediaFileName!,
                FreezeCropSizeMode = FreezeCropSizeMode,
                CropFrames = mapper.Map<List<CropFrameEntrySerializationModel>>(CropFrames),
                TrimmingMarkers = mapper.Map<List<TrimmingMarkerSerializationModel>>(VideoPlayerViewModel.TrimmingMarkers)
            });
        }
    }

    [RelayCommand]
    void AddMarker()
    {
        var frameNumber = VideoPlayerViewModel.InputFrameNumber;
        var insertIndex = VideoPlayerViewModel.TrimmingMarkers.FindLastIndex(x => x.FrameNumber <= frameNumber);
        if (VideoPlayerViewModel.TrimmingMarkers[insertIndex].FrameNumber == frameNumber) return;
        VideoPlayerViewModel.TrimmingMarkers.Insert(insertIndex + 1, new(frameNumber));
    }

    [RelayCommand]
    void AddTrim()
    {
        var inputFrameNumber = VideoPlayerViewModel.InputFrameNumber;
        if (VideoPlayerViewModel.TrimmingMarkers.LastOrDefault(w => w.FrameNumber < inputFrameNumber) is { } marker && !marker.TrimAfter)
        {
            marker.TrimAfter = true;

            var (frameStart, frameEnd) = (marker.FrameNumber,
                (long)(VideoPlayerViewModel.TrimmingMarkers.LastOrDefault(w => w.FrameNumber < marker.FrameNumber) is { } prevTrim ? prevTrim.FrameNumber : VideoPlayerViewModel.InputMediaDuration.TotalSeconds * VideoPlayerViewModel.MediaFrameRate));
            var frameDuration = frameEnd - frameStart;

            // move every crop frame back to fill in the space (they're in input space)
            for (int i = 0; i < CropFrames.Count; ++i)
                if (CropFrames[i].FrameNumber > frameStart)
                {
                    var (fn, rect) = (CropFrames[i].FrameNumber, CropFrames[i].CropRectangle);
                    CropFrames.RemoveAt(i);
                    CropFrames.Insert(i, new(fn - frameDuration, rect));
                }

            EnsureCropKeyFramesExistForTrimmedSegmentBorders();
        }
    }

    [RelayCommand]
    async Task ExportVideoAsync()
    {
        var mainSyncronizationContext = SynchronizationContext.Current!;

        if (await dialogService.SelectTranscodeOutputParameters(this) is { } outputParameters)
        {
            TimeSpan duration = default;

            var encodingResult = await dialogService.ShowOperationProgressDialog("Please wait, encoding...", true, async vm =>
            {
                var totalFrames = VideoPlayerViewModel.GetFrameNumberFromPosition(VideoPlayerViewModel.OutputMediaDuration);
                var processedFrames = 0;

                await Task.Run(() =>
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        videoTranscoderService.Transcode(
                            new()
                            {
                                FileName = VideoPlayerViewModel.MediaFileName!,
                                CropFrames = CropFrames,
                                TrimmingMarkers = VideoPlayerViewModel.TrimmingMarkers,
                                EncoderTitle = $"CuteVideoEditor {Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}",
                            }, outputParameters, () =>
                            {
                                if (Interlocked.Increment(ref processedFrames) is { } processedFramesValue && processedFramesValue % 15 == 0)
                                    mainSyncronizationContext.Post(_ => vm.Progress = (double)processedFramesValue / totalFrames, null);
                            });
                        duration = sw.Elapsed;

                        vm.Result = true;
                    }
                    catch { vm.Result = false; }
                });
            });

            await dialogService.ShowInformationMessageDialog("Encoding finished",
                encodingResult ? $"Encoding finished in {duration}." : "Encoding failed.");
        }
    }

    public VideoEditorViewModel(IDialogService dialogService, IMapper mapper,
        IVideoPlayerViewModel videoPlayerViewModel, IVideoTranscoderService videoTranscoderService)
    {
        this.dialogService = dialogService;
        this.mapper = mapper;
        VideoPlayerViewModel = videoPlayerViewModel;
        this.videoTranscoderService = videoTranscoderService;

        this.WhenAnyValue(x => x.VideoPlayerViewModel.InputMediaDuration, x => x.VideoPlayerViewModel.MediaFrameRate,
                x => x.VideoPlayerViewModel.InputMediaPosition, x => x.VideoPlayerViewModel.OutputMediaPosition)
            .Subscribe(_ => OnPropertyChanged(nameof(CurrentCropRect)));

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
                    VideoOverlayMargins = new((VideoPlayerPixelSize.Width - overlayWidth) / 2, 0, (VideoPlayerPixelSize.Width - overlayWidth) / 2, 0);
                }
                else
                {
                    var overlayHeight = VideoPlayerPixelSize.Width / mediaAspectRatio;
                    VideoOverlayMargins = new(0, (VideoPlayerPixelSize.Height - overlayHeight) / 2, 0, (VideoPlayerPixelSize.Height - overlayHeight) / 2);
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
        VideoPlayerViewModel.TrimmingMarkers.ActOnEveryObject((s, e) => RebuildTrimmingMarkers());
        this.WhenAnyValue(x => x.VideoPlayerViewModel.InputMediaDuration, x => x.VideoPlayerViewModel.MediaFrameRate)
            .Subscribe(_ => RebuildTrimmingMarkers());
    }

    public void LoadProjectFile(string projectFileName)
    {
        CropFrames.Clear();
        VideoPlayerViewModel.TrimmingMarkers.Clear();

        using (var inputFile = File.OpenRead(projectFileName))
            try
            {
                if (JsonSerializer.Deserialize<SerializationModel>(inputFile) is { } model)
                {
                    FreezeCropSizeMode = model.FreezeCropSizeMode;
                    ProjectFileName = projectFileName;
                    VideoPlayerViewModel.MediaFileName = Path.GetDirectoryName(projectFileName) is { } projectDirectoryName ? Path.Combine(projectDirectoryName, model.MediaFileName) : model.MediaFileName;
                    CropFrames.AddRange(mapper.Map<List<CropFrameEntryModel>>(model.CropFrames));
                    VideoPlayerViewModel.TrimmingMarkers.AddRange(mapper.Map<List<TrimmingMarkerModel>>(model.TrimmingMarkers));
                    return;
                }
            }
            catch (JsonException) { }

        // if we couldn't parse it as a project file, load it as a video file
        VideoPlayerViewModel.TrimmingMarkers.Add(new(0));
        ProjectFileName = null;
        VideoPlayerViewModel.MediaFileName = projectFileName;
    }

    void TogglePlayPause()
    {
        if (VideoPlayerViewModel.MediaPlayerState is MediaPlayerState.Playing)
            VideoPlayerViewModel.MediaPlayerState = MediaPlayerState.Paused;
        else
            VideoPlayerViewModel.MediaPlayerState = MediaPlayerState.Playing;
    }

    void CropFrameStep(bool forward)
    {
        var outputFrameNumber = VideoPlayerViewModel.OutputFrameNumber;
        var nextCropMarkerFrameNumber = forward
            ? CropFrames.FirstOrDefault(w => w.FrameNumber > outputFrameNumber).FrameNumber
            : CropFrames.LastOrDefault(w => w.FrameNumber < outputFrameNumber).FrameNumber;
        if (forward && nextCropMarkerFrameNumber == 0)
            nextCropMarkerFrameNumber = VideoPlayerViewModel.GetFrameNumberFromPosition(VideoPlayerViewModel.OutputMediaDuration);
        VideoPlayerViewModel.OutputFrameNumber = nextCropMarkerFrameNumber;
    }

    public bool ProcessKey(ushort key, bool ctrl, bool up)
    {
        switch (((VIRTUAL_KEY)key, ctrl, up))
        {
            case (VIRTUAL_KEY.VK_SPACE, false, true):
                TogglePlayPause();
                return true;
            case (VIRTUAL_KEY.VK_LEFT, false, false):
                VideoPlayerViewModel.FrameStep(false);
                return true;
            case (VIRTUAL_KEY.VK_RIGHT, false, false):
                VideoPlayerViewModel.FrameStep(true);
                return true;
            case (VIRTUAL_KEY.VK_M, false, true):
                AddMarker();
                return true;
            case (VIRTUAL_KEY.VK_HOME, false, true):
                VideoPlayerViewModel.OutputFrameNumber = 0;
                return true;
            case (VIRTUAL_KEY.VK_END, false, true):
                VideoPlayerViewModel.OutputMediaPosition = VideoPlayerViewModel.OutputMediaDuration;
                return true;
            case (VIRTUAL_KEY.VK_LEFT, true, false):
                CropFrameStep(false);
                return true;
            case (VIRTUAL_KEY.VK_RIGHT, true, false):
                CropFrameStep(true);
                return true;
        }

        return false;
    }


    void RebuildTrimmingMarkers()
    {
        TimeSpan lastStart = TimeSpan.MinValue, lastEnd = TimeSpan.MinValue,
            lastMarkerPosition = TimeSpan.MinValue;

        DisjunctOutputTrims.Clear();
        NonDisjunctOutputMarkers.Clear();

        foreach (var marker in VideoPlayerViewModel.TrimmingMarkers)
        {
            var markerPosition = VideoPlayerViewModel.GetPositionFromFrameNumber(VideoPlayerViewModel.GetOutputFrameNumberFromInputFrameNumber(marker.FrameNumber));
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
        if (lastStart != TimeSpan.MinValue && !VideoPlayerViewModel.TrimmingMarkers[^1].TrimAfter)
            DisjunctOutputTrims.Add(new(lastStart, VideoPlayerViewModel.OutputMediaDuration));
        else if (lastStart == TimeSpan.MinValue && lastEnd == TimeSpan.MinValue && DisjunctOutputTrims.Count == 0)
            DisjunctOutputTrims.Add(new(TimeSpan.Zero, TimeSpan.Zero));
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
            VideoPlayerViewModel.Dispose();

            disposedValue = true;
        }
    }

    ~VideoEditorViewModel()
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

public readonly struct DisjunctTrimmingMarkerEntry
{
    public readonly TimeSpan From, To;

    public DisjunctTrimmingMarkerEntry(TimeSpan from, TimeSpan to) =>
        (From, To) = (from, to);
}