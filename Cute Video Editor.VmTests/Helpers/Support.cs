using AutoMapper;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cute_Video_Editor.VmTests.Helpers;

static class Support
{
    public static IHost BuildHost() => Host.CreateDefaultBuilder()
        .ConfigureServices((_, services) => services
            .AddScoped<MainViewModel>()
            .AddScoped(_ => Mock.Of<IDialogService>())
            .AddScoped(_ => Mock.Of<IMapper>()))
        .Build();

    public static MainViewModel CreateViewModel() =>
        BuildHost().Services.GetRequiredService<MainViewModel>();
}
