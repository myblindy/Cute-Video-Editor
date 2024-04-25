using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;
using FFmpegInteropX;
using Microsoft.UI.Xaml.Controls;
using ReactiveUI;

namespace CuteVideoEditor.Views.Dialogs;

[ObservableObject]
public sealed partial class ExportVideoContentDialog : ContentDialog
{
    private readonly IDialogService dialogService;

    [ObservableProperty]
    FFmpegTranscodeOutput? outputModel;

    [ObservableProperty]
    MainViewModel? mainViewModel;

    public ExportVideoViewModel ViewModel { get; }

    public ExportVideoContentDialog(IDialogService dialogService, ExportVideoViewModel viewModel)
    {
        this.dialogService = dialogService;
        ViewModel = viewModel;
        InitializeComponent();

        this.WhenAnyValue(x => x.OutputModel!.FileName).WhereNotNull().Subscribe(fn =>
            ViewModel!.SelectOutputFileType(fn));
    }

    private async void BrowseOutputFileName(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (OutputModel is not null && await dialogService.SelectSaveVideoFileAsync(OutputModel.FileName) is { } outputFileName)
            OutputModel.FileName = outputFileName;
    }
}
