#pragma once

#include "../common.h"
#include "enum.h"

namespace reversi
{
	class BitBoard
	{
	private:
		uint64_t _player;
		uint64_t _opponent;

	public:
		uint64_t player() { return this->_player; }
		uint64_t opponent() { return this->_opponent; }
		uint64_t calc_player_mobility();
		uint64_t calc_opponent_mobility();
		uint64_t calc_flipped_discs(BoardCoordinate& coord);
	};
}