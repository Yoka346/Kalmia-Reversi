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
	template<bool SAVE_MOVE_HISTORY>
	class Position
	{
	private:
		// Rankというのはチェス用語で, 盤面の水平方向のラインを意味する.
		static constexpr int HASH_RANK_LEN_0 = 16;
		static constexpr int HASH_RANK_LEN_1 = 256;
		static utils::ConstantArray<uint64_t, HASH_RANK_LEN_0 * HASH_RANK_LEN_1> HASH_RANK;

		Bitboard _bitboard;
		DiscColor _side_to_move;
		std::vector<Move>* move_history;

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
			  _side_to_move(DiscColor::BLACK), move_history(SAVE_MOVE_HISTORY ? new std::vector<Move> : nullptr) { ; }

		Position(Position<true>& pos) : _bitboard(pos._bitboard), _side_to_move(pos._side_to_move)
		{
			if constexpr (SAVE_MOVE_HISTORY)
				this->move_history = pos.move_history;
			else
				this->move_history = nullptr;
		}

		Position(Position<false>& pos) : _bitboard(pos._bitboard), _side_to_move(pos._side_to_move)
		{
			if constexpr (SAVE_MOVE_HISTORY)
				this->move_history = new std::vector<Move>();
			else
				this->move_history = nullptr;
		}

		~Position() { if (this->move_history) delete this->move_history; }

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
		inline bool update(BoardCoordinate& coord)
		{
			if constexpr (CHECK_LEGALITY)
			{
				uint64_t moves = this->_bitboard.calc_player_mobility();
				if (!is_legal(coord))
					return false;
			}

			uint64_t flipped = this->_bitboard.calc_flipped_discs(coord);
			Move move(coord, flipped);
			update(move);
		}

		inline int get_next_moves(Array<Move, MAX_MOVE_NUM>& moves)
		{
			uint64_t mobility = this->_bitboard.calc_player_mobility();
			auto move_count = 0;
			int coord;
			FOREACH_BIT(coord, mobility)
				moves[move_count++].coord = static_cast<BoardCoordinate>(coord);
			return move_count;
		}

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

	template<bool SAVE_MOVE_HISTORY>
	inline ConstantArray<uint64_t, Position<SAVE_MOVE_HISTORY>::HASH_RANK_LEN_0* Position<SAVE_MOVE_HISTORY>::HASH_RANK_LEN_1> Position<SAVE_MOVE_HISTORY>::HASH_RANK(Position<SAVE_MOVE_HISTORY>::init_hash_rank);
}