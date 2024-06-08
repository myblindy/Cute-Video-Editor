#include "pch.h"
#include "Transcode.h"
#include "FFmpegController.h"

#include "TranscodeInput.g.cpp"
#include "TranscodeInputCropFrameEntry.g.cpp"
#include "TranscodeInputCropRectangle.g.cpp"
#include "TranscodeInputTrimmingMarkerEntry.g.cpp"
#include "TranscodeOutput.g.cpp"
#include "Transcode.g.cpp"

using namespace std;

namespace winrt::CuteVideoEditor_Video::implementation
{
	Transcode::Transcode()
		: ffmpegController(make_unique<FFmpegController>())
	{
	}

	void Transcode::Run(CuteVideoEditor_Video::TranscodeInput const& input, CuteVideoEditor_Video::TranscodeOutput const& output)
	{
		if (!ffmpegController)
			throw_hresult(RO_E_CLOSED);

		ffmpegController->OpenInputVideo(StringUtils::PlatformStringToUtf8String(input.FileName()).c_str(), true);
		ffmpegController->SetValidTrimmingRanges(to_vector(input.TrimmingMarkers()));

		ffmpegController->OpenOutputVideo(StringUtils::PlatformStringToUtf8String(output.FileName()).c_str(),
			output.Type(), output.CRF(), static_cast<uint32_t>(output.PixelSize().Width), static_cast<uint32_t>(output.PixelSize().Height),
			StringUtils::PlatformStringToUtf8String(input.EncoderTitle()).c_str(),
			to_vector(input.CropFrames()), true);

		uint64_t encodedFrameIndex = 0;
		for (auto frame : ffmpegController->EnumerateInputFrames())
		{
			ffmpegController->EncodeFrame(frame);
			frameOutputProgress(*this, encodedFrameIndex++);
		}
	}

	void Transcode::Close()
	{
		// dispose pattern
		ffmpegController.reset();
	}
}
