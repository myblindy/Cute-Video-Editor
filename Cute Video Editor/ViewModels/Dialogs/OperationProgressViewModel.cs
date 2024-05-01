using CommunityToolkit.Mvvm.ComponentModel;

namespace CuteVideoEditor.ViewModels.Dialogs;
public partial class OperationProgressViewModel : ObservableObject
{
    [ObservableProperty]
    string? description;

    [ObservableProperty]
    double progress;

    [ObservableProperty]
    bool result;
}
