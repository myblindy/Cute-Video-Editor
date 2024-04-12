using AutoMapper;

namespace CuteVideoEditor.Core.Models;

public class RectSerializationModel
{
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public int Height { get; set; }
    public int Width { get; set; }
}

public class CropFrameEntrySerializationModel
{
    public int FrameNumber { get; set; }
    public RectSerializationModel? Rect { get; set; }
}

public class SerializationModel
{
    public string? MediaFileName { get; set; }
    public List<CropFrameEntrySerializationModel>? CropFrames { get; set; }
}

public class SerializationMapperProfile : Profile
{
    public SerializationMapperProfile()
    {
        CreateMap<RectModel, RectSerializationModel>().ReverseMap();
        CreateMap<CropFrameEntrySerializationModel, CropFrameEntryModel>().ReverseMap();
    }
}