#pragma once

#include "Transcode.h"

enum FFmpegControllerThreadedType
{
	FrameThreads, SlideThreads, SingleThread
};

class FFmpegController
{
	// input data
	AutoReleasePtr<AVFormatContext, avformat_close_input> inputFormatContext;
	AVStream* inputVideoStream{};
	AutoReleasePtr<AVCodecContext, avcodec_free_context> inputCodecContext;
	winrt::Windows::Foundation::TimeSpan mediaDuration{};
	double frameRate{};
	std::vector<std::pair<winrt::Windows::Foundation::TimeSpan, winrt::Windows::Foundation::TimeSpan>> validTrimmingRanges;
	std::vector<winrt::CuteVideoEditor_Video::TranscodeInputCropFrameEntry> cropFrames;
	FFmpegControllerThreadedType inputThreadType;

	int64_t inputFrameNumber{};
	bool flushing{};
	int validTrimmingRangeEntryIndex{};
	int cropFrameEntryIndex{};

	AutoReleasePtr<AVPacket, av_packet_unref> inputPacket = av_packet_alloc();
	AutoReleasePtr<AVPacket, av_packet_unref> outputPacket = av_packet_alloc();

	// output data
	AutoReleasePtr<AVFormatContext, avformat_free_context> outputFormatContext;
	AutoReleasePtr<AVCodecContext, avcodec_free_context> outputCodecContext;
	AVFilterContext* bufferSourceContext{}, * bufferSinkContext{};
	AVStream* outputVideoStream{};
	double frameRateMultiplier = 1;
	int64_t encodedFrameNumber{};

	// input -> output filter
	AutoReleasePtr<AVFilterInOut, avfilter_inout_free>
		filterInputs = avfilter_inout_alloc(),
		filterOutputs = avfilter_inout_alloc();
	AutoReleasePtr<AVFilterGraph, avfilter_graph_free> filterGraph = avfilter_graph_alloc();
	AVFilterContext* cropFilterContext{};
	AutoReleasePtr<AVFrame, av_frame_free> filteredFrame = av_frame_alloc();

	// helpers
	void throw_av_error(int ret);
	winrt::Windows::Foundation::TimeSpan GetDurationFromFrameNumber(int64_t frameNumber) const;
	int64_t GetFrameNumberFromDuration(winrt::Windows::Foundation::TimeSpan duration) const;
	winrt::CuteVideoEditor_Video::TranscodeInputCropRectangle GetCurrentCropRectangle();
	void SetupEncodingParameters(AVCodecContext& ctx, winrt::CuteVideoEditor_Video::OutputType outputType, uint32_t crf);
	void WriteFilteredFrame(bool flush);

	std::vector<AVFrame*> temporaryFrames;
	AVFrame* GetTemporaryFrame(AVPixelFormat pix_fmt, int width, int height);
	void ConvertFrame(AVFrame* srcFrame, AVFrame* dstFrame);

public:
	void OpenInputVideo(const char* filenameUtf8, bool dumpFormat);
	FFmpegControllerThreadedType GetInputThreadType() const { return inputThreadType; }
	winrt::Windows::Foundation::TimeSpan GetMediaDuration() const { return mediaDuration; }
	void SetValidTrimmingRanges(const std::vector<winrt::CuteVideoEditor_Video::TranscodeInputTrimmingMarkerEntry>& trimmingMarkers);
	asyncpp::generator<AVFrame*> EnumerateInputFrames();
	bool Seek(winrt::Windows::Foundation::TimeSpan position);

	void OpenOutputVideo(const char* filenameUtf8, winrt::CuteVideoEditor_Video::OutputType outputType, uint32_t crf,
		uint32_t width, uint32_t height, const char* encoderTitleUtf8,
		const std::vector<winrt::CuteVideoEditor_Video::TranscodeInputCropFrameEntry>& cropFrames, bool dumpFormat);

	void EncodeFrame(AVFrame* frame);

	AVFrame* GetRgbaTemporaryFrame(AVFrame* frame, int maxWidth = 0, int maxHeight = 0);
	void ReleaseTemporaryFrame(AVFrame* frame);

	double GetFrameRate() const { return frameRate; }
	winrt::Windows::Foundation::TimeSpan GetFramePosition(AVFrame* frame) const;
	winrt::Windows::Foundation::TimeSpan GetFrameDuration(AVFrame* frame) const;

	~FFmpegController();
};
