#pragma once
#include "../utils/random.h"
#include "engine.h"

namespace engine
{
	class RandomMover : public Engine
	{
	private:
		Random rand;

	public:
		RandomMover(uint64_t rand_seed) :Engine("Random Mover", "0.0"), rand(rand_seed) { }
		void generate_move(reversi::DiscColor side_to_move, reversi::BoardCoordinate& move);

	};
}
