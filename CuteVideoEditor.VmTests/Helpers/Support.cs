using AutoMapper;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.Core.Contracts.Services;
using CuteVideoEditor.Core.Contracts.ViewModels;
using CuteVideoEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Cute_Video_Editor.VmTests.Helpers;

static class Support
{
    public static IHost BuildHost() => Host.CreateDefaultBuilder()
        .ConfigureServices((_, services) => services
            .AddScoped<VideoEditorViewModel>()
            .AddScoped<IVideoPlayerViewModel, VideoPlayerViewModel>()
            .AddScoped(_ => Mock.Of<IDialogService>())
            .AddScoped(_ => Mock.Of<IMapper>())
            .AddScoped(_ => Mock.Of<IVideoTranscoderService>()))
        .Build();

    public static VideoEditorViewModel CreateViewModel() =>
        BuildHost().Services.GetRequiredService<VideoEditorViewModel>();
}
