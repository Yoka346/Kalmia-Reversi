/**
* GGF(Generic Game Format)�t�@�C���̃p�[�T�[. �R�~����8x8���o�[�V�ɂ̂ݑΉ�.
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
		float first_player_score;
		bool is_resigned;
		bool is_timeout;
		bool is_mutual;
	};
	
	struct GGFMove
	{
		reversi::DiscColor color;
		std::optional<float> eval_score;
		std::optional<float> time;
	};

	struct GGFGameType
	{
		int32_t board_size;
		int32_t random_disc_count;
		bool is_anti_game = false;
		reversi::DiscColor prefered_color;

		GGFGameType() { ; }
		GGFGameType(std::istringstream& iss);

	private:
		void parse_board_size(std::istringstream& iss);
		void parse_options(std::istringstream& iss);
	};

	struct GGFReversiGame
	{
		std::string place;
		std::string date;	// �d�l��(https://skatgame.net/mburo/ggsa/ggf)�ɂ�, ���t�̃t�H�[�}�b�g�� year.month.day_hour:minute:second.zone �ƂȂ��Ă��邪,
							// �����ɂ���Ă�UNIX���Ԃ�p���Ă��蓝�ꐫ���Ȃ��̂�, ���t�͓ǂݎ��������������̂܂܊i�[����.
		std::string black_player_name;
		std::string white_player_name;
		float black_player_rating;
		float white_player_rating;
		utils::GameTimerOptions black_thinking_time;
		utils::GameTimerOptions white_thinking_time;
		GGFGameType game_type;
		GGFGameResult game_result;
		reversi::Position position;

		GGFReversiGame(const std::string& ggf_str);

	private:
		bool find_game_start_delimiter(std::istringstream& iss);
		void parse_properties(std::istringstream& iss);
		void parse_property(const std::string& property_name, std::istringstream& iss);
	};

	class GGFParserException : public exception
	{
	public:
		GGFParserException(const std::string& msg) : msg(msg) { ; }
		const char* what() const noexcept override { return this->msg.c_str(); }

	private:
		std::string msg;
	};
}