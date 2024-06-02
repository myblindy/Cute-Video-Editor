#include "pch.h"
#include "FFmpegController.h"
#include "Transcode.h"

using namespace std;
using namespace chrono;
using namespace winrt;
using namespace CuteVideoEditor_Video;
using namespace Windows::Foundation;

static AVCodecID GetCodecId(OutputType type)
{
	switch (type)
	{
	case OutputType::Mp4:
		return AV_CODEC_ID_H264;
	case OutputType::Vp8:
		return AV_CODEC_ID_VP8;
	case OutputType::Vp9:
		return AV_CODEC_ID_VP9;
	default:
		throw_hresult(E_INVALIDARG);
	}
}

void FFmpegController::throw_av_error(int ret)
{
	if (ret < 0)
	{
		char err[AV_ERROR_MAX_STRING_SIZE];
		av_make_error_string(err, AV_ERROR_MAX_STRING_SIZE, ret);
		av_log(nullptr, AV_LOG_ERROR, "%s\n", err);

		throw_hresult(E_FAIL);
	}
}
#define check_av_result(cmd) do { if((ret = cmd) < 0) throw_av_error(ret); } while(0)
#define check_av_pointer(ptr) do { if(!(ptr)) { av_log(nullptr, AV_LOG_ERROR, "Pointer returned as null.\n"); throw_hresult(E_FAIL); } } while(0)

void FFmpegController::OpenInputVideo(const char* filenameUtf8, bool dumpFormat, FFmpegControllerThreadedType& threadType)
{
	int ret;

	check_av_result(avformat_open_input(&inputFormatContext, filenameUtf8, nullptr, nullptr));
	check_av_result(avformat_find_stream_info(&*inputFormatContext, nullptr));

	auto inputVideoStreamIndex = av_find_best_stream(&*inputFormatContext, AVMEDIA_TYPE_VIDEO, -1, -1, nullptr, 0);
	check_av_result(inputVideoStreamIndex);
	inputVideoStream = inputFormatContext->streams[inputVideoStreamIndex];

	auto inputCodec = avcodec_find_decoder(inputVideoStream->codecpar->codec_id);
	check_av_pointer(inputCodec);

	inputCodecContext = avcodec_alloc_context3(inputCodec);
	check_av_pointer(inputCodecContext);
	check_av_result(avcodec_parameters_to_context(&*inputCodecContext, inputVideoStream->codecpar));

	inputCodecContext->framerate = av_guess_frame_rate(&*inputFormatContext, inputVideoStream, nullptr);
	inputCodecContext->pkt_timebase = inputVideoStream->time_base;
	inputCodecContext->strict_std_compliance = FF_COMPLIANCE_EXPERIMENTAL;

	// multi-threaded decoding
	inputCodecContext->thread_count = 0;
	if (inputCodec->capabilities & AV_CODEC_CAP_FRAME_THREADS)
	{
		inputCodecContext->thread_type = FF_THREAD_FRAME;
		threadType = FFmpegControllerThreadedType::FrameThreads;
	}
	else if (inputCodec->capabilities & AV_CODEC_CAP_SLICE_THREADS)
	{
		inputCodecContext->thread_type = FF_THREAD_SLICE;
		threadType = FFmpegControllerThreadedType::SlideThreads;
	}
	else
	{
		inputCodecContext->thread_count = 1;
		threadType = FFmpegControllerThreadedType::SingleThread;
	}

	check_av_result(avcodec_open2(&*inputCodecContext, inputCodec, nullptr));

	mediaDuration = TimeSpan{ inputFormatContext->duration * 10 };
	frameRate = av_q2d(inputCodecContext->framerate);

	if (dumpFormat)
		av_dump_format(&*inputFormatContext, 0, filenameUtf8, 0);
}

