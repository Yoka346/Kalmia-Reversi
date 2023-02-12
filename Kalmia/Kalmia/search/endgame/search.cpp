#include "search.h"

#include "../../utils/bitmanip.h"

using namespace std;
using namespace std::chrono;

using namespace reversi;

namespace search::endgame
{
	BoardCoordinate EndgameSolver::solve_best_move(Position root_pos, milliseconds time_limit, int8_t& final_disc_diff, bool& timeout)
	{
		Array<Move, MAX_MOVE_NUM> moves;
		int32_t move_num = root_pos.get_next_moves(moves);

		if (move_num == 0)
			if (root_pos.can_pass())
			{
				moves[0].coord = BoardCoordinate::PASS;
				move_num++;
			}
			else
			{
				final_disc_diff = root_pos.player_disc_count() - root_pos.opponent_disc_count();
				timeout = false;
				return BoardCoordinate::NULL_COORD;
			}

		if(move_num > 1)
			sort_moves(root_pos, moves, move_num);

		Bitboard bitboard = root_pos.bitboard();
		Move& best_move = moves[0];
		auto best_score = MIN_SCORE;
		this->_internal_node_count = 0UL;
		this->_leaf_node_count = 0UL;
		this->time_limit = time_limit;
		this->timeout = false;
		this->_is_searching = true;
		this->search_start_time = steady_clock::now();
		for (int i = 0; i < move_num; i++)
		{
			root_pos.update<false>(moves[i].coord);

			int8_t score;
			if(moves[i].coord == BoardCoordinate::PASS)
				score = -search_with_tt<true>(root_pos, MIN_SCORE, -best_score);
			else
				score = -search_with_tt<false>(root_pos, MIN_SCORE, -best_score);

			if (this->timeout)
			{
				this->search_end_time = steady_clock::now();
				final_disc_diff = best_score;
				timeout = this->timeout;
				return best_move.coord;
			}
			root_pos.set_bitboard(bitboard);

			if (score > best_score)
			{
				best_move = moves[i];
				best_score = score;
			}
		}

		this->search_end_time = steady_clock::now();
		final_disc_diff = best_score;
		timeout = this->timeout;
		this->_is_searching = false;
		return best_move.coord;
	}

	template<bool AFTER_PASS>
	int8_t EndgameSolver::search_with_tt(Position& pos, int8_t alpha, int8_t beta)
	{
		if (this->timeout || this->stop_flag)
			return 0;

		auto hash_code = pos.calc_hash_code();
		TTEntry* entry = this->tt.get_entry(hash_code);

		if (entry)	// 置換表に探索情報が存在
		{
			if (beta <= entry->lower_bound)	// betaカット
				return entry->lower_bound;

			if (alpha >= entry->upper_bound) // alphaカット
				return entry->upper_bound;

			if (entry->upper_bound == entry->lower_bound)	// 置換表に登録されているのは真の値.
				return entry->lower_bound;

			// 探索窓を狭める.
			if (entry->lower_bound > alpha)
				alpha = entry->lower_bound;

			if (entry->upper_bound < beta)
				beta = entry->upper_bound;
		}

		Array<Move, MAX_MOVE_NUM> moves;
		int32_t move_num = pos.get_next_moves(moves);
		if (move_num == 0)
		{
			if constexpr (AFTER_PASS)	// 2回連続のパス -> 終局.
			{
				this->_leaf_node_count++;
				int8_t disc_diff = pos.get_disc_diff();
				return disc_diff;
			}

			pos.pass();
			int8_t score = -search_with_tt<true>(pos, -beta, -alpha);
			pos.pass();
			return score;
		}

		this->_internal_node_count++;
		Bitboard bitboard = pos.bitboard();
		int32_t empty_count = bitboard.empty_count();
		bool use_tt = (empty_count - 1) > SEARCH_WITHOUT_TT_THRESHOLD;
		sort_moves(pos, moves, move_num);

		int8_t new_alpha = alpha;
		int8_t best_score = MIN_SCORE;
		for (int32_t i = 0; i < move_num; i++)
		{
			Move& move = moves[i];
			pos.update<false>(move);

			int8_t score;
			if (use_tt)
				score = -search_with_tt<false>(pos, -beta, -new_alpha);
			else
				score = -search_without_tt<false>(pos, -beta, -new_alpha);

			pos.set_bitboard(bitboard);	// undo

			if (score >= beta)	// beta cut
			{
				this->tt.set_entry(hash_code, score, MAX_SCORE);
				return score;
			}

			if (score > best_score)
			{
				if (score > new_alpha)
					new_alpha = score;	// 探索窓を狭める.
				best_score = score;
			}
		}

		if (best_score > alpha)	// 探索窓[alpha, beta]内の値であれば, best_scoreは真の値
			this->tt.set_entry(hash_code, best_score, best_score);
		else
			this->tt.set_entry(hash_code, MIN_SCORE, best_score);

		return best_score;
	}

	template<bool AFTER_PASS>
	int8_t EndgameSolver::search_without_tt(Position& pos, int8_t alpha, int8_t beta)
	{
		if (this->timeout || this->stop_flag)
			return 0;

		uint64_t mobility = pos.bitboard().calc_player_mobility();
		int32_t move_num = std::popcount(mobility);
		if (move_num == 0)
		{
			if constexpr (AFTER_PASS)	// 2回連続のパス -> 終局.
			{
				this->_leaf_node_count++;
				int8_t disc_diff = pos.get_disc_diff();

				if (steady_clock::now() - this->search_start_time >= this->time_limit)
					this->timeout = true;

				return disc_diff;
			}

			pos.pass();
			int8_t score = -search_without_tt<true>(pos, -beta, -alpha);
			pos.pass();
			return score;
		}

		this->_internal_node_count++;
		Bitboard bitboard = pos.bitboard();
		int32_t move;
		FOREACH_BIT(move, mobility)	// move orderingをしないので, 生のbitのまま着手を列挙.
		{
			pos.update<false>(static_cast<BoardCoordinate>(move));
			int8_t score = -search_without_tt<false>(pos, -beta, -alpha);
			pos.set_bitboard(bitboard);

			if (score > alpha)
				alpha = score;

			if (alpha >= beta)
				return alpha;
		}
		return alpha;
	}

	void EndgameSolver::sort_moves(reversi::Position& pos, utils::Array<Move, reversi::MAX_MOVE_NUM>& moves, int32_t move_num)
	{
		Array<int32_t, MAX_MOVE_NUM> next_move_nums;
		for (int32_t i = 0; i < move_num; i++)
		{
			pos.calc_flipped_discs(moves[i]);
			next_move_nums[i] = pos.get_next_move_num_after(moves[i]);
		}

		// 要素数が少ないので, とりあえず挿入ソート
		for (int32_t i = 1; i < move_num; i++)
		{
			if (next_move_nums[i - 1] > next_move_nums[i])
			{
				int32_t j = i;
				Move tmp_move = moves[i];
				int32_t tmp_next_move_num = next_move_nums[i];
				do
				{
					next_move_nums[j] = next_move_nums[j - 1];
					moves[j] = moves[j - 1];
					j--;
				} while (j > 0 && next_move_nums[j - 1] > tmp_next_move_num);
				moves[j] = tmp_move;
				next_move_nums[j] = tmp_next_move_num;
			}
		}
	}
}