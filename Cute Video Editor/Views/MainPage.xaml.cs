using CuteVideoEditor.ViewModels;

using Microsoft.UI.Xaml.Controls;
using ReactiveUI;
using Windows.Media.Playback;
using Windows.Storage;

namespace CuteVideoEditor.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    readonly MediaPlayer mediaPlayer;

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();

        // media player
        mediaPlayer = new()
        {
            AudioCategory = MediaPlayerAudioCategory.Movie
        };
        mediaPlayer.MediaOpened += (s, e) =>
        {

        };
        mediaPlayer.MediaFailed += (s, e) =>
        {

        };

        // load media on demand
        ViewModel.WhenAnyValue(x => x.MediaFileName).Subscribe(async mediaFileName =>
        {
            var file = await StorageFile.GetFileFromPathAsync(mediaFileName);
            using var stream = await file.OpenReadAsync();


        });
    }
}
