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

	BoardCoordinate coordinate_2d_to_1d(int32_t x, int32_t y)
	{
		if (x < 0 || y < 0 || x >= BOARD_SIZE || y >= BOARD_SIZE)
			throw out_of_range("x or y is out of range 0 to BOARD_SIZE - 1.");
		return static_cast<BoardCoordinate>(x + y * BOARD_SIZE);
	}

	BoardCoordinate parse_coordinate(const string& str)
	{
		constexpr const char* WHITESPACE = " \n\r\t\f\v";

		auto lstr = str;
		transform(lstr.begin(), lstr.end(), lstr.begin(), [](char ch) { return tolower(ch); });

		auto loc = lstr.find_first_not_of(WHITESPACE);
		if (loc != string::npos)
			lstr = lstr.substr(loc);

		if (lstr == "pass")
			return BoardCoordinate::PASS;

		if (lstr.size() < 2 || lstr[0] < 'a' || lstr[0] > 'h' || lstr[1] < '1' || lstr[1] > '8')
			return BoardCoordinate::NULL_COORD;

		return static_cast<BoardCoordinate>((lstr[0] - 'a') + (lstr[1] - '1') * BOARD_SIZE);
	}
	
	DiscColor parse_color(const string& str)
	{
		string lstr = str;
		transform(lstr.begin(), lstr.end(), lstr.begin(), tolower);
		if (lstr == "b" || lstr == "black")
			return DiscColor::BLACK;
		else if (lstr == "w" || lstr == "white")
			return DiscColor::WHITE;
		return DiscColor::EMPTY;
	}
}