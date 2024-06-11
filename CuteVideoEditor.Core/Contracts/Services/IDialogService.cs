using CuteVideoEditor.Core.Models;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;

namespace CuteVideoEditor.Contracts.Services;

public enum MessageDialogResult
{
    OK,
    Cancel,
    Extra
}

public interface IDialogService
{
    bool IsDialogOpen { get; }

    Task<string?> SelectSaveProjectFileAsync(string? filename);
    Task<string?> SelectSaveVideoFileAsync(string? filename);
    Task<string?> SelectVideoFileAsync();
    Task<VideoTranscodeOutput?> SelectTranscodeOutputParameters(VideoEditorViewModel mainViewModel);
    Task<bool> ShowOperationProgressDialog(string? description, bool autoClose, Func<OperationProgressViewModel, Task> operation);
    Task<MessageDialogResult> ShowInformationMessageDialog(string content, string title, string? extraButton = null);
    Task ShowErrorMessageDialog(string content, string title);
}