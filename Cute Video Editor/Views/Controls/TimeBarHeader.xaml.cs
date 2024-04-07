using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Windows.Media.Animation;

namespace CuteVideoEditor.Views.Controls;

[ObservableObject]
public sealed partial class TimeBarHeader : UserControl
{
    public static readonly DependencyProperty EndProperty =
        DependencyProperty.Register(nameof(End), typeof(TimeSpan), typeof(TimeBarHeader), new PropertyMetadata(TimeSpan.Zero, (s, e) => ((TimeBarHeader)s).RebuildTicks()));
    public TimeSpan End
    {
        get { return (TimeSpan)GetValue(EndProperty); }
        set { SetValue(EndProperty, value); }
    }

    public static readonly DependencyProperty StartProperty =
        DependencyProperty.Register(nameof(Start), typeof(TimeSpan), typeof(TimeBarHeader), new PropertyMetadata(TimeSpan.Zero, (s, e) => ((TimeBarHeader)s).RebuildTicks()));
    public TimeSpan Start
    {
        get { return (TimeSpan)GetValue(StartProperty); }
        set { SetValue(StartProperty, value); }
    }

    public ObservableCollection<TimeBarHeaderTickEntry> Ticks = [];

    void RebuildTicks()
    {
        Ticks.Clear();
        var length = End - Start;
        if (length == default) return;

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

    public TimeBarHeader()
    {
        InitializeComponent();

        SizeChanged += (s, e) => RebuildTicks();
    }

    public static double GetXOffset(TimeSpan timeSpan, TimeBarHeader timeBarHeader) =>
        (timeSpan - timeBarHeader.Start).TotalSeconds / (timeBarHeader.End - timeBarHeader.Start).TotalSeconds * timeBarHeader.ActualWidth + 4;

    public static double GetTickHeight(float multiplier) => 40 * multiplier;
}

public readonly struct TimeBarHeaderTickEntry
{
    public readonly TimeSpan TimeSpan;
    public readonly float Multiplier;
    public readonly string? DisplayText;
    public readonly TimeBarHeader TimeBarHeader;

    public TimeBarHeaderTickEntry(TimeSpan timeSpan, float mult, TimeBarHeader timeBarHeader) =>
        (TimeSpan, Multiplier, TimeBarHeader, DisplayText) =
            (timeSpan, mult, timeBarHeader, mult == 1 ? timeSpan.ToString(@"hh\:mm\:ss") : null);
}