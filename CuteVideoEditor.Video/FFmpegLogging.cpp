#include "pch.h"
#include "FFmpegLogging.h"

#include "FFmpegLogging.g.cpp"

using namespace std;

namespace winrt::CuteVideoEditor_Video::implementation
{
	mutex FFmpegLogging::io_mutex;
	string FFmpegLogging::line;

	CuteVideoEditor_Video::LogLevel FFmpegLogging::logLevel = (CuteVideoEditor_Video::LogLevel)av_log_get_level();
	void FFmpegLogging::LogLevel(CuteVideoEditor_Video::LogLevel const& value)
	{
		logLevel = value;
		av_log_set_level(static_cast<int>(value));
	}

	CuteVideoEditor_Video::IFFmpegLogProvider FFmpegLogging::logProvider{};
	void FFmpegLogging::LogProvider(CuteVideoEditor_Video::IFFmpegLogProvider const& value)
	{
		logProvider = value;

		av_log_set_callback([](void* ptr, int level, const char* fmt, va_list vl)
			{
				if (auto logProvider = FFmpegLogging::logProvider; level <= (int)logLevel)
				{
					char pLine[1024];
					int printPrefix = 1;
					av_log_format_line(ptr, level, fmt, vl, pLine, sizeof(pLine), &printPrefix);

					lock_guard<mutex> lock(io_mutex);

					// concatenate the (partial) result
					line += pLine;

					// send if the line ends with a new line
					if (!line.empty() && line[line.size() - 1] == '\n')
					{
						line.resize(line.size() - 1);
						logProvider.Log((CuteVideoEditor_Video::LogLevel)level, StringUtils::Utf8ToPlatformString(line.c_str()));
						line.clear();
					}
				}
			});
	}
}
