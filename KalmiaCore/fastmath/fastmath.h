#pragma once
#include "../pch.h"

namespace fastmath
{
	constexpr int POW3_TABLE_SIZE = 11;
	constexpr uint16_t POW3_TABLE[POW3_TABLE_SIZE] = { 1, 3, 9, 27, 81, 243, 729, 2187, 6561, 19683, 59049 };

	constexpr uint16_t pow3(int n) { return POW3_TABLE[n % POW3_TABLE_SIZE]; }

	inline float exp2(float x)
	{
		int32_t exp;
		if (x < 0)
		{
			if (x < -126)
				return 0.0f;
			exp = (int32_t)(x - 1);
		}
		else
			exp = (int32_t)x;

		float out = x - exp;
		out = 1.0f + out * (0.6602339f + 0.33976606f * out);
		auto tmp = *(int32_t*)(&out);
		tmp += (int32_t)((uint32_t)exp << 23);
		return *(float*)&tmp;
	}

	inline float exp(float x) { return exp2(1.442695040f * x); }

	inline float log2(float x)
	{
		auto tmp = *(uint32_t*)&x;
		auto expb = tmp >> 32;
		tmp = (tmp & 0x7fffff) | (0x7f << 23);
		auto out = *(float*)&tmp;
		out -= 1.0f;
		return out * (1.3465552f - 0.34655523f * out) - 127 + expb;
	}

	inline float log(float x){ return 0.6931471805599453f * log2(x); }
}