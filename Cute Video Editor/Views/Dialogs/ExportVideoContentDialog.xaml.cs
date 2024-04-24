using CommunityToolkit.Mvvm.ComponentModel;
using FFmpegInteropX;
using Microsoft.UI.Xaml.Controls;

namespace CuteVideoEditor.Views.Dialogs;

[ObservableObject]
public sealed partial class ExportVideoContentDialog : ContentDialog
{
    [ObservableProperty]
    FFmpegTranscodeOutput? outputModel;

    partial void OnOutputModelChanged(FFmpegTranscodeOutput? value)
    {

    }

    public ExportVideoContentDialog()
    {
        InitializeComponent();
    }
}
