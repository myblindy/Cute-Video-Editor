using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.Core.Helpers;
using CuteVideoEditor.Core.Models;
using CuteVideoEditor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;

namespace CuteVideoEditor.Views.Controls;

[ObservableObject]
public sealed partial class TimeBarHeaderControl : UserControl
{
    public static readonly DependencyProperty EndProperty = DependencyProperty.Register(nameof(End), typeof(TimeSpan), typeof(TimeBarHeaderControl),
        new PropertyMetadata(TimeSpan.Zero, (s, e) => ((TimeBarHeaderControl)s).Rebuild(RebuildType.Ticks)));
    public TimeSpan End
    {
        get { return (TimeSpan)GetValue(EndProperty); }
        set { SetValue(EndProperty, value); }
    }

    public static readonly DependencyProperty StartProperty = DependencyProperty.Register(nameof(Start), typeof(TimeSpan), typeof(TimeBarHeaderControl),
        new PropertyMetadata(TimeSpan.Zero, (s, e) => ((TimeBarHeaderControl)s).Rebuild(RebuildType.Ticks)));
    public TimeSpan Start
    {
        get { return (TimeSpan)GetValue(StartProperty); }
        set { SetValue(StartProperty, value); }
    }

    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(TimeSpan), typeof(TimeBarHeaderControl),
        new PropertyMetadata(TimeSpan.Zero, (s, e) => ((TimeBarHeaderControl)s).Rebuild(RebuildType.Position)));
    public TimeSpan Position
    {
        get { return (TimeSpan)GetValue(PositionProperty); }
        set { SetValue(PositionProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(nameof(ViewModel), typeof(VideoEditorViewModel), typeof(TimeBarHeaderControl),
        new PropertyMetadata(null, (s, e) => ((TimeBarHeaderControl)s).OnViewModelChanged()));
    public VideoEditorViewModel? ViewModel
    {
        get { return (VideoEditorViewModel?)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public ObservableCollection<TimeBarHeaderControlTickEntry> Ticks { get; } = [];
    public ObservableCollection<TimeBarHeaderControlDisjunctTrimmingMarkerEntry> DisjunctOutputTrims { get; } = [];
    public ObservableCollection<TimeBarHeaderControlNonDisjunctMarkerEntry> NonDisjunctOutputMarkers { get; } = [];
    public ObservableCollection<TimeBarHeaderControlCropFrameEntry> CropFrames { get; } = [];

    [ObservableProperty]
    TimeBarHeaderControlTickEntry positionTick;

    void OnViewModelChanged()
    {
        if (ViewModel is not null)
        {
            DisjunctOutputTrims.KeepInSync(ViewModel.DisjunctOutputTrims, w => new(w, this));
            NonDisjunctOutputMarkers.KeepInSync(ViewModel.NonDisjunctOutputMarkers, w => new(w, this));
            CropFrames.KeepInSync(ViewModel.CropFrames, w => new(ViewModel, w, this));
        }
    }

    [Flags]
    enum RebuildType { Ticks = 1 << 0, Position = 1 << 1, All = Ticks | Position }
    void Rebuild(RebuildType rebuildType)
    {
        if ((rebuildType & RebuildType.Ticks) != 0)
        {
            Ticks.Clear();
            var length = End - Start;
            if (length != default && ActualWidth != 0)
            {
                const double TicksPerPixel = 0.005;
                var tickCount = (int)Math.Ceiling(ActualWidth * TicksPerPixel);
                var tickDuration = TimeSpan.FromTicks(length.Ticks / tickCount);
                for (var tick = Start; tick < End; tick += tickDuration)
                {
                    Ticks.Add(new(tick, 1f, this));
                    Ticks.Add(new(tick + tickDuration * 0.25, 0.25f, this));
                    Ticks.Add(new(tick + tickDuration * 0.5, 0.5f, this));
                    Ticks.Add(new(tick + tickDuration * 0.75, 0.25f, this));
                }
            }
        }

        if ((rebuildType & RebuildType.Position) != 0)
        {
            PositionTick = new(Position, 0f, this);
        }

        DisjunctOutputTrims.ResetSync();
        NonDisjunctOutputMarkers.ResetSync();
        CropFrames.ResetSync();
        OnPropertyChanged(nameof(PositionTick));
    }

    public TimeBarHeaderControl()
    {
        InitializeComponent();

        SizeChanged += (s, e) => Rebuild(RebuildType.All);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        var ppt = e.GetCurrentPoint(this);
        if (ppt.PointerDeviceType is Microsoft.UI.Input.PointerDeviceType.Mouse && ppt.Properties.IsLeftButtonPressed)
            ViewModel!.VideoPlayerViewModel.OutputMediaPosition = TimeSpan.FromSeconds(
                ViewModel!.VideoPlayerViewModel.OutputMediaDuration.TotalSeconds * ppt.Position.X / ActualWidth);
    }

    public static double GetXOffset(TimeSpan timeSpan, TimeBarHeaderControl? timeBarHeader) => timeBarHeader is null ? 0 :
        (timeSpan - timeBarHeader.Start).TotalSeconds / (timeBarHeader.End - timeBarHeader.Start).TotalSeconds * timeBarHeader.ActualWidth /*+ 4*/;

    public static double GetTickHeight(float multiplier) => 40 * multiplier;
}

public readonly struct TimeBarHeaderControlTickEntry
{
    public readonly TimeSpan TimeSpan;
    public readonly float Multiplier;
    public readonly string? DisplayText;
    public readonly TimeBarHeaderControl TimeBarHeader;

    public TimeBarHeaderControlTickEntry(TimeSpan timeSpan, float mult, TimeBarHeaderControl timeBarHeader) =>
        (TimeSpan, Multiplier, TimeBarHeader, DisplayText) =
            (timeSpan, mult, timeBarHeader, mult == 1 ? timeSpan.ToString(@"hh\:mm\:ss") : null);
}

public readonly struct TimeBarHeaderControlDisjunctTrimmingMarkerEntry
{
    public readonly TimeSpan From, To;
    public TimeSpan Duration => To - From;
    public readonly TimeBarHeaderControl TimeBarHeader;

    public TimeBarHeaderControlDisjunctTrimmingMarkerEntry(TimeSpan from, TimeSpan to, TimeBarHeaderControl timeBarHeader) =>
        (From, To, TimeBarHeader) = (from, to, timeBarHeader);

    public TimeBarHeaderControlDisjunctTrimmingMarkerEntry(DisjunctTrimmingMarkerEntry entry, TimeBarHeaderControl timeBarHeader) :
        this(entry.From, entry.To, timeBarHeader)
    {
    }
}

public readonly struct TimeBarHeaderControlNonDisjunctMarkerEntry
{
    public readonly TimeSpan Position;
    public readonly TimeBarHeaderControl TimeBarHeader;

    public TimeBarHeaderControlNonDisjunctMarkerEntry(TimeSpan position, TimeBarHeaderControl timeBarHeader) =>
        (Position, TimeBarHeader) = (position, timeBarHeader);
}

public readonly struct TimeBarHeaderControlCropFrameEntry
{
    public readonly TimeSpan Position;
    public readonly TimeBarHeaderControl TimeBarHeader;

    public TimeBarHeaderControlCropFrameEntry(VideoEditorViewModel vm, CropFrameEntryModel cropFrame, TimeBarHeaderControl timeBarHeader) =>
        (Position, TimeBarHeader) =
            (vm.VideoPlayerViewModel.GetPositionFromFrameNumber(cropFrame.FrameNumber), timeBarHeader);
}