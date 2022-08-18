#pragma once
#include "types.h"
#include "constant.h"
#include <iostream>
#include <sstream>

using namespace std;

namespace reversi
{
	string coordinate_to_string(BoardCoordinate coord)
	{
		auto x = coord % BOARD_SIZE;
		auto y = coord / BOARD_SIZE;
		std::stringstream ss;
		ss << static_cast<char>('A' + x) << y + 1;
		return ss.str();
	}
}