void FFmpegController::SetupEncodingParameters(AVCodecContext& ctx, OutputType outputType, uint32_t crf)
{
	int ret;

	SYSTEM_INFO si;
	GetNativeSystemInfo(&si);
	ctx.thread_count = min(16, si.dwNumberOfProcessors);
	ctx.slices = 8;

	switch (outputType)
	{
	case OutputType::Mp4:
	{
		// crf, preset
		check_av_result(av_opt_set_int(ctx.priv_data, "crf", crf, 0));
		check_av_result(av_opt_set(ctx.priv_data, "preset", "medium", 0));
		break;
	}
	case OutputType::Vp8:
		throw_hresult(E_NOTIMPL);
		break;
	case OutputType::Vp9:
		// crf, bitrate = 0, speed = 1/2 (lower better), row-mt 1
		ctx.bit_rate = 0;
		ctx.qmin = max(crf - 2, 0);
		ctx.qmax = crf + 2;
		ctx.qcompress = 1;

		check_av_result(av_opt_set_int(ctx.priv_data, "crf", crf, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "speed", 2, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "row-mt", 1, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "lag-in-frames", 25, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "cpu-used", 4, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "auto-alt-ref", 1, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "arnr-maxframes", 7, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "arnr-strength", 4, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "aq-mode", 4, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "tile-columns", 6, 0));
		check_av_result(av_opt_set_int(ctx.priv_data, "tile-rows", 2, 0));
		break;
	}
}

TimeSpan FFmpegController::GetDurationFromFrameNumber(int64_t frameNumber) const
{
	return TimeSpanFromSeconds(frameNumber / frameRate);
}

int64_t FFmpegController::GetFrameNumberFromDuration(TimeSpan duration) const
{
	return llround(TimeSpanToSeconds(duration) * frameRate);
}

void FFmpegController::SetValidTrimmingRanges(const vector<TranscodeInputTrimmingMarkerEntry>& trimmingMarkers)
{
	validTrimmingRanges.clear();

	if (trimmingMarkers.empty())
		validTrimmingRanges.push_back({ {}, mediaDuration });
	else
	{
		// calculate the timespan of the first entry's frame number 
		auto lastTimeSpan = GetDurationFromFrameNumber(trimmingMarkers[0].FrameNumber());
		for (auto i = 1; i < trimmingMarkers.size(); ++i)
		{
			auto timeSpan = GetDurationFromFrameNumber(trimmingMarkers[i].FrameNumber());
			if (!trimmingMarkers[i - 1].TrimAfter())
				validTrimmingRanges.push_back({ lastTimeSpan, timeSpan });
			lastTimeSpan = timeSpan;
		}

		if (!trimmingMarkers[trimmingMarkers.size() - 1].TrimAfter())
			validTrimmingRanges.push_back({ lastTimeSpan, mediaDuration });
	}
}

