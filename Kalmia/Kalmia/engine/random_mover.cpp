#pragma once
#include "random_mover.h"
#include "../utils/array.h"
#include "../reversi/constant.h"
#include "../reversi/types.h"
#include "../reversi/move.h"
#include <functional>

using namespace std;
using namespace utils;
using namespace reversi;

namespace engine
{
	void RandomMover::init_options()
	{
		using namespace placeholders;
		EventHandler func = bind(&RandomMover::on_rand_seed_change, *this, _1);
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
			options.push_back(option);
	}

	void RandomMover::generate_move(reversi::DiscColor side_to_move, reversi::BoardCoordinate& move)
	{
		if (this->_position.side_to_move() != side_to_move)
			this->_position.pass();

		Array<Move, MAX_MOVE_NUM> moves;
		auto num = this->_position.get_next_moves(moves);
		move = num ? moves[this->rand.next(num)].coord : BoardCoordinate::PASS;
	}

	// ’…èŒˆ’è‚Íˆêu‚ÅI‚í‚é‚Ì‚Å“Á‚É‚â‚é‚±‚Æ‚Í‚È‚¢.
	bool RandomMover::stop_thinking(std::chrono::milliseconds timeout_ms) {}
}