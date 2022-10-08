#pragma once
#include "../utils/random.h"
#include "engine.h"
#include <map>

namespace engine
{
	class RandomMover : public Engine
	{
	private:
		inline static const std::string NAME = "Random Mover";
		inline static const std::string VERSION = "0.0";

		Random rand;
		std::map<std::string, EngineOption> options;

		void init_options();

		// event handlers
		void on_rand_seed_change(EngineOption& sender, string& err_msg) { this->rand = Random(sender); }

	public:
		RandomMover() : Engine(NAME, VERSION), rand(), options() { init_options(); }
		RandomMover(uint64_t rand_seed) : Engine(NAME, VERSION), rand(rand_seed), options() { init_options(); }
		bool set_option(const std::string& name, const std::string& value, std::string& err_msg) override;
		void get_options(EngineOptions& options) override;
		reversi::BoardCoordinate generate_move(reversi::DiscColor color) override;
		bool stop_thinking(std::chrono::milliseconds timeout_ms) override;
		void quit() override { }
	};
}
