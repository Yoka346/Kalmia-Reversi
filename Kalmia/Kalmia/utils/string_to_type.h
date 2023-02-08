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

inline bool try_stof(const std::string& str, float& out, size_t* idx = nullptr)
{
	try
	{
		out = std::stof(str, idx);
		return true;
	}
	catch (std::invalid_argument)
	{
		return false;
	}
}
