#pragma once
#include "random_mover.h"
#include <functional>

using namespace std;

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
}