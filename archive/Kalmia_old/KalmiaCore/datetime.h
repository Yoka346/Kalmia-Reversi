#pragma once
#include "pch.h"
#include <ctime>

#pragma warning(disable : 4996)

struct DateTime
{
public:
	DateTime() : DateTime(1970, 1, 1, 0, 0, 0) { ; }
	DateTime(uint32_t year, uint32_t month, uint32_t day, uint32_t hour, uint32_t minute, uint32_t second)
		:year(year), month(month), day(day), hour(hour), minute(minute), second(second) { }
	
	static DateTime get_now() 
	{
		time_t now = time(nullptr);
		struct tm* dt_now = localtime(&now);
		return DateTime(dt_now->tm_year, dt_now->tm_mon, dt_now->tm_mday, dt_now->tm_hour, dt_now->tm_min, dt_now->tm_sec);
	}

	uint32_t get_year() const { return this->year; }
	uint32_t get_month() const { return this->month; }
	uint32_t get_day() const { return this->day; }
	uint32_t get_hour() const { return this->hour; }
	uint32_t get_minute() const  { return this->minute; }
	uint32_t get_second() const { return this->second; }
	const uint32_t* as_array() const { return (uint32_t*)this; }

private:
	uint32_t year;
	uint32_t month;
	uint32_t day;
	uint32_t hour;
	uint32_t minute;
	uint32_t second;
};