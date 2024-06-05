#pragma once
#include <algorithm>
#include <cassert>
#include <cstddef>
#include <cstdint>
#include <type_traits>
#include <utility>

namespace asyncpp {

	/**
	 * \brief Add a tag to a pointer
	 * \tparam ID The tag to add
	 * \tparam T The type of the pointer
	 * \param v The pointer to tag
	 * \return The tagged pointer
	 */
	template<size_t ID, typename T>
	void* ptr_tag(T* ptr) noexcept
		requires(alignof(T) > ID)
	{
		assert((reinterpret_cast<uintptr_t>(ptr) & (alignof(T) - 1)) == 0);
		//NOLINTNEXTLINE(performance-no-int-to-ptr)
		return reinterpret_cast<void*>(reinterpret_cast<uintptr_t>(ptr) | ID);
	}

	/**
	 * \brief Add a enum tag to a pointer
	 * \tparam ID The tag to add
	 * \tparam T The type of the pointer
	 * \param v The pointer to tag
	 * \return The tagged pointer
	 */
	template<auto ID, typename T>
	void* ptr_tag(T* ptr) noexcept
		requires(std::is_enum_v<decltype(ID)>)
	{
		return ptr_tag<static_cast<size_t>(ID), T>(ptr);
	}

	/**
	 * \brief Add a tag to a pointer
	 * \tparam ID The tag to add
	 * \tparam T The type of the pointer
	 * \param v The pointer to tag
	 * \return The tagged pointer
	 */
	template<size_t ID, typename T>
	const void* ptr_tag(const T* ptr) noexcept
		requires(alignof(T) > ID)
	{
		assert((reinterpret_cast<uintptr_t>(ptr) & (alignof(T) - 1)) == 0);
		//NOLINTNEXTLINE(performance-no-int-to-ptr)
		return reinterpret_cast<const void*>(reinterpret_cast<uintptr_t>(ptr) | ID);
	}

	/**
	 * \brief Add a enum tag to a pointer
	 * \tparam ID The tag to add
	 * \tparam T The type of the pointer
	 * \param v The pointer to tag
	 * \return The tagged pointer
	 */
	template<auto ID, typename T>
	const void* ptr_tag(const T* ptr) noexcept
		requires(std::is_enum_v<decltype(ID)>)
	{
		return ptr_tag<static_cast<size_t>(ID), T>(ptr);
	}

	/**
	 * \brief Split a pointer back into the original pointer and tag.
	 * \tparam T The type of the pointer
	 * \param v The tagged pointer
	 * \return pair of pointer and tag
	 */
	template<typename T>
	std::pair<T*, size_t> ptr_untag(void* ptr) noexcept {
		const auto align_mask = static_cast<uintptr_t>(alignof(T) - 1);
		auto uptr = reinterpret_cast<uintptr_t>(ptr);
		return {reinterpret_cast<T*>(uptr & ~align_mask), uptr & align_mask};
	}

	/**
	 * \brief Split a pointer back into the original pointer and enum tag.
	 * \tparam T The type of the pointer
	 * \tparam TTag The type of the tag
	 * \param v The tagged pointer
	 * \return pair of pointer and tag
	 */
	template<typename T, typename TTag>
	std::pair<T*, TTag> ptr_untag(void* vptr) noexcept {
		auto [ptr, tag] = ptr_untag<T>(vptr);
		return {ptr, static_cast<TTag>(tag)};
	}

	/**
	 * \brief Split a pointer back into the original pointer and tag.
	 * \tparam T The type of the pointer
	 * \param v The tagged pointer
	 * \return pair of pointer and tag
	 */
	template<typename T>
	std::pair<const T*, size_t> ptr_untag(const void* vptr) noexcept {
		const auto align_mask = static_cast<uintptr_t>(alignof(T) - 1);
		auto uptr = reinterpret_cast<uintptr_t>(vptr);
		return {reinterpret_cast<const T*>(uptr & ~align_mask), uptr & align_mask};
	}

	/**
	 * \brief Split a pointer back into the original pointer and enum tag.
	 * \tparam T The type of the pointer
	 * \tparam TTag The type of the tag
	 * \param v The tagged pointer
	 * \return pair of pointer and tag
	 */
	template<typename T, typename TTag>
	std::pair<const T*, TTag> ptr_untag(const void* vptr) noexcept {
		auto [ptr, tag] = ptr_untag<T>(vptr);
		return {ptr, static_cast<TTag>(tag)};
	}

	/**
	 * \brief Get the smallest alignment of a number of types
	 * \return The smallest required alignment.
	 */
	template<typename T1, typename... TExtra>
	constexpr size_t min_alignof() noexcept {
		if constexpr (sizeof...(TExtra) > 0)
			return std::min(alignof(T1), min_alignof<TExtra...>());
		else
			return alignof(T1);
	}

	/**
	 * \brief Get the tag of a tagged pointer which could be a number of types.
	 * \param v The tagged pointer
	 * \return The tag associated with the pointer
	 * \note This is useful if the tag is used to decide the type of the pointer.
	 */
	template<typename... T>
	size_t ptr_get_tag(const void* vptr) noexcept {
		constexpr auto align_mask = static_cast<uintptr_t>(min_alignof<T...>() - 1);
		return reinterpret_cast<uintptr_t>(vptr) & align_mask;
	}

} // namespace asyncpp
