#pragma once
#include <iostream>
#include <sstream>
#include <map>
#include <functional>

#include "../../utils/static_initializer.h"
#include "../../io/logger.h"
#include "../../reversi/constant.h"
#include "../../engine/engine.h"

namespace protocol
{
	class GTP
	{
	private:
		static engine::Engine* engine;
		static std::map <std::string, std::function<void(int, std::istream&)>> commands;
		inline static bool quit = false;
		static io::Logger logger;

		// version 2.0 commands
		inline static void exec_protocol_version_command(int id, std::istream& args) { gtp_success(id, GTP::VERSION); }
		inline static void exec_name_command(int id, std::istream& args) { gtp_success(id, GTP::engine->name()); }
		inline static void exec_version_command(int id, std::istream& args) { gtp_success(id, GTP::engine->version()); }
		static void exec_known_command_command(int id, std::istream& args);	// known_commandという名前のコマンドを実行するので, 関数名は誤植ではない.
		static void exec_list_commands_command(int id, std::istream& args);
		static void exec_quit_command(int id, std::istream& args);
		static void exec_board_size_command(int id, std::istream& args);
		static void exec_clear_board_command(int id, std::istream& args) { GTP::engine->clear_position(); gtp_success(id); }
		static void exec_komi_command(int id, std::istream& args) { gtp_success(id); }	// これは囲碁専用のコマンドなので, 特に何もしない.
		static void exec_play_command(int id, std::istream& args);
		static void exec_genmove_command(int id, std::istream& args);
		inline static void exec_undo_command(int id, std::istream& args){} // 保留


	public:
		inline static const std::string VERSION = "2.0";

		static void init();
		static void mainloop(engine::Engine* engine, const std::string& log_file_path);
		inline static void mainloop(engine::Engine* engine) { std::string null_path = ""; mainloop(engine, null_path); }
		static void gtp_success(int id, const std::string& msg);
		inline static void gtp_success(int id) { std::string empty = "";  gtp_success(id, empty); }
		static void gtp_failure(int id, const std::string& msg);
	};
}