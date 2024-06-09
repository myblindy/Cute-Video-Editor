using CuteVideoEditor.Core.Models;
using CuteVideoEditor_Video;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace CuteVideoEditor.Core.Services;

public class SettingsService
{
    class SettingsJsonModel
    {
        public uint LastCrf { get; set; } = 12;
        public OutputType LastVideoOutputType { get; set; } = OutputType.Vp9;
    }
    SettingsJsonModel model;

    readonly string baseLocalAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    const string customLocalAppDataPath = "CuteVideoEditor";
    readonly string fullConfigPath;

    public SettingsService()
    {
        fullConfigPath = Path.Combine(baseLocalAppDataPath, customLocalAppDataPath, "config.json");
        ReadSettings();
    }

    [MemberNotNull(nameof(model))]
    public void ReadSettings()
    {
        try
        {
            if (File.Exists(fullConfigPath))
            {
                using var stream = File.OpenRead(fullConfigPath);
                model = JsonSerializer.Deserialize<SettingsJsonModel>(stream) ?? new();
                return;
            }
        }
        catch { }

        model = new();
    }

    public void SaveSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullConfigPath)!);

        try
        {
            using var stream = File.Create(fullConfigPath);
            JsonSerializer.Serialize(stream, model);
        }
        catch { }
    }

    public uint LastCrf
    {
        get => model.LastCrf;
        set { model.LastCrf = value; SaveSettings(); }
    }

    public OutputType LastVideoOutputType
    {
        get => model.LastVideoOutputType;
        set { model.LastVideoOutputType = value; SaveSettings(); }
    }
}
