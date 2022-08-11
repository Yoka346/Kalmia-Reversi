#pragma once

#include "../common.h"
#include "../config.h"
#include "../utils/static_initializer.h"
#include "constant.h"
#include "types.h"
#include "bitboard.h"

namespace reversi
{
	class Position
	{
	private:
		// Rank�Ƃ����̂̓`�F�X�p���, �Ֆʂ̐��������̃��C�����Ӗ�����.
		static constexpr int HASH_RANK_LEN_0 = 16;
		static constexpr int HASH_RANK_LEN_1 = 256;
		static ConstantArray<uint64_t, HASH_RANK_LEN_0 * HASH_RANK_LEN_1> HASH_RANK;

		Bitboard _bitboard;
		SquareState _side_to_move;

	public:
		static void init();
	};
}