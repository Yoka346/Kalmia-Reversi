#pragma once
#include "engine_option.h"
#include <string>
#include <cassert>

using namespace std;

namespace engine
{
	EngineOption::EngineOption(bool value, size_t idx, const EventHandler& on_value_change) : idx(0), type("check"), min(0), max(0)
	{
		string value_str = value ? "true" : "false";
		this->default_value = this->current_value = value_str;
	}

	EngineOption::EngineOption(string& value, size_t idx, const EventHandler& on_value_change) : default_value(value), current_value(value), idx(0), type("string"), min(0), max(0) {}

	EngineOption::EngineOption(int32_t value, int32_t min, int32_t max, size_t idx, const EventHandler& on_value_change) : min(min), max(max), idx(idx), on_value_change(on_value_change)
	{
		auto valur_str = std::to_string(value);
		this->default_value = this->current_value = valur_str;
	}

	EngineOption& EngineOption::operator=(const string& value)
	{
		assert(!type.empty());

		if ((type != "button" && value.empty())
			|| (type == "check" && value != "true" && value != "false"))
			return *this;

		if (type == "spin")
		{
			auto i = stoi(value);
			if (i < this->min || i > this->max)
				return *this;
		}

		if (type != "button")
			this->current_value = value;

		if (this->on_value_change)
			this->on_value_change(*this);
		
		return *this;
	}

	EngineOption::operator int() const
	{
		assert(type == "check" || type == "spin");
		return (type == "spin") ? stoi(this->current_value) : this->current_value == "true";
	}
}