using CuteVideoEditor.ViewModels;
using Microsoft.UI.Xaml.Controls;
using ReactiveUI;
using Windows.Media.Playback;
using Windows.Storage;
using System.Reactive.Linq;
using CuteVideoEditor.Core.Helpers;

namespace CuteVideoEditor.Views;

public sealed partial class VideoEditorPage : Page
{
    public VideoEditorViewModel ViewModel { get; }

    public VideoEditorPage(VideoEditorViewModel mainViewModel)
    {
        ViewModel = mainViewModel;
        InitializeComponent();

        Unloaded += (s, e) => ViewModel.Dispose();
    }
}
