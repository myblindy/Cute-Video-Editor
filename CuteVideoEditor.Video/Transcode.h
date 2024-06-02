#pragma once

#include "TranscodeInputCropRectangle.g.h"
#include "TranscodeInputCropFrameEntry.g.h"
#include "TranscodeInputTrimmingMarkerEntry.g.h"
#include "TranscodeInput.g.h"
#include "TranscodeOutput.g.h"
#include "Transcode.g.h"

class FFmpegController;

namespace winrt::CuteVideoEditor_Video::implementation
{
	struct TranscodeInputCropRectangle : TranscodeInputCropRectangleT<TranscodeInputCropRectangle>
	{
		int CenterX() const { return center_x; }
		void CenterX(int const value) { center_x = value; }

		int CenterY() const { return center_y; }
		void CenterY(int const value) { center_y = value; }

		int Width() const { return width; }
		void Width(int const value) { width = value; }

		int Height() const { return height; }
		void Height(int const value) { height = value; }

		TranscodeInputCropRectangle() = default;
		TranscodeInputCropRectangle(int center_x, int center_y, int width, int height)
		{
			this->center_x = center_x;
			this->center_y = center_y;
			this->width = width;
			this->height = height;
		}

	private:
		int center_x = {};
		int center_y = {};
		int width = {};
		int height = {};
	};

	struct TranscodeInputCropFrameEntry : TranscodeInputCropFrameEntryT<TranscodeInputCropFrameEntry>
	{
		CuteVideoEditor_Video::TranscodeInputCropRectangle CropRectangle() const { return crop_rectangle; }
		void CropRectangle(CuteVideoEditor_Video::TranscodeInputCropRectangle const& value) { crop_rectangle = value; }

		int64_t FrameNumber() const { return frame_number; }
		void FrameNumber(int64_t const value) { frame_number = value; }

		TranscodeInputCropFrameEntry() = default;
		TranscodeInputCropFrameEntry(int64_t frame_number, CuteVideoEditor_Video::TranscodeInputCropRectangle crop_rectangle)
		{
			this->crop_rectangle = crop_rectangle;
			this->frame_number = frame_number;
		}
	private:
		CuteVideoEditor_Video::TranscodeInputCropRectangle crop_rectangle = {};
		int64_t frame_number = {};
	};

	struct TranscodeInputTrimmingMarkerEntry : TranscodeInputTrimmingMarkerEntryT<TranscodeInputTrimmingMarkerEntry>
	{
		bool TrimAfter() const { return trim_after; }
		void TrimAfter(bool const value) { trim_after = value; }

		int64_t FrameNumber() const { return frame_number; }
		void FrameNumber(int64_t const value) { frame_number = value; }

		TranscodeInputTrimmingMarkerEntry() { }
		TranscodeInputTrimmingMarkerEntry(int64_t frame_number, bool trim_after)
		{
			this->trim_after = trim_after;
			this->frame_number = frame_number;
		}
	private:
		bool trim_after = {};
		int64_t frame_number = {};
	};

	struct TranscodeInput : TranscodeInputT<TranscodeInput>
	{
		hstring FileName() const { return filename; }
		void FileName(hstring const& value) { filename = value; }

		int VideoStreamIndex() const { return video_stream_index; }
		void VideoStreamIndex(int const value) { video_stream_index = value; }

		Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputCropFrameEntry> CropFrames() const { return crop_frames; }
		void CropFrames(Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputCropFrameEntry> const value) { crop_frames = value; }

		Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputTrimmingMarkerEntry> TrimmingMarkers() const { return trimming_markers; }
		void TrimmingMarkers(Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputTrimmingMarkerEntry> const value) { trimming_markers = value; }

		hstring EncoderTitle() const { return encoder_title; }
		void EncoderTitle(hstring const& value) { encoder_title = value; }

		TranscodeInput() { }
		TranscodeInput(hstring const& input, int const video_stream_index,
			Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputCropFrameEntry> crop_frames,
			Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputTrimmingMarkerEntry> trimming_markers,
			hstring const& encoder_title)
		{
			this->filename = input;
			this->video_stream_index = video_stream_index;
			this->crop_frames = crop_frames;
			this->trimming_markers = trimming_markers;
			this->encoder_title = encoder_title;
		}

	private:
		hstring filename;
		int video_stream_index = {};
		Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputCropFrameEntry> crop_frames;
		Windows::Foundation::Collections::IVectorView<CuteVideoEditor_Video::TranscodeInputTrimmingMarkerEntry> trimming_markers;
		hstring encoder_title;
	};

	struct TranscodeOutput : TranscodeOutputT<TranscodeOutput>
	{
		hstring FileName() const { return filename; }
		OutputType Type() const { return type; }
		uint32_t CRF() const { return crf; }
		OutputPresetType Preset() const { return preset; }
		Windows::Foundation::Size PixelSize() const { return pixelSize; }
		double FrameRateMultiplier() const { return frameRateMultiplier; }

		TranscodeOutput(hstring const& FileName, OutputType Type, uint32_t CRF, double FrameRateMultiplier,
			Windows::Foundation::Size const& PixelSize, OutputPresetType Preset)
			: filename(FileName), type(Type), crf(CRF), frameRateMultiplier(FrameRateMultiplier), pixelSize(PixelSize), preset(Preset)
		{}

	private:
		hstring filename;
		OutputType type;
		uint32_t crf;
		OutputPresetType preset;
		Windows::Foundation::Size pixelSize;
		double frameRateMultiplier;
	};

	struct Transcode : TranscodeT<Transcode>
	{
		Transcode();

		void Run(CuteVideoEditor_Video::TranscodeInput const& input, CuteVideoEditor_Video::TranscodeOutput const& output);

		winrt::event_token FrameOutputProgress(Windows::Foundation::EventHandler<std::uint64_t> const& handler) { return frameOutputProgress.add(handler); }
		void FrameOutputProgress(winrt::event_token const& token) noexcept { frameOutputProgress.remove(token); }

		virtual ~Transcode() { Close(); }
		void Close();

	private:
		std::unique_ptr<FFmpegController> ffmpegController;
		winrt::event<Windows::Foundation::EventHandler<std::uint64_t>> frameOutputProgress;
	};
}

namespace winrt::CuteVideoEditor_Video::factory_implementation
{
	struct TranscodeInputCropFrameEntry : TranscodeInputCropFrameEntryT<TranscodeInputCropFrameEntry, implementation::TranscodeInputCropFrameEntry>
	{
	};

	struct TranscodeInputCropRectangle : TranscodeInputCropRectangleT<TranscodeInputCropRectangle, implementation::TranscodeInputCropRectangle>
	{
	};

	struct TranscodeInputTrimmingMarkerEntry : TranscodeInputTrimmingMarkerEntryT<TranscodeInputTrimmingMarkerEntry, implementation::TranscodeInputTrimmingMarkerEntry>
	{
	};

	struct TranscodeInput : TranscodeInputT<TranscodeInput, implementation::TranscodeInput>
	{
	};

	struct TranscodeOutput : TranscodeOutputT<TranscodeOutput, implementation::TranscodeOutput>
	{
	};

	struct Transcode : TranscodeT<Transcode, implementation::Transcode>
	{
	};
}
