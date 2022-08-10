#pragma once

#include "../utils/array.h"

using namespace utils;

namespace reversi
{
	constexpr int BOARD_SIZE = 8;
	constexpr int SQUARE_NUM = BOARD_SIZE * BOARD_SIZE;
	constexpr int MAX_MOVE_NUM = 34;

	constexpr ConstantArray<uint64_t, SQUARE_NUM> COORD_TO_BIT(
		[](uint64_t* data, size_t len)
		{
			for (auto coord = 0; coord < SQUARE_NUM; coord++)
				data[coord] = 1ULL << coord;
		});
}