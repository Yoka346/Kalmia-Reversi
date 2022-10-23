#include "engine_option.h"
#include <string>
#include <sstream>
#include <cassert>

using namespace std;

namespace engine
{
	string engine_options_to_string(EngineOptions options)
	{
		ostringstream oss;
		for (auto& item : options)
		{
			EngineOption option = item.second;
			const string& type = option.type();
			oss << "option name " << item.first << " type " << type;
			if (type != "button")
				oss << " default " << option.default_value();
			if (type == "spin")
				oss << " min " << option.min() << " max " << option.max();
			oss << "\n";
		}
		return oss.str();
	}

	EngineOption::EngineOption(bool value, size_t idx, const EventHandler& on_value_change) : _idx(0), _type("check"), _min(0), _max(0)
	{
		string value_str = value ? "true" : "false";
		this->_default_value = this->_current_value = value_str;
	}

	EngineOption::EngineOption(const string& value, size_t idx, const EventHandler& on_value_change) : _default_value(value), _current_value(value), _idx(0), _type("string"), _min(0), _max(0) {}

	EngineOption::EngineOption(int32_t value, int32_t min, int32_t max, size_t idx, const EventHandler& on_value_change) : _min(min), _max(max), _idx(idx), _type("spin"), on_value_change(on_value_change)
	{
		auto valur_str = std::to_string(value);
		this->_default_value = this->_current_value = valur_str;
	}

	EngineOption& EngineOption::operator=(const string& value)
	{
		assert(!_type.empty());

		this->_last_err_msg = "";

		if ((_type != "button" && value.empty())
			|| (_type == "check" && value != "true" && value != "false"))
			return *this;

		if (_type == "spin")
		{
			auto i = stoi(value);
			if (i < this->_min || i > this->_max)
				return *this;
		}

		if (_type != "button")
			this->_current_value = value;

		if (this->on_value_change)
			this->on_value_change(*this, this->_last_err_msg);
		
		return *this;
	}

	EngineOption::operator int32_t() const
	{
		assert(_type == "check" || _type == "spin");
		return (_type == "spin") ? stol(this->_current_value) : this->_current_value == "true";
	}
}