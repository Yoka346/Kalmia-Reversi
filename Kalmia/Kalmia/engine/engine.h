#pragma once

#include <iostream>
#include <vector>
#include <map>
#include <tuple>
#include <functional>
#include <future>
#include <atomic>
#include <optional>

#include "../utils//game_timer.h"
#include "../reversi/types.h"
#include "../reversi/move.h"
#include "../reversi/position.h"
#include "engine_option.h"

namespace engine
{
	enum class EngineState
	{
		NOT_READY,
		READY,
		PLAYING,
		GAME_OVER
	};

	enum class EvalScoreType
	{
		WIN_RATE,
		DISC_DIFF,
		EXACT_WLD,
		EXACT_DISC_DIFF,
		OTHER
	};

	/**
	* @struct
	* @brief エンジンの思考情報(探索局面数や経過時間, 最善応手列 etc...)をまとめる構造体.
	**/
	struct ThinkInfo
	{
		std::optional<std::chrono::milliseconds> ellapsed_ms = std::nullopt;
		std::optional<uint64_t> node_count = std::nullopt;
		std::optional<double> nps = std::nullopt;
		std::optional<int32_t> depth = std::nullopt;
		std::optional<int32_t> selected_depth = std::nullopt;
		std::optional<double> eval_score = std::nullopt;
		std::optional <std::vector <reversi::BoardCoordinate >> pv = std::nullopt;
	};

	/**
	* @struct
	* @brief MultiPVの要素. 最善応手列と評価をまとめた構造体.
	**/
	struct MultiPVItem
	{
		std::optional<uint64_t> node_count = std::nullopt;
		std::optional<double> eval_score = std::nullopt;	
		EvalScoreType eval_score_type = EvalScoreType::OTHER;
		bool eval_score_is_exact = false;
		reversi::GameResult exact_wld = reversi::NOT_OVER;
		std::optional<int32_t> exact_disc_diff = std::nullopt;
		std::vector<reversi::BoardCoordinate> pv;
	};

	using MultiPV = std::vector<MultiPVItem>;

	/**
	* @struct
	* @brief エンジンの着手.
	**/
	struct EngineMove
	{
		reversi::BoardCoordinate coord = reversi::NULL_COORD;
		std::optional<float> eval_score = std::nullopt;
		std::optional<std::chrono::milliseconds> ellapsed_ms = std::nullopt;
	};

	/**
	* @class
	* @brief 思考エンジンが継承する抽象クラス.
	**/
	class Engine
	{
	public:
		// エンジンが文字列を送信するしたに呼び出されるハンドラ.
		std::function<void(const std::string&)> on_message_was_sent = [](const auto&) { ; };

		// エンジンがエラー文字列を送信したときに呼び出されるハンドラ.
		std::function<void(const std::string&)> on_err_message_was_sent = [](const auto&) { ; };

		// エンジンが思考情報を送信したときに呼び出されるハンドラ.
		std::function<void(const ThinkInfo&)> on_think_info_was_sent = [](const auto&) { ; };

		// エンジンがmulti PVを送信したときに呼び出されるハンドラ.
		std::function<void(const MultiPV&)> on_multi_pv_was_sent = [](const auto&) { ; };

		// エンジンがgo関数の結果を送信したときに呼び出されるハンドラ.
		std::function<void(const EngineMove& move)> on_move_was_sent = [](const auto& move) { ; };

		// エンジンがanalyze関数の実行を終えたときに呼び出されるハンドラ.
		std::function<void()> on_analysis_ended = []() {};

		Engine(const std::string& name, const std::string& version, const std::string& author)
			: _name(name), _version(version), _author(author), _score_type(EvalScoreType::OTHER), _position(), position_history()
		{
			
		}

		EngineState state() const { return this->_state; }
		const std::string& name() const { return this->_name; }
		const std::string& version() const { return this->_version; }
		const std::string& author() const { return this->_author; }
		const reversi::Position& position() const { return this->_position; }
		const EvalScoreType score_type() const { return this->_score_type; }

		bool ready();
		void start_game();
		void end_game();
		void set_position(reversi::Position& pos);
		void clear_position();

		virtual void quit() { ; };
		virtual void set_main_time(reversi::DiscColor color, std::chrono::milliseconds main_time_ms) = 0;
		virtual void set_byoyomi(reversi::DiscColor color, std::chrono::milliseconds byoyomi) = 0;
		virtual void set_byoyomi_stones(reversi::DiscColor color, int32_t byoyomi_stones) = 0;
		virtual void set_time_inc(reversi::DiscColor color, std::chrono::milliseconds inc) = 0;

		/**
		* @fn
		* @brief 思考エンジンの強さを設定する.
		* @param (level) 思考エンジンの強さレベル. レベルの値が意味するものは, 思考エンジンによって異なる.
		**/
		virtual void set_level(int32_t level) = 0;

