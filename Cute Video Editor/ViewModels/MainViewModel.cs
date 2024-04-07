using CommunityToolkit.Mvvm.ComponentModel;

namespace CuteVideoEditor.ViewModels;

public partial class MainViewModel : ObservableRecipient
{
    [ObservableProperty]
    string? mediaFileName;

    [ObservableProperty]
    TimeSpan mediaDuration;

    [ObservableProperty]
    TimeSpan mediaPosition;

    public MainViewModel()
    {
    }
}
