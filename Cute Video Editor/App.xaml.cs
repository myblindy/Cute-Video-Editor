using CuteVideoEditor.Activation;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.Core.Models;
using CuteVideoEditor.Services;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;
using CuteVideoEditor.Views;
using CuteVideoEditor.Views.Dialogs;
using FFmpegInteropX;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<FFmpegLogProvider>();

                // Core Services

                // Views and ViewModels
                services.AddScoped<MainViewModel>();
                services.AddScoped<MainPage>();
                services.AddScoped<ExportVideoViewModel>();
                services.AddScoped<ExportVideoContentDialog>();

                // Mapper
                services.AddAutoMapper(typeof(SerializationMapperProfile));

                // Configuration
            })
            .ConfigureLogging(context =>
                context.AddDebug())
            .Build();

        UnhandledException += App_UnhandledException;

        FFmpegInteropLogging.SetLogProvider(GetService<FFmpegLogProvider>());
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

class FFmpegLogProvider(ILogger<FFmpegLogProvider> logger) : ILogProvider
{
    public void Log(FFmpegInteropX.LogLevel level, string message) =>
        logger.Log(level switch
        {
            FFmpegInteropX.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            FFmpegInteropX.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            FFmpegInteropX.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            FFmpegInteropX.LogLevel.Verbose => Microsoft.Extensions.Logging.LogLevel.Trace,
            _ => Microsoft.Extensions.Logging.LogLevel.Debug
        }, message);
}