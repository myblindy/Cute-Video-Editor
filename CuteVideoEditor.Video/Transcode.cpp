#include "pch.h"
#include "Transcode.h"
#include "FFmpegController.h"

#include "TranscodeInput.g.cpp"
#include "TranscodeInputCropFrameEntry.g.cpp"
#include "TranscodeInputCropRectangle.g.cpp"
#include "TranscodeInputTrimmingMarkerEntry.g.cpp"
#include "TranscodeOutput.g.cpp"
#include "TranscodeFrameOutputProgressEventArgs.g.cpp"
#include "Transcode.g.cpp"

using namespace std;
using namespace winrt;
using namespace Windows::Graphics::Imaging;

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
		const uint64_t frameOutputProgressInterval = 60;
		for (auto frame : ffmpegController->EnumerateInputFrames())
		{
			if (encodedFrameIndex++ % frameOutputProgressInterval == 0)
			{
				auto frameBitmap = ffmpegController->GetRgbaTemporaryFrame(frame, 600, 600);

				SoftwareBitmap softwareFrameBitmap{ BitmapPixelFormat::Bgra8, frameBitmap->width, frameBitmap->height };
				auto softwareBitmapBuffer = softwareFrameBitmap.LockBuffer(BitmapBufferAccessMode::Write);
				auto softwareBitmapBufferData = softwareBitmapBuffer.CreateReference().data();
				memcpy(softwareBitmapBufferData, frameBitmap->data[0], frameBitmap->linesize[0] * frameBitmap->height);

				ffmpegController->ReleaseTemporaryFrame(frameBitmap);
				frameOutputProgress(*this, make<TranscodeFrameOutputProgressEventArgs>(encodedFrameIndex, softwareFrameBitmap));
			}

			ffmpegController->EncodeFrame(frame);
		}
	}

	void Transcode::Close()
	{
		// dispose pattern
		ffmpegController.reset();
	}
}
