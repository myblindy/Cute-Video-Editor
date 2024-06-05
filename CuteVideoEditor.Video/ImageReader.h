#pragma once

#include <FFmpegController.h>

#include "ImageReader.g.h"

namespace winrt::CuteVideoEditor_Video::implementation
{
	struct ImageReader : ImageReaderT<ImageReader>
	{
		ImageReader(hstring const& fileName);
		~ImageReader() { Close(); }
		void Close();

		void SetTrimmingMarkers(Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputTrimmingMarkerEntry> trimmingMarkers);
		bool AdvanceFrame();

		hstring FileName() const { return fileName; }
		int32_t VideoStreamIndex() const { return videoStreamIndex; }
		Windows::Foundation::TimeSpan MediaDuration() const { return mediaDuration; }
		double FrameRate() const { return frameRate; }
		Windows::Foundation::TimeSpan Position() const { return position; }
		void Position(Windows::Foundation::TimeSpan const value);
		Windows::Foundation::TimeSpan FrameDuration() const { return frameDuration; }
		Windows::Graphics::Imaging::SoftwareBitmap CurrentFrameBitmap() const { return currentFrameBitmap; }

	private:
		void InitializeFrameEnumerator();
		bool ReadCurrentFrame(bool initialize);

		std::unique_ptr<FFmpegController> ffmpegController;
		asyncpp::generator<AVFrame*> ffmpegFrameGenerator;
		asyncpp::generator<AVFrame*>::iterator ffmpegFrameIterator;
		Windows::Graphics::Imaging::SoftwareBitmap currentFrameBitmap{ nullptr };

		hstring fileName;
		int32_t videoStreamIndex{};
		Windows::Foundation::TimeSpan mediaDuration{}, position{}, frameDuration{};
		double frameRate{};
	};
}

namespace winrt::CuteVideoEditor_Video::factory_implementation
{
	struct ImageReader : ImageReaderT<ImageReader, implementation::ImageReader>
	{
	};
}
