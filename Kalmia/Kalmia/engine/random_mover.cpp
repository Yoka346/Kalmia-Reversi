#include "random_mover.h"

#include <future>

#include "../utils/array.h"
#include "../reversi/constant.h"
#include "../reversi/types.h"
#include "../reversi/move.h"

using namespace std;
using namespace utils;
using namespace reversi;

namespace engine
{
	void RandomMover::init_options()
	{
		using namespace placeholders;
		EventHandler func = bind(&RandomMover::on_rand_seed_change, this, _1, _2);
		this->options["rand_seed"] = EngineOption(12345678, 0, INT32_MAX, this->options.size(), func);
	};

	void RandomMover::go(bool ponder) 
	{
		Array<Move, MAX_MOVE_NUM> moves;
		auto num = position().get_next_moves(moves);
		auto idx = this->rand.next(num);
		EngineMove move;
		move.coord = num ? moves[idx].coord : BoardCoordinate::PASS;
		send_move(move);
	}
}