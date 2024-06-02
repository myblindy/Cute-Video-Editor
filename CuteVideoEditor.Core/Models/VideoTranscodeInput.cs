namespace CuteVideoEditor.Core.Models;
public class VideoTranscodeInput
{
    public required string FileName { get; set; }
    public required IList<CropFrameEntryModel> CropFrames { get; set; }
    public required IList<TrimmingMarkerModel> TrimmingMarkers { get; set; }
    public required string EncoderTitle { get; set; }
}
