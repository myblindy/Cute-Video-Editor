using CommunityToolkit.Mvvm.ComponentModel;
using CuteVideoEditor.ViewModels.Dialogs;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using ReactiveUI;

namespace CuteVideoEditor.Views.Dialogs;

[ObservableObject]
public sealed partial class OperationProgressContentDialog : ContentDialog
{
    public OperationProgressViewModel ViewModel { get; }

    [ObservableProperty]
    WriteableBitmap? previewFrameImage;

    public OperationProgressContentDialog(OperationProgressViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();

        vm.WhenAnyValue(x => x.PreviewFrame).Subscribe(frame =>
        {
            if (frame is null)
                PreviewFrameImage = null;
            else
            {
                if (PreviewFrameImage is null || PreviewFrameImage.PixelWidth != frame.PixelWidth || PreviewFrameImage.PixelHeight != frame.PixelHeight)
                    PreviewFrameImage = new WriteableBitmap(frame.PixelWidth, frame.PixelHeight);

                frame.CopyToBuffer(PreviewFrameImage.PixelBuffer);
            }
        });
    }
}
