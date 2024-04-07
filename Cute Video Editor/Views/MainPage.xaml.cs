using CuteVideoEditor.ViewModels;

using Microsoft.UI.Xaml.Controls;
using ReactiveUI;
using Windows.Media.Playback;
using Windows.Storage;

using FFmpegInteropX;
using System.Reactive.Linq;
using Windows.UI.Core;

namespace CuteVideoEditor.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    readonly MediaPlayer mediaPlayer;
    FFmpegMediaSource? mediaSource;

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();

        // media player
        mediaPlayer = new()
        {
            AudioCategory = MediaPlayerAudioCategory.Movie
        };
        mediaPlayer.PlaybackSession.PositionChanged += (s, e) =>
            App.MainDispatcherQueue.TryEnqueue(() => ViewModel.MediaPosition = s.Position);
        mediaPlayer.MediaOpened += (s, e) =>
            App.MainDispatcherQueue.TryEnqueue(() => ViewModel.MediaDuration = s.NaturalDuration);
        mediaPlayer.MediaFailed += (s, e) =>
        {

        };
        mediaPlayerElement.SetMediaPlayer(mediaPlayer);

        // load media on demand
        ViewModel.WhenAnyValue(x => x.MediaFileName).Distinct().Subscribe(async mediaFileName =>
        {
            mediaSource?.Dispose();
            mediaSource = null;

            if (!string.IsNullOrWhiteSpace(mediaFileName))
            {
                var file = await StorageFile.GetFileFromPathAsync(mediaFileName);
                var stream = await file.OpenReadAsync();

                mediaSource = await FFmpegMediaSource.CreateFromStreamAsync(stream);
                await mediaSource.OpenWithMediaPlayerAsync(mediaPlayer);
            }
            else
                mediaPlayer.Source = null;
        });

        // for testing
        ViewModel.MediaFileName = @"D:\temp\[FANCAM] 230709 트와이스 TWICE World Tour Ready To Be Atlanta Encore Firework + Celebrate + TT [T7PDj4LSHRE].mp4";
    }

    private void Page_PreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Space)
            if (mediaPlayerElement.MediaPlayer.PlaybackSession.PlaybackState is MediaPlaybackState.Playing)
                mediaPlayerElement.MediaPlayer.Pause();
            else
                mediaPlayerElement.MediaPlayer.Play();
    }
}
