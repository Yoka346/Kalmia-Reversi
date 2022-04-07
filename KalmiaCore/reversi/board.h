#pragma once
#include "../pch.h"
#include "../bitmanipulation.h"
#include "../bitvector.h"

namespace reversi
{
	static constexpr int BOARD_SIZE = 8;
	static constexpr int SQUARE_NUM = 64;

	static constexpr uint64_t COORD_TO_BIT[SQUARE_NUM] =
	{
		1ULL, 1ULL << 1, 1ULL << 2, 1ULL << 3, 1ULL << 4, 1ULL << 5, 1ULL << 6, 1ULL << 7,
		1ULL << 8, 1ULL << 9, 1ULL << 10, 1ULL << 11, 1ULL << 12, 1ULL << 13, 1ULL << 14, 1ULL << 15,
		1ULL << 16, 1ULL << 17, 1ULL << 18, 1ULL << 19, 1ULL << 20, 1ULL << 21, 1ULL << 22, 1ULL << 23,
		1ULL << 24, 1ULL << 25, 1ULL << 26, 1ULL << 27, 1ULL << 28, 1ULL << 29, 1ULL << 30, 1ULL << 31,
		1ULL << 32, 1ULL << 33, 1ULL << 34, 1ULL << 35, 1ULL << 36, 1ULL << 37, 1ULL << 38, 1ULL << 39,
		1ULL << 40, 1ULL << 41, 1ULL << 42, 1ULL << 43, 1ULL << 44, 1ULL << 45, 1ULL << 46, 1ULL << 47,
		1ULL << 48, 1ULL << 49, 1ULL << 50, 1ULL << 51, 1ULL << 52, 1ULL << 53, 1ULL << 54, 1ULL << 55,
		1ULL << 56, 1ULL << 57, 1ULL << 58, 1ULL << 59, 1ULL << 60, 1ULL << 61, 1ULL << 62, 1ULL << 63
	};

	enum BoardCoordinate : byte
	{
		A1, B1, C1, D1, E1, F1, G1, H1,
		A2, B2, C2, D2, E2, F2, G2, H2,
		A3, B3, C3, D3, E3, F3, G3, H3,
		A4, B4, C4, D4, E4, F4, G4, H4,
		A5, B5, C5, D5, E5, F5, G5, H5,
		A6, B6, C6, D6, E6, F6, G6, H6,
		A7, B7, C7, D7, E7, F7, G7, H7,
		A8, B8, C8, D8, E8, F8, G8, H8,
		PASS, NULL_COORD
	};

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

	__declspec(dllexport) std::string coordinate_to_string(BoardCoordinate coord);

	enum DiscColor : byte
	{
		BLACK,
		WHITE,
		NONE
	};

#define opponent_disc_color(color) static_cast<DiscColor>(color ^ DiscColor::WHITE)

	enum Player : int
	{
		CURRENT,
		OPPONENT,
	};

	enum GameResult : int
	{
		WIN = 1,
		LOSS = -1,
		DRAW = 0,
		NOT_OVER = 2
	};

	class Mobility
	{
	public:
		Mobility() :mobility(0ULL), mobility_num(0) { ; }
		Mobility(uint64_t mobility) :mobility(mobility), mobility_num(popcount(mobility)) { ; }
		inline uint64_t get_raw_mobility() const { return this->mobility; }
		inline void set_raw_mobility(uint64_t raw_mobility) { this->mobility = raw_mobility;  this->mobility_num = popcount(raw_mobility); move_to_first(); }
		inline int count() { return this->mobility_num; }
		inline void move_to_first() { this->current_coordinate = BoardCoordinate::A1; this->mobility_count = 0; this->mask = 1ULL; }
		__declspec(dllexport) bool move_to_next_coord(BoardCoordinate& coord);

	private:
		uint64_t mobility;
		int mobility_num;
		BoardCoordinate current_coordinate = BoardCoordinate::A1;
		int mobility_count = 0;
		uint64_t mask = 1ULL;
	};

