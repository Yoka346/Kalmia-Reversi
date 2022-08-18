#pragma once
#include <exception>
#include <cassert>

namespace utils
{
	/**
	* @class 
	* @brief	�f�o�b�O���ɂ̂ݔ͈̓`�F�b�N���s���z��.
	**/
	template<class ElementType, size_t LEN>
	class Array
	{
	private:
		ElementType data[LEN];

	public:
		Array() : data() { ; }

		Array(ElementType* data, size_t data_len = LEN) : data()
		{
			if (data_len >= LEN)
				throw std::out_of_range("The length of \"data\" cannnot be greater than \"LEN\".");

			for (int i = 0; i < data_len; i++)
				this->data[i] = data[i];
		}

		ElementType& operator[](size_t idx)
		{
			assert(idx >= 0 && idx < LEN);
			return this->data[idx];
		}

		size_t length() const { return LEN; }
		ElementType* as_raw_array() const { return this->data; }
	};

	/**
	* @class
	* @brief	�萔�z��.
	* @detail	�C�j�V�����C�U��constexpr�֐��ł���, ConstantArray�I�u�W�F�N�g��constexpr�Ƃ��Đ錾����Ă����, �R���p�C�����萔�z��ɂȂ�.
	*			�����łȂ����, ���s���萔�z��.
	**/
	template<class ElementType, size_t LEN>
	class ConstantArray
	{
	private:
		ElementType data[LEN];

	public:
		/**
		* �C�j�V�����C�U���璷��LEN�̔z��𐶐�.
		* 
		* @param initializer �z��̃f�[�^�̏��������s���֐��̃|�C���^.
		**/
		constexpr ConstantArray(void (*initializer)(ElementType*, size_t)) :data() { initializer(this->data, LEN); }

		constexpr const ElementType& operator[](size_t idx) const
		{
			assert(idx >= 0 && idx < LEN);
			return this->data[idx];
		}

		constexpr int length() const { return LEN; }
		constexpr const ElementType* as_raw_array() const { return this->data; }
	};
}
