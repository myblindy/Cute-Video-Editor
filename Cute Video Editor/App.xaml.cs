using CuteVideoEditor.Activation;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.Core.Models;
using CuteVideoEditor.Services;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.Views;
using FFmpegInteropX;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI.Core;

namespace CuteVideoEditor;

public partial class App : Application
{
    readonly IHost host;

    public static T GetService<T>()
        where T : class
    {
        if ((Current as App)!.host.Services.GetService<T>() is not { } service)
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");

        return service;
    }

    public static WindowEx MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar { get; set; }

    public static DispatcherQueue MainDispatcherQueue { get; } = DispatcherQueue.GetForCurrentThread();

    public App()
    {
        InitializeComponent();

        RequestedTheme = ApplicationTheme.Dark;

        host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureServices((context, services) =>
            {
                // Default Activation Handler
                services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

                // Other Activation Handlers

                // Services
                services.AddSingleton<IActivationService, ActivationService>();
                services.AddSingleton<IPageService, PageService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<DialogService>();

                // Core Services

                // Views and ViewModels
                services.AddScoped<MainViewModel>();
                services.AddScoped<MainPage>();

                // Mapper
                services.AddAutoMapper(typeof(SerializationMapperProfile));

                // Configuration
            })
            .Build();

        UnhandledException += App_UnhandledException;

        using FFmpegTranscode transcode = new();
        transcode.Run(new(@"C:\Users\miteam\Downloads\123941-655.mp4", 0, [new(new(60, 60, 40, 40), 0)]), new()
        {
            FileName = @"C:\Users\miteam\Downloads\meow_out.webm",
            FrameRate = 30,
            Bitrate = 1000000,
            PixelSize = new(40, 40),
            Type = OutputType.Vp8
        });
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        await GetService<IActivationService>().ActivateAsync(args);
    }
}
