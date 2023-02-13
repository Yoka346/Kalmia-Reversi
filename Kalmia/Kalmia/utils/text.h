#pragma once
#include <string>

namespace utils
{
	inline void remove_head_whitespace(std::string& str)
	{
		constexpr const char* WHITESPACE = " \n\r\t\f\v";
		auto loc = str.find_first_not_of(WHITESPACE);
		if (loc != std::string::npos)
			str = str.substr(loc);
	}
}
