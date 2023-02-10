/**
* GGF(Generic Game Format)�t�@�C���̃p�[�T�[. �R�~����8x8���o�[�V�ɂ̂ݑΉ�.
* �Œ���̊����̓ǂݍ��݂�, NBoard�ɑΉ������邽�߂Ɏ��������̂�, �ȉ��̃����N�̎d�l��100%�������Ă͂��Ȃ�(���Ɏ��ԊǗ����ӂ͊��S�ȑΉ��͂��Ă��Ȃ�).
* 
* GGF�d�l��: https://skatgame.net/mburo/ggsa/ggf
* GGF�̎��Ԃ̎d�l��: https://skatgame.net/mburo/ggsa/clock
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
		std::string date;	// �d�l���ɂ�, ���t�̃t�H�[�}�b�g�� year.month.day_hour:minute:second.zone �ƂȂ��Ă��邪,
							// �����ɂ���Ă�UNIX���Ԃ�p���Ă��蓝�ꐫ���Ȃ��̂�, ���t�͓ǂݎ��������������̂܂܊i�[����.
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