	typedef struct Move
	{
		DiscColor color;
		BoardCoordinate coord;
		uint64_t flipped;
		Move() : color(DiscColor::BLACK), coord(BoardCoordinate::A1), flipped(0ULL) { ; }
		Move(DiscColor color, BoardCoordinate coord, uint64_t flipped) :color(color), coord(coord), flipped(flipped) { ; }
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
		inline void swap() { auto tmp = this->current_player; this->current_player = this->opponent_player; this->opponent_player = tmp; }

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

		Board() : side_to_move(DiscColor::BLACK),
				  bitboard(Bitboard(COORD_TO_BIT[BoardCoordinate::E4] | COORD_TO_BIT[BoardCoordinate::D5],
									COORD_TO_BIT[BoardCoordinate::D4] | COORD_TO_BIT[BoardCoordinate::E5])) { ; }

		Board(DiscColor side_to_move, Bitboard bitboard) : side_to_move(side_to_move), bitboard(bitboard) { ; }

		inline DiscColor get_side_to_move() { return this->side_to_move; }
		inline DiscColor get_opponent_color() { return opponent_disc_color(this->side_to_move); }
		inline Bitboard get_bitboard() { return this->bitboard; }
		inline Bitboard set_bitboard(Bitboard& bitboard) { return this->bitboard = bitboard; }
		inline void copy_to(Board& dest) { dest.side_to_move = this->side_to_move; dest.bitboard = this->bitboard; }
		inline int get_current_player_disc_count() { return this->bitboard.get_current_player_disc_count(); }
		inline int get_opponent_player_disc_count() { return this->bitboard.get_opponent_player_disc_count(); }
		inline int get_empty_count() { return this->bitboard.get_empty_count(); }
		inline void pass() { this->side_to_move = opponent_disc_color(this->side_to_move); this->bitboard.swap(); }

		inline Player get_square_side(BoardCoordinate coord) 
		{ 
			return static_cast<Player>(2 - 2 * ((this->bitboard.current_player >> coord) & 1) - ((this->bitboard.opponent_player >> coord) & 1)); 
		}

		__declspec(dllexport) DiscColor get_square_color(BoardCoordinate coord);
		__declspec(dllexport) void get_move(BoardCoordinate coord, Move& move);
		__declspec(dllexport) void get_current_player_mobility(Mobility& mobility);
		__declspec(dllexport) void get_opponent_player_mobility(Mobility& mobility);
		__declspec(dllexport) void update(Move& move);
		__declspec(dllexport) uint64_t get_hash_code();
		__declspec(dllexport) GameResult get_game_result();
		__declspec(dllexport) std::string to_string();

	private:
		static const int HASH_RANK_LEN_0 = 16;	// Rank is a chess term which means horizontal line.
		static const int HASH_RANK_LEN_1 = 256;

		static bool initialized;
		static uint64_t hash_rank[HASH_RANK_LEN_0][HASH_RANK_LEN_1];

		Bitboard bitboard;
		DiscColor side_to_move;

		static uint64_t calc_flipped_discs(uint64_t p, uint64_t o, BoardCoordinate coord);
		static uint64_t calc_mobility(uint64_t p, uint64_t o);

#ifdef USE_AVX2
		static uint64_t calc_flipped_discs_AVX2(uint64_t p, uint64_t o, BoardCoordinate coord);
		static uint64_t calc_mobility_AVX2(uint64_t p, uint64_t o);
#elif defined(USE_SSE41) 
		static uint64_t calc_flipped_discs_SSE(uint64_t p, uint64_t o, BoardCoordinate coord);
		static uint64_t calc_mobility_SSE(uint64_t p, uint64_t o);
#else
		static uint64_t calc_flipped_discs_CPU(uint64_t p, uint64_t o, BoardCoordinate coord);
		static uint64_t calc_mobility_CPU(uint64_t p, uint64_t o);
#endif

#if defined(USE_AVX2) || defined(USE_SSE41) 
		uint64_t calc_hash_code_SSE();
#else
		uint64_t calc_hash_code_CPU();
#endif
	};
};




