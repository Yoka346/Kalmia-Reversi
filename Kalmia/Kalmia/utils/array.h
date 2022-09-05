#pragma once
#include <initializer_list>
#include <exception>
#include <cassert>

namespace utils
{
	template<class T>
	constexpr int64_t index_of(const T* data, T target, size_t len)
	{
		for (auto i = 0ULL; i < len; i++)
			if (data[i] == target)
				return i;
		return -1ULL;
	}

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
		constexpr Array() : data() { ; }

		constexpr Array(const ElementType* data, size_t data_len = LEN) : data()
		{
			if (data_len >= LEN)
				throw std::out_of_range("The length of \"data\" cannnot be greater than \"LEN\".");

			for (int i = 0; i < data_len; i++)
				this->data[i] = data[i];
		}

		constexpr Array(std::initializer_list<ElementType> init_list) : data() 
		{
			size_t i = 0;
			for (auto& value : init_list)
				this->data[i++] = value;
		}

		inline ElementType& operator[](size_t idx)
		{
			assert(idx >= 0 && idx < LEN);
			return this->data[idx];
		}

		constexpr size_t length() const { return LEN; }
		inline ElementType* as_raw_array() const { return this->data; }
		constexpr size_t index_of(ElementType target) { return utils::index_of(this->data, target, LEN); }
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

		constexpr ConstantArray(const ElementType* data) : data()
		{
			for (size_t i = 0; i < LEN; i++)
				this->data[i] = data[i];
		}

		constexpr ConstantArray(std::initializer_list<ElementType> init_list) : data() 
		{
			size_t i = 0;
			for (auto& value : init_list)
				this->data[i++] = value;
		}

		constexpr const ElementType& operator[](size_t idx) const
		{
			assert(idx >= 0 && idx < LEN);
			return this->data[idx];
		}

		constexpr int length() const { return LEN; }
		constexpr const ElementType* as_raw_array() const { return this->data; }
		constexpr int64_t index_of(ElementType target) const { return utils::index_of(this->data, target, LEN); }
	};
}
