#pragma once
#include <asyncpp/detail/std_import.h>
#include <asyncpp/ref.h>

#include <cassert>
#include <condition_variable>
#include <cstddef>
#include <functional>
#include <memory>
#include <mutex>
#include <variant>
#include <vector>

namespace asyncpp {

	template<typename TResult>
	class promise;

	namespace detail {
		template<typename T, typename TError>
		struct promise_state : intrusive_refcount<promise_state<T, TError>> {
			inline constexpr static size_t pending_index = 0;
			inline constexpr static size_t fulfilled_index = 1;
			inline constexpr static size_t rejected_index = 2;

			std::mutex m_mtx{};
			std::condition_variable m_cv{};
			std::variant<std::monostate, T, TError> m_value{};
			std::vector<std::function<void()>> m_on_settle{};
			std::vector<std::function<void(const T&)>> m_on_fulfill{};
			std::vector<std::function<void(const TError&)>> m_on_reject{};

			bool try_fulfill(auto&& value) {
				std::unique_lock lck{m_mtx};
				if (m_value.index() != pending_index) return false;
				m_value.template emplace<fulfilled_index>(std::forward<decltype(value)>(value));
				m_cv.notify_all();
				auto settle_cbs = std::move(m_on_settle);
				auto fulfill_cbs = std::move(m_on_fulfill);
				m_on_settle.clear();
				m_on_fulfill.clear();
				m_on_reject.clear();
				auto& res = std::get<fulfilled_index>(m_value);
				lck.unlock();
				for (auto& cbfn : settle_cbs) {
					try {
						cbfn();
					} catch (...) { std::terminate(); }
				}
				for (auto& cbfn : fulfill_cbs) {
					try {
						cbfn(res);
					} catch (...) { std::terminate(); }
				}
				return true;
			}

			bool try_reject(auto&& value) {
				std::unique_lock lck{m_mtx};
				if (m_value.index() != pending_index) return false;
				m_value.template emplace<rejected_index>(std::forward<decltype(value)>(value));
				m_cv.notify_all();
				auto settle_cbs = std::move(m_on_settle);
				auto reject_cbs = std::move(m_on_reject);
				m_on_settle.clear();
				m_on_fulfill.clear();
				m_on_reject.clear();
				auto& error = std::get<rejected_index>(m_value);
				lck.unlock();
				for (auto& cbfn : settle_cbs) {
					try {
						cbfn();
					} catch (...) { std::terminate(); }
				}
				for (auto& cbfn : reject_cbs) {
					try {
						cbfn(error);
					} catch (...) { std::terminate(); }
				}
				return true;
			}

			void then(std::function<void(const T&)> then_cb, std::function<void(const TError&)> catch_cb) {
				std::unique_lock lck{m_mtx};
				switch (m_value.index()) {
				case pending_index:
					if (then_cb) m_on_fulfill.emplace_back(std::move(then_cb));
					if (catch_cb) m_on_reject.emplace_back(std::move(catch_cb));
					break;
				case fulfilled_index:
					lck.unlock();
					if (then_cb) then_cb(std::get<fulfilled_index>(m_value));
					break;
				case rejected_index:
					lck.unlock();
					if (catch_cb) catch_cb(std::get<rejected_index>(m_value));
					break;
				}
			}

			void on_settle(std::function<void()> settle_cb) {
				if (!settle_cb) return;
				std::unique_lock lck{m_mtx};
				if (m_value.index() == pending_index)
					m_on_settle.emplace_back(std::move(settle_cb));
				else {
					lck.unlock();
					settle_cb();
				}
			}
		};

		template<typename T>
		struct is_promise : std::false_type {};
		template<typename T>
		struct is_promise<promise<T>> : std::true_type {};
	} // namespace detail

	/**
     * \brief Promise type that allows waiting for a result in both synchronous and asynchronous code.
     * \tparam TResult Type of the result
     */
	template<typename TResult>
	class promise {
	protected:
		using state = detail::promise_state<TResult, std::exception_ptr>;
		ref<state> m_state{};

