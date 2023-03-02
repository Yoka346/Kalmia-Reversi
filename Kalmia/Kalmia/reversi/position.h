#pragma once

#include <vector>

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
	public:
		Position() 
			: _bitboard(COORD_TO_BIT[reversi::E4] | COORD_TO_BIT[reversi::D5], COORD_TO_BIT[reversi::D4] | COORD_TO_BIT[reversi::E5]),
			  _side_to_move(DiscColor::BLACK) { ; }

		Position(Bitboard bitboard, DiscColor side_to_move) : _bitboard(bitboard), _side_to_move(side_to_move) { ; }

		const Bitboard& bitboard() const { return this->_bitboard; }
		DiscColor side_to_move() const { return this->_side_to_move; }
		void set_side_to_move(DiscColor color) { if (this->_side_to_move != color) pass(); }
		DiscColor opponent_color() const { return to_opponent_color(this->_side_to_move); }
		int empty_square_count() const { return this->_bitboard.empty_count(); }
		int player_disc_count() const { return this->_bitboard.player_disc_count(); }
		int opponent_disc_count() const { return this->_bitboard.opponent_disc_count(); }
		int disc_count() const { return this->_bitboard.disc_count(); }
		int black_disc_count() const { return (this->_side_to_move == DiscColor::BLACK) ? player_disc_count() : opponent_disc_count(); }
		int white_disc_count() const { return (this->_side_to_move == DiscColor::WHITE) ? player_disc_count() : opponent_disc_count(); }
		
		bool operator==(const Position& right)
		{
			return this->_side_to_move == right._side_to_move && this->_bitboard == right._bitboard;
		}

		void set_bitboard(const Bitboard& bitboard) { this->_bitboard = bitboard; }

		/**
		* @fn
		* @brief 指定された座標のマスにあるディスクの色を取得する.
		* @return 指定された座標のマスにあるディスクの色, ディスクが無ければDiscColor::EMPTY.
		**/
		DiscColor square_color_at(BoardCoordinate coord) const
		{
			auto owner = square_owner_at(coord);
			if (owner == Player::NULL_PLAYER)
				return DiscColor::EMPTY;
			return (owner == Player::FIRST) ? this->_side_to_move : opponent_color();
		}

		/**
		* @fn
		* @brief 指定された座標のマスにあるディスクの持ち主を取得する.
		* @return 現在の手番のディスクであればPlayer::FIRST, 相手の手番のディスクであればPlayer::SECOND, どちらでもなければPlayer::NULL_PLAYER.
		**/
		Player square_owner_at(BoardCoordinate coord) const
		{
			return static_cast<Player>(2 - 2 * ((this->_bitboard.player >> coord) & 1) - ((this->_bitboard.opponent >> coord) & 1));
		}

		bool is_legal(const BoardCoordinate& coord) const { return (coord == BoardCoordinate::PASS) ? can_pass() : this->_bitboard.calc_player_mobility() & COORD_TO_BIT[coord]; }
		void pass() { this->_side_to_move = opponent_color(); this->_bitboard.swap(); }

		void put_player_disc_at(BoardCoordinate coord) { this->_bitboard.put_player_disc_at(coord); }
		void put_opponent_disc_at(BoardCoordinate coord) { this->_bitboard.put_opponent_disc_at(coord); }

		template<DiscColor COLOR>
		void put_disc_at(BoardCoordinate coord) 
		{ 
			if constexpr (COLOR == DiscColor::EMPTY) 
				return;

			if (this->_side_to_move == COLOR)
				put_player_disc_at(coord);
			else
				put_opponent_disc_at(coord);
		}

		void remove_disc_at(BoardCoordinate coord) { this->_bitboard.remove_disc_at(coord); }

		/**
		* @fn
		* @brief 合法手かどうか確認せずに与えられた着手で盤面を更新する(探索用). パスを与えた際の動作は未定義. pass関数を使うこと.
		**/
		void update(const Move& move)	
		{
			this->_side_to_move = opponent_color();
			this->_bitboard.update(move.coord, move.flipped); 
		}

		/**
		* @fn
		* @brief 与えられた着手の座標で盤面を更新する(非探索用). update(const Move&)と違ってパスにも対応.
		* @return 与えられた着手が合法手であればtrue. そうでなければfalse. falseが返った場合は, 盤面は更新されていない.
		**/
		bool update(const BoardCoordinate& coord)
		{
			if (!is_legal(coord))
				return false;

			if (coord == BoardCoordinate::PASS)
			{
				pass();
				return true;
			}

			uint64_t flipped = this->_bitboard.calc_flipped_discs(coord);;
			this->_side_to_move = opponent_color();
			this->_bitboard.update(coord, flipped);
			return true;
		}

		void undo(Move& move)
		{
			this->_bitboard.undo(move.coord, move.flipped);
			this->_side_to_move = opponent_color();
		}

		int32_t get_next_moves(Array<Move, MAX_MOVE_NUM>& moves) const
		{
			uint64_t mobility = this->_bitboard.calc_player_mobility();
			auto move_count = 0;
			int coord;
			FOREACH_BIT(coord, mobility)
				moves[move_count++].coord = static_cast<BoardCoordinate>(coord);
			return move_count;
		}

		int32_t get_next_move_num_after(BoardCoordinate move) const
		{
			Bitboard bitboard = this->_bitboard;
			uint64_t flipped = bitboard.calc_flipped_discs(move);
			bitboard.update(move, flipped);
			return std::popcount(bitboard.calc_player_mobility());
		}

		int32_t get_next_move_num_after(Move& move) const
		{
			Bitboard bitboard = this->_bitboard;
			bitboard.update(move.coord, move.flipped);
			return std::popcount(bitboard.calc_player_mobility());
		}

		void calc_flipped_discs(Move& move) const { move.flipped = this->_bitboard.calc_flipped_discs(move.coord); }
		int32_t get_disc_diff() const { return this->_bitboard.player_disc_count() - this->_bitboard.opponent_disc_count(); }
		
		bool is_gameover() const
		{
			return std::popcount(this->_bitboard.calc_player_mobility()) == 0
				&& std::popcount(this->_bitboard.calc_opponent_mobility()) == 0;
		}

		bool can_pass() const
		{
			return std::popcount(this->_bitboard.calc_player_mobility()) == 0
				&& std::popcount(this->_bitboard.calc_opponent_mobility()) != 0;
		}

		/**
		* @fn
		* @brief 現在の手番からみたゲームの勝敗を返す.
		* @detail 処理の中身は単純にディスクの個数を比較しているだけなので, 本当に終局しているかどうかは確認していない.
		**/
		GameResult get_game_result() const
		{
			int32_t diff = get_disc_diff();
			if (diff == 0)
				return GameResult::DRAW;

			return (diff > 0) ? GameResult::WIN : GameResult::LOSS;
		}

		uint64_t calc_hash_code() const { return this->_bitboard.calc_hash_code(); }

	private:
		Bitboard _bitboard;
		DiscColor _side_to_move;
	};
}