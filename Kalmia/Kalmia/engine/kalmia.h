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
	class Kalmia : public Engine
	{
	private:
		inline static const std::string NAME = "Kalmia";
		inline static const std::string VERSION = "1.5";

		std::map<std::string, EngineOption> options;

		search::mcts::UCT tree;
		utils::Random rand;
		std::future<void> think_task;
		io::Logger logger;

		double softmax_temperture = 0.0;

		bool think_task_is_completed();
		void stop_pondering();
		void write_log(const std::string& str);
		void get_search_info_string(std::string& str);

		// event handlers
		void on_softmax_temperture_changed(const EngineOption& sender, std::string& err_message);

	public:
		Kalmia(search::mcts::UCTOptions tree_options, const std::string& log_file_path);
		Kalmia(search::mcts::UCTOptions tree_options, const std::string& log_file_path, std::ostream* log_out);
		void init_options(); 
		bool set_option(const std::string& name, const std::string& value, std::string& err_msg);
		void get_options(EngineOptions& options);
		bool update_position(reversi::DiscColor color, reversi::BoardCoordinate move);
		bool undo_position();
		void generate_move(reversi::DiscColor color, reversi::BoardCoordinate& move);
		bool stop_thinking(std::chrono::milliseconds timeout_ms);
		void quit();
	};
}