void FFmpegController::OpenOutputVideo(const char* filenameUtf8, OutputType outputType, uint32_t crf,
	uint32_t width, uint32_t height, const char* encoderTitleUtf8,
	const std::vector<winrt::CuteVideoEditor_Video::TranscodeInputCropFrameEntry>& cropFrames,
	bool dumpFormat)
{
	int ret;

	check_av_result(avformat_alloc_output_context2(&outputFormatContext, nullptr, nullptr, filenameUtf8));
	outputFormatContext->avoid_negative_ts = AVFMT_AVOID_NEG_TS_MAKE_NON_NEGATIVE;
	check_av_result(av_dict_set(&outputFormatContext->metadata, "encoder-app", encoderTitleUtf8, 0));

	// build the codec
	auto outputCodec = avcodec_find_encoder(GetCodecId(outputType));
	check_av_pointer(outputCodec);

	check_av_pointer(outputCodecContext = avcodec_alloc_context3(outputCodec));

	SetupEncodingParameters(*outputCodecContext, outputType, crf);
	outputCodecContext->width = width;
	outputCodecContext->height = height;
	outputCodecContext->framerate = inputCodecContext->framerate;
	outputCodecContext->time_base = av_inv_q(inputCodecContext->framerate);
	outputCodecContext->gop_size = 600;
	outputCodecContext->max_b_frames = 2;
	outputCodecContext->pix_fmt = inputCodecContext->pix_fmt;
	outputCodecContext->strict_std_compliance = FF_COMPLIANCE_EXPERIMENTAL;

	if (outputFormatContext->oformat->flags & AVFMT_GLOBALHEADER)
		outputCodecContext->flags |= AV_CODEC_FLAG_GLOBAL_HEADER;

	check_av_result(avcodec_open2(&*outputCodecContext, outputCodec, nullptr));

	// video stream
	check_av_pointer(outputVideoStream = avformat_new_stream(&*outputFormatContext, nullptr));

	check_av_result(avcodec_parameters_from_context(outputVideoStream->codecpar, &*outputCodecContext));
	outputVideoStream->time_base = outputCodecContext->time_base;

	if (dumpFormat)
		av_dump_format(&*outputFormatContext, 0, filenameUtf8, 1);

	// open the output file
	if (!(outputFormatContext->oformat->flags & AVFMT_NOFILE))
		check_av_result(avio_open(&outputFormatContext->pb, filenameUtf8, AVIO_FLAG_WRITE));
	check_av_result(avformat_write_header(&*outputFormatContext, nullptr));

	// build the filter
	auto bufferSource = avfilter_get_by_name("buffer");
	auto bufferSink = avfilter_get_by_name("buffersink");
	check_av_pointer(bufferSource);
	check_av_pointer(bufferSink);

	auto args = std::format("video_size={}x{}:pix_fmt={}:time_base={}/{}:pixel_aspect={}/{}",
		inputCodecContext->width, inputCodecContext->height, (int)inputCodecContext->pix_fmt,
		inputCodecContext->pkt_timebase.num, inputCodecContext->pkt_timebase.den,
		inputCodecContext->sample_aspect_ratio.num, inputCodecContext->sample_aspect_ratio.den);

	check_av_result(avfilter_graph_create_filter(&bufferSourceContext, bufferSource, "in", args.c_str(), nullptr, &*filterGraph));
	check_av_result(avfilter_graph_create_filter(&bufferSinkContext, bufferSink, "out", nullptr, nullptr, &*filterGraph));
	check_av_result(av_opt_set_bin(bufferSinkContext, "pix_fmts",
		(uint8_t*)&outputCodecContext->pix_fmt, sizeof(outputCodecContext->pix_fmt), AV_OPT_SEARCH_CHILDREN));

	// endpoints for the filter graph
	filterInputs->name = av_strdup("out");
	filterInputs->filter_ctx = bufferSinkContext;
	filterInputs->pad_idx = 0;
	filterInputs->next = nullptr;

	filterOutputs->name = av_strdup("in");
	filterOutputs->filter_ctx = bufferSourceContext;
	filterOutputs->pad_idx = 0;
	filterOutputs->next = nullptr;

	// build the filter link
	auto filterSpec = std::format("[in]crop[cropped];[cropped]scale={}:{}[scaled];[scaled]setsar=1:1[out]",
		outputCodecContext->width, outputCodecContext->height);
	check_av_result(avfilter_graph_parse_ptr(&*filterGraph, filterSpec.c_str(), &filterInputs, &filterOutputs, nullptr));
	check_av_result(avfilter_graph_config(&*filterGraph, nullptr));

	check_av_pointer(cropFilterContext = avfilter_graph_get_filter(&*filterGraph, "Parsed_crop_0"));

	this->cropFrames = cropFrames;
}

TranscodeInputCropRectangle FFmpegController::GetCurrentCropRectangle()
{
	auto& cropFrame = cropFrames[cropFrameEntryIndex];
	assert(encodedFrameNumber >= cropFrame.FrameNumber());

	if (cropFrameEntryIndex == cropFrames.size() - 1)
		return cropFrame.CropRectangle();

	auto& nextCropFrame = cropFrames[cropFrameEntryIndex + 1];
	assert(encodedFrameNumber < nextCropFrame.FrameNumber());

	auto f = (double)(encodedFrameNumber - cropFrame.FrameNumber()) / (nextCropFrame.FrameNumber() - cropFrame.FrameNumber());
	auto center_x = cropFrame.CropRectangle().CenterX() + f * (nextCropFrame.CropRectangle().CenterX() - cropFrame.CropRectangle().CenterX());
	auto center_y = cropFrame.CropRectangle().CenterY() + f * (nextCropFrame.CropRectangle().CenterY() - cropFrame.CropRectangle().CenterY());
	auto width = cropFrame.CropRectangle().Width() + f * (nextCropFrame.CropRectangle().Width() - cropFrame.CropRectangle().Width());
	auto height = cropFrame.CropRectangle().Height() + f * (nextCropFrame.CropRectangle().Height() - cropFrame.CropRectangle().Height());

	return { (int)center_x, (int)center_y, (int)width, (int)height };
}

