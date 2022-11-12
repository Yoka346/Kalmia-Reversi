#include "random.h"
#include <cassert>

using namespace std;
using namespace utils;

random_device Random::rand_device = random_device();

uint32_t Random::next(uint32_t upper_bound) { return static_cast<uint32_t>(next_64(upper_bound)); }
uint32_t Random::next(uint32_t min, uint32_t upper_bound) { return static_cast<uint32_t>(next_64(min, upper_bound)); }

/**
* @fn
* @brief	upper_bound未満の乱数を生成する関数.
* @detail	よく見かける余剰を用いた方法だと若干偏りが出るので, 
			2^ceil(log(upper_bound)) 以下の乱数を生成する操作をupper_boundを下回るまで繰り返す.
			upper_boundがある程度大きな整数であれば, 大抵の場合はループの回数は1回で済む.
**/
uint64_t Random::next_64(uint64_t upper_bound)
{
	if (upper_bound <= 1)
		return 0;

	auto n = log2_ceiling(upper_bound);
	uint64_t res;
	while ((res = (this->rand() >> (64 - n))) >= upper_bound);
	assert(res < upper_bound);
	return res;
}

uint64_t Random::next_64(uint64_t min, uint64_t upper_bound)
{
	auto res = next_64(min + upper_bound) - min;
	assert(res >= min && res < upper_bound);
	return res;
}