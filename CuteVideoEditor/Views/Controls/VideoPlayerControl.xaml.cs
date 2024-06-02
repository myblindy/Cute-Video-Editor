using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.Core.Contracts.ViewModels;
using CuteVideoEditor.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CuteVideoEditor.Views.Controls;

[ObservableObject]
public sealed partial class VideoPlayerControl : UserControl
{
    public static readonly DependencyProperty VideoPlayerViewModelProperty = DependencyProperty.Register(nameof(VideoPlayerViewModel),
        typeof(IVideoPlayerViewModel), typeof(VideoPlayerControl), new PropertyMetadata(null, (o, e) =>
        {
            if (o is VideoPlayerControl videoPlayerControl)
            {
                if (e.OldValue is VideoPlayerViewModel oldViewModel)
                    oldViewModel?.Dispose();
                if (e.NewValue is VideoPlayerViewModel viewModel)
                {
                    viewModel.FrameReady += (s, e) =>
                    {
                        if (e is null)
                            videoPlayerControl.VideoPlayerImage = null;
                        else
                        {
                            if (videoPlayerControl.VideoPlayerImage is null
                                || videoPlayerControl.VideoPlayerImage.PixelWidth != e.PixelWidth || videoPlayerControl.VideoPlayerImage.PixelHeight != e.PixelHeight)
                            {
                                videoPlayerControl.VideoPlayerImage = new WriteableBitmap(e.PixelWidth, e.PixelHeight);
                            }

                            e.CopyToBuffer(videoPlayerControl.VideoPlayerImage.PixelBuffer);
                        }
                    };
                    viewModel.TriggerFrameReady();
                }
            }
        }));

    [ObservableProperty]
    WriteableBitmap? videoPlayerImage;

    public IVideoPlayerViewModel? VideoPlayerViewModel
    {
        get => (IVideoPlayerViewModel?)GetValue(VideoPlayerViewModelProperty);
        set => SetValue(VideoPlayerViewModelProperty, value);
    }

    public VideoPlayerControl()
    {
        InitializeComponent();
    }
}
