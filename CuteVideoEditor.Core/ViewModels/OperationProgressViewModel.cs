using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Graphics.Imaging;

namespace CuteVideoEditor.ViewModels.Dialogs;
public partial class OperationProgressViewModel : ObservableObject
{
    [ObservableProperty]
    string? description;

    [ObservableProperty]
    double progress;

    [ObservableProperty]
    bool result;

    [ObservableProperty]
    SoftwareBitmap? previewFrame;
}
