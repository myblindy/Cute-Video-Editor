using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CuteVideoEditor.Views.Dialogs;

public sealed partial class MessageContentDialog : ContentDialog
{
    public MessageContentDialog()
    {
        InitializeComponent();
    }

    public static async Task Information(XamlRoot xamlRoot, string title, string message)
    {
        var dlg = new MessageContentDialog();
        dlg.XamlRoot = xamlRoot;
        dlg.Title = title;
        dlg.DescriptionTextBlock.Text = message;

        dlg.PrimaryButtonText = "OK";
        dlg.SecondaryButtonText = null;

        await dlg.ShowAsync();
    }
}
