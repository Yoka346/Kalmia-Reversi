#pragma once
#include "../utils/bitmanip.h"
#include "types.h"
#include "mobility.h"
#include "flip.h"

namespace reversi
{
	struct Bitboard
	{
		uint64_t player;
		uint64_t opponent;

		Bitboard(uint64_t player, uint64_t opponent) : player(player), opponent(opponent) { ; }
		uint64_t discs() const { return this->player | this->opponent; }
		uint64_t empties() const { return ~discs(); }
		int32_t player_disc_count() const { return std::popcount(this->player); }
		int32_t opponent_disc_count() const { return std::popcount(this->opponent); }
		int32_t disc_count() const { return std::popcount(discs()); }
		int32_t empty_count() const { return std::popcount(empties()); }
		bool operator==(const Bitboard& right) { return this->player == right.player && this->opponent == right.opponent; }
		const Bitboard& operator=(const Bitboard& right) { this->player = right.player; this->opponent = right.opponent; return *this; }
		uint64_t calc_player_mobility() const { return calc_mobility(this->player, this->opponent); }
		uint64_t calc_opponent_mobility() const {return calc_mobility(this->opponent, this->player);}
		uint64_t calc_flipped_discs( const BoardCoordinate& coord) const { return reversi::calc_flipped_discs(this->player, this->opponent, coord); }

		void put_player_disc_at(BoardCoordinate coord)
		{
			auto bit = COORD_TO_BIT[coord];
			this->player |= bit;

			if (this->opponent & bit)
				this->opponent ^= COORD_TO_BIT[coord];
		}

		void put_opponent_disc_at(BoardCoordinate coord)
		{
			auto bit = COORD_TO_BIT[coord];
			this->opponent |= bit;

			if (this->player & bit)
				this->player ^= COORD_TO_BIT[coord];
		}

		void update(const BoardCoordinate& coord, const uint64_t& flipped)
		{
			auto player = this->player;
			this->player = this->opponent ^ flipped;
			this->opponent = player | (COORD_TO_BIT[coord] | flipped);
		}

		void undo(const BoardCoordinate& coord, const uint64_t& flipped)
		{
			auto player = this->player;
			this->player = this->opponent ^ flipped;
			this->opponent = player ^ (COORD_TO_BIT[coord] | flipped);
		}

		void swap()
		{
			auto tmp = this->player;
			this->player = this->opponent;
			this->opponent = tmp;
		}
	};
}