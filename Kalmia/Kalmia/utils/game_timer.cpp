#include "game_timer.h"

#include <cmath>

#include "../utils/exception.h"

using namespace std;
using namespace std::chrono;

using namespace utils;

void GameTimer::set_main_time(milliseconds main_time)
{
	if (this->_is_ticking)
		throw invalid_operation("timer is ticking.");

	this->_main_time = main_time;
	this->_time_left = this->_main_time + this->_byoyomi;
}

void GameTimer::set_byoyomi(milliseconds byoyomi)
{
	if (this->_is_ticking)
		throw invalid_operation("timer is ticking.");

	if (this->_time_left <= this->_byoyomi)
	{
		this->_byoyomi = byoyomi;
		this->_time_left = this->_byoyomi;
	}
	else
	{
		this->_time_left -= this->_byoyomi;
		this->_byoyomi = byoyomi;
		this->_time_left += this->_byoyomi;
	}
}

void GameTimer::set_byoyomi_stones(int32_t byoyomi_stones)
{
	if (this->_is_ticking)
		throw invalid_operation("timer is ticking.");

	this->_byoyomi_stones = byoyomi_stones;
}

void GameTimer::set_increment(milliseconds inc)
{
	if (this->_is_ticking)
		throw invalid_operation("timer is ticking.");

	this->_increment = inc;
}

void GameTimer::set_main_time_left(milliseconds main_time_left)
{
	if (this->_is_ticking)
		throw invalid_operation("timer is ticking.");

	if (main_time_left > this->_main_time)
		throw invalid_argument("main time left cannnot be greater than main time.");

	if (this->_time_left <= this->_byoyomi)
		this->_time_left += main_time_left;
	else
		this->_time_left = main_time_left + this->_byoyomi;
}

void GameTimer::set_byoyomi_stones_left(int32_t byoyomi_stones_left)
{
	if (this->_is_ticking)
		throw invalid_operation("timer is ticking.");

	if (byoyomi_stones_left > this->_byoyomi_stones)
		throw invalid_argument("byoyomi stones left cannnot be greater than byoyomi stones.");

	this->_byoyomi_stones_left = _byoyomi_stones_left;
}

void GameTimer::set(milliseconds main_time_ms, milliseconds byoyomi_ms, int32_t byoyomi_stones, milliseconds inc_ms)
{
	this->_is_ticking = false;
	this->_main_time = main_time_ms;
	this->_byoyomi = byoyomi_ms;
	this->_byoyomi_stones = byoyomi_stones;
	this->_increment = inc_ms;
	this->_time_left = main_time_ms + _byoyomi;
	this->_byoyomi_stones_left = _byoyomi_stones_left;
}

void GameTimer::set_left(milliseconds main_time_left, int32_t byoyomi_stones_left)
{
	this->_is_ticking = false;
	this->_time_left = main_time_left + this->_byoyomi;
	this->_byoyomi_stones_left = byoyomi_stones_left;
}

void GameTimer::start()
{
	if (is_ticking())
		throw utils::invalid_operation("timer is ticking.");

	if (timeout())
		throw utils::invalid_operation("timeout.");

	this->check_point = high_resolution_clock::now();
	this->_is_ticking = true;
}

void GameTimer::stop()
{
	if (!this->_is_ticking)
		throw utils::invalid_operation("timer is not ticking.");

	auto ellapsed = duration_cast<milliseconds>(high_resolution_clock::now() - this->check_point);
	this->_time_left -= ellapsed;
	this->_is_ticking = false;

	if (this->_time_left < milliseconds::zero())
	{
		this->_time_left = milliseconds::zero();
		this->_timeout = true;
		return;
	}

	if (this->_time_left < this->_byoyomi)
		if (--this->_byoyomi_stones_left == 0)
		{
			this->_byoyomi_stones_left = this->_byoyomi_stones;
			this->_time_left = this->_byoyomi;
		}
	
	this->_time_left += this->_increment;
}

void GameTimer::reset()
{
	this->_is_ticking = this->_timeout = false;
	this->_time_left = this->_main_time + this->_byoyomi;
	this->_byoyomi_stones_left = this->_byoyomi_stones;
}

template<bool INCLUDE_BYOYOMI>
milliseconds GameTimer::time_left()
{
	if (!is_ticking())
		if constexpr (INCLUDE_BYOYOMI)
			return this->_time_left;
		else
			return std::max(milliseconds::zero(), this->_time_left - this->_byoyomi);

	auto prev_check_point = this->check_point;
	this->check_point = high_resolution_clock::now();
	this->_time_left -= duration_cast<milliseconds>(this->check_point - prev_check_point);

	if constexpr (INCLUDE_BYOYOMI)
		return std::max(milliseconds::zero(), this->_time_left);

	return std::max(milliseconds::zero(), this->_time_left - this->_byoyomi);
}

milliseconds GameTimer::byoyomi_left()
{
	if (time_left<false>() != milliseconds::zero())
		return this->_byoyomi;
	else
		return this->_time_left;
}