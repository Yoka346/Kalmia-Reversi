#pragma once

#include "../common.h"
#include "types.h"
#include "mobility.h"
#include "flip.h"

namespace reversi
{
	class BitBoard
	{
	private:
		uint64_t _player;
		uint64_t _opponent;

	public:
		inline uint64_t player() { return this->_player; }
		inline uint64_t opponent() { return this->_opponent; }
		inline uint64_t discs() { return this->_player | this->_opponent; }
		inline uint64_t empties() { return ~discs(); }
		inline uint64_t calc_player_mobility() { return calc_mobility(this->_player, this->_opponent); }
		inline uint64_t calc_opponent_mobility(){return calc_mobility(this->_opponent, this->_player);}
		inline uint64_t calc_flipped_discs(BoardCoordinate& coord) { return reversi::calc_flipped_discs(this->_player, this->_opponent, coord); }

		inline void swap()
		{
			auto tmp = this->_player;
			this->_player = this->_opponent;
			this->_opponent = tmp;
		}
	};
}