#pragma once

/**
 * @class
 * @brief	Provides compile-time constant array.
*/
template<class ElementType, int length>
struct ConstantArray
{
	ElementType data[length];
	constexpr ConstantArray(void (*Initializer)(ElementType*, int)) : data() { Initializer(this->data, length); }
	inline const ElementType& operator[](size_t idx) const { return this->data[idx]; }
};
