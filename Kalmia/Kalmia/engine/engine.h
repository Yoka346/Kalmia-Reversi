#pragma once
#include "../utils//game_timer.h"
#include "../reversi/types.h"
#include "../reversi/position.h"
#include "engine_option.h"
#include <vector>
#include <tuple>
#include <functional>

namespace engine
{
	using EngineOptions = std::vector<std::pair<std::string, EngineOption>>;

	class Engine
	{
	protected:
		std::string _name;
		std::string _version;
		utils::GameTimer timer;
		reversi::Position _position;
		bool _is_thinking;

		/**
		* @fn
		* @brief 文字列で表現される情報をエンジンの呼び出し元(GUIやサーバーなど)に送る.
		* @param (msg) 送る文字列.
		* @detail 思考ログなどの文字列で表現される情報をエンジンの呼び出し元(GUIやサーバーなど)に送る.
		* 送った文字列は, プロトコルによって適切な形で出力される. 例えば, USIプロトコルであれば, info stringコマンドを利用し,
		* GTPであれば, エラー出力に出力する.
		**/
		inline void send_text_message(std::string msg) { this->on_message_is_sent(msg); }

	public:
		// エンジンが文字列を送信するときに呼び出されるハンドラ.
		std::function<void(std::string&)> on_message_is_sent = [](std::string&) {};

		Engine(std::string name, std::string version) : _name(name), _version(version), _position(), timer(), _is_thinking(false) { ; }
		inline const std::string& name() const { return this->_name; }
		inline const std::string& version() const { return this->_version; }
		inline const reversi::Position& position() const { return this->_position; }
		inline virtual void set_position(reversi::Position& pos) { this->_position = pos; }
		inline bool is_thinking() { return this->_is_thinking; }

		inline void set_time(std::chrono::milliseconds main_time, std::chrono::milliseconds byoyomi, std::chrono::milliseconds inc)
		{
			this->timer = GameTimer(main_time, byoyomi, inc);
		}

		/**
		* @fn
		* @brief 思考エンジンが保有している盤面の情報を着手moveによって更新する.
		* @param (move) 着手(ディスクを配置する座標).
		* @return 盤面の更新に成功したらtrue.
		**/
		inline virtual bool update_position(reversi::BoardCoordinate move) { return this->_position.update<true>(move); }

		/**
		* @fn
		* @brief 思考エンジンのオプション値を設定する.
		* @param (name) オプション名.
		* @param (value) オプション値.
		* @param (err_msg) エラーメッセージ.
		* @return オプション値の設定に成功したらtrue. 失敗したらエラーメッセージをerr_msgに格納してfalseを返す.
		**/
		virtual bool set_option(const std::string& name, const std::string& value, std::string& err_msg) = 0;

		/**
		* @fn
		* @brief 思考エンジンのオプション値を設定する.
		* @param (name) オプション名.
		* @param (value) オプション値.
		* @return オプション値の設定に成功したらtrue.
		**/
		inline bool set_option(const std::string name, const std::string& value)
		{
			std::string dummy;
			return set_option(name, value, dummy);
		}

		/**
		* @fn
		* @brief エンジンのオプション名とオプション値のタプルのリストを取得する.
		* @params (options) エンジンのオプション名とオプション値のタプルのリスト
		**/
		virtual void get_options(EngineOptions& options) = 0;

		/**
		* @fn
		* @brief 最善手を生成する.
		* @param (side_to_move) 手番.
		* @param (move) 生成された着手の格納先.
		* @note この関数はstop_thinking関数が呼ばれた際に直ちに終了し, 暫定の最善手をmoveに格納しなければならない.
		* また, 思考中は_is_thinkingをtrueに, 思考終了時をfalseにしなければならない.
		**/
		virtual void generate_move(reversi::DiscColor side_to_move, reversi::BoardCoordinate& move) = 0;

		/**
		* @fn
		* @brief 思考エンジンが思考中の場合はそれを停止する.
		* @param (timeout_ms) タイムアウト時間(ms).
		* @return 思考が正常に終了したらtrue.
		* @note この関数は別スレッドで実行中のgenerate_move関数を終了させる際に用いる. 
		**/
		virtual bool stop_thinking(std::chrono::milliseconds timeout_ms) = 0;
	};
}