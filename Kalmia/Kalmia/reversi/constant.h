#pragma once

#include "../utils/array.h"

using namespace utils;

namespace reversi
{
	constexpr int32_t BOARD_SIZE = 8;
	constexpr int32_t SQUARE_NUM = BOARD_SIZE * BOARD_SIZE;
	constexpr int32_t MAX_MOVE_NUM = 34;

	constexpr ConstantArray<uint64_t, SQUARE_NUM> COORD_TO_BIT(
		[](uint64_t* data, size_t len)
		{
			for (int32_t coord = 0; coord < SQUARE_NUM; coord++)
				data[coord] = 1ULL << coord;
		});
}