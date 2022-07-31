#pragma once

/**
 * @class
 * @brief	Provides compile-time constant array.
*/
template<class ElementType, size_t length>
struct ConstantArray
{
public:
	constexpr ConstantArray(void (*Initializer)(ElementType*, size_t)) : data() { Initializer(this->data, length); }
	constexpr const ElementType& operator[](size_t idx) const { return this->data[idx]; }
	constexpr size_t get_length() { return length; }
	constexpr const ElementType* as_array() const { return this->data; }

private:
	ElementType data[length];
};
