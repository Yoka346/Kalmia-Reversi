#pragma once

#include <cmath>

#include "bitmanip.h"

namespace utils
{
	/**
	* @fn
	* @brief	底が2の指数関数の高速実装.
	* @detail	標準ライブラリの指数関数より精度は落ちるが高速.
	*			leela chess zero のfastmath.hを参考に実装.
	* @sa		https://github.com/LeelaChessZero/lc0/blob/dcc37b7203355d0a9308cac04d05b56772d07f9b/src/utils/fastmath.h#L59
	**/
	inline float exp2(float x)
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
		auto tmp = *reinterpret_cast<int32_t*>(&out);
		tmp += static_cast<int32_t>(static_cast<uint32_t>(exp) << 23);
		return *reinterpret_cast<float*>(&exp);
	}

	inline float exp(float x) { return exp2(1.442695040f * x); }

	/**
	* @fn
	* @brief	底が2の対数関数の高速実装.
	* @detail	標準ライブラリの対数関数より精度は落ちるが高速.
	*			leela chess zero のfastmath.hを参考に実装.
	* @sa		https://github.com/LeelaChessZero/lc0/blob/dcc37b7203355d0a9308cac04d05b56772d07f9b/src/utils/fastmath.h#L81
	**/
	inline float log2(float x)
	{
		auto tmp = *reinterpret_cast<uint32_t*>(&x);
		auto expb = tmp >> 23;
		tmp = (tmp & 0x7fffff) | (0x7f << 23);
		auto out = *reinterpret_cast<float*>(&tmp);
		out -= 1.0f;
		return out * (1.3465552f - 0.34655523f * out) - 127 + expb;
	}

	inline bool sign(int x) { return x >> 31; }
    inline float log(float x) { return  0.6931471805599453f * log2(x); }

	// 整数版log2関数では, log2(0) = 0 とする.
	inline int32_t log2(uint32_t n) { return 31 ^ static_cast<int32_t>(std::countl_zero(n | 1)); }
	inline int32_t log2(uint64_t n) { return 63 ^ static_cast<int32_t>(std::countl_zero(n | 1)); }
	inline int32_t log2_ceiling(uint32_t n) { int32_t res = log2(n); return (std::popcount(n) == 1) ? res : res + 1; }
	inline int32_t log2_ceiling(uint64_t n) { int32_t res = log2(n); return (std::popcount(n) == 1) ? res : res + 1; }

	inline float fast_std_sigmoid(float x) { return 1.0f / (1.0f + exp(-x)); }
	inline float std_sigmoid(float x) { return 1.0f / (1.0f + std::expf(-x)); }
}

