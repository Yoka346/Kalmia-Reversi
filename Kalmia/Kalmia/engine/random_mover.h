#pragma once

#include <map>

#include "../utils/random.h"
#include "engine.h"

namespace engine
{
	class RandomMover : public Engine
	{
	public:
		RandomMover() : Engine(NAME, VERSION, AUTHOR), rand(), options() { init_options(); }
		RandomMover(uint64_t rand_seed) : Engine(NAME, VERSION, AUTHOR), rand(rand_seed), options() { init_options(); }

		void quit() override {}
		void set_main_time(reversi::DiscColor color, std::chrono::milliseconds main_time_ms) override {}
		void set_byoyomi(reversi::DiscColor color, std::chrono::milliseconds byoyomi) override {}
		void set_byoyomi_stones(reversi::DiscColor color, int32_t byoyomi_stones) override {}
		void set_time_inc(reversi::DiscColor color, std::chrono::milliseconds inc) override {}

	protected:
		reversi::BoardCoordinate generate_move(bool ponder) override;

	private:
		inline static const std::string NAME = "Random Mover";
		inline static const std::string VERSION = "0.0";
		inline static const std::string AUTHOR = "Yoka346";

		utils::Random rand;
		std::map<std::string, EngineOption> options;

		void init_options();

		// event handlers
		void on_rand_seed_change(EngineOption& sender, std::string& err_msg) { this->rand = Random(sender); }
	};
}
