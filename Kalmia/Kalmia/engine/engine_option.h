#pragma once
#include <iostream>
#include <functional>

namespace engine
{
	class EngineOption;

	using EventHandler = std::function<void(const EngineOption&)>;

	/**
	* @class
	* @brief エンジンの設定項目
	* @detail エンジンの各種設定は, オプション名(string) -> EngineOptionItemのハッシュテーブルで管理される.
	**/
	class EngineOption
	{

	private:
		inline static const EventHandler NULL_HANDLER = [](const EngineOption& sender) {};

		// オプションのデフォルト値.
		std::string default_value;

		// オプションの現在値.
		std::string current_value;

		// オプション値の型.
		// USIプロトコルでは, GUIにボタンで設定するか, スピンコントロールで設定するかなどの,
		// 情報を伝える必要があるために必要. GTPなどのその他のプロトコルではほとんど不要.
		std::string type;

		// オプション値が整数である際の範囲. 
		int32_t min, max;

		size_t idx;

		// 新しいオプション値が設定されるときに呼び出されるハンドラ.
		EventHandler on_value_change;

	public:
		EngineOption(bool value, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);
		EngineOption(std::string& value, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);
		EngineOption(int32_t value, int32_t min, int32_t max, size_t idx, const EventHandler& on_value_change = NULL_HANDLER);

		// 代入演算子. これが呼び出されたタイミングでon_value_changedハンドラが呼び出される.
		EngineOption& operator=(const std::string& value);

		operator int() const;
		inline std::string to_string() { return this->current_value; }
	};
}