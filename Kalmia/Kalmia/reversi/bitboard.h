#pragma once

#include "../common.h"
#include "../utils/bitmanip.h"

#include "types.h"
#include "mobility.h"
#include "flip.h"

namespace reversi
{
	class Bitboard
	{
	private:
		uint64_t _player;
		uint64_t _opponent;

	public:
		Bitboard(uint64_t player, uint64_t opponent) : _player(player), _opponent(opponent) { ; }
		inline uint64_t player() const { return this->_player; }
		inline uint64_t opponent() const { return this->_opponent; }
		inline uint64_t discs() const { return this->_player | this->_opponent; }
		inline uint64_t empties() const { return ~discs(); }
		inline int32_t player_disc_count() const { return std::popcount(this->_player); }
		inline int32_t opponent_disc_count() const { return std::popcount(this->_opponent); }
		inline int32_t disc_count() const { return std::popcount(discs()); }
		inline int32_t empty_count() const { return std::popcount(empties()); }
		inline uint64_t calc_player_mobility() const { return calc_mobility(this->_player, this->_opponent); }
		inline uint64_t calc_opponent_mobility() const {return calc_mobility(this->_opponent, this->_player);}
		inline uint64_t calc_flipped_discs(BoardCoordinate& coord) const { return reversi::calc_flipped_discs(this->_player, this->_opponent, coord); }

		inline void update(BoardCoordinate& coord, uint64_t& flipped)
		{
			this->_player |= (COORD_TO_BIT[coord] | flipped);
			this->_opponent ^= flipped;
			swap();
		}

		inline void undo(BoardCoordinate& coord, uint64_t& flipped)
		{
			swap();
			this->_player ^= (COORD_TO_BIT[coord] | flipped);
			this->_opponent ^= flipped;
		}

		inline void swap()
		{
			auto tmp = this->_player;
			this->_player = this->_opponent;
			this->_opponent = tmp;
		}
	};
}