	public:
		using result_type = TResult;

		/// \brief Construct a new promise object in its pending state
		promise() : m_state{make_ref<state>()} {}
		/// \brief Copy constructor
		promise(const promise& other) : m_state{other.m_state} {}
		/// \brief Copy assignment
		promise& operator=(const promise& other) {
			if (&other != this) m_state = other.m_state;
			return *this;
		}

		/**
         * \brief Check if the promise is pending
         * \note This is a temporary snapshot and should only be used for logging. Consider the returned value potentially invalid by the moment the call returns.
         */
		bool is_pending() const noexcept {
			std::unique_lock lck{m_state->m_mtx};
			return m_state->m_value.index() == state::pending_index;
		}

		/**
         * \brief Check if the promise is fulfilled
         * \note Unlike is_pending() this value is permanent.
         * \return true if the promise contains a value.
         */
		bool is_fulfilled() const noexcept {
			std::unique_lock lck{m_state->m_mtx};
			return m_state->m_value.index() == state::fulfilled_index;
		}

		/**
         * \brief Check if the promise is rejected
         * \note Unlike is_pending() this value is permanent.
         * \return true if the promise contains an exception.
         */
		bool is_rejected() const noexcept {
			std::unique_lock lck{m_state->m_mtx};
			return m_state->m_value.index() == state::rejected_index;
		}

		/**
         * \brief Fulfill the promise with a value.
         * \throw std::logic_error if the promise is already fulfilled or rejected.
         * \param value The value to store inside the promise
         * \note Callbacks and waiting coroutines are resumed inside this call.
         */
		void fulfill(auto&& value) {
			if (!m_state->try_fulfill(std::forward<decltype(value)>(value)))
				throw std::logic_error("promise is not pending");
		}

		/**
         * \brief Try to fulfill the promise with a value.
         * \return True if the promise was fulfilled, false if the promise was not pending
         * \param value The value to store inside the promise
         * \note Callbacks and waiting coroutines are resumed inside this call.
         */
		bool try_fulfill(auto&& value) { return m_state->try_fulfill(std::forward<decltype(value)>(value)); }

		/**
         * \brief Reject the promise with an exception
         * \throw std::logic_error if the promise is already fulfilled or rejected
         * \param ex The exception to use for rejection
         * \note Callbacks and waiting coroutines are resumed inside this call
         */
		void reject(std::exception_ptr error) {
			if (!m_state->try_reject(std::forward<decltype(error)>(error)))
				throw std::logic_error("promise is not pending");
		}

		/**
         * \brief Try to reject the promise with an exception
         * \return True if the promise was rejected, false if the promise was not pending
         * \param ex The exception to use for rejection
         * \note Callbacks and waiting coroutines are resumed inside this call
         */
		bool try_reject(std::exception_ptr error) { return m_state->try_reject(std::forward<decltype(error)>(error)); }

		/**
         * \brief Reject the promise with an exception
         * \throw std::logic_error if the promise is already fulfilled or rejected
         * \tparam TException The exception type to use for rejection
         * \param args Arguments passed to the constructor of the exception type
         * \note Callbacks and waiting coroutines are resumed inside this call
         */
		template<typename TException, typename... Args>
		void reject(Args&&... args) {
			reject(std::make_exception_ptr(TException{std::forward<Args>(args)...}));
		}

		/**
         * \brief Try to reject the promise with an exception
         * \return True if the promise was rejected, false if the promise was not pending
         * \tparam TException The exception type to use for rejection
         * \param args Arguments passed to the constructor of the exception type
         * \note Callbacks and waiting coroutines are resumed inside this call
         */
		template<typename TException, typename... Args>
		bool try_reject(Args&&... args) {
			return try_reject(std::make_exception_ptr(TException{std::forward<Args>(args)...}));
		}

