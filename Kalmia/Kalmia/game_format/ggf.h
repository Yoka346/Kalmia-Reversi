/**
* GGF(Generic Game Format)ファイルのパーサー. コミ無し8x8リバーシにのみ対応.
* 最低限の棋譜の読み込みと, NBoardに対応させるために実装したので, 以下のリンクの仕様を100%満たしてはいない(特に時間管理周辺は完全な対応はしていない).
* 
* GGF仕様書: https://skatgame.net/mburo/ggsa/ggf
* GGFの時間の仕様書: https://skatgame.net/mburo/ggsa/clock
**/
#pragma once
#include <string>
#include <chrono>
#include <optional>

#include "../utils/game_timer.h"
#include "../reversi/types.h"
#include "../reversi/position.h"

namespace game_format
{
	struct GGFGameResult
	{
		std::optional<float> first_player_score;
		bool is_resigned = false;
		bool is_timeout = false;
		bool is_mutual = false;
		
		bool is_unknown() const { return !this->first_player_score.has_value(); }
	};
	
	struct GGFMove
	{
		reversi::DiscColor color;
		reversi::BoardCoordinate coord;
		std::optional<float> eval_score;
		std::optional<float> time;
	};

	struct GGFReversiGame
	{
		std::string place;
		std::string date;	// 仕様書には, 日付のフォーマットは year.month.day_hour:minute:second.zone となっているが,
							// 棋譜によってはUNIX時間を用いており統一性がないので, 日付は読み取った文字列をそのまま格納する.
		std::string black_player_name;
		std::string white_player_name;
		float black_player_rating = 0.0f;
		float white_player_rating = 0.0f;
		utils::GameTimerOptions black_thinking_time;
		utils::GameTimerOptions white_thinking_time;
		GGFGameResult game_result;
		reversi::Position position;
		std::vector<GGFMove> moves;

		GGFReversiGame(const std::string& ggf_str);

	private:
		bool find_game_start_delimiter(std::istringstream& iss);
		void parse_properties(std::istringstream& iss);
		void parse_property(const std::string& property_name, std::istringstream& iss);
		void parse_time(const std::string& time_str, utils::GameTimerOptions& timer_options);
		void parse_result(const std::string& res_str);
		void parse_position(const std::string& pos_str);
		void parse_move(reversi::DiscColor color, const std::string& move_str);
	};

	class GGFParserException : public std::exception
	{
	public:
		GGFParserException(const std::string& msg) : msg(msg) { ; }
		char const* what() const noexcept override { return this->msg.c_str(); }

	private:
		std::string msg;
	};
}