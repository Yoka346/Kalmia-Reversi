#pragma once
#include "pch.h"

/**
 * @class
 * @brief	Provides random value generator.
 * @detail	This is the wrapper class for std::mt19937_64
*/
class Random
{
public:
	Random() :rand(rand_device()) { ; }
	Random(uint64_t seed) : rand(seed) { ; }
	inline uint32_t next() { return (uint32_t)(this->rand() >> 32); }
	inline uint64_t next_64() { return this->rand(); }

	// to generate random real number between 0.0 and 1.0, generates significand n-bit(24-bit for single, 53-bit for double), then multiplies 1.0 * 2^n.
	inline float next_single() { return (this->rand() >> 40) * (1.0f / (1U << 24)); }
	inline double next_double() { return (this->rand() >> 11) * (1.0 / (1ULL << 53)); }

	DLL_EXPORT uint32_t next(uint32_t max);
	DLL_EXPORT uint64_t next_64(uint64_t max);
	DLL_EXPORT uint32_t next(uint32_t min, uint32_t max);
	DLL_EXPORT uint64_t next_64(uint64_t min, uint64_t max);

private:
	static std::random_device rand_device;
	std::mt19937_64 rand;
};