		/**
         * \brief Register a callback to be executed once a result is available. If the promise is not pending the callback is directly executed.
         * \param cb Callback to invoke as soon as a result is available.
         */
		void then(std::function<void(const TResult&)> then_cb,
				  std::function<void(const std::exception_ptr&)> catch_cb) {
			m_state->then(std::move(then_cb), std::move(catch_cb));
		}

		/**
         * \brief Register a callback to be executed once a result is available. If the promise is not pending the callback is directly executed.
         * \param cb Callback to invoke as soon as a result is available.
         */
		void on_settle(std::function<void()> settle_cb) { m_state->on_settle(std::move(settle_cb)); }

		/**
         * \brief Synchronously get the result. If the promise is rejected the rejecting exception gets thrown.
         * \return TResult& Reference to the result value
         */
		[[nodiscard]] TResult& get() const {
			state& info = *m_state;
			std::unique_lock lck{info.m_mtx};
			while (info.m_value.index() == state::pending_index) {
				info.m_cv.wait(lck);
			}
			if (info.m_value.index() == state::fulfilled_index) return std::get<state::fulfilled_index>(info.m_value);
			std::rethrow_exception(std::get<state::rejected_index>(info.m_value));
		}

		/**
         * \brief Synchronously get the result with a timeout. If the promise is rejected the rejecting exception gets thrown.
         * \return TResult* Pointer to the result value or nullptr on timeout
         */
		template<class Rep, class Period>
		[[nodiscard]] TResult* get(std::chrono::duration<Rep, Period> timeout) const {
			auto until = std::chrono::steady_clock::now() + timeout;
			state& info = *m_state;
			std::unique_lock lck{info.m_mtx};
			if (!info.m_cv.wait_until(lck, until, [&info]() { return info.m_value.index() != state::pending_index; }))
				return nullptr;
			if (info.m_value.index() == state::fulfilled_index) return &std::get<state::fulfilled_index>(info.m_value);
			std::rethrow_exception(std::get<state::rejected_index>(info.m_value));
		}

		/**
         * \brief Synchronously try get the result. If the promise is rejected the rejecting exception gets thrown.
         * \return TResult* Pointer to the result value or nullptr on timeout
         */
		[[nodiscard]] std::pair<TResult*, std::exception_ptr> try_get(std::nothrow_t) const noexcept {
			state& info = *m_state;
			std::unique_lock lck{info.m_mtx};
			if (info.m_value.index() == state::pending_index) return {nullptr, nullptr};
			if (info.m_value.index() == state::fulfilled_index)
				return {&std::get<state::fulfilled_index>(info.m_value), nullptr};
			return {nullptr, std::get<state::rejected_index>(info.m_value)};
		}

		/**
         * \brief Synchronously try get the result. If the promise is rejected the rejecting exception gets thrown.
         * \return TResult* Pointer to the result value or nullptr on timeout
         */
		[[nodiscard]] TResult* try_get() const {
			auto res = try_get(std::nothrow);
			if (res.second) std::rethrow_exception(std::move(res.second));
			return res.first;
		}

		/**
         * \brief Asynchronously get the result. If the promise is rejected the rejecting exception gets thrown.
         * \return TResult& Pointer to the result value
         */
		[[nodiscard]] auto operator co_await() const noexcept {
			struct awaiter {
				constexpr explicit awaiter(ref<state> state) : m_state(std::move(state)) {}
				[[nodiscard]] constexpr bool await_ready() noexcept {
					assert(m_state);
					std::unique_lock lck{m_state->m_mtx};
					return m_state->m_value.index() != state::pending_index;
				}
				[[nodiscard]] bool await_suspend(coroutine_handle<> hndl) noexcept {
					assert(m_state);
					assert(hndl);
					std::unique_lock lck{m_state->m_mtx};
					if (m_state->m_value.index() == state::pending_index) {
						m_state->m_on_settle.emplace_back([hndl]() mutable { hndl.resume(); });
						return true;
					}
					return false;
				}
				[[nodiscard]] TResult& await_resume() {
					assert(m_state);
					std::unique_lock lck{m_state->m_mtx};
					assert(m_state->m_value.index() != state::pending_index);
					if (m_state->m_value.index() == state::fulfilled_index)
						return std::get<state::fulfilled_index>(m_state->m_value);
					std::rethrow_exception(std::get<state::rejected_index>(m_state->m_value));
				}

			private:
				ref<state> m_state;
			};
			assert(this->m_state);
			return awaiter{m_state};
		}

