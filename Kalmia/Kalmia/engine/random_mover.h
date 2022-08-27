#pragma once
#include "../utils/random.h"
#include "engine.h"
#include <map>

namespace engine
{
	class RandomMover : public Engine
	{
	private:
		const std::string NAME = "Random Mover";
		const std::string VERSION = "0.0";

		Random rand;
		std::map<std::string, EngineOption> options;

		// event handlers
		void on_rand_seed_change(const EngineOption& sender) { this->rand = Random(sender); }

	public:
		RandomMover() : Engine(NAME, VERSION), rand(), options() { init_options(); }
		RandomMover(uint64_t rand_seed) : Engine(NAME, VERSION), rand(rand_seed), options() { init_options(); }
		void init_options();
		bool set_option(const std::string& name, const std::string& value, std::string& err_msg);
		void get_options(EngineOptions& options);
		void generate_move(reversi::DiscColor side_to_move, reversi::BoardCoordinate& move);
		bool stop_thinking(std::chrono::milliseconds timeout_ms);
	};
}
