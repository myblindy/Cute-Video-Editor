#include "pch.h"
#include "ImageReader.h"

#include "ImageReader.g.cpp"

using namespace std;
using namespace winrt;
using namespace Windows::Foundation::Collections;
using namespace Windows::Graphics::Imaging;

namespace winrt::CuteVideoEditor_Video::implementation
{
	ImageReader::ImageReader(hstring const& fileName)
		: ffmpegController(make_unique<FFmpegController>())
	{
		FFmpegControllerThreadedType threadType{};
		ffmpegController->OpenInputVideo(
			StringUtils::PlatformStringToUtf8String(fileName).c_str(), false, threadType);
		frameRate = ffmpegController->GetFrameRate();
		mediaDuration = ffmpegController->GetMediaDuration();

		ffmpegController->SetValidTrimmingRanges({});

		ReadCurrentFrame(true);
	}

	void ImageReader::Close()
	{
		ffmpegController.reset();
	}

	void ImageReader::SetTrimmingMarkers(IVectorView<CuteVideoEditor_Video::TranscodeInputTrimmingMarkerEntry> trimmingMarkers)
	{
		ffmpegController->SetValidTrimmingRanges(to_vector(trimmingMarkers));
	}

	void ImageReader::InitializeFrameEnumerator()
	{
		ffmpegFrameGenerator = ffmpegController->EnumerateInputFrames();
		ffmpegFrameIterator = ffmpegFrameGenerator.begin();
	}

	bool ImageReader::ReadCurrentFrame(bool initialize)
	{
		if (initialize)
		{
			InitializeFrameEnumerator();

			if (ffmpegFrameIterator == ffmpegFrameGenerator.end())
				return false;

			// read the video stream geometry and create the bitmap
			auto frame = *ffmpegFrameIterator;
			currentFrameBitmap = { BitmapPixelFormat::Rgba8, frame->width, frame->height };
		}

		if (!*ffmpegFrameIterator)
			return false;

		auto rgbaFrame = ffmpegController->GetRgbaTemporaryFrame(*ffmpegFrameIterator);

		auto pixelBuffer = currentFrameBitmap.LockBuffer(BitmapBufferAccessMode::Write);
		auto buffer = pixelBuffer.CreateReference().data();
		memcpy(buffer, rgbaFrame->data[0], rgbaFrame->linesize[0] * rgbaFrame->height);

		ffmpegController->ReleaseTemporaryFrame(rgbaFrame);
		frameDuration = ffmpegController->GetFrameDuration(*ffmpegFrameIterator);

		return true;
	}

	bool ImageReader::AdvanceFrame()
	{
		if (++ffmpegFrameIterator == ffmpegFrameGenerator.end() || *ffmpegFrameIterator == nullptr)
			return false;

		position = ffmpegController->GetFramePosition(*ffmpegFrameIterator);
		return ReadCurrentFrame(false);
	}

	void ImageReader::Position(Windows::Foundation::TimeSpan const value)
	{
		// only start a seek if we're going backwards, or far enough forward
		if (value == position) return;
		if (value >= position && value - position <= chrono::seconds(1))
		{
			while (TimeSpanToSeconds(value - position) >= 0.99 / frameRate)
				if (!AdvanceFrame())
					break;
			return;
		}

		auto seekForward = value > position;
		ffmpegController->Seek(position = value, seekForward);
		InitializeFrameEnumerator();
		ReadCurrentFrame(false);
	}
}
