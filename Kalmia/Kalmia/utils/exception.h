#pragma once
#include <exception>

namespace utils
{
	/**
	* @class
	* @brief 仕様上意図されていない操作が行われた際に送出される例外.
	* @note 標準ライブラリで提供されている例外クラスに揃えるため, このクラス名のみ全て小文字のスネークケースを用いた.
	**/
	class invalid_operation : public std::logic_error
	{
	public:
		invalid_operation(const std::string& message) : std::logic_error(message) { ; }
	};
}
