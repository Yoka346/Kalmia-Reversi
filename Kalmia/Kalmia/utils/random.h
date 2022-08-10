#pragma once

#include "../common.h"
#include "math_functions.h"
#include <random>

namespace utils
{
	/**
	* @class
	* @brief	乱数生成器(mt19937_64)
	**/
	class Random
	{
	public:
		Random() :rand(rand_device()) { ; }
		Random(uint64_t seed) : rand(seed) { ; }
		uint32_t next() { return static_cast<uint32_t>(this->rand() >> 32); }
		uint64_t next_64() { return this->rand(); }
		uint32_t next(uint32_t upper_bound);
		uint32_t next(uint32_t min, uint32_t upper_bound);
		uint64_t next_64(uint64_t upper_bound);
		uint64_t next_64(uint64_t min, uint64_t upper_bound);

		/**
		* @fn
		* @brief	0.0以上1.0未満のランダムな32bit浮動小数点数を生成する.
		* @detail	ランダムな32bit浮動小数点数を生成するために, まず仮数部24bitを乱数生成器で生成し, 1.0 * 2^24を乗じることで,
		*			0.0以上1.0未満の実数とする.
		**/
		float next_single() { return (this->rand() >> 40) * (1.0f / (1U << 24)); }

		/**
		* @fn
		* @brief	0.0以上1.0未満のランダムな64bit浮動小数点数を生成する.
		* @detail	ランダムな32bit浮動小数点数を生成するために, まず仮数部53bitを乱数生成器で生成し, 1.0 * 2^53を乗じることで,
		*			0.0以上1.0未満の実数とする.
		**/
		float next_double() { return (this->rand() >> 11) * (1.0f / (1ULL << 53)); }

	private:
		static std::random_device rand_device;
		std::mt19937_64 rand;
	};
}