		/**
         * \brief Get a fufilled promise with the specified value
         * \param value Value for the fulfilled promise
         * \return A promise in its fulfilled state
         */
		[[nodiscard]] static promise make_fulfilled(TResult&& value) {
			promise res;
			res.fulfill(std::forward<decltype(value)>(value));
			return res;
		}

		/**
         * \brief Get a rejected promise with the specified exception
         * \param exception Exception to store in the rejected promise
         * \return A promise in its rejected state
         */
		[[nodiscard]] static promise make_rejected(std::exception_ptr exception) {
			promise res;
			res.reject(std::move(exception));
			return res;
		}

		/**
         * \brief Get a rejected promise with the specified exception
         * \tparam TException The type of the exception to store
         * \param args Parameters to pass to the exception type constructor
         * \return A promise in its rejected state
         */
		template<typename TException, typename... Args>
		[[nodiscard]] static promise make_rejected(Args&&... args) {
			promise res;
			res.reject<TException, Args...>(std::forward<Args>(args)...);
			return res;
		}
	};

	/**
     * \brief Promise type that allows waiting for a result in both synchronous and asynchronous code.
     * \tparam TResult Type of the result
     */
	template<>
	class promise<void> : private promise<std::monostate> {
	public:
		using result_type = void;

		/// \brief Construct a new promise object in its pending state
		promise() = default;
		/// \brief Copy constructor
		promise(const promise& other) = default;
		/// \brief Copy assignment
		promise& operator=(const promise& other) = default;

		using promise<std::monostate>::is_pending;
		using promise<std::monostate>::is_fulfilled;
		using promise<std::monostate>::is_rejected;
		using promise<std::monostate>::reject;
		using promise<std::monostate>::try_reject;
		using promise<std::monostate>::on_settle;

		void fulfill() { promise<std::monostate>::fulfill(std::monostate{}); }
		[[nodiscard]] bool try_fulfill() { return promise<std::monostate>::try_fulfill(std::monostate{}); }

		void then(std::function<void()> then_cb, std::function<void(const std::exception_ptr&)> catch_cb) {
			std::function<void(const std::monostate&)> cbfn{};
			if (then_cb) cbfn = [cbfn = std::move(then_cb)](const std::monostate&) { cbfn(); };
			promise<std::monostate>::then(cbfn, std::move(catch_cb));
		}

		void get() const { static_cast<void>(promise<std::monostate>::get()); }
		template<class Rep, class Period>
		[[nodiscard]] bool get(std::chrono::duration<Rep, Period> timeout) const {
			return promise<std::monostate>::get(timeout) != nullptr;
		}

		[[nodiscard]] std::pair<bool, std::exception_ptr> try_get(std::nothrow_t) const noexcept {
			auto res = promise<std::monostate>::try_get(std::nothrow);
			if (res.second) return {true, res.second};
			return {res.first != nullptr, nullptr};
		}
		[[nodiscard]] bool try_get() const {
			auto res = promise<std::monostate>::try_get(std::nothrow);
			if (res.second) std::rethrow_exception(std::move(res.second));
			return res.first != nullptr;
		}

