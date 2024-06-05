#pragma once
/**
 * \file async_main.h
 * \brief Provides a main function running a simple_dispatcher and awaits the function async_main, stopping after it is done.
 */

#include <asyncpp/fire_and_forget.h>
#include <asyncpp/simple_dispatcher.h>
#include <asyncpp/task.h>

asyncpp::task<int> async_main(int argc, const char** argv);
//NOLINTNEXTLINE(misc-definitions-in-headers)
int main(int argc, const char** argv) {
	using asyncpp::eager_fire_and_forget_task;
	using asyncpp::simple_dispatcher;
	simple_dispatcher disp;
	std::pair<int, std::exception_ptr> result{-1, nullptr};
	disp.push([&]() {
		[](simple_dispatcher* disp, std::pair<int, std::exception_ptr>& result, int argc,
		   const char** argv) -> eager_fire_and_forget_task<> {
			try {
				result.first = co_await async_main(argc, argv);
			} catch (...) {
				result.second = std::current_exception();
				result.first = -1;
			}
			disp->stop();
		}(&disp, result, argc, argv);
	});
	disp.run();
	if (result.second) std::rethrow_exception(result.second);
	return result.first;
}
