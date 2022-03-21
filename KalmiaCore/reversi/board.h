#pragma once
#include "../pch.h"
#include "../bitmanipulation.h"

#define BOARD_SIZE 8
#define SQUARE_NUM 64

namespace reversi
{
	class Board;

	enum class BoardCoordinate : unsigned char
	{
		A1, B1, C1, D1, E1, F1, G1, H1,
		A2, B2, C2, D2, E2, F2, G2, H2,
		A3, B3, C3, D3, E3, F3, G3, H3,
		A4, B4, C4, D4, E4, F4, G4, H4,
		A5, B5, C5, D5, E5, F5, G5, H5,
		A6, B6, C6, D6, E6, F6, G6, H6,
		A7, B7, C7, D7, E7, F7, G7, H7,
		A8, B8, C8, D8, E8, F8, G8, H8,
		PASS, NONE
	};

	inline uint64_t operator<<(uint64_t bits, BoardCoordinate coord) { return bits << static_cast<unsigned char>(coord); }
	inline uint64_t operator>>(uint64_t bits, BoardCoordinate coord) { return bits >> static_cast<unsigned char>(coord); }

	inline BoardCoordinate& operator++(BoardCoordinate& coord) 
	{ 
		coord = static_cast<BoardCoordinate>(static_cast<unsigned char>(coord) + 1); 
		return coord;
	};

	inline BoardCoordinate operator++(BoardCoordinate& coord, int)
	{
		auto prev = coord;
		++coord;
		return prev;
	}

	enum class DiscColor : unsigned char
	{
		BLACK,
		WHITE,
		NONE
	};

	inline DiscColor opponent_disc_color(DiscColor color) 
	{ 
		return static_cast<DiscColor>(static_cast<unsigned char>(color) ^ static_cast<int>(DiscColor::WHITE));
	}

	class Mobility
	{
	public:
		Mobility(uint64_t mobility) :mobility(mobility), mobilityNum(popcount(mobility)) { ; }
		inline uint64_t get_raw_mobility() { return this->mobility; }

		inline bool move_to_next_coord(BoardCoordinate& coord) 
		{ 
			while (this->mobilityCount != this->mobilityNum)
			{
				if (this->mobility & this->mask)
				{
					this->mobilityCount++;
					coord = this->current_coordinate++;
					this->mask <<= 1;
					return true;
				}
				this->current_coordinate++;
				this->mask <<= 1;
			}
			return false;
		}

	private:
		static const uint64_t MASK_MAX = 1ULL << (SQUARE_NUM - 1);
		uint64_t mobility;
		int mobilityNum;
		BoardCoordinate current_coordinate = BoardCoordinate::A1;
		int mobilityCount = 0;
		uint64_t mask = 1;
	};

#define foreach_mobility(coord, mobility) while(mobility.move_to_next_coord(coord)) 

	typedef struct Bitboard
	{
		uint64_t current_player;
		uint64_t opponent_player;

		Bitboard(uint64_t current_player, uint64_t opponent_player) : current_player(current_player), opponent_player(opponent_player) { ; }
		inline uint64_t get_empty() const { return ~(this->current_player | this->opponent_player); }
		inline int32_t get_empty_count() const { return popcount(get_empty()); }
		inline int32_t get_current_player_disc_count() const { return popcount(this->current_player); }
		inline int32_t get_opponent_player_disc_count() const { return popcount(this->opponent_player); }

		inline bool operator==(const Bitboard& right) const
		{
			this->current_player == right.current_player && this->opponent_player == right.opponent_player;
		}

		inline bool operator !=(const Bitboard& right) const { !(*this == right); }
	};

	class Board
	{
	public:
		static void static_initializer();

	private:
		static const int HASH_RANK_LEN_0 = 16;
		static const int HASH_RANK_LEN_1 = 256;

		static bool initialized;
		static uint64_t hash_rank[HASH_RANK_LEN_0][HASH_RANK_LEN_1];

		Bitboard bitboard;
		DiscColor side_to_move;
		
		static bool is_initialized() { return initialized; }

		inline DiscColor get_side_to_move() { return this->side_to_move; }
		inline DiscColor get_opponent_color() { return opponent_disc_color(this->side_to_move); }
	};
};




