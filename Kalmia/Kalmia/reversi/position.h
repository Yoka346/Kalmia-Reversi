#pragma once
#include "../utils/static_initializer.h"
#include "../utils/random.h"
#include "../utils/array.h"
#include "constant.h"
#include "types.h"
#include "move.h"
#include "bitboard.h"

namespace reversi
{
	class Position
	{
	private:
		// Rankというのはチェス用語で, 盤面の水平方向のラインを意味する.
		static constexpr int HASH_RANK_LEN_0 = 16;
		static constexpr int HASH_RANK_LEN_1 = 256;
		static utils::ConstantArray<uint64_t, HASH_RANK_LEN_0 * HASH_RANK_LEN_1> HASH_RANK;

		Bitboard _bitboard;
		DiscColor _side_to_move;

	public:
		inline static size_t to_hash_rank_idx(size_t i, size_t j) { return i + (j << 4); }
		static void init_hash_rank(uint64_t* hash_rank, size_t len);

		Position() 
			: _bitboard(COORD_TO_BIT[reversi::E4] | COORD_TO_BIT[reversi::D5], COORD_TO_BIT[reversi::D4] | COORD_TO_BIT[reversi::E5]),
			  _side_to_move(DiscColor::BLACK) { ; }

		inline DiscColor side_to_move() const { return this->_side_to_move; }
		inline DiscColor opponent_color() const { return to_opponent_color(this->_side_to_move); }
		inline int get_empty_square_count() const { return this->_bitboard.empty_count(); }
		inline int player_disc_count() const { return this->_bitboard.player_disc_count(); }
		inline int opponent_disc_count() const { return this->_bitboard.opponent_disc_count(); }
		inline int disc_count() const { return this->_bitboard.disc_count(); }
		inline int black_disc_count() const { (this->_side_to_move == DiscColor::BLACK) ? player_disc_count() : opponent_disc_count(); }
		inline int white_disc_count() const { (this->_side_to_move == DiscColor::WHITE) ? player_disc_count() : opponent_disc_count(); }

		inline DiscColor square_color_at(BoardCoordinate coord) const
		{
			(2 - 2 * ((this->_bitboard.player() >> coord) & 1) - ((this->_bitboard.opponent() >> coord) & 1))
				? opponent_color() : this->_side_to_move;
		}

		inline bool is_legal(BoardCoordinate& coord) const { return this->_bitboard.calc_player_mobility() & COORD_TO_BIT[coord]; }
		inline void pass() { this->_side_to_move = opponent_color(); this->_bitboard.swap(); }
		inline bool update(Move& move) { this->_side_to_move = opponent_color(); this->_bitboard.update(move.coord, move.flipped); }

		template<bool CHECK_LEGALITY>
		bool update(BoardCoordinate& coord);

		int get_next_moves(Array<Move, MAX_MOVE_NUM>& moves);
		inline void calc_flipped_discs(Move& move) { move.flipped = this->_bitboard.calc_flipped_discs(move.coord); }
		inline int32_t get_disc_diff() { return this->_bitboard.player_disc_count() - this->_bitboard.opponent_disc_count(); }

		/**
		* @fn
		* @brief 現在の手番からみたゲームの勝敗を返す.
		* @detail 処理の中身は単純にディスクの個数を比較しているだけなので, 本当に終局しているかどうかは確認していない.
		**/
		inline GameResult get_game_result()
		{
			int32_t diff = get_disc_diff();
			if (!diff)
				return GameResult::DRAW;
			return (diff > 0) ? GameResult::WIN : GameResult::LOSS;
		}
	};
}