#pragma once
#include <iostream>
#include <string>

inline bool try_stoi(const std::string& str, int32_t& out, size_t* idx = nullptr, int base = 10)
{
	try
	{
		out = std::stoi(str, idx, base);
		return true;
	}
	catch (std::invalid_argument)
	{
		return false;
	}
}
