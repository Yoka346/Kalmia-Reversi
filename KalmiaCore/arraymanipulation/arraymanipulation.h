#pragma once

namespace arraymanipulation 
{
	template<class T>
	constexpr int index_of(T* a, int offset, int length, int target)
	{
		for (auto i = offset; i < length; i++)
			if (a[i] == target)
				return i;
		return -1;
	}
}
