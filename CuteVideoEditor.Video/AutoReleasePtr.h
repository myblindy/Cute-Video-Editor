template <typename T, auto ReleaseFunc>
struct AutoReleasePtr
{
	T* ptr = nullptr;

	AutoReleasePtr() = default;
	AutoReleasePtr(T* p) : ptr(p) { }

	T** operator&() { return &ptr; }
	T* operator->() { return ptr; }
	T& operator*() { return *ptr; }
	operator bool() const { return ptr; }

	// remove copy semantics
	AutoReleasePtr(const AutoReleasePtr&) = delete;
	AutoReleasePtr& operator=(const AutoReleasePtr&) = delete;

	// move semantics
	AutoReleasePtr(AutoReleasePtr&& other) noexcept
	{
		ptr = other.ptr;
		other.ptr = nullptr;
	}
	AutoReleasePtr& operator=(AutoReleasePtr&& other) noexcept
	{
		if (ptr != other.ptr)
		{
			Cleanup();
			ptr = other.ptr;
			other.ptr = nullptr;
		}

		return *this;
	}

private:
	void Cleanup()
	{
		if (ptr)
		{
			if constexpr (std::is_same_v<decltype(ReleaseFunc), void(*)(T*)>)
				ReleaseFunc(ptr);
			else
				ReleaseFunc(&ptr);
		}
	}

public:
	~AutoReleasePtr()
	{
		Cleanup();
	}
};
