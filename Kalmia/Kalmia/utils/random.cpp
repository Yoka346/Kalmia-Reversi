#include "random.h"

using namespace std;
using namespace utils;

random_device Random::rand_device = random_device();

inline uint32_t Random::next(uint32_t upper_bound) { return (uint32_t)(next_64(upper_bound)); }
inline uint32_t Random::next(uint32_t min, uint32_t upper_bound) { return (uint32_t)(next_64(min, upper_bound)); }

inline uint64_t Random::next_64(uint64_t upper_bound)
{
	if (upper_bound == 0)
		return 0;
}

inline uint64_t Random::next_64(uint64_t min, uint64_t upper_bound)
{

}