#pragma once
#include <functional>
#include <initializer_list>
#include <exception>
#include <cassert>

namespace utils
{
	/**
	* @class 
	* @brief	デバッグ時にのみ範囲チェックを行う配列.
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
			if (data_len > LEN)
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

		constexpr Array(void (*initializer)(ElementType*, size_t)) : data() { initializer(this->data, LEN); }

		constexpr const ElementType* begin() const { return &this->data[0]; }
		constexpr const ElementType* end() const { return this->data + LEN; }

		inline ElementType& operator[](size_t idx)
		{
			assert(idx >= 0 && idx < LEN);
			return this->data[idx];
		}

		constexpr const ElementType& operator[](size_t idx) const
		{
			assert(idx >= 0 && idx < LEN);
			return this->data[idx];
		}

		constexpr size_t length() const { return LEN; }
		inline ElementType* as_raw_array() { return this->data; }
		constexpr const ElementType* as_raw_array() const { return this->data; }
	};

	/**
	* @class
	* @brief	読み取り専用配列. Arrayオブジェクトの参照を内部に持ち, そのArray内のデータに対する読み取り機能を提供する.
	* @detail	使い方としては, private変数としてArrayオブジェクトを宣言して, そのArrayオブジェクトの読み取りのみを外部に公開したい場合にこのラッパーを用いると便利.
	**/
	template<class ElementType, size_t LEN>
	class ReadonlyArray
	{
	private:
		Array<ElementType, LEN>& data;

	public:
		constexpr ReadonlyArray(Array<ElementType, LEN>& data) : data(data) {}

		constexpr const ElementType* begin() const { return this->data.begin(); }
		constexpr const ElementType* end() const { return this->data.end(); }
		inline const ElementType& operator[](size_t idx) const { this->data[idx]; }
		constexpr size_t length() const { return LEN; }
		inline const ElementType* as_raw_array() const { return this->data.as_raw_array(); }
	};
}
