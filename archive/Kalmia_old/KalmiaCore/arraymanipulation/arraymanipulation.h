#pragma once

namespace arraymanipulation 
{
	template<class T>
	constexpr size_t index_of(T* a, size_t offset, size_t length, T target)
	{
		for (size_t i = offset; i < length; i++)
			if (a[i] == target)
				return i;
		return -1;
	}

	template<class T>
	constexpr T max(T* a, size_t length)
	{
		auto max = a[0];
		for (size_t i = 0; i < length; i++)
			if (a[i] > max)
				max = a[i];
		return max;
	}
}
