#pragma once
#include <iostream>
#include <string>
#include <functional>

namespace engine
{
	class EngineOption;

	using EventHandler = std::function<void(EngineOption&, std::string&)>;
	using EngineOptions = std::vector<std::pair<std::string, EngineOption>>;

	std::string engine_options_to_string(EngineOptions options);

	/**
	* @class
	* @brief エンジンの設定項目
	* @detail エンジンの各種設定は, オプション名(string) -> EngineOptionItemのハッシュテーブルで管理される.
	**/
	class EngineOption
	{
	public:
		EngineOption() { ; }
		EngineOption(bool value, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);
		EngineOption(const char* value, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);
		EngineOption(int32_t value, int32_t min, int32_t max, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);

		const std::string& default_value() const { return this->_default_value; }
		const std::string& current_value() const { return this->_current_value; }
		const std::string& type() const { return this->_type; }
		int32_t min() const { return this->_min; }
		int32_t max() const { return this->_max; }
		size_t idx() const { return this->_idx; }
		const std::string& last_err_msg() const { return this->_last_err_msg; }

		// 代入演算子. これが呼び出されたタイミングでon_value_changedハンドラが呼び出される.
		EngineOption& operator=(const std::string& value);

		operator int32_t() const;

	private:
		inline static const EventHandler NULL_HANDLER = [](EngineOption& sender, std::string& err_msg) {};

		// オプションのデフォルト値.
		std::string _default_value;

		// オプションの現在値.
		std::string _current_value;

		// オプション値の型.
		// USIプロトコルでは, GUIにボタンで設定するか, スピンコントロールで設定するかなどの,
		// 情報を伝える必要があるために必要. GTPなどのその他のプロトコルではほとんど不要.
		std::string _type;

		// オプション値が整数である際の範囲. 
		int32_t _min, _max;

		size_t _idx;

		// 新しいオプション値が設定されるときに呼び出されるハンドラ.
		EventHandler on_value_change;
		std::string _last_err_msg;
		const std::string& to_string() const { return this->_current_value; }
	};
}