		/**
		* @fn
		* @brief 序盤における手加減度を設定する.
		* @param (contempt) Bookに登録されている先手からみた石差で引き分けとみなす値. 例えば, contempt = 2 であれば, 先手からみて2石分勝っている局面を引き分けとみなす.
		**/
		virtual void set_book_contempt(int32_t contempt) = 0;

		/**
		* @fn
		* @brief 現在の対局をBookに登録する.
		**/
		virtual void add_current_game_to_book() {};

		/**
		* @fn
		* @brief 思考エンジンが保有している盤面の情報を着手moveによって更新する.
		* @param (color) ディスクの色.
		* @param (move) 着手(ディスクを配置する座標).
		* @return 盤面の更新に成功したらtrue.
		**/
		bool update_position(reversi::DiscColor color, reversi::BoardCoordinate move);

		/**
		* @fn
		* @brief 思考エンジンが保有している盤面を1手前の状態に戻す.
		* @return 盤面の更新に成功したらtrue.
		**/
		bool undo_position();

		/**
		* @fn
		* @brief 思考エンジンのオプション値を設定する.
		* @param (name) オプション名.
		* @param (value) オプション値.
		* @param (err_msg) エラーメッセージ.
		* @return オプション値の設定に成功したらtrue. 失敗したらエラーメッセージをerr_msgに格納してfalseを返す.
		**/
		bool set_option(const std::string& name, const std::string& value, std::string& err_msg);

		/**
		* @fn
		* @brief 思考エンジンのオプション値を設定する.
		* @param (name) オプション名.
		* @param (value) オプション値.
		* @return オプション値の設定に成功したらtrue.
		**/
		bool set_option(const std::string name, const std::string& value)
		{
			std::string dummy;
			return set_option(name, value, dummy);
		}

		/**
		* @fn
		* @brief エンジンのオプション名とオプション値のタプルのリストを取得する.
		* @params (options) エンジンのオプション名とオプション値のタプルのリスト
		**/
		void get_options(EngineOptions& options);

		/**
		* @fn
		* @brief 最善手を生成する.
		* @param (ponder)
		* @detail この関数の結果は, on_move_is_sentハンドラによって通知される.
		**/
		virtual void go(bool ponder) = 0;

		/**
		* @fn
		* @brief 現在の局面の候補手を解析する.
		* @param (move_num) 上位何手までの解析を行うか.
		**/
		virtual void analyze(int32_t move_num) = 0;

		/**
		* @fn
		* @brief 思考エンジンが思考中の場合はそれを停止する.
		* @param (timeout_ms) タイムアウト時間(ms).
		* @return 思考が正常に終了したらtrue.
		* @note この関数は別スレッドで実行中のgo関数を終了させる際に用いる.
		**/
		virtual bool stop_thinking(std::chrono::milliseconds timeout) = 0;

		/**
		* @fn
		* @brief 評価値が取り得る最小値を返す.
		* @return 評価値の最小値.
		* @detail 例えば, 予想石差を評価値とする場合は -64 を返し, 予想勝率を評価値とする場合は 0.0 を返す.
		**/
		virtual double get_eval_score_min() { return 0.0; }

		/**
		* @fn
		* @brief 評価値が取り得る最大値を返す.
		* @return 評価値の最大値.
		* @detail 例えば, 予想石差を評価値とする場合は 64 を返し, 予想勝率を評価値とする場合は 1.0 を返す.
		**/
		virtual double get_eval_score_max() { return 0.0; }

	protected:
		EvalScoreType _score_type;
		std::map<std::string, EngineOption> options;

		bool stop_flag() { return this->_stop_flag.load(); }
		virtual bool on_ready() { return true; }
		virtual void on_start_game() { ; }
		virtual void on_end_game() { ; }
		virtual void on_cleared_position() { ; }
		virtual void on_position_was_set() { ; }
		virtual void on_undid_position() { ; }
		virtual void on_updated_position(reversi::BoardCoordinate move) { ; }

		/**
		* @fn
		* @brief 文字列で表現される情報をエンジンの呼び出し元に送る.
		* @param (msg) 送る文字列.
		**/
		void send_text_message(const std::string& msg);

		/**
		* @fn
		* @brief エラーメッセージをエンジンの呼び出し元に送る.
		* @param (msg) 送る文字列.
		**/
		void send_err_message(const std::string& msg);

		/**
		* @fn
		* @brief エンジンの思考情報をエンジンの呼び出し元に送る.
		**/
		void send_think_info(ThinkInfo& think_info);

		/**
		* @fn
		* @brief Multi PVをエンジンのエンジンの呼び出し元に送る.
		**/
		void send_multi_pv(MultiPV& multi_pv);

		/**
		* @fn
		* @brief エンジンの着手をエンジンの呼び出し元に送る.
		**/
		void send_move(EngineMove& move);

	private:
		EngineState _state = EngineState::NOT_READY;
		std::string _name;
		std::string _version;
		std::string _author;
		reversi::Position _position;
		std::vector<reversi::Position> position_history;
		std::atomic<bool> _stop_flag;
		std::mutex start_thinking_mutex;
	};
}