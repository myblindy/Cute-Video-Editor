using CuteVideoEditor.Core.Models;
using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;

namespace CuteVideoEditor.Contracts.Services;

public interface IDialogService
{
    Task<string?> SelectSaveProjectFileAsync(string? filename);
    Task<string?> SelectSaveVideoFileAsync(string? filename);
    Task<string?> SelectVideoFileAsync();
    Task<VideoTranscodeOutput?> SelectTranscodeOutputParameters(VideoEditorViewModel mainViewModel);
    Task<bool> ShowOperationProgressDialog(string? description, bool autoClose, Func<OperationProgressViewModel, Task> operation);
    Task ShowInformationMessageDialog(string content, string title);
}