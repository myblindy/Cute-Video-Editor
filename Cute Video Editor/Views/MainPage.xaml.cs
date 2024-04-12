using CuteVideoEditor.ViewModels;
using Microsoft.UI.Xaml.Controls;
using ReactiveUI;
using Windows.Media.Playback;
using Windows.Storage;
using FFmpegInteropX;
using System.Reactive.Linq;
using System.Diagnostics;
using CuteVideoEditor.Core.Helpers;

namespace CuteVideoEditor.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    readonly MediaPlayer mediaPlayer = new();
    PeriodicTimer? frameTimer;
    FFmpegMediaSource? mediaSource;

    public MainPage(MainViewModel mainViewModel)
    {
        ViewModel = mainViewModel;
        InitializeComponent();

        Unloaded += (s, e) => frameTimer?.Dispose();

        // media player
        InitializeMediaPlayer();

        // view model controlling the media player
        ViewModel.WhenAnyValue(x => x.MediaPlaybackState).Subscribe(state =>
        {
            switch (state)
            {
                case MediaPlaybackState.Playing:
                    mediaPlayer.Play();
                    break;
                case MediaPlaybackState.Paused:
                    mediaPlayer.Pause();
                    break;
            }
        });
        ViewModel.UpdateMediaPosition += position =>
            mediaPlayer.PlaybackSession.Position = position.Clamp(TimeSpan.Zero, ViewModel.MediaDuration);

        // load media on demand
        ViewModel.WhenAnyValue(x => x.MediaFileName).Subscribe(async mediaFileName =>
        {
            mediaSource?.Dispose();
            mediaSource = null;

            if (!string.IsNullOrWhiteSpace(mediaFileName))
            {
                var file = await StorageFile.GetFileFromPathAsync(mediaFileName);
                var stream = await file.OpenReadAsync();

                mediaSource = await FFmpegMediaSource.CreateFromStreamAsync(stream);
                ViewModel.MediaPixelSize = new(mediaSource.CurrentVideoStream.PixelWidth, mediaSource.CurrentVideoStream.PixelHeight);
                ViewModel.MediaDuration = mediaSource.Duration;
                ViewModel.MediaFrameRate = mediaSource.CurrentVideoStream.FramesPerSecond;

                frameTimer?.Dispose();
                frameTimer = new(TimeSpan.FromSeconds(1 / ViewModel.MediaFrameRate));

                async Task FrameTimerHandler(PeriodicTimer timer)
                {
                    while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
                        App.MainDispatcherQueue.TryEnqueue(() => ViewModel.MediaPosition = mediaPlayer.PlaybackSession.Position);
                }
                _ = FrameTimerHandler(frameTimer);

                await mediaSource.OpenWithMediaPlayerAsync(mediaPlayer);
            }
            else
                mediaPlayer.Source = null;
        });
    }

    private void InitializeMediaPlayer()
    {
        mediaPlayer.AudioCategory = MediaPlayerAudioCategory.Movie;
        mediaPlayer.PlaybackSession.PlaybackStateChanged += (s, e) => App.MainDispatcherQueue.TryEnqueue(() =>
            ViewModel.MediaPlaybackState = s.PlaybackState);
        mediaPlayer.MediaOpened += (s, e) =>
        {
        };
        mediaPlayer.MediaFailed += (s, e) =>
        {
        };
        mediaPlayer.SourceChanged += (s, e) =>
        {
        };
        mediaPlayerElement.SetMediaPlayer(mediaPlayer);
        mediaPlayerElement.SizeChanged += (s, e) =>
            ViewModel.VideoPlayerPixelSize = new((int)mediaPlayerElement.ActualWidth, (int)mediaPlayerElement.ActualHeight);
    }
}
