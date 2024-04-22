using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CuteVideoEditor.Views.Controls;

[ObservableObject]
public sealed partial class TimeBarHeaderControl : UserControl
{
    public static readonly DependencyProperty EndProperty =
        DependencyProperty.Register(nameof(End), typeof(TimeSpan), typeof(TimeBarHeaderControl), new PropertyMetadata(TimeSpan.Zero, (s, e) =>
            ((TimeBarHeaderControl)s).Rebuild(RebuildType.Ticks)));
    public TimeSpan End
    {
        get { return (TimeSpan)GetValue(EndProperty); }
        set { SetValue(EndProperty, value); }
    }

    public static readonly DependencyProperty StartProperty =
        DependencyProperty.Register(nameof(Start), typeof(TimeSpan), typeof(TimeBarHeaderControl), new PropertyMetadata(TimeSpan.Zero, (s, e) =>
            ((TimeBarHeaderControl)s).Rebuild(RebuildType.Ticks)));
    public TimeSpan Start
    {
        get { return (TimeSpan)GetValue(StartProperty); }
        set { SetValue(StartProperty, value); }
    }

    public static readonly DependencyProperty PositionProperty =
        DependencyProperty.Register(nameof(Position), typeof(TimeSpan), typeof(TimeBarHeaderControl), new PropertyMetadata(TimeSpan.Zero, (s, e) =>
            ((TimeBarHeaderControl)s).Rebuild(RebuildType.Position)));
    public TimeSpan Position
    {
        get { return (TimeSpan)GetValue(PositionProperty); }
        set { SetValue(PositionProperty, value); }
    }

    public ObservableCollection<TimeBarHeaderControlTickEntry> Ticks = [];

    [ObservableProperty]
    TimeBarHeaderControlTickEntry positionTick;

    public ICommand? PositionPercentageUpdateRequestCommand { get; set; }

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
            PositionPercentageUpdateRequestCommand?.Execute(ppt.Position.X / ActualWidth);
    }

    public static double GetXOffset(TimeSpan timeSpan, TimeBarHeaderControl? timeBarHeader) => timeBarHeader is null ? 0 :
        (timeSpan - timeBarHeader.Start).TotalSeconds / (timeBarHeader.End - timeBarHeader.Start).TotalSeconds * timeBarHeader.ActualWidth + 4;

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