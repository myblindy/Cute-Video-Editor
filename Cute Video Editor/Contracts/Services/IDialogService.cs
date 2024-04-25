using CuteVideoEditor.ViewModels;
using FFmpegInteropX;

namespace CuteVideoEditor.Contracts.Services;

public interface IDialogService
{
    Task<string?> SelectSaveProjectFileAsync(string? filename);
    Task<string?> SelectSaveVideoFileAsync(string? filename);
    Task<string?> SelectVideoFileAsync();
    Task<FFmpegTranscodeOutput?> SelectTranscodeOutputParameters(MainViewModel mainViewModel);
}