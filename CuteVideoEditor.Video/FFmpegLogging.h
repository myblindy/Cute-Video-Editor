#pragma once

#include "FFmpegLogging.g.h"

namespace winrt::CuteVideoEditor_Video::implementation
{
	struct FFmpegLogging
	{
		static CuteVideoEditor_Video::LogLevel LogLevel() { return logLevel; }
		static void LogLevel(CuteVideoEditor_Video::LogLevel const& value);

		static CuteVideoEditor_Video::IFFmpegLogProvider LogProvider() { return logProvider; }
		static void LogProvider(CuteVideoEditor_Video::IFFmpegLogProvider const& value);

	private:
		static CuteVideoEditor_Video::LogLevel logLevel;
		static CuteVideoEditor_Video::IFFmpegLogProvider logProvider;

		static std::mutex io_mutex;
		static std::string line;
	};
}

namespace winrt::CuteVideoEditor_Video::factory_implementation
{
	struct FFmpegLogging : FFmpegLoggingT<FFmpegLogging, implementation::FFmpegLogging>
	{
	};
}
