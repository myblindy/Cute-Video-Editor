#pragma once
#define _CRT_SECURE_NO_WARNINGS

#include <windows.h>
#include <unknwn.h>
#include <restrictederrorinfo.h>
#include <hstring.h>

#include <functional>
#include <format>
#include <mutex>

// prevent compiler warnings due to name conflicts
#pragma push_macro("GetCurrentTime")
#pragma push_macro("TRY")
#undef GetCurrentTime
#undef TRY

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.ApplicationModel.Activation.h>
#include <winrt/Windows.Graphics.Imaging.h>
#include <winrt/Windows.UI.Xaml.Interop.h>
//#include <winrt/Microsoft.UI.Composition.h>
//#include <winrt/Microsoft.UI.Xaml.h>
//#include <winrt/Microsoft.UI.Xaml.Controls.h>
//#include <winrt/Microsoft.UI.Xaml.Controls.Primitives.h>
//#include <winrt/Microsoft.UI.Xaml.Data.h>
//#include <winrt/Microsoft.UI.Xaml.Interop.h>
//#include <winrt/Microsoft.UI.Xaml.Markup.h>
//#include <winrt/Microsoft.UI.Xaml.Media.h>
//#include <winrt/Microsoft.UI.Xaml.Navigation.h>
//#include <winrt/Microsoft.UI.Xaml.Shapes.h>
//#include <winrt/Microsoft.UI.Dispatching.h>
#include <wil/cppwinrt_helpers.h>

#pragma pop_macro("TRY")
#pragma pop_macro("GetCurrentTime")

#pragma warning(disable : 4244)

#include <d3d11.h>

extern "C"
{
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libswscale/swscale.h>
#include <libswresample/swresample.h>
#include <libavutil/imgutils.h>
#include <libavutil/opt.h>
#include <libavutil/hwcontext_d3d11va.h>
#include <libavfilter/buffersink.h>
#include <libavfilter/buffersrc.h>
#include <libavfilter/avfilter.h>
}

#include "stb_image.h"

#define STBIW_WINDOWS_UTF8
#include "stb_image_write.h"

#pragma warning(default : 4244)

#include "StringUtils.h"
#include "autoreleaseptr.h"
#include <asyncpp/generator.h>

// Disable debug string output on non-debug build
#if !_DEBUG
#define DebugMessage(x)
#else
#define DebugMessage(x) OutputDebugString(x)
#endif

template<class T>
std::vector<T> inline to_vector(winrt::Windows::Foundation::Collections::IVector<T> input)
{
	return to_vector(input.GetView());
}

template<class T>
std::vector<T> inline to_vector(winrt::Windows::Foundation::Collections::IVectorView<T> input)
{
	std::vector<T> output;
	for (auto i : input)
		output.emplace_back(i);
	return output;
}

// Creates a weak handler function proxy to the passed instance function (two arguments, e.g. event handler).
// The class T must implement enable_shared_from_this!
template<class T, typename TSender, typename TArgs>
std::function<void(TSender, TArgs)> inline weak_handler(T* instance, void(T::* instanceMethod)(TSender, TArgs))
{
	auto wr = instance->weak_from_this();
	auto handler = [wr, instanceMethod](TSender sender, TArgs args)
		{
			auto instanceLocked = std::dynamic_pointer_cast<T>(wr.lock());
			if (instanceLocked)
			{
				(instanceLocked.get()->*instanceMethod)(sender, args);
			}
		};
	return handler;
}

// Creates a weak handler function proxy to the passed instance function (no arguments, e.g. dispatcher handler).
// The class T must implement enable_shared_from_this!
template<class T>
std::function<void()> inline weak_handler(T* instance, void(T::* instanceMethod)())
{
	std::weak_ptr<T> wr = instance->weak_from_this();
	auto handler = [wr, instanceMethod]()
		{
			auto instanceLocked = wr.lock();
			if (instanceLocked)
			{
				(instanceLocked.get()->*instanceMethod)();
			}
		};
	return handler;
}

winrt::Windows::Foundation::TimeSpan inline TimeSpanFromSeconds(double seconds)
{
	return std::chrono::duration_cast<winrt::Windows::Foundation::TimeSpan>(
		std::chrono::nanoseconds(static_cast<int64_t>(seconds * 1e9)));
}

double inline TimeSpanToSeconds(winrt::Windows::Foundation::TimeSpan timeSpan)
{
	return std::chrono::duration_cast<std::chrono::duration<double>>(timeSpan).count();
}