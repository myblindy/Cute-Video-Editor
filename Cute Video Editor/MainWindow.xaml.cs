using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.Helpers;
using CuteVideoEditor.Services;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.Views;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CuteVideoEditor;

[ObservableObject]
public sealed partial class MainWindow : WindowEx
{
    private readonly DialogService dialogService = App.GetService<DialogService>();
    private readonly UISettings settings;

    public ObservableCollection<MainWindowTabEntry> Tabs { get; } = [];

    [ObservableProperty]
    MainWindowTabEntry? selectedTab;

    partial void OnSelectedTabChanged(MainWindowTabEntry? value)
    {
        foreach (var tab in Tabs)
            tab.Visibility = tab.Page == value?.Page ? Visibility.Visible : Visibility.Collapsed;
    }

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico"));

        // Theme change code picked from https://github.com/microsoft/WinUI-Gallery/pull/1239
        settings = new UISettings();
        settings.ColorValuesChanged += Settings_ColorValuesChanged; // cannot use FrameworkElement.ActualThemeChanged event

        AppTitleBar.Loaded += (s, e) => SetRegionsForCustomTitleBar();
        AppTitleBar.SizeChanged += (s, e) => SetRegionsForCustomTitleBar();
        TabView.SizeChanged += (s, e) => SetRegionsForCustomTitleBar();

        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        SetTitleBar(AppTitleBar);
        Activated += MainWindow_Activated;
        AppTitleBarText.Text = "AppDisplayName".GetLocalized();
    }

    void SetRegionsForCustomTitleBar()
    {
        if (AppTitleBar.XamlRoot is null) return;

        // Specify the interactive regions of the title bar.
        double scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

        RightPaddingColumn.Width = new GridLength(App.MainWindow.AppWindow.TitleBar.RightInset / scaleAdjustment);
        LeftPaddingColumn.Width = new GridLength(App.MainWindow.AppWindow.TitleBar.LeftInset / scaleAdjustment);

        var transform = TabView.TransformToVisual(null);
        var bounds = transform.TransformBounds(new(0, 0, TabView.ActualWidth, TabView.ActualHeight));
        var toolBarRect = GetRect(bounds, scaleAdjustment);

        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(App.MainWindow.AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, [toolBarRect]);
    }

    private static Windows.Graphics.RectInt32 GetRect(Rect bounds, double scale) =>
        new(_X: (int)Math.Round(bounds.X * scale), _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale), _Height: (int)Math.Round(bounds.Height * scale));

    UnhookWindowsHookExSafeHandle hookHandle;
    HOOKPROC keyboardHookProc;
    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (App.AppTitlebar is null)
        {
            App.AppTitlebar = AppTitleBarBorder;
            TitleBarHelper.ApplySystemThemeToCaptionButtons();

            keyboardHookProc = new(KeyboardHookProc);
            hookHandle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD, keyboardHookProc, null, PInvoke.GetCurrentThreadId());
        }
    }

    private LRESULT KeyboardHookProc(int code, WPARAM wParam, LPARAM lParam)
    {
        // perhaps this would be better to send the key data, and have the vm handle it? 
        // or the page?

        MainViewModel? GetActiveViewModel() => SelectedTab?.Page.ViewModel;

        if (code >= 0)
        {
            var up = (lParam & 0x80000000) != 0;
            var vk = (Windows.System.VirtualKey)(nuint)wParam;
            if (GetActiveViewModel()?.ProcessKey(vk, up) is true)
                return default;
        }

        return PInvoke.CallNextHookEx(hookHandle, code, wParam, lParam);
    }

    private void Settings_ColorValuesChanged(UISettings sender, object args) =>
        App.MainDispatcherQueue.TryEnqueue(TitleBarHelper.ApplySystemThemeToCaptionButtons);

    private async void TabView_AddTabButtonClick(Microsoft.UI.Xaml.Controls.TabView sender, object args)
    {
        if (await dialogService.SelectVideoFileAsync() is { } mediaFileName)
        {
            using var scope = App.GetService<IServiceScopeFactory>().CreateScope();
            var tabPage = scope.ServiceProvider.GetRequiredService<MainPage>();
            tabPage.ViewModel.LoadProjectFile(mediaFileName);

            MainWindowTabEntry tabEntry = new(tabPage, this);
            Tabs.Add(tabEntry);
            SelectedTab = tabEntry;
        }
    }

    private void TabView_TabCloseRequested(Microsoft.UI.Xaml.Controls.TabView sender, Microsoft.UI.Xaml.Controls.TabViewTabCloseRequestedEventArgs args)
    {
        if (SelectedTab is not null)
            Tabs.Remove(SelectedTab);
    }

    public static string? GetTabName(string mediaFileName) =>
        Path.GetFileName(mediaFileName);
}

public partial class MainWindowTabEntry(MainPage page, MainWindow mainWindow) : ObservableObject
{
    public MainPage Page { get; } = page;
    public MainWindow MainWindow { get; } = mainWindow;
    [ObservableProperty]
    Visibility visibility = Visibility.Visible;
}