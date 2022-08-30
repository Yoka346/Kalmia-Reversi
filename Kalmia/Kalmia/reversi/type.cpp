#pragma once
#include "types.h"
#include "constant.h"
#include <iostream>
#include <sstream>
#include <string>
#include <algorithm>

using namespace std;

namespace reversi
{
	string coordinate_to_string(BoardCoordinate coord)
	{
		if (coord == BoardCoordinate::NULL_COORD)
			return "null";

		if (coord == BoardCoordinate::PASS)
			return "pass";

		auto x = coord % BOARD_SIZE;
		auto y = coord / BOARD_SIZE;
		std::stringstream ss;
		ss << static_cast<char>('A' + x) << y + 1;
		return ss.str();
	}

	BoardCoordinate parse_coordinate(const std::string& str)
	{
		auto lstr = str;
		transform(lstr.begin(), lstr.end(), lstr.begin(), tolower);

		if (lstr == "pass")
			return BoardCoordinate::PASS;

		if (lstr.size() != 2 || lstr[0] < 'a' || lstr[0] > 'h' || lstr[1] < '1' || lstr[1] > '8')
			return BoardCoordinate::NULL_COORD;

		return static_cast<BoardCoordinate>((lstr[0] - 'a') + (lstr[1] - '1') * BOARD_SIZE);
	}
	
	DiscColor parse_color(const string& str)
	{
		auto lstr = str;
		transform(lstr.begin(), lstr.end(), lstr.begin(), tolower);
		if (lstr == "b" || lstr == "black")
			return DiscColor::BLACK;
		else if (lstr == "w" || lstr == "white")
			return DiscColor::WHITE;
		return DiscColor::EMPTY;
	}
}