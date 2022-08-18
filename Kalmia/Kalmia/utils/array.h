#pragma once
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
	* @brief	定数配列.
	* @detail	イニシャライザがconstexpr関数でかつ, ConstantArrayオブジェクトがconstexprとして宣言されていれば, コンパイル時定数配列になる.
	*			そうでなければ, 実行時定数配列.
	**/
	template<class ElementType, size_t LEN>
	class ConstantArray
	{
	private:
		ElementType data[LEN];

	public:
		/**
		* イニシャライザから長さLENの配列を生成.
		* 
		* @param initializer 配列のデータの初期化を行う関数のポインタ.
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
