#pragma once

/**
 * @class
 * @brief	Provides runtime constant array.
*/
template<class ElementType, size_t length>
class ReadOnlyArray
{
public:
	ReadOnlyArray(void (*Initializer)(ElementType*, size_t)) : data() { Initializer(this->data, length); }
	const ElementType& operator[](size_t idx) const { return this->data[idx]; }
	size_t get_length() { return length; }
	const ElementType* as_array() const { return this->data; }

private:
	ElementType data[length];
};