#pragma once
#include "game_timer.h"

#include <cmath>

#include "../utils/exception.h"

using namespace utils;

using namespace std::chrono;

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
	this->_time_left_ms -= ellapsed;
	this->_is_ticking = false;

	if (this->_time_left_ms < milliseconds::zero())
	{
		this->_time_left_ms = milliseconds::zero();
		this->_timeout = true;
		return;
	}

	if (this->_time_left_ms < this->_byoyomi_ms)
		if (--this->_byoyomi_stones_left == 0)
		{
			this->_byoyomi_stones_left = this->_byoyomi_stones;
			this->_time_left_ms = this->_byoyomi_ms;
		}
	
	this->_time_left_ms += this->_increment_ms;
}

void GameTimer::reset()
{
	this->_is_ticking = this->_timeout = false;
	this->_time_left_ms = this->_main_time_ms + this->_byoyomi_ms;
}

template<bool INCLUDE_BYOYOMI>
milliseconds GameTimer::time_left_ms()
{
	if (!is_ticking())
		this->_time_left_ms;

	auto prev_check_point = this->check_point;
	this->check_point = high_resolution_clock::now();
	this->_time_left_ms -= duration_cast<milliseconds>(this->check_point - prev_check_point);

	if constexpr (INCLUDE_BYOYOMI)
		return std::max(milliseconds::zero(), this->_time_left_ms);

	return std::max(milliseconds::zero(), this->_time_left_ms - this->_byoyomi_ms);
}

milliseconds GameTimer::byoyomi_left_ms()
{
	if (time_left_ms<false>() != milliseconds::zero())
		return this->_byoyomi_ms;
	else
		return this->_time_left_ms;
}