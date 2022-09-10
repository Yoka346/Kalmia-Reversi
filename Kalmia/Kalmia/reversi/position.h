#pragma once
#include <vector>

#include "../utils/static_initializer.h"
#include "../utils/random.h"
#include "../utils/array.h"
#include "constant.h"
#include "types.h"
#include "move.h"
#include "bitboard.h"

namespace reversi
{
	/**
	* @class
	* @brief リバーシの盤面を表現するクラス.
	**/
	class Position
	{
	private:
		// Rankというのはチェス用語で, 盤面の水平方向のラインを意味する.
		static constexpr int HASH_RANK_LEN_0 = 16;
		static constexpr int HASH_RANK_LEN_1 = 256;
		static utils::Array<uint64_t, HASH_RANK_LEN_0 * HASH_RANK_LEN_1> HASH_RANK;

		Bitboard _bitboard;
		DiscColor _side_to_move;

	public:
		inline static size_t to_hash_rank_idx(size_t i, size_t j) { return i + (j << 4); }

		inline static void init_hash_rank(uint64_t* hash_rank, size_t len)
		{
			Random rand;
			for (int i = 0; i < HASH_RANK_LEN_0; i++)
				for (int j = 0; j < HASH_RANK_LEN_1; j++)
					hash_rank[to_hash_rank_idx(i, j)] = rand.next_64();
		}

		Position() 
			: _bitboard(COORD_TO_BIT[reversi::E4] | COORD_TO_BIT[reversi::D5], COORD_TO_BIT[reversi::D4] | COORD_TO_BIT[reversi::E5]),
			  _side_to_move(DiscColor::BLACK) { ; }

		inline DiscColor side_to_move() const { return this->_side_to_move; }
		inline DiscColor opponent_color() const { return to_opponent_color(this->_side_to_move); }
		inline int empty_square_count() const { return this->_bitboard.empty_count(); }
		inline int player_disc_count() const { return this->_bitboard.player_disc_count(); }
		inline int opponent_disc_count() const { return this->_bitboard.opponent_disc_count(); }
		inline int disc_count() const { return this->_bitboard.disc_count(); }
		inline int black_disc_count() const { return (this->_side_to_move == DiscColor::BLACK) ? player_disc_count() : opponent_disc_count(); }
		inline int white_disc_count() const { return (this->_side_to_move == DiscColor::WHITE) ? player_disc_count() : opponent_disc_count(); }

		inline DiscColor square_color_at(BoardCoordinate coord) const
		{
			auto owner = square_owner_at(coord);
			if (owner == Player::NULL_PLAYER)
				return DiscColor::EMPTY;
			return (owner == Player::CURRENT) ? this->_side_to_move : opponent_color();
		}

		inline Player square_owner_at(BoardCoordinate coord) const
		{
			return static_cast<Player>(2 - 2 * ((this->_bitboard.player() >> coord) & 1) - ((this->_bitboard.opponent() >> coord) & 1));
		}

		inline bool is_legal(BoardCoordinate& coord) const { return this->_bitboard.calc_player_mobility() & COORD_TO_BIT[coord]; }
		inline void pass() { this->_side_to_move = opponent_color(); this->_bitboard.swap(); }

		template<bool CHECK_LEGALITY>
		inline bool update(Move& move) 
		{
			if constexpr (CHECK_LEGALITY)
				if (!is_legal(move.coord))
					return false;

			this->_side_to_move = opponent_color();
			this->_bitboard.update(move.coord, move.flipped); 
			return true;
		}

		template<bool CHECK_LEGALITY>
		inline bool update(BoardCoordinate& coord)
		{
			if constexpr (CHECK_LEGALITY)
				if (!is_legal(coord))
					return false;

			uint64_t flipped = this->_bitboard.calc_flipped_discs(coord);
			Move move(coord, flipped);
			update(move);
		}

		inline void undo(Move& move)
		{
			this->_bitboard.undo(move.coord, move.flipped);
			this->_side_to_move = opponent_color();
		}

		inline int get_next_moves(Array<Move, MAX_MOVE_NUM>& moves) const
		{
			uint64_t mobility = this->_bitboard.calc_player_mobility();
			auto move_count = 0;
			int coord;
			FOREACH_BIT(coord, mobility)
				moves[move_count++].coord = static_cast<BoardCoordinate>(coord);
			return move_count;
		}

		inline void calc_flipped_discs(Move& move) const { move.flipped = this->_bitboard.calc_flipped_discs(move.coord); }
		inline int32_t get_disc_diff() const { return this->_bitboard.player_disc_count() - this->_bitboard.opponent_disc_count(); }
		
		inline bool is_gameover() const
		{
			return std::popcount(this->_bitboard.calc_player_mobility()) == 0
				&& std::popcount(this->_bitboard.calc_opponent_mobility()) == 0;
		}

		inline bool can_pass() const
		{
			return std::popcount(this->_bitboard.calc_player_mobility()) == 0
				&& std::popcount(this->_bitboard.calc_opponent_mobility()) != 0;
		}

		/**
		* @fn
		* @brief 現在の手番からみたゲームの勝敗を返す.
		* @detail 処理の中身は単純にディスクの個数を比較しているだけなので, 本当に終局しているかどうかは確認していない.
		**/
		inline GameResult get_game_result() const
		{
			int32_t diff = get_disc_diff();
			if (!diff)
				return GameResult::DRAW;
			return (diff > 0) ? GameResult::WIN : GameResult::LOSS;
		}
	};

	inline Array<uint64_t, Position::HASH_RANK_LEN_0* Position::HASH_RANK_LEN_1> Position::HASH_RANK(Position::init_hash_rank);
}