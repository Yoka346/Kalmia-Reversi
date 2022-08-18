#pragma once
#include "game_timer.h"
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
	this->_is_ticking = false;
	this->_time_left_ms -= ellapsed;
	if (this->_time_left_ms < milliseconds::zero())
	{
		this->_time_left_ms = milliseconds::zero();
		this->_timeout = true;
	}

	if (this->_time_left_ms < this->_byoyomi_ms)
		this->_time_left_ms = this->_byoyomi_ms;
	
	this->_time_left_ms += this->_increment_ms;
}

void GameTimer::reset()
{
	this->_is_ticking = this->_timeout = false;
	this->_time_left_ms = this->_main_time_ms + this->_byoyomi_ms;
}

milliseconds GameTimer::time_left_ms()
{
	if (!is_ticking())
		return milliseconds::zero();

	auto prev_check_point = this->check_point;
	this->check_point = high_resolution_clock::now();
	return this->_main_time_ms -= duration_cast<milliseconds>(this->check_point - prev_check_point);
}