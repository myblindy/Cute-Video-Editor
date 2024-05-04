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
using FFmpegInteropX;
using System.Diagnostics;
using System.Reflection;

namespace CuteVideoEditor.ViewModels;

public partial class VideoEditorViewModel : ObservableRecipient
{
    private readonly IDialogService dialogService;
    private readonly IMapper mapper;

    [ObservableProperty]
    string? projectFileName;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentCropRect))]
    bool freezeCropSizeMode;

    public event Action<TimeSpan>? UpdateMediaPosition;

    public Thickness VideoOverlayMargins { get; private set; }
    public double VideoOverlayScale { get; private set; }

    public ObservableCollection<CropFrameEntryModel> CropFrames { get; } = [];
    public ObservableCollection<TrimmingMarkerModel> TrimmingMarkers { get; } = [new(0)];

    partial void OnFreezeCropSizeModeChanged(bool value)
    {
        // if entering freeze crop size mode, delete all crop frames except the first one
        if (value)
            while (CropFrames.Count > 1)
                CropFrames.RemoveAt(1);
    }

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
        get => GetCropRectAt(CurrentOutputFrameNumber);
        set => MaterializeCropKeyFrame(CurrentOutputFrameNumber, value.Rect);
    }

    public CropRectModel GetCropRectAt(long outputFrameNumber)
    {
        if (CropFrames.Count == 0 || MediaFrameRate == 0 || MediaDuration == default)
            return new(default, CropRectType.None);
        else if (!FreezeCropSizeMode)
            return new(CropFrames[0].Rect, CropRectType.FirstFrameUnfrozen);
        else
        {
            if (CropFrames.FirstOrDefault(x => x.FrameNumber == outputFrameNumber) is { Rect: { Width: > 0, Height: > 0 } } cropFrame)
                return new(cropFrame.Rect, CropRectType.KeyFrame);
            else
            {
                // extrapolate
                if (outputFrameNumber > CropFrames[^1].FrameNumber)
                    return new(CropFrames[^1].Rect, CropRectType.Interpolated);

                // interpolate
                if (CropFrames.TakeWhile(w => w.FrameNumber < outputFrameNumber).Count() is { } idx
                    && idx >= 0 && idx < CropFrames.Count)
                {
                    return new(RectModel.Interpolate(CropFrames[idx - 1].Rect, CropFrames[idx].Rect,
                        (outputFrameNumber - CropFrames[idx - 1].FrameNumber) / (double)(CropFrames[idx].FrameNumber - CropFrames[idx - 1].FrameNumber)), CropRectType.Interpolated);
                }
            }
        }

        throw new InvalidOperationException();
    }

    public void MaterializeCropKeyFrame(long outputFrameNumber, RectModel? rect = null)
    {
        if (MediaFrameRate == 0 || MediaDuration == default)
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
            if (CropFrames[existingCropFrameIndex].Rect != rect)
            {
                CropFrames[existingCropFrameIndex] = new(outputFrameNumber, rect.Value);
                OnPropertyChanged(nameof(CurrentCropRect));
            }
        }
    }

    void EnsureCropKeyFramesExistForTrimmedSegmentBorders()
    {
        var (lastTrimStart, lastTrimAfter) = (-1L, false);
        var totalOutputFrameCount = GetFrameNumberFromPosition(OutputMediaDuration);
        foreach (var trim in TrimmingMarkers)
        {
            var (trimFrameStart, trimFrameEnd) = (trim.FrameNumber,
                TrimmingMarkers.LastOrDefault(w => w.FrameNumber < trim.FrameNumber) is { } prevTrim ? prevTrim.FrameNumber : MediaDuration.TotalSeconds * MediaFrameRate);

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand), nameof(PauseCommand))]
    MediaPlaybackState mediaPlaybackState;

    public ObservableCollection<DisjunctTrimmingMarkerEntry> DisjunctOutputTrims { get; } = [];
    public ObservableCollection<TimeSpan> NonDisjunctOutputMarkers { get; } = [];

    public SizeModel LargestOutputPixelSize =>
        new(CropFrames.Max(w => w.Rect.Width), CropFrames.Max(w => w.Rect.Height));

    [RelayCommand(CanExecute = nameof(CanPause))]
    void Pause() => MediaPlaybackState = MediaPlaybackState.Paused;
    bool CanPause() => MediaPlaybackState == MediaPlaybackState.Playing;

    [RelayCommand(CanExecute = nameof(CanPlay))]
    void Play() => MediaPlaybackState = MediaPlaybackState.Playing;
    bool CanPlay() => MediaPlaybackState is not MediaPlaybackState.Playing;

    partial void OnInputMediaPositionChanged(TimeSpan value)
    {
        // handle trim skips while playing
        if (MediaPlaybackState == MediaPlaybackState.Playing)
        {
            // is the new position within a trim?
            var newFrameNumber = GetFrameNumberFromPosition(value);
            for (int i = 0; i < TrimmingMarkers.Count; ++i)
            {
                var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                    i == TrimmingMarkers.Count - 1 ? MediaDuration.TotalSeconds * MediaFrameRate : TrimmingMarkers[i + 1].FrameNumber);
                if (frameStart <= newFrameNumber && newFrameNumber < frameEnd)
                {
                    if (TrimmingMarkers[i].TrimAfter)
                    {
                        // skip to the next non-trimming marker, if any, otherwise stop
                        if (GetNextNonTrimmedInputFrameNumber(newFrameNumber, true, out var nextNonTrimmedInputFrameNumber))
                            UpdateMediaPosition?.Invoke(GetPositionFromFrameNumber(nextNonTrimmedInputFrameNumber));
                        else
                            Pause();
                    }
                    else
                        break;
                }
                else if (frameStart > newFrameNumber)
                    return;
            }
        }
    }

    bool GetNextNonTrimmedInputFrameNumber(long inputFrameNumber, bool forward, out long nextNonTrimmedInputFrameNumber)
    {
        nextNonTrimmedInputFrameNumber = 0;

        for (int i = 0; i < TrimmingMarkers.Count; ++i)
        {
            var (frameStart, frameEnd) = (TrimmingMarkers[i].FrameNumber,
                i == TrimmingMarkers.Count - 1 ? MediaDuration.TotalSeconds * MediaFrameRate : TrimmingMarkers[i + 1].FrameNumber);
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

    [RelayCommand]
    async Task SaveProjectAsync()
    {
        if ((ProjectFileName ??= await dialogService.SelectSaveProjectFileAsync(ProjectFileName)) is not null)
        {
            using var outputFile = File.Create(ProjectFileName!);
            await JsonSerializer.SerializeAsync(outputFile, new SerializationModel
            {
                MediaFileName = MediaFileName!,
                FreezeCropSizeMode = FreezeCropSizeMode,
                CropFrames = mapper.Map<List<CropFrameEntrySerializationModel>>(CropFrames),
                TrimmingMarkers = mapper.Map<List<TrimmingMarkerSerializationModel>>(TrimmingMarkers)
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
        var inputFrameNumber = CurrentInputFrameNumber;
        if (TrimmingMarkers.LastOrDefault(w => w.FrameNumber < inputFrameNumber) is { } marker && !marker.TrimAfter)
        {
            marker.TrimAfter = true;

            var (frameStart, frameEnd) = (marker.FrameNumber,
                (long)(TrimmingMarkers.LastOrDefault(w => w.FrameNumber < marker.FrameNumber) is { } prevTrim ? prevTrim.FrameNumber : MediaDuration.TotalSeconds * MediaFrameRate));
            var frameDuration = frameEnd - frameStart;

            // move every crop frame back to fill in the space (they're in input space)
            for (int i = 0; i < CropFrames.Count; ++i)
                if (CropFrames[i].FrameNumber > frameStart)
                {
                    var (fn, rect) = (CropFrames[i].FrameNumber, CropFrames[i].Rect);
                    CropFrames.RemoveAt(i);
                    CropFrames.Insert(i, new(fn - frameDuration, rect));
                }

            EnsureCropKeyFramesExistForTrimmedSegmentBorders();
            OnPropertyChanged(nameof(OutputMediaDuration));
            OnPropertyChanged(nameof(OutputMediaPosition));
        }
    }

    [RelayCommand]
    async Task ExportVideoAsync()
    {
        if (await dialogService.SelectTranscodeOutputParameters(this) is { } outputParameters)
        {
            TimeSpan duration = default;

            var encodingResult = await dialogService.ShowOperationProgressDialog("Please wait, encoding...", true, async vm =>
            {
                var totalFrames = GetFrameNumberFromPosition(OutputMediaDuration);
                var processedFrames = 0;

                using var transcoder = new FFmpegTranscode();
                transcoder.FrameOutputProgress += (s, e) =>
                {
                    if (Interlocked.Increment(ref processedFrames) is { } processedFramesValue && processedFramesValue % 30 == 0)
                        App.MainDispatcherQueue.TryEnqueue(() => vm.Progress = (double)processedFramesValue / totalFrames);
                };

                await Task.Run(() =>
                {
                    try
                    {
                        var sw = Stopwatch.StartNew();
                        transcoder.Run(new()
                        {
                            EncoderTitle = $"Cute Video Editor {Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}",
                            FileName = MediaFileName!,
                            CropFrames = CropFrames.Select(w => new FFmpegTranscodeInputCropFrameEntry(
                                w.FrameNumber, new(w.Rect.CenterX, w.Rect.CenterY, w.Rect.Width, w.Rect.Height))).ToList(),
                            TrimmingMarkers = TrimmingMarkers.Select(w => new FFmpegTranscodeInputTrimmingMarkerEntry(
                                w.FrameNumber, w.TrimAfter)).ToList()
                        }, outputParameters);
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

    public VideoEditorViewModel(IDialogService dialogService, IMapper mapper)
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
        TrimmingMarkers.Clear();

        using (var inputFile = File.OpenRead(projectFileName))
            try
            {
                if (JsonSerializer.Deserialize<SerializationModel>(inputFile) is { } model)
                {
                    FreezeCropSizeMode = model.FreezeCropSizeMode;
                    ProjectFileName = projectFileName;
                    MediaFileName = Path.GetDirectoryName(projectFileName) is { } projectDirectoryName ? Path.Combine(projectDirectoryName, model.MediaFileName) : model.MediaFileName;
                    CropFrames.AddRange(mapper.Map<List<CropFrameEntryModel>>(model.CropFrames));
                    TrimmingMarkers.AddRange(mapper.Map<List<TrimmingMarkerModel>>(model.TrimmingMarkers));
                    return;
                }
            }
            catch (JsonException) { }

        // if we couldn't parse it as a project file, load it as a video file
        TrimmingMarkers.Add(new(0));
        ProjectFileName = null;
        MediaFileName = projectFileName;
    }

    void TogglePlayPause()
    {
        if (MediaPlaybackState is MediaPlaybackState.Playing)
            MediaPlaybackState = MediaPlaybackState.Paused;
        else
            MediaPlaybackState = MediaPlaybackState.Playing;
    }

    void FrameStep(bool forward)
    {
        var inputFrameNumber = CurrentInputFrameNumber;
        var newInputFrameNumber = inputFrameNumber + (forward ? 2 : -1);
        if (GetNextNonTrimmedInputFrameNumber(newInputFrameNumber, forward, out var nextNonTrimmedInputFrameNumber))
            UpdateMediaPosition?.Invoke(GetPositionFromFrameNumber(nextNonTrimmedInputFrameNumber));
    }

    void CropFrameStep(bool forward)
    {
        var outputFrameNumber = CurrentOutputFrameNumber;
        var nextCropMarkerFrameNumber = forward
            ? CropFrames.FirstOrDefault(w => w.FrameNumber > outputFrameNumber).FrameNumber
            : CropFrames.LastOrDefault(w => w.FrameNumber < outputFrameNumber).FrameNumber;
        if (forward && nextCropMarkerFrameNumber == 0)
            nextCropMarkerFrameNumber = GetFrameNumberFromPosition(OutputMediaDuration);
        UpdateMediaPosition?.Invoke(GetPositionFromFrameNumber(GetInputFrameNumberFromOutputFrameNumber(nextCropMarkerFrameNumber)));
    }

    internal bool ProcessKey(VirtualKey key, bool ctrl, bool up)
    {
        switch ((key, ctrl, up))
        {
            case (VirtualKey.Space, false, true):
                TogglePlayPause();
                return true;
            case (VirtualKey.Left, false, false):
                FrameStep(false);
                return true;
            case (VirtualKey.Right, false, false):
                FrameStep(true);
                return true;
            case (VirtualKey.M, false, true):
                AddMarker();
                return true;
            case (VirtualKey.Home, false, true):
                UpdateMediaPosition?.Invoke(
                    GetPositionFromFrameNumber(GetInputFrameNumberFromOutputFrameNumber(0)));
                return true;
            case (VirtualKey.End, false, true):
                UpdateMediaPosition?.Invoke(
                    GetPositionFromFrameNumber(GetInputFrameNumberFromOutputFrameNumber(GetFrameNumberFromPosition(OutputMediaDuration))));
                return true;
            case (VirtualKey.Left, true, false):
                CropFrameStep(false);
                return true;
            case (VirtualKey.Right, true, false):
                CropFrameStep(true);
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

    public TimeSpan GetPositionFromFrameNumber(long outputFrameNumber)
    {
        if (MediaFrameRate is 0) return TimeSpan.Zero;
        var newPositionTicks = TimeSpan.FromMilliseconds(Math.Round(outputFrameNumber / MediaFrameRate * 1000)).Ticks;
        var frameDurationTicks = TimeSpan.FromSeconds(1 / MediaFrameRate).Ticks;

        if (newPositionTicks % frameDurationTicks > frameDurationTicks / 2)
            newPositionTicks += frameDurationTicks - (newPositionTicks % frameDurationTicks);
        return TimeSpan.FromTicks(newPositionTicks);
    }

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