cppcoro::generator<AVFrame*> FFmpegController::EnumerateInputFrames()
{
	int ret;

	AutoReleasePtr<AVFrame, av_frame_free> inputFrame = av_frame_alloc();
	while (!flushing)
	{
		// end?
		if (validTrimmingRangeEntryIndex >= validTrimmingRanges.size())
			break;

		if ((ret = av_read_frame(&*inputFormatContext, &*inputPacket)) < 0)
			break;

		if (inputPacket->stream_index == inputVideoStream->index)
		{
			if ((ret = avcodec_send_packet(&*inputCodecContext, &*inputPacket)) < 0)
				break;

		process_flushed_frames:
			while (ret >= 0)
			{
				ret = avcodec_receive_frame(&*inputCodecContext, &*inputFrame);
				if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
					break;
				check_av_result(ret);

				inputFrame->pts = inputFrame->best_effort_timestamp;

				// handle trimming
				while (validTrimmingRangeEntryIndex < validTrimmingRanges.size() - 1
					&& GetDurationFromFrameNumber(inputFrameNumber) >= validTrimmingRanges[validTrimmingRangeEntryIndex].second)
				{
					++validTrimmingRangeEntryIndex;
				}
				++inputFrameNumber;
				if (validTrimmingRangeEntryIndex >= validTrimmingRanges.size()
					|| GetDurationFromFrameNumber(inputFrameNumber) >= validTrimmingRanges[validTrimmingRangeEntryIndex].second)
				{
					break;
				}
				else if (GetDurationFromFrameNumber(inputFrameNumber) < validTrimmingRanges[validTrimmingRangeEntryIndex].first)
					continue;

				// yield the frame
				co_yield &*inputFrame;
			}
		}

		av_packet_unref(&*inputPacket);
	}

	// start flushing
	if (!flushing)
	{
		// flush stuff
		check_av_result(avcodec_send_packet(&*inputCodecContext, nullptr));
		flushing = true;
		goto process_flushed_frames;
	}

	// yield a flush
	co_yield nullptr;
}

void FFmpegController::EncodeFrame(AVFrame* frame)
{
	int ret;

	if (!frame)
	{
		// flushing the filter graph
		WriteFilteredFrame(true);
		return;
	}

	// handle cropping
	while (cropFrameEntryIndex < cropFrames.size() - 1 && encodedFrameNumber >= cropFrames[cropFrameEntryIndex + 1].FrameNumber())
		++cropFrameEntryIndex;
	auto cropRectangle = GetCurrentCropRectangle();

	check_av_result(avfilter_graph_send_command(&*filterGraph, "Parsed_crop_0", "x", to_string(cropRectangle.CenterX() - cropRectangle.Width() / 2).c_str(), nullptr, 0, 0));
	check_av_result(avfilter_graph_send_command(&*filterGraph, "Parsed_crop_0", "y", to_string(cropRectangle.CenterY() - cropRectangle.Height() / 2).c_str(), nullptr, 0, 0));
	check_av_result(avfilter_graph_send_command(&*filterGraph, "Parsed_crop_0", "w", to_string(cropRectangle.Width()).c_str(), nullptr, 0, 0));
	check_av_result(avfilter_graph_send_command(&*filterGraph, "Parsed_crop_0", "h", to_string(cropRectangle.Height()).c_str(), nullptr, 0, 0));

	// push the frame through the filter
	check_av_result(av_buffersrc_add_frame_flags(bufferSourceContext, frame, 0));

	// pull filtered frames from the filter
	while (1)
	{
		ret = av_buffersink_get_frame(bufferSinkContext, &*filteredFrame);
		if (ret < 0)
		{
			// no more frames
			if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
				return;
			check_av_result(ret);
		}

		filteredFrame->time_base = av_buffersink_get_time_base(bufferSinkContext);
		filteredFrame->pict_type = AV_PICTURE_TYPE_NONE;

		WriteFilteredFrame(false);

		av_frame_unref(&*filteredFrame);
	}
}

void FFmpegController::WriteFilteredFrame(bool flush)
{
	int ret;

	av_packet_unref(&*outputPacket);

	if (filteredFrame->pts != AV_NOPTS_VALUE)
	{
		auto inputFrameNumber = av_rescale_q(filteredFrame->pts, filteredFrame->time_base, outputCodecContext->time_base);
		auto outputFrameNumber = encodedFrameNumber++;

		filteredFrame->pts = av_rescale_q(outputFrameNumber,
			av_mul_q(outputCodecContext->time_base, av_d2q(1 / frameRateMultiplier, INT_MAX)), outputVideoStream->time_base);
	}

	ret = avcodec_send_frame(&*outputCodecContext, flush ? nullptr : &*filteredFrame);
	while (ret >= 0)
	{
		ret = avcodec_receive_packet(&*outputCodecContext, &*outputPacket);
		if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
			return;
		check_av_result(ret);

		outputPacket->stream_index = 0;
		outputPacket->dts = 0;
		check_av_result(av_interleaved_write_frame(&*outputFormatContext, &*outputPacket));
	}
}

