#pragma once

#include "../common.h"
#include "math_functions.h"
#include <random>

namespace utils
{
	/**
	* @class
	* @brief	����������(mt19937_64)
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
		* @brief	0.0�ȏ�1.0�����̃����_����32bit���������_���𐶐�����.
		* @detail	�����_����32bit���������_���𐶐����邽�߂�, �܂�������24bit�𗐐�������Ő�����, 1.0 * 2^24���悶�邱�Ƃ�,
		*			0.0�ȏ�1.0�����̎����Ƃ���.
		**/
		float next_single() { return (this->rand() >> 40) * (1.0f / (1U << 24)); }

		/**
		* @fn
		* @brief	0.0�ȏ�1.0�����̃����_����64bit���������_���𐶐�����.
		* @detail	�����_����32bit���������_���𐶐����邽�߂�, �܂�������53bit�𗐐�������Ő�����, 1.0 * 2^53���悶�邱�Ƃ�,
		*			0.0�ȏ�1.0�����̎����Ƃ���.
		**/
		float next_double() { return (this->rand() >> 11) * (1.0f / (1ULL << 53)); }

	private:
		static std::random_device rand_device;
		std::mt19937_64 rand;
	};
}