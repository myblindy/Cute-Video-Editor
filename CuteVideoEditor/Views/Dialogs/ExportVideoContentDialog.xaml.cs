using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.Contracts.Services;
using CuteVideoEditor.ViewModels.Dialogs;
using Microsoft.UI.Xaml.Controls;
using Windows.Globalization.NumberFormatting;

namespace CuteVideoEditor.Views.Dialogs;

[ObservableObject]
public sealed partial class ExportVideoContentDialog : ContentDialog
{
    private readonly IDialogService dialogService;

    public ExportVideoViewModel ViewModel { get; }

    public ExportVideoContentDialog(IDialogService dialogService, ExportVideoViewModel viewModel)
    {
        this.dialogService = dialogService;
        ViewModel = viewModel;
        InitializeComponent();

        FrameRateMultiplierNumberBox.NumberFormatter = new DecimalFormatter()
        {
            FractionDigits = 3,
            IntegerDigits = 1,
            NumberRounder = new IncrementNumberRounder
            {
                Increment = 0.001,
                RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp
            }
        };
    }

    private async void BrowseOutputFileName(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (await dialogService.SelectSaveVideoFileAsync(ViewModel.FileName) is { } outputFileName)
            ViewModel.FileName = outputFileName;
    }
}
