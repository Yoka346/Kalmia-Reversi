#pragma once
#include "engine.h"

#include <iostream>
#include <map>
#include <thread>
#include <future>

#include "../utils/random.h"
#include "../io/logger.h"
#include "../search/mcts/uct.h"
#include "../search/endgame/search.h"

namespace engine
{
	enum class MoveSelection
	{
		STOCHASTICALLY,
		BEST
	};

	class Kalmia : public Engine
	{
	public:
		Kalmia();

		void init_options();
		void quit() override;
		void set_main_time(reversi::DiscColor color, std::chrono::milliseconds main_time_ms) override;
		void set_byoyomi(reversi::DiscColor color, std::chrono::milliseconds byoyomi) override;
		void set_byoyomi_stones(reversi::DiscColor color, int32_t byoyomi_stones) override;
		void set_time_inc(reversi::DiscColor color, std::chrono::milliseconds inc) override;
		void set_level(int32_t level) override;
		double get_eval_score_min() override;
		double get_eval_score_max() override;

	protected:
		bool on_ready() override;
		void on_cleared_position() override;
		void on_position_was_set() override;
		void on_undid_position() override;
		void on_updated_position(reversi::BoardCoordinate move) override;
		bool on_stop_thinking(std::chrono::milliseconds timeout) override;
		reversi::BoardCoordinate generate_move(bool ponder) override;

	private:
		inline static const std::string NAME = "Kalmia";
		inline static const std::string VERSION = "2.0";
		inline static const std::string AUTHOR = "Yoka346";
		static constexpr int32_t DEFAULT_ENDGAME_SOLVER_TT_SIZE_MIB = 256;

		std::string value_func_weight_path;
		std::unique_ptr<search::mcts::UCT> tree;
		search::endgame::EndgameSolver endgame_solver;
		utils::Random rand;
		std::future<search::mcts::SearchEndStatus> search_task;
		std::future < reversi::BoardCoordinate> endgame_solve_task;
		std::unique_ptr<io::Logger> logger;
		std::mutex logger_mutex;
		utils::GameTimer timer[2];

		bool search_task_is_completed();
		void stop_pondering();
		void stop_if_pondering();
		void write_log(const std::string& str);
		std::string search_info_to_string(const search::mcts::SearchInfo& search_info);
		void send_all_mid_search_info();
		void send_all_endgame_search_info();
		void collect_think_info(const search::mcts::SearchInfo& search_info, ThinkInfo& think_info);
		void collect_multi_pv(const search::mcts::SearchInfo& search_info, MultiPV& multi_pv);

		reversi::BoardCoordinate generate_mid_game_move(bool ponder);
		reversi::BoardCoordinate generate_end_game_move(bool ponder);
		void wait_for_mid_search();
		void wait_for_endgame_search();
		template <MoveSelection MOVE_SELECT>
		reversi::BoardCoordinate select_move(const search::mcts::SearchInfo& search_info, bool& extra_search_is_need);
		void update_score_type();

		// event handlers
		void on_value_func_weight_path_changed(EngineOption& sender, std::string& err_message);
		void on_thread_num_changed(EngineOption& sender, std::string& err_message);
		void on_node_num_limit_changed(EngineOption& sender, std::string& err_message);
		void on_endgame_move_num_changed(EngineOption& sender, std::string& err_message);
		void on_endgame_tt_size_mib_changed(EngineOption& sender, std::string& err_message);
		void on_enable_early_stopping_changed(EngineOption& sender, std::string& err_message);
		void on_thought_log_path_changed(EngineOption& sender, std::string& err_message);
	};

	template reversi::BoardCoordinate Kalmia::select_move<MoveSelection::STOCHASTICALLY>(const search::mcts::SearchInfo&, bool&);
	template reversi::BoardCoordinate Kalmia::select_move<MoveSelection::BEST>(const search::mcts::SearchInfo&, bool&);
}