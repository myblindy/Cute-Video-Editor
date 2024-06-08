using CuteVideoEditor.Activation;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.Core.Contracts.Services;
using CuteVideoEditor.Core.Contracts.ViewModels;
using CuteVideoEditor.Core.Models;
using CuteVideoEditor.Helpers;
using CuteVideoEditor.Services;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;
using CuteVideoEditor.Views;
using CuteVideoEditor.Views.Dialogs;
using CuteVideoEditor_Video;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Reflection;
using Windows.Win32;

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
        UnhandledException += App_UnhandledException;
        InitializeComponent();

        RequestedTheme = ApplicationTheme.Dark;

        host = Host.CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();
                config.AddJsonFile("appsettings.json", true);
#if DEBUG
                config.AddJsonFile($"appsettings.Development.json", true);
#endif
                //config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Default Activation Handler
                services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

                // Other Activation Handlers

                // Services
                services.AddSingleton<IActivationService, ActivationService>();
                services.AddSingleton<IPageService, PageService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IFFmpegLogProvider, FFmpegLogProvider>();
                services.AddSingleton<IVideoTranscoderService, VideoTranscoderService>();

                // Views and ViewModels
                services.AddScoped<VideoEditorViewModel>();
                services.AddScoped<VideoEditorPage>();
                services.AddTransient<ExportVideoViewModel>();
                services.AddTransient<ExportVideoContentDialog>();
                services.AddTransient<OperationProgressContentDialog>();
                services.AddTransient<OperationProgressViewModel>();
                services.AddTransient<IVideoPlayerViewModel, VideoPlayerViewModel>();

                // Mapper
                services.AddAutoMapper(typeof(SerializationMapperProfile), typeof(WinRTVideoComponentMappingProfile));

                // Configuration
            })
            .ConfigureLogging((context, config) =>
                config.AddConfiguration(context.Configuration.GetSection("Logging")))
            .Build();


        FFmpegLogging.LogLevel = CuteVideoEditor_Video.LogLevel.Warning;
        FFmpegLogging.LogProvider = GetService<IFFmpegLogProvider>();

        RegisterForActivation();
    }

    private unsafe static void RegisterForActivation()
    {
        // executable path
        var buffer = stackalloc char[1024];
        PInvoke.GetModuleFileName(null, buffer, 1024);
        var executablePath = new string(buffer);

        ActivationRegistrationManager.RegisterForFileTypeActivation([".cve"],
            $"{executablePath},0", "Cute Video Editor File", ["open"], executablePath);
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

class FFmpegLogProvider(ILogger<FFmpegLogProvider> logger) : IFFmpegLogProvider
{
    public void Log(CuteVideoEditor_Video.LogLevel level, string message) =>
        logger.Log(level switch
        {
            CuteVideoEditor_Video.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            CuteVideoEditor_Video.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            CuteVideoEditor_Video.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            CuteVideoEditor_Video.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            _ => Microsoft.Extensions.Logging.LogLevel.Trace
        }, "{Message}", message);
}