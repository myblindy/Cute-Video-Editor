using CuteVideoEditor.Core.Models;

namespace CuteVideoEditor.Core.Contracts.Services;
public interface IVideoTranscoderService
{
    void Transcode(VideoTranscodeInput input, VideoTranscodeOutput output, Action frameProcessed);
}