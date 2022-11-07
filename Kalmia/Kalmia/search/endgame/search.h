#pragma once
#include <cstdint>
#include <chrono>

#include "../../reversi/constant.h"
#include "../../reversi/types.h"
#include "../../reversi/position.h"
#include "tt.h"

namespace search::endgame
{
	/**
	* @class
	* @brief 終盤完全読み機能を提供するクラス.
	* @detail 置換表付きNegaAlphaで石差が最大になるような手を探索する.
	**/
	class EndgameSolver
	{
	private:
		static constexpr int32_t SEARCH_WITHOUT_TT_THRESHOLD = 6;
		static constexpr int8_t MAX_SCORE = reversi::SQUARE_NUM;
		static constexpr int8_t MIN_SCORE = -reversi::SQUARE_NUM;

		TranspositionTable tt;
		std::chrono::steady_clock::time_point search_start_time;
		std::chrono::steady_clock::time_point search_end_time;

		uint64_t _internal_node_count = 0;
		uint64_t _leaf_node_count = 0;
		bool _is_searching = false;
		std::chrono::milliseconds time_limit{ INT32_MAX };
		bool timeout = false;
		bool stop_flag = false;

		template<bool AFTER_PASS>
		int8_t search_with_tt(reversi::Position& pos, int8_t alpha, int8_t beta);

		template<bool AFTER_PASS>
		int8_t search_without_tt(reversi::Position& pos, int8_t alpha, int8_t beta);

		void sort_moves(reversi::Position& pos, utils::Array<reversi::Move, reversi::MAX_MOVE_NUM>& moves, int32_t move_num);

	public:
		EndgameSolver(size_t transposition_table_size) :tt(transposition_table_size) { ; }

		uint64_t internal_node_count() { return this->_internal_node_count; }
		uint64_t leaf_node_count() { return this->_leaf_node_count; }
		uint64_t total_node_count() { return this->_internal_node_count + this->_leaf_node_count; }
		uint64_t is_searching() { return this->_is_searching; }

		std::chrono::milliseconds search_ellapsed()
		{
			using namespace std::chrono;
			return this->_is_searching
				? duration_cast<milliseconds>(steady_clock::now() - this->search_start_time)
				: duration_cast<milliseconds>(this->search_end_time - this->search_start_time);
		}

		double nps() { return (this->_internal_node_count + this->_leaf_node_count) / (this->search_ellapsed().count() * 1.0e-3); }
		reversi::BoardCoordinate solve_best_move(reversi::Position root_pos, std::chrono::milliseconds time_limit, int8_t& final_disc_diff, bool& timeout);
		void send_stop_search_signal() { this->stop_flag = true; }
	}; 
	
	template int8_t EndgameSolver::search_with_tt<true>(reversi::Position& pos, int8_t alpha, int8_t beta);
	template int8_t EndgameSolver::search_with_tt<false>(reversi::Position& pos, int8_t alpha, int8_t beta);

	template int8_t EndgameSolver::search_without_tt<true>(reversi::Position& pos, int8_t alpha, int8_t beta);
	template int8_t EndgameSolver::search_without_tt<false>(reversi::Position& pos, int8_t alpha, int8_t beta);
}