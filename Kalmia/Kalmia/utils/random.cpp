#include "random.h"
#include <cassert>

using namespace std;
using namespace utils;

random_device Random::rand_device = random_device();

uint32_t Random::next(uint32_t upper_bound) { return static_cast<uint32_t>(next_64(upper_bound)); }
uint32_t Random::next(uint32_t min, uint32_t upper_bound) { return static_cast<uint32_t>(next_64(min, upper_bound)); }

/**
* @fn
* @brief	upper_bound�����̗����𐶐�����֐�.
* @detail	�悭��������]���p�������@���Ǝ኱�΂肪�o��̂�, 
			2^ceil(log(upper_bound)) �ȉ��̗����𐶐����鑀���upper_bound�������܂ŌJ��Ԃ�.
			upper_bound��������x�傫�Ȑ����ł����, ���̏ꍇ�̓��[�v�̉񐔂�1��ōς�.
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