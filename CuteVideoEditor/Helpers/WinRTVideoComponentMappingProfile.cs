using AutoMapper;

namespace CuteVideoEditor.Helpers;
class WinRTVideoComponentMappingProfile : Profile
{
    public WinRTVideoComponentMappingProfile()
    {
        CreateMap<Core.Models.CropFrameEntryModel, CuteVideoEditor_Video.TranscodeInputCropFrameEntry>().ReverseMap();
        CreateMap<Core.Models.TrimmingMarkerModel, CuteVideoEditor_Video.TranscodeInputTrimmingMarkerEntry>().ReverseMap();
        CreateMap<Core.Models.RectModel, CuteVideoEditor_Video.TranscodeInputCropRectangle>().ReverseMap();
    }
}
