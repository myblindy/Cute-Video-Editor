using AutoMapper;
using CuteVideoEditor_Video;
using CuteVideoEditor.Core.Contracts.Services;
using CuteVideoEditor.Core.Models;
using Windows.Graphics.Imaging;

namespace CuteVideoEditor.Services;
class VideoTranscoderService(IMapper mapper) : IVideoTranscoderService
{
    public void Transcode(VideoTranscodeInput input, VideoTranscodeOutput output, Action<ulong, SoftwareBitmap> frameProcessed)
    {
        using var transcoder = new Transcode();
        transcoder.FrameOutputProgress += (s, e) => frameProcessed(e.FrameNumber, e.FrameBitmap);
        transcoder.Run(new(input.FileName, 0,
                mapper.Map<List<TranscodeInputCropFrameEntry>>(input.CropFrames),
                mapper.Map<List<TranscodeInputTrimmingMarkerEntry>>(input.TrimmingMarkers),
                input.EncoderTitle),
            new(output.FileName, output.OutputType, output.Crf, output.FrameRateMultiplier,
                new(output.PixelWidth, output.PixelHeight), OutputPresetType.Medium));
    }
}
