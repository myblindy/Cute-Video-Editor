using CuteVideoEditor.Contracts.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CuteVideoEditor.Views.Dialogs;

public sealed partial class MessageContentDialog : ContentDialog
{
    public MessageContentDialog()
    {
        InitializeComponent();
    }

    public static async Task<MessageDialogResult> Information(XamlRoot xamlRoot, string title, string message, string? extraButtonText = null)
    {
        var dlg = new MessageContentDialog();
        dlg.XamlRoot = xamlRoot;
        dlg.Title = title;
        dlg.DescriptionTextBlock.Text = message;
        dlg.MessageTypeIcon.Source = new BitmapImage(new Uri("ms-appx:///Assets/success-icon.png"));

        dlg.PrimaryButtonText = "OK";
        dlg.SecondaryButtonText = extraButtonText;

        return await dlg.ShowAsync() switch
        {
            ContentDialogResult.Primary => MessageDialogResult.OK,
            ContentDialogResult.Secondary => MessageDialogResult.Extra,
            _ => MessageDialogResult.Cancel
        };
    }

    public static async Task Error(XamlRoot xamlRoot, string title, string message)
    {
        var dlg = new MessageContentDialog();
        dlg.XamlRoot = xamlRoot;
        dlg.Title = title;
        dlg.DescriptionTextBlock.Text = message;
        dlg.MessageTypeIcon.Source = new BitmapImage(new Uri("ms-appx:///Assets/fail-icon.png"));

        dlg.PrimaryButtonText = "OK";

        await dlg.ShowAsync();
    }
}
