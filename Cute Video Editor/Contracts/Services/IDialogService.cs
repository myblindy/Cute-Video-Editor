using CuteVideoEditor.ViewModels;
using CuteVideoEditor.ViewModels.Dialogs;
using FFmpegInteropX;

namespace CuteVideoEditor.Contracts.Services;

public interface IDialogService
{
    Task<string?> SelectSaveProjectFileAsync(string? filename);
    Task<string?> SelectSaveVideoFileAsync(string? filename);
    Task<string?> SelectVideoFileAsync();
    Task<FFmpegTranscodeOutput?> SelectTranscodeOutputParameters(MainViewModel mainViewModel);
    Task<bool> ShowOperationProgressDialog(string? description, bool autoClose, Func<OperationProgressViewModel, Task> operation);
    Task ShowMessageDialog(string content, string title);
}