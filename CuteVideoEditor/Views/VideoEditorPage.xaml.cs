using CuteVideoEditor.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace CuteVideoEditor.Views;

public sealed partial class VideoEditorPage : Page
{
    public VideoEditorViewModel ViewModel { get; }

    public VideoEditorPage(VideoEditorViewModel mainViewModel)
    {
        ViewModel = mainViewModel;
        InitializeComponent();

        Unloaded += (s, e) => ViewModel.Dispose();
        videoPlayerControl.SizeChanged += (s, e) => ViewModel.VideoPlayerPixelSize = new((int)e.NewSize.Width, (int)e.NewSize.Height);
    }
}
