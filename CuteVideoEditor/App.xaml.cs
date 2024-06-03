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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

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
                //services.AddSingleton<FFmpegLogProvider>();
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

        UnhandledException += App_UnhandledException;

        //FFmpegInteropLogging.SetLogLevel(FFmpegInteropX.LogLevel.Trace);
        //FFmpegInteropLogging.SetLogProvider(GetService<FFmpegLogProvider>());

        //var vm = GetService<VideoEditorViewModel>();
        //vm.LoadProjectFile(@"D:\premiere\mina swagger.cve");
        //vm.ExportVideoHack(new()
        //{
        //    FileName = @"d:\temp\mina swagger.mkv",
        //    OutputType = VideoOutputType.Vp9,
        //    PixelWidth = vm.LargestOutputPixelSize.Width,
        //    PixelHeight = vm.LargestOutputPixelSize.Height,
        //    Crf = 12,
        //    FrameRateMultiplier = 1
        //});

        //async Task t()
        //{
        //    using var ir = new ImageReader(@"d:\vids\sexy\100801 Miss A - Bad girl Good girl min suzy boobs bra skirts.mp4");
        //    var timeSpan = TimeSpan.FromSeconds(5307 / ir.FrameRate);
        //    ir.Position = timeSpan;
            
        //    using (var stream = File.Create(@"d:\temp\a.jpg"))
        //    {
        //        using var wrtStream = stream.AsRandomAccessStream();
        //        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, wrtStream);
                
        //        encoder.SetSoftwareBitmap(ir.CurrentFrameBitmap);
        //        await encoder.FlushAsync();
        //    }
            
        //    Process.Start(new ProcessStartInfo("d:\\temp\\a.jpg") { UseShellExecute = true });
        //}
        //_ = t();
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

//class FFmpegLogProvider(ILogger<FFmpegLogProvider> logger) : ILogProvider
//{
//    public void Log(FFmpegInteropX.LogLevel level, string message) =>
//        logger.Log(level switch
//        {
//            FFmpegInteropX.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
//            FFmpegInteropX.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
//            FFmpegInteropX.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
//            FFmpegInteropX.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
//            _ => Microsoft.Extensions.Logging.LogLevel.Trace
//        }, message);
//}