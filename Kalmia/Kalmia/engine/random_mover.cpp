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

	bool RandomMover::set_option(const string& name, const string& value, string& err_msg)
	{
		if (!this->options.count(name))
		{
			err_msg = "invalid option.";
			return false;
		}

		this->options[name] = value;
		return false;
	}

	void RandomMover::get_options(EngineOptions& options)
	{
		for (auto& option : this->options)
			options.emplace_back(option);
	}

	void quit() {}

	BoardCoordinate RandomMover::generate_move(reversi::DiscColor color)
	{
		this->timer[color].start();
		this->_is_thinking = true;
		if (this->_position.side_to_move() != color)
			this->_position.pass();

		Array<Move, MAX_MOVE_NUM> moves;
		auto num = this->_position.get_next_moves(moves);
		auto idx = this->rand.next(num);
		auto move = num ? moves[idx].coord : BoardCoordinate::PASS;
		this->timer[color].stop();
		this->_is_thinking = false;
		return move;
	}

	// ’…èŒˆ’è‚Íˆêu‚ÅI‚í‚é‚Ì‚Å“Á‚É‚â‚é‚±‚Æ‚Í‚È‚¢.
	bool RandomMover::stop_thinking(std::chrono::milliseconds timeout_ms) { return true; }
}