		[[nodiscard]] auto operator co_await() const noexcept {
			struct awaiter {
			private:
				decltype(std::declval<promise<std::monostate>>().operator co_await()) m_awaiter;

			public:
				constexpr explicit awaiter(decltype(m_awaiter)&& awaiter)
					: m_awaiter(std::forward<decltype(awaiter)>(awaiter)) {}
				[[nodiscard]] constexpr bool await_ready() noexcept { return m_awaiter.await_ready(); }
				[[nodiscard]] bool await_suspend(coroutine_handle<> hndl) noexcept {
					return m_awaiter.await_suspend(hndl);
				}
				void await_resume() { static_cast<void>(m_awaiter.await_resume()); }
			};
			return awaiter{promise<std::monostate>::operator co_await()};
		}

		[[nodiscard]] static promise make_fulfilled() {
			promise res;
			res.fulfill();
			return res;
		}
		[[nodiscard]] static promise make_rejected(std::exception_ptr exception) {
			promise res;
			res.reject(std::move(exception));
			return res;
		}
		template<typename TException, typename... Args>
		[[nodiscard]] static promise make_rejected(Args&&... args) {
			promise res;
			res.reject<TException, Args...>(std::forward<Args>(args)...);
			return res;
		}
	};

	/**
	 * \brief Return a promise that is fulfilled/rejected once the first of
	 *		  the specified promises is fulfilled or rejected. This can be
	 *		  used to start multiple operations and continue once the
	 *		  the first one is ready. The results of the remaining promises
	 *		  are ignored.
	 * \tparam T The type of the returned promise
	 * \tparam TArgs The type of the promises
	 * \param args The promises to wait for
	 * \return A promise that copies the state of the first finished argument.
	 */
	template<typename T, typename... TArgs>
	inline promise<T> promise_first(promise<TArgs>... args) {
		promise<T> result_promise;
		(args.then(
			 [result_promise](const typename promise<TArgs>::result_type& res) mutable {
				 result_promise.try_fulfill(res);
			 },
			 [result_promise](const std::exception_ptr& exception) mutable { result_promise.try_reject(exception); }),
		 ...);
		return result_promise;
	}

	/**
	 * \brief Return a promise that is fulfilled once the first of the specified
	 *		  promises is fulfilled. This can be used to start multiple operations
	 *		  and continue once the the first successful is ready. If none of the
	 *         promises gets fulfilled the exception of the last failed promise is returned.
	 *		  The results of the remaining promises are ignored.
	 * \tparam T The type of the returned promise
	 * \tparam TArgs The type of the promises
	 * \param args The promises to wait for
	 * \return A promise that copies the state of the first successful argument.
	 */
	template<typename T, typename... TArgs>
	static promise<T> promise_first_successful(promise<TArgs>... args) {
		promise<T> result_promise;
		auto finished = std::make_shared<std::atomic<size_t>>(0);
		(args.then(
			 [result_promise, finished](const typename promise<TArgs>::result_type& res) mutable {
				 finished->fetch_add(1);
				 result_promise.try_fulfill(res);
			 },
			 [result_promise, finished](const std::exception_ptr& exception) mutable {
				 if (finished->fetch_add(1) + 1 == sizeof...(args)) result_promise.try_reject(exception);
			 }),
		 ...);
		return result_promise;
	}

	/**
	 * \brief Return a promise that is fulfilled once all the specified promises are fulfilled.
	 *		  This can be used to start multiple operations in parallel and collect the results.
	 * \tparam TArgs The type of the promises
	 * \param args The promises to wait for
	 * \return A promise that gets fulfilled once all are finished.
	 */
	template<typename... TArgs>
	static promise<void> promise_all(promise<TArgs>... args) {
		struct state {
			std::atomic<size_t> count{};
			promise<void> result;
		};
		auto shared = std::make_shared<state>();
		(args.on_settle([shared]() mutable {
			auto curid = shared->count.fetch_add(1);
			if (curid + 1 == sizeof...(args)) { shared->result.fulfill(); }
		}),
		 ...);
		return shared->result;
	}
} // namespace asyncpp
