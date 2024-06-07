using AutoMapper;
using CuteVideoEditor_Video;
using CuteVideoEditor.Core.Contracts.Services;
using CuteVideoEditor.Core.Models;

namespace CuteVideoEditor.Services;
class VideoTranscoderService(IMapper mapper) : IVideoTranscoderService
{
    static OutputType ConvertOutputType(VideoOutputType videoOutputType) =>
        videoOutputType switch
        {
            VideoOutputType.Mp4 => OutputType.Mp4,
            VideoOutputType.Vp8 => OutputType.Vp8,
            VideoOutputType.Vp9 => OutputType.Vp9,
            _ => throw new NotImplementedException()
        };

    public void Transcode(VideoTranscodeInput input, VideoTranscodeOutput output, Action frameProcessed)
    {
        using var transcoder = new Transcode();
        transcoder.FrameOutputProgress += (s, e) => frameProcessed();
        transcoder.Run(new(input.FileName, 0,
                mapper.Map<List<TranscodeInputCropFrameEntry>>(input.CropFrames),
                mapper.Map<List<TranscodeInputTrimmingMarkerEntry>>(input.TrimmingMarkers),
                input.EncoderTitle),
            new(output.FileName, ConvertOutputType(output.OutputType), output.Crf, output.FrameRateMultiplier,
                new(output.PixelWidth, output.PixelHeight), OutputPresetType.Medium));
    }
}
