#pragma once
#include "../utils/bitmanip.h"
#include "types.h"
#include "mobility.h"
#include "flip.h"

namespace reversi
{
	class Bitboard
	{
	public:
		Bitboard(uint64_t player, uint64_t opponent) : _player(player), _opponent(opponent) { ; }
		uint64_t player() const { return this->_player; }
		uint64_t opponent() const { return this->_opponent; }
		uint64_t discs() const { return this->_player | this->_opponent; }
		uint64_t empties() const { return ~discs(); }
		int32_t player_disc_count() const { return std::popcount(this->_player); }
		int32_t opponent_disc_count() const { return std::popcount(this->_opponent); }
		int32_t disc_count() const { return std::popcount(discs()); }
		int32_t empty_count() const { return std::popcount(empties()); }
		bool operator==(const Bitboard& right) { return this->_player == right._player && this->_opponent == right._opponent; }
		const Bitboard& operator=(const Bitboard& right) { this->_player = right._player; this->_opponent = right._opponent; return *this; }
		uint64_t calc_player_mobility() const { return calc_mobility(this->_player, this->_opponent); }
		uint64_t calc_opponent_mobility() const {return calc_mobility(this->_opponent, this->_player);}
		uint64_t calc_flipped_discs( const BoardCoordinate& coord) const { return reversi::calc_flipped_discs(this->_player, this->_opponent, coord); }

		void put_player_disc_at(BoardCoordinate coord)
		{
			auto bit = COORD_TO_BIT[coord];
			this->_player |= bit;

			if (this->_opponent & bit)
				this->_opponent ^= COORD_TO_BIT[coord];
		}

		void put_opponent_disc_at(BoardCoordinate coord)
		{
			auto bit = COORD_TO_BIT[coord];
			this->_opponent |= bit;

			if (this->_player & bit)
				this->_player ^= COORD_TO_BIT[coord];
		}

		void update(const BoardCoordinate& coord, const uint64_t& flipped)
		{
			auto player = this->_player;
			this->_player = this->_opponent ^ flipped;
			this->_opponent = player | (COORD_TO_BIT[coord] | flipped);
		}

		void undo(const BoardCoordinate& coord, const uint64_t& flipped)
		{
			auto player = this->_player;
			this->_player = this->_opponent ^ flipped;
			this->_opponent = player ^ (COORD_TO_BIT[coord] | flipped);
		}

		void swap()
		{
			auto tmp = this->_player;
			this->_player = this->_opponent;
			this->_opponent = tmp;
		}

	private:
		uint64_t _player;
		uint64_t _opponent;
	};
}