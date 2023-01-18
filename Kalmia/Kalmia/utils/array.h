/**
* �z��N���X.
* �����v���O�����u�Z�I�v��array.h���Q�l�ɂ��Ă���(https://github.com/gikou-official/Gikou/blob/master/src/common/array.h).
**/

#pragma once
#include <cstring>
#include <memory>
#include <functional>
#include <initializer_list>
#include <algorithm>
#include <stdexcept>
#include <cassert>

namespace utils
{
	template<class, size_t...> class Array;
	template<class, size_t...> struct Element;

	// �ȉ�, ElementType�̓��ꉻ.
	// �e���v���[�g�G�C���A�X�𒼐ړ��ꉻ���邱�Ƃ͕s�\�Ȃ̂�, ElementType�\���̂�using����ł���.
	
	// 2�����ȏ�̎���, Array�̗v�f��Array�ɂȂ�(�z��̔z��).
	template<class T, size_t LEN_0, size_t ...LEN_1>	
	struct Element<T, LEN_0, LEN_1...>	
	{
		using Type = Array<T, LEN_1...>;
	};

	// 1�����̎���, Array�̗v�f�͒l���̂���.
	template<class T, size_t LEN>
	struct Element<T, LEN>
	{
		using Type = T;
	};

	/**
	* @class 
	* @brief	�f�o�b�O���ɂ̂ݔ͈̓`�F�b�N���s���z��.
	**/
	template<class T, size_t LEN_0, size_t ...LEN_1>
	class Array<T, LEN_0, LEN_1...>
	{
	public:
		using ElementType = Element<T, LEN_0, LEN_1...>::Type;

		constexpr Array() : data() { ; }

		constexpr Array(const ElementType* data, size_t data_len = LEN_0) : data()
		{
			if (data_len > LEN_0)
				throw std::out_of_range("The length of \"data\" cannnot be greater than \"LEN\".");

			for (size_t i = 0; i < data_len; i++)
				this->data[i] = data[i];
		}

		constexpr Array(std::initializer_list<ElementType> init_list) : data()
		{
			size_t i = 0;
			for (auto& value : init_list)
				this->data[i++] = value;
		}

		constexpr Array(void (*initializer)(ElementType*, size_t)) : data() { initializer(this->data, LEN_0); }

		constexpr const ElementType* begin() const { return &this->data[0]; }
		constexpr const ElementType* end() const { return this->data + LEN_0; }

		ElementType& operator[](size_t idx)
		{
			assert(idx >= 0 && idx < LEN_0);
			return this->data[idx];
		}

		constexpr const ElementType& operator[](size_t idx) const
		{
			assert(idx >= 0 && idx < LEN_0);
			return this->data[idx];
		}

		constexpr bool operator==(const Array<ElementType, LEN_0, LEN_1...>& right) const { return std::equal(begin(), end(), right.begin()); }
		constexpr size_t length() const { return LEN_0; }
		constexpr ElementType* as_raw_array() { return this->data; }
		constexpr const ElementType* as_raw_array() const { return this->data; }
		void clear() { std::memset(this->data, 0, sizeof(ElementType) * LEN_0); }

	private:
		ElementType data[LEN_0];
	};

	/**
	* @class
	* @brief	�ǂݎ���p�z��. Array�I�u�W�F�N�g�̎Q�Ƃ�����Ɏ���, ����Array���̃f�[�^�ɑ΂���ǂݎ��@�\��񋟂���.
	* @detail	�g�����Ƃ��Ă�, private�ϐ��Ƃ���Array�I�u�W�F�N�g��錾����, ����Array�I�u�W�F�N�g�̓ǂݎ��݂̂��O���Ɍ��J�������ꍇ�ɂ��̃��b�p�[��p����ƕ֗�.
	**/
	template<class T, size_t LEN_0, size_t ...LEN_1>
	class ReadonlyArray
	{
	public:
		constexpr ReadonlyArray(Array<T, LEN_0, LEN_1...>& data) : data(data) {}

		constexpr const T* begin() const { return this->data.begin(); }
		constexpr const T* end() const { return this->data.end(); }
		const T& operator[](size_t idx) const { return this->data[idx]; }
		constexpr bool operator==(const ReadonlyArray<T, LEN_0, LEN_1...>& right) const { return this->data == right.data; }
		constexpr bool operator==(const Array<T, LEN_0, LEN_1...>& right) const { return this->data == right; }
		constexpr size_t length() const { return LEN_0; }
		const T* as_raw_array() const { return this->data.as_raw_array(); }

	private:
		Array<T, LEN_0, LEN_1...>& data;
	};

	/**
	* @class
	* @brief	�f�o�b�O���ɂ̂ݔ͈̓`�F�b�N���s�����I�z��.
	**/
	template<class T>
	class DynamicArray
	{
	public:
		DynamicArray(size_t length) : data(std::make_unique<T[]>(length)), _length(length) { ; }

		DynamicArray(const DynamicArray<T>& src) 
		{
			this->_length = src._length;
			this->data = std::make_unique<T[]>(this->_length);
			std::memcpy(this->data.get(), src.data.get(), sizeof(T) * this->_length);
		}

		DynamicArray(DynamicArray<T>&& src) : _length(src._length), data(std::move(src.data)) { ; }

		const T* begin() const { return this->data.get(); }
		const T* end() const { return this->data.get() + this->_length; }
		T* begin() { return this->data.get(); }
		T* end() { return this->data.get() + this->_length; }

		T& operator[](size_t idx)
		{
			assert(idx >= 0 && idx < this->_length);
			return this->data.get()[idx];
		}

		const T& operator[](size_t idx) const
		{
			assert(idx >= 0 && idx < this->_length);
			return this->data.get()[idx];
		}

		DynamicArray<T>& operator=(const DynamicArray<T>& right) 
		{ 
			this->_length = right._length;
			this->data.reset();
			this->data = std::make_unique<T[]>(this->_length);
			std::memcpy(this->data.get(), right.data.get(), sizeof(T) * this->_length);
			return *this;
		}

		DynamicArray<T>& operator=(DynamicArray<T>&& right)
		{
			this->_length = right._length;
			this->data.reset();
			this->data = std::move(right.data);
			return *this;
		}

		bool operator==(const DynamicArray<T>& right) const { return this->_length == right._length && std::equal(begin(), end(), right.begin()); }
		size_t length() const { return this->_length; }
		T* as_raw_array() { return this->data.get(); }
		const T* as_raw_array() const { return this->data.get(); }

		void reset(size_t len)
		{
			this->data.reset(new T[len]);
			this->_length = len;
		}

	private:
		std::unique_ptr<T[]> data;
		size_t _length;
	};

	/**
	* @class
	* @brief	�ǂݎ���p���I�z��. DynamicArray�I�u�W�F�N�g�̎Q�Ƃ�����Ɏ���, ����DynamicArray���̃f�[�^�ɑ΂���ǂݎ��@�\��񋟂���.
	**/
	template<class T>
	class ReadonlyDynamicArray
	{
	public:
		ReadonlyDynamicArray(DynamicArray<T>& data) : data(data) { ; }

		const T* begin() const { return this->data.begin(); }
		const T* end() const { return this->data.end(); }
		const T& operator[](size_t idx) const { this->data[idx]; }
		bool operator==(const ReadonlyDynamicArray<T>& right) const { return this->data == right.data; }
		bool operator==(const DynamicArray<T>& right) const { return this->data == right; }
		size_t length() const { return this->data.length(); }
		const T* as_raw_array() const { return this->data.as_raw_array(); }

	private:
		DynamicArray<T>& data;
	};
}
