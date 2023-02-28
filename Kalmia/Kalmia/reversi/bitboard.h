#pragma once
#include "../utils/bitmanip.h"
#include "../utils/unroller.h"
#include "../utils/random.h"
#include "constant.h"
#include "types.h"
#include "mobility.h"
#include "flip.h"

namespace reversi
{
	struct Bitboard
	{
	private:
		// Rankというのはチェス用語で, 盤面の水平方向のラインを意味する.
		static constexpr size_t HASH_RANK_LEN_0 = 16;
		static constexpr size_t HASH_RANK_LEN_1 = 256;

		// 盤面のRank毎に割り当てられた乱数表. ハッシュ値の計算の際に用いる.
		static utils::Array<uint64_t, HASH_RANK_LEN_0, HASH_RANK_LEN_1> HASH_RANK;

	public:
		uint64_t player;
		uint64_t opponent;

		static void init_hash_rank(Array<uint64_t, Bitboard::HASH_RANK_LEN_1>* hash_rank, size_t len)
		{
			Random rand;
			for (int i = 0; i < HASH_RANK_LEN_0; i++)
				for (int j = 0; j < HASH_RANK_LEN_1; j++)
					hash_rank[i][j] = rand.next_64();
		}

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
		uint64_t calc_opponent_mobility() const { return calc_mobility(this->opponent, this->player); }
		uint64_t calc_flipped_discs(const BoardCoordinate& coord) const { return reversi::calc_flipped_discs(this->player, this->opponent, coord); }

		void put_player_disc_at(BoardCoordinate coord)
		{
			auto bit = COORD_TO_BIT[coord];
			this->player |= bit;

			if (this->opponent & bit)
				this->opponent ^= bit;
		}

		void put_opponent_disc_at(BoardCoordinate coord)
		{
			auto bit = COORD_TO_BIT[coord];
			this->opponent |= bit;

			if (this->player & bit)
				this->player ^= bit;
		}

		void remove_disc_at(BoardCoordinate coord)
		{
			auto bit = COORD_TO_BIT[coord];
			if (this->player & bit)
				this->player ^= bit;

			if (this->opponent & bit)
				this->opponent ^= bit;
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
			this->player = this->opponent ^ (COORD_TO_BIT[coord] | flipped);
			this->opponent = player | flipped;
		}

		void swap()
		{
			auto tmp = this->player;
			this->player = this->opponent;
			this->opponent = tmp;
		}

		uint64_t calc_hash_code() const
		{
			auto p = reinterpret_cast<const uint8_t*>(this);
			uint64_t h0 = 0;
			uint64_t h1 = 0;
			utils::LoopUnroller<8>()(
				[&](const int32_t i)
				{
					const auto j = static_cast<size_t>(i) << 1;
			h0 ^= HASH_RANK[j][p[j]];
			h1 ^= HASH_RANK[j + 1][p[j + 1]];
				});
			return h0 ^ h1;
		}
	};

	inline Array<uint64_t, Bitboard::HASH_RANK_LEN_0, Bitboard::HASH_RANK_LEN_1> Bitboard::HASH_RANK(Bitboard::init_hash_rank);
}