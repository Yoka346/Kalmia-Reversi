#pragma once
#pragma inline_depth(255)

#include <cstdint>

#include "../config.h"

namespace utils
{
	/**
	* @struct
	* @brief ���[�v��W�J����e���v���[�g�\����.
	* @detail N��̃��[�v��, N�̓��������̗���ɕϊ�����. �Ⴆ��,
	*
	* int32_t sum = 0;
	* LoopUnroller<3>()([&](const int32_t i){ sum += i + 1; });
	*
	* �Ƃ����R�[�h���������ꍇ, ���̃R�[�h�͈ȉ��̂悤�ɓW�J�����.
	*
	* int32_t sum = 0;
	* sum += 1;
	* sum += 2;
	* sum += 3;
	*
	* ���[�v��W�J���邱�Ƃ�, ���[�v�J�E���^�͈̔̓`�F�b�N�������郁���b�g��, �R���p�C���̍œK���������₷���Ȃ�Ƃ��������b�g������.
	*
	* @cite �����̎v�l�G���W��Apery��Unroller�\���̂����ɂ��Ă���.
	* https://github.com/HiraokaTakuya/apery/blob/d14471fc879062bfabbd181eaa91e90c7cc28a71/src/common.hpp#L249
	**/
	template<int32_t N>
	struct LoopUnroller
	{
		template <typename T> constexpr FORCE_INLINE void operator()(T t)
		{
			LoopUnroller<N - 1>()(t);
			t(N - 1);
		}
	};

	template<>
	struct LoopUnroller<0>
	{
		template <typename T> constexpr FORCE_INLINE void operator()(T t) { ; }
	};
}