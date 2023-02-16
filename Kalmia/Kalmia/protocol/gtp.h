#pragma once
#include <iostream>
#include <sstream>
#include <fstream>
#include <string>
#include <map>
#include <functional>

#include "protocol.h"
#include "../reversi/constant.h"
#include "../engine/engine.h"

namespace protocol
{
	class GTP : public IProtocol
	{
	public:
		using CommandHandler = std::function<void(int, std::istringstream&)>;
		using CommandMap = std::map <std::string, CommandHandler>;

		inline static const std::string VERSION = "2.0";

		GTP(std::istream* gtp_in = &std::cin, std::ostream* gtp_out = &std::cout) : engine(engine), gtp_in(gtp_in), gtp_out(gtp_out), commands(), quit_flag(true), logger() { init(); }
		void init();
		void mainloop(engine::Engine* engine, const std::string& log_file_path);
		void mainloop(engine::Engine* engine) { std::string null_path = ""; mainloop(engine, null_path); }

	private:
		engine::Engine* engine;
		CommandMap commands;
		bool quit_flag;
		std::istream* gtp_in;
		std::ostream* gtp_out;
		std::ofstream logger;

		void gtp_success(int id, const std::string& msg);
		void gtp_success(int id) { std::string empty = "";  gtp_success(id, empty); }
		void gtp_failure(int id, const std::string& msg);
		void init_engine(engine::Engine* engine);
		CommandHandler to_handler(void (GTP::*exec_cmd)(int, std::istringstream&));

		// version 2.0 commands

		void exec_protocol_version_command(int id, std::istringstream& args) { gtp_success(id, GTP::VERSION); }
		void exec_name_command(int id, std::istringstream& args) { gtp_success(id, GTP::engine->name()); }
		void exec_version_command(int id, std::istringstream& args) { gtp_success(id, GTP::engine->version()); }
		void exec_known_command_command(int id, std::istringstream& args);	// known_commandという名前のコマンドを実行するので, 関数名は誤植ではない.
		void exec_list_commands_command(int id, std::istringstream& args);
		void exec_quit_command(int id, std::istringstream& args);
		void exec_board_size_command(int id, std::istringstream& args);
		void exec_clear_board_command(int id, std::istringstream& args) { GTP::engine->clear_position(); gtp_success(id); }
		void exec_komi_command(int id, std::istringstream& args) { gtp_success(id); }	// これは囲碁専用のコマンドなので, 特に何もしない.
		void exec_play_command(int id, std::istringstream& args);
		void exec_genmove_command(int id, std::istringstream& args);
		void exec_undo_command(int id, std::istringstream& args);
		void exec_time_settings_command(int id, std::istringstream& args);
		void exec_time_left_command(int id, std::istringstream& args);
		void exec_loadsgf_command(int id, std::istringstream& args);
		void exec_color_command(int id, std::istringstream& args);
		void exec_reg_genmove_command(int id, std::istringstream& args);
		void exec_showboard_command(int id, std::istringstream& args);

		// version 1.0 commands(legacy)

		void exec_black_command(int id, std::istringstream& args);
		void exec_playwhite_command(int id, std::istringstream& args);
		void exec_genmove_black_command(int id, std::istringstream& args);
		void exec_genmove_white_command(int id, std::istringstream& args);

		// gogui-rules commands
		// GoGuiというGUIプログラムをリバーシに対応させるために必要.

		void exec_rules_game_id_command(int id, std::istringstream& args) { gtp_success(id, "Reversi"); }
		void exec_rules_board_size_command(int id, std::istringstream& args) { gtp_success(id, std::to_string(reversi::BOARD_SIZE)); }
		void exec_rules_legal_moves_command(int id, std::istringstream& args);
		void exec_rules_side_to_move_command(int id, std::istringstream& args) { gtp_success(id, color_to_string(this->engine->position().side_to_move())); }
		void exec_rules_final_result_command(int id, std::istringstream& args);

		// original commands

		// 現在の盤面からの着手の列をf5d6c3...形式で入力するコマンド.
		void exec_set_moves_command(int id, std::istringstream& args);

		// EngineOptionのリストを表示するコマンド.
		void exec_list_options(int id, std::istringstream& args);

		// EngineOptionの値を設定するコマンド.
		void exec_set_option_command(int id, std::istringstream& args);
	};
}