#pragma once
#include "engine.h"

#include <iostream>
#include <map>
#include <thread>
#include <future>

#include "../utils/random.h"
#include "../io/logger.h"
#include "../search/mcts/uct.h"

namespace engine
{
	enum class MoveSelection
	{
		STOCHASTICALLY,
		BEST
	};

	class Kalmia : public Engine
	{
	private:
		inline static const std::string NAME = "Kalmia";
		inline static const std::string VERSION = "1.5";

		std::map<std::string, EngineOption> options;

		search::mcts::UCT tree;
		utils::Random rand;
		std::future<search::mcts::SearchEndStatus> search_task;
		io::Logger logger;

		double softmax_temperature = 1.0;

		bool search_task_is_completed();
		void stop_pondering();
		void write_log(const std::string& str);
		std::string search_info_to_string(const search::mcts::SearchInfo& search_info);

		reversi::BoardCoordinate generate_mid_game_move(reversi::DiscColor color);
		void wait_for_search();
		template <MoveSelection MOVE_SELECT>
		reversi::BoardCoordinate select_move(const search::mcts::SearchInfo& search_info, bool& extra_search_is_need);

		// event handlers
		void on_thread_num_changed(EngineOption& sender, std::string& err_message);
		void on_node_num_limit_changed(EngineOption& sender, std::string& err_message);
		void on_softmax_temperature_changed(EngineOption& sender, std::string& err_message);

	public:
		Kalmia(const std::string& value_func_param_file_path, const std::string& log_file_path);
		Kalmia(const std::string& value_func_param_file_path, const std::string& log_file_path, std::ostream* log_out);

		void init_options(); 
		bool set_option(const std::string& name, const std::string& value, std::string& err_msg) override;
		void get_options(EngineOptions& options) override;
		bool update_position(reversi::DiscColor color, reversi::BoardCoordinate move) override;
		bool undo_position() override;
	    reversi::BoardCoordinate generate_move(reversi::DiscColor color) override;
		bool stop_thinking(std::chrono::milliseconds timeout_ms) override;
		void quit() override;
	};

	template reversi::BoardCoordinate Kalmia::select_move<MoveSelection::STOCHASTICALLY>(const search::mcts::SearchInfo&, bool&);
	template reversi::BoardCoordinate Kalmia::select_move<MoveSelection::BEST>(const search::mcts::SearchInfo&, bool&);
}