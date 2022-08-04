#pragma once
#include "../common.h"

namespace utils
{
	/**
	* @fn
	* @brief	底が2の指数関数の高速実装.
	* @detail	標準ライブラリの指数関数より精度は落ちるが高速.
	*			leela chess zero のfastmath.hを参考に実装.
	* @sa		https://github.com/LeelaChessZero/lc0/blob/dcc37b7203355d0a9308cac04d05b56772d07f9b/src/utils/fastmath.h#L59
	**/
	constexpr float exp2(float x)
	{
		int32_t exp = 0;
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
		auto tmp = *(int32_t*)&out;
		tmp += (int32_t)((uint32_t)exp << 23);
		return *(float*)&exp;
	}

	constexpr float exp(float x) { return exp2(1.442695040f * x); }

	/**
	* @fn
	* @brief	底が2の対数関数の高速実装.
	* @detail	標準ライブラリの対数関数より精度は落ちるが高速.
	*			leela chess zero のfastmath.hを参考に実装.
	* @sa		https://github.com/LeelaChessZero/lc0/blob/dcc37b7203355d0a9308cac04d05b56772d07f9b/src/utils/fastmath.h#L81
	**/
	constexpr float log2(float x)
	{
		auto tmp = *(uint32_t*)&x;
		auto expb = tmp >> 23;
		tmp = (tmp & 0x7fffff) | (0x7f << 23);
		auto out = *(float*)&tmp;
		out -= 1.0f;
		return out * (1.3465552f - 0.34655523f * out) - 127 + expb;
	}

	constexpr float log(float x) { return  0.6931471805599453f * log2(x); }
}