AVFrame* FFmpegController::GetTemporaryFrame(AVPixelFormat pix_fmt, int width, int height)
{
	if (auto it = find_if(temporaryFrames.begin(), temporaryFrames.end(),
		[=](AVFrame* frame) { return frame->format == pix_fmt && frame->width == width && frame->height == height; }); it != temporaryFrames.end())
	{
		auto frame = *it;
		temporaryFrames.erase(it);
		return frame;
	}

	int ret;

	auto frame = av_frame_alloc();
	check_av_pointer(frame);

	check_av_result(av_image_alloc(frame->data, frame->linesize, width, height, pix_fmt, 1));

	frame->width = width;
	frame->height = height;
	frame->format = pix_fmt;

	return frame;
}

void FFmpegController::ReleaseTemporaryFrame(AVFrame* frame)
{
	temporaryFrames.emplace_back(frame);
}

void FFmpegController::ConvertFrame(AVFrame* srcFrame, AVFrame* dstFrame)
{
	int ret;

	AutoReleasePtr<SwsContext, sws_freeContext> swsContext = sws_getContext(
		srcFrame->width, srcFrame->height, (AVPixelFormat)srcFrame->format,
		dstFrame->width, dstFrame->height, (AVPixelFormat)dstFrame->format,
		SWS_FAST_BILINEAR | SWS_FULL_CHR_H_INT | SWS_ACCURATE_RND, nullptr, nullptr, nullptr);
	check_av_pointer(swsContext);

	check_av_result(sws_scale(&*swsContext, srcFrame->data, srcFrame->linesize, 0, srcFrame->height, dstFrame->data, dstFrame->linesize));
}

AVFrame* FFmpegController::GetRgbaTemporaryFrame(AVFrame* frame)
{
	auto rgbaFrame = GetTemporaryFrame(AV_PIX_FMT_BGRA, frame->width, frame->height);
	ConvertFrame(frame, rgbaFrame);
	return rgbaFrame;
}

TimeSpan FFmpegController::GetFrameDuration(AVFrame* frame) const
{
	return TimeSpanFromSeconds(frame->duration * av_q2d(inputVideoStream->time_base));
}

bool FFmpegController::Seek(TimeSpan position)
{
	int ret;

	// convert the timespan to pts
	auto dPts = (TimeSpanToSeconds(position) - 1.0 / frameRate)
		* inputVideoStream->time_base.den / inputVideoStream->time_base.num;
	auto pts = llround(dPts);

	check_av_result(avformat_seek_file(&*inputFormatContext, inputVideoStream->index, INT64_MIN, pts, pts, AVSEEK_FLAG_BACKWARD));
	avcodec_flush_buffers(&*inputCodecContext);

	// fast forward until the expected timestamp
	AutoReleasePtr<AVPacket, av_packet_unref> packet = av_packet_alloc();
	AutoReleasePtr<AVFrame, av_frame_free> frame = av_frame_alloc();
	while (true)
	{
		check_av_result(av_read_frame(&*inputFormatContext, &*packet));
		if (packet->stream_index == inputVideoStream->index)
		{
			check_av_result(avcodec_send_packet(&*inputCodecContext, &*packet));
			while (true)
			{
				ret = avcodec_receive_frame(&*inputCodecContext, &*frame);
				if (ret == AVERROR(EAGAIN) || ret == AVERROR_EOF)
					break;
				check_av_result(ret);

				if (frame->best_effort_timestamp >= pts)
				{
					// found the frame, stop here
					inputFrameNumber = llround(1 + frame->best_effort_timestamp
						* frameRate * inputVideoStream->time_base.num / inputVideoStream->time_base.den);
					validTrimmingRangeEntryIndex = 0;
					return true;
				}
			}
		}
	}

	// we couldn't find the frame
	return false;
}

FFmpegController::~FFmpegController()
{
	for (auto& frame : temporaryFrames)
	{
		av_freep(&frame->data[0]);
		av_frame_free(&frame);
	}
	temporaryFrames.clear();

	// write trailer and close the writer for open output videos
	if (outputFormatContext)
	{
		int ret;
		check_av_result(av_write_trailer(&*outputFormatContext));
		check_av_result(avio_closep(&outputFormatContext->pb));
	}
}
