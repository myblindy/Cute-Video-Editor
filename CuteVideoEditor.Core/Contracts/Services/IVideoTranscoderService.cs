using CuteVideoEditor.Core.Models;
using Windows.Graphics.Imaging;

namespace CuteVideoEditor.Core.Contracts.Services;
public interface IVideoTranscoderService
{
    void Transcode(VideoTranscodeInput input, VideoTranscodeOutput output, Action<ulong, SoftwareBitmap?> frameProcessed);
}