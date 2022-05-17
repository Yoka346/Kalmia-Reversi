#include "random.h"

inline uint32_t Random::next(uint32_t max) { return (uint32_t)next_64(max); }

inline uint64_t Random::next_64(uint64_t max)
{
	if (max == 0)
		return 0;

	auto n = fastmath::log2_ceiling(max);
	uint64_t res;
	while ((res = this->rand() >> (64 - n)) >= max);
	return res;
}

inline uint32_t Random::next(uint32_t min, uint32_t max) { return next_64(min + max) - min; }

inline uint64_t Random::next_64(uint64_t min, uint64_t max) 
{ 
	auto res = next_64(min + max) - min;
	assert(res >= min && res < max);
	return res;
}
