using AutoMapper;

namespace CuteVideoEditor.Core.Models;

public class TrimmingMarkerSerializationModel
{
    public long FrameNumber { get; set; }
    public bool TrimAfter { get; set; }
}

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
    public RectSerializationModel? CropRectangle { get; set; }
}

public class SerializationModel
{
    public required bool FreezeCropSizeMode { get; set; }
    public required string MediaFileName { get; set; }
    public required List<CropFrameEntrySerializationModel> CropFrames { get; set; }
    public required List<TrimmingMarkerSerializationModel> TrimmingMarkers { get; set; }
}

public class SerializationMapperProfile : Profile
{
    public SerializationMapperProfile()
    {
        CreateMap<RectModel, RectSerializationModel>().ReverseMap();
        CreateMap<CropFrameEntryModel, CropFrameEntrySerializationModel>().ReverseMap();
        CreateMap<TrimmingMarkerModel, TrimmingMarkerSerializationModel>().ReverseMap();
    }
}