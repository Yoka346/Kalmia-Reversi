#pragma once
#include <memory>
#include <functional>
#include <initializer_list>
#include <algorithm>
#include <stdexcept>
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
	public:
		constexpr Array() : data() { ; }

		constexpr Array(const ElementType* data, size_t data_len = LEN) : data()
		{
			if (data_len > LEN)
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

		constexpr Array(void (*initializer)(ElementType*, size_t)) : data() { initializer(this->data, LEN); }

		constexpr const ElementType* begin() const { return &this->data[0]; }
		constexpr const ElementType* end() const { return this->data + LEN; }

		ElementType& operator[](size_t idx)
		{
			assert(idx >= 0 && idx < LEN);
			return this->data[idx];
		}

		constexpr const ElementType& operator[](size_t idx) const
		{
			assert(idx >= 0 && idx < LEN);
			return this->data[idx];
		}

		constexpr bool operator==(const Array<ElementType, LEN>& right) const { return std::equal(begin(), end(), right.begin()); }
		constexpr size_t length() const { return LEN; }
		constexpr ElementType* as_raw_array() { return this->data; }
		constexpr const ElementType* as_raw_array() const { return this->data; }

	private:
		ElementType data[LEN];
	};

	/**
	* @class
	* @brief	�ǂݎ���p�z��. Array�I�u�W�F�N�g�̎Q�Ƃ�����Ɏ���, ����Array���̃f�[�^�ɑ΂���ǂݎ��@�\��񋟂���.
	* @detail	�g�����Ƃ��Ă�, private�ϐ��Ƃ���Array�I�u�W�F�N�g��錾����, ����Array�I�u�W�F�N�g�̓ǂݎ��݂̂��O���Ɍ��J�������ꍇ�ɂ��̃��b�p�[��p����ƕ֗�.
	**/
	template<class ElementType, size_t LEN>
	class ReadonlyArray
	{
	public:
		constexpr ReadonlyArray(Array<ElementType, LEN>& data) : data(data) {}

		constexpr const ElementType* begin() const { return this->data.begin(); }
		constexpr const ElementType* end() const { return this->data.end(); }
		const ElementType& operator[](size_t idx) const { return this->data[idx]; }
		constexpr bool operator==(const ReadonlyArray<ElementType, LEN>& right) const { return this->data == right.data; }
		constexpr bool operator==(const Array<ElementType, LEN>& right) const { return this->data == right; }
		constexpr size_t length() const { return LEN; }
		const ElementType* as_raw_array() const { return this->data.as_raw_array(); }

	private:
		Array<ElementType, LEN>& data;
	};

	/**
	* @class
	* @brief	�f�o�b�O���ɂ̂ݔ͈̓`�F�b�N���s�����I�z��.
	**/
	template<class ElementType>
	class DynamicArray
	{
	public:
		DynamicArray(size_t length) : data(std::make_unique<ElementType[]>(length)), _length(length) { ; }

		DynamicArray(const DynamicArray<ElementType>& src) 
		{
			this->_length = src._length;
			this->data = std::make_unique<ElementType[]>(this->_length);
			std::memcpy(this->data.get(), src.data.get(), sizeof(ElementType) * this->_length);
		}

		DynamicArray(DynamicArray<ElementType>&& src) : _length(src._length), data(std::move(src.data)) { ; }

		const ElementType* begin() const { return this->data.get(); }
		const ElementType* end() const { return this->data.get() + this->_length; }
		ElementType* begin() { return this->data.get(); }
		ElementType* end() { return this->data.get() + this->_length; }

		ElementType& operator[](size_t idx)
		{
			assert(idx >= 0 && idx < this->_length);
			return this->data.get()[idx];
		}

		const ElementType& operator[](size_t idx) const
		{
			assert(idx >= 0 && idx < this->_length);
			return this->data.get()[idx];
		}

		DynamicArray<ElementType>& operator=(const DynamicArray<ElementType>& right) 
		{ 
			this->_length = right._length;
			this->data.reset();
			this->data = std::make_unique<ElementType[]>(this->_length);
			std::memcpy(this->data.get(), right.data.get(), sizeof(ElementType) * this->_length);
			return *this;
		}

		DynamicArray<ElementType>& operator=(DynamicArray<ElementType>&& right)
		{
			this->_length = right._length;
			this->data.reset();
			this->data = std::move(right.data);
			return *this;
		}

		bool operator==(const DynamicArray<ElementType>& right) const { return this->_length == right._length && std::equal(begin(), end(), right.begin()); }
		size_t length() const { return this->_length; }
		ElementType* as_raw_array() { return this->data.get(); }
		const ElementType* as_raw_array() const { return this->data.get(); }

		void reset(size_t len)
		{
			this->data.reset(new ElementType[len]);
			this->_length = len;
		}

	private:
		std::unique_ptr<ElementType[]> data;
		size_t _length;
	};

	/**
	* @class
	* @brief	�ǂݎ���p���I�z��. DynamicArray�I�u�W�F�N�g�̎Q�Ƃ�����Ɏ���, ����DynamicArray���̃f�[�^�ɑ΂���ǂݎ��@�\��񋟂���.
	**/
	template<class ElementType>
	class ReadonlyDynamicArray
	{
	public:
		ReadonlyDynamicArray(DynamicArray<ElementType>& data) : data(data) { ; }

		const ElementType* begin() const { return this->data.begin(); }
		const ElementType* end() const { return this->data.end(); }
		const ElementType& operator[](size_t idx) const { this->data[idx]; }
		bool operator==(const ReadonlyDynamicArray<ElementType>& right) const { return this->data == right.data; }
		bool operator==(const DynamicArray<ElementType>& right) const { return this->data == right; }
		size_t length() const { return this->data.length(); }
		const ElementType* as_raw_array() const { return this->data.as_raw_array(); }

	private:
		DynamicArray<ElementType>& data;
	};
}
