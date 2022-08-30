#pragma once
#include <iostream>
#include <cstdint>
#include <tuple>

namespace reversi
{
	enum BoardCoordinate : uint8_t
	{
		A1, B1, C1, D1, E1, F1, G1, H1,
		A2, B2, C2, D2, E2, F2, G2, H2,
		A3, B3, C3, D3, E3, F3, G3, H3,
		A4, B4, C4, D4, E4, F4, G4, H4,
		A5, B5, C5, D5, E5, F5, G5, H5,
		A6, B6, C6, D6, E6, F6, G6, H6,
		A7, B7, C7, D7, E7, F7, G7, H7,
		A8, B8, C8, D8, E8, F8, G8, H8,
		PASS, NULL_COORD
	};

	constexpr BoardCoordinate& operator++(BoardCoordinate& coord)
	{
		coord = static_cast<BoardCoordinate>(coord + 1);
		return coord;
	}

	constexpr BoardCoordinate operator++(BoardCoordinate& coord, int)
	{
		auto prev = coord;
		++coord;
		return prev;
	}

	std::string coordinate_to_string(BoardCoordinate coord);
	BoardCoordinate parse_coordinate(const std::string& str);

	enum DiscColor
	{
		BLACK,
		WHITE,
		EMPTY
	};

	/**
	* @fn
	* @brief êŒÇÃêFÇîΩì]Ç≥ÇπÇÈ.
	* @return îΩì]ÇµÇΩêŒÇÃêF.
	**/
	constexpr DiscColor to_opponent_color(DiscColor color)
	{
		return static_cast<DiscColor>(color ^ DiscColor::WHITE);
	}

	DiscColor parse_color(const std::string& str);

	inline std::string color_to_string(DiscColor color)
	{
		(color == DiscColor::EMPTY) ? "empty" : ((color == DiscColor::BLACK) ? "black" : "white");
	}

	enum class GameResult : int8_t
	{
		WIN = 1,
		LOSS = -WIN,
		DRAW = 0,
		NOT_OVER = -2
	};
}
