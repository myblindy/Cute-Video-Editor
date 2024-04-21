namespace CuteVideoEditor.Contracts.Services;

public interface IDialogService
{
    Task<string?> SelectSaveProjectFileAsync();
    Task<string?> SelectVideoFileAsync();
}