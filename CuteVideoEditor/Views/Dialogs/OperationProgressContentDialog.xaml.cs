using CuteVideoEditor.ViewModels.Dialogs;
using Microsoft.UI.Xaml.Controls;

namespace CuteVideoEditor.Views.Dialogs;
public sealed partial class OperationProgressContentDialog : ContentDialog
{
    public OperationProgressViewModel ViewModel { get; }

    public OperationProgressContentDialog(OperationProgressViewModel vm)
    {
        ViewModel = vm;
        InitializeComponent();
    }
}
