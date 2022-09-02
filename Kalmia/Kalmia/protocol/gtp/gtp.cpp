#include "gtp.h"

#include <string>
#include <chrono>
#include <exception>
#include <filesystem>
#include <functional>

#include "../../utils/string_to_type.h"
#include "../../utils/exception.h"
#include "../../io/logger.h"
#include "../../reversi/constant.h"
#include "../../reversi/types.h"

using namespace std;
using namespace std::chrono;

using namespace io;
using namespace engine;
using namespace reversi;

namespace protocol
{
	void parse_command(istringstream& line, int& id, string& cmd_name)
	{
		string token;
		line >> token;

		bool has_id = try_stoi(token, id);
		if (has_id)
			line >> cmd_name;
		else
		{
			id = -1;
			cmd_name = token;
		}
	}

	// ここでコマンドのテーブルを初期化する.
	void GTP::init()
	{
		// version 2.0 commands
		commands["protocol_version"] = to_handler(exec_protocol_version_command);
		commands["name"] = to_handler(exec_name_command);
		commands["version"] = to_handler(exec_version_command);
		commands["known_command"] = to_handler(exec_known_command_command);
		commands["list_commands"] = to_handler(exec_list_commands_command);
		commands["quit"] = to_handler(exec_quit_command);
		commands["boardsize"] = to_handler(exec_board_size_command);	
		commands["clear_board"] = to_handler(exec_clear_board_command);
		commands["komi"] = to_handler(exec_komi_command);
		commands["play"] = to_handler(exec_play_command);
		commands["genmove"] = to_handler(exec_genmove_command);
		commands["undo"] = to_handler(exec_undo_command);
		commands["time_settings"] = to_handler(exec_time_settings_command);
		commands["time_left"] = to_handler(exec_time_left_command);
		commands["loadsgf"] = to_handler(exec_loadsgf_command);
		commands["color"] = to_handler(exec_color_command);
		commands["reg_genmove"] = to_handler(exec_reg_genmove_command);
		commands["showboard"] = to_handler(exec_showboard_command);

		// version 1.0 commands(legacy)
		commands["black"] = to_handler(exec_black_command);	// 何故か黒番の時はplayがつかない仕様.
		commands["playwhite"] = to_handler(exec_playwhite_command);
		commands["genmove_black"] = to_handler(exec_genmove_black_command);
		commands["genmove_white"] = to_handler(exec_genmove_white_command);

		// gogui-rules commands
		commands["gogui-rules_game_id"] = to_handler(exec_rules_game_id_command);
		commands["gogui-rules_board"] = commands["showboard"];
		commands["gogui-rules_board_size"] = to_handler(exec_rules_board_size_command);
		commands["gogui-rules_legal_moves"] = to_handler(exec_rules_legal_moves_command);
		commands["gogui-rules_side_to_move"] = to_handler(exec_rules_side_to_move_command);
		commands["gogui-rules_final_result"] = to_handler(exec_rules_final_result_command);

		// original commands
		commands["set_moves"] = to_handler(exec_set_moves_command);
	}

	void GTP::mainloop(Engine* engine, const std::string& log_file_path)
	{
		if (!this->quit)
			throw invalid_operation("cannnot execute mainloop before another mainloop has been quit.");

		if (engine == nullptr)
			throw invalid_argument("specified engine was null.");
		
		this->engine = engine;
		this->quit = false;
		this->logger = ofstream(log_file_path);

		this->engine->on_message_is_sent = [](const string& msg) {cerr << msg; };	// ログなどのテキスト情報はGTPではエラー出力に出力するのが一般的.

		int id;
		string cmd_name;
		string line;
		while (!GTP::quit)
		{
			try
			{
				getline(*this->gtp_in, line);

				this->logger << "Input: " << line << endl;

				istringstream line_stream(line);
				parse_command(line_stream, id, cmd_name);

				auto handler = this->commands.find(cmd_name);
				if (handler != end(this->commands))
					handler->second(id, line_stream);
				else
					gtp_failure(id, "unknown command.");
			}
			catch (gtp_error e)
			{
				gtp_failure(id, e.msg());
			}
			this->gtp_out->flush();
		}
	}

	void GTP::gtp_success(int id, const string& msg)
	{
		stringstream ss;
		if (id != -1)
			ss << id << " ";
		ss << msg;
		auto output = ss.str();

		this->logger << "Status: success\n";
		this->logger << "Output: " << output << "\n" << endl;
		*this->gtp_out << "=" << output << "\n" << endl;
	}

	void GTP::gtp_failure(int id, const string& msg)
	{
		stringstream ss;
		if (id != -1)
			ss << id << " ";
		ss << msg;
		auto output = ss.str();

		this->logger << "Status: fail\n";
		this->logger << "Output: " << output << "\n" << endl;
		*this->gtp_out << "?" << output << "\n" << endl;
	}

	GTP::CommandHandler GTP::to_handler(void (GTP::* exec_cmd)(int, std::istringstream&))
	{
		using namespace placeholders;
		return bind(exec_cmd, this, _1, _2);
	}

	// version 2.0 commands

	void GTP::exec_known_command_command(int id, istringstream& args)
	{
		if (args.eof())
		{
			gtp_failure(id, "invalid option.");
			return;
		}

		string cmd_name;
		args >> cmd_name;
		gtp_success(id, this->commands.count(cmd_name) ? "true" : "false");
	}

	void GTP::exec_list_commands_command(int id, istringstream& args)
	{
		stringstream ss;
		for (auto& it : this->commands)
			ss << it.first << "\n";
		auto str = ss.str();
		gtp_success(id, str.substr(0, str.size() - 1));	// 末尾の余計な改行を除く
	}

	void GTP::exec_quit_command(int id, istringstream& args)
	{
		this->engine->quit();
		this->quit = true;
		gtp_success(id);
	}

	void GTP::exec_board_size_command(int id, istringstream& args)
	{
		if (args.eof())
		{
			gtp_failure(id, "invalid option.");
			return;
		}

		string arg;
		args >> arg;
		int size;
		bool is_int = try_stoi(arg, size);
		if (!is_int)
		{
			gtp_failure(id, "board size must be integer.");
			return;
		}

		if (size == BOARD_SIZE)	// 今のところは8x8の盤面のみに対応.
			gtp_success(id);
		else
			gtp_failure(id, "unacceptable size.");
	}

	void GTP::exec_play_command(int id, istringstream& args)
	{
		string strs[2];
		int i = 0;
		while (!args.eof() && i < 2)
			args >> strs[i++];

		if (i != 2)
		{
			gtp_failure(id, "invalid option.");
			return;
		}

		auto color = parse_color(strs[0]);
		if (color == DiscColor::EMPTY)
		{
			gtp_failure(id, "invalid coordinate.");
			return;
		}

		auto coord = parse_coordinate(strs[1]);
		if (coord == BoardCoordinate::NULL_COORD)
		{
			gtp_failure(id, "invalid coordinate");
			return;
		}

		if (!this->engine->update_position(color, coord))
		{
			gtp_failure(id, "invalid move.");
			return;
		}
		gtp_success(id);
	}

	void GTP::exec_genmove_command(int id, std::istringstream& args)
	{
		if (args.eof())
		{
			gtp_failure(id, "invalid move.");
			return;
		}

		string color_str;
		args >> color_str;
		auto color = parse_color(color_str);
		if (color == DiscColor::EMPTY)
		{
			gtp_failure(id, "invalid color.");
			return;
		}

		BoardCoordinate move;
		this->engine->generate_move(color, move);
		this->engine->update_position(color, move);
		gtp_success(id, coordinate_to_string(move));
	}

	void GTP::exec_undo_command(int id, istringstream& args)
	{
		if (!this->engine->undo_position())
		{
			gtp_failure(id, "cannot undo.");
			return;
		}
		gtp_success(id);
	}

	void GTP::exec_time_settings_command(int id, istringstream& args)
	{
		string time_strs[3];
		int i = 0;
		while (!args.eof() && i < 3)
			args >> time_strs[i++];

		if (i != 3)
		{
			gtp_failure(id, "invalid option.");
			return;
		}

		int32_t main_time, byoyomi, byoyomi_stones;
		try
		{
			main_time = stoi(time_strs[0]);
			byoyomi = stoi(time_strs[1]);
			byoyomi_stones = stoi(time_strs[2]);
		}
		catch (invalid_argument)
		{
			gtp_failure(id, "main time, byoyomi time and byoyomi stones must be integer.");
			return;
		}

		if (main_time < 0 || byoyomi < 0 || byoyomi_stones < 0)
		{
			gtp_failure(id, "main time, byoyomi time and byoyomi stone must be more than or equal 0.");
			return;
		}

		milliseconds main_time_ms(main_time * 1000);
		milliseconds byoyomi_ms(byoyomi * 1000);
		this->engine->set_time(DiscColor::BLACK, main_time_ms, byoyomi_ms, byoyomi_stones, milliseconds::zero());
		this->engine->set_time(DiscColor::WHITE, main_time_ms, byoyomi_ms, byoyomi_stones, milliseconds::zero());
		gtp_success(id);
	}

	void GTP::exec_time_left_command(int id, istringstream& args)
	{
		string arg_strs[3];
		int i = 0;
		while (!args.eof() && i < 3)
			args >> arg_strs[i++];

		if (i != 3)
		{
			gtp_failure(id, "invalid option.");
			return;
		}

		auto color = parse_color(arg_strs[0]);
		if (color == DiscColor::EMPTY)
		{
			gtp_failure(id, "invalid color.");
			return;
		}

		int32_t time_left, byoyomi_stones_left;
		try
		{
			time_left = stoi(arg_strs[1]);
			byoyomi_stones_left = stoi(arg_strs[2]);
		}
		catch (invalid_argument)
		{
			gtp_failure(id, "time left and byoyomi stones left must be integer.");
			return;
		}

		if (time_left < 0 || byoyomi_stones_left < 0)
		{
			gtp_failure(id, "time left and byoyomi stones left must be more than or equal 0.");
			return;
		}

		milliseconds time_left_ms(time_left * 1000);
		this->engine->set_time_left(color, time_left_ms, byoyomi_stones_left);
		gtp_success(id);
	}

	void GTP::exec_loadsgf_command(int id, istringstream& args)
	{
		// ToDo: SGFファイルのパーサーの実装.
		gtp_failure(id, "not supported.");
	}

	void GTP::exec_color_command(int id, istringstream& args)
	{
		if (args.eof())
		{
			gtp_failure(id, "invalid option.");
			return;
		}

		string coord_str;
		args >> coord_str;
		auto coord = parse_coordinate(coord_str);
		if (coord == BoardCoordinate::NULL_COORD)
		{
			gtp_failure(id, "invalid coordinate.");
			return;
		}
		gtp_success(id, color_to_string(this->engine->position().square_color_at(coord)));
	}

	void GTP::exec_reg_genmove_command(int id, istringstream& args)
	{
		if (args.eof())
		{
			gtp_failure(id, "invalid move.");
			return;
		}

		string color_str;
		args >> color_str;
		auto color = parse_color(color_str);
		if (color == DiscColor::EMPTY)
		{
			gtp_failure(id, "invalid color.");
			return;
		}

		BoardCoordinate move;
		this->engine->generate_move(color, move);
		gtp_success(id, coordinate_to_string(move));
	}

	void GTP::exec_showboard_command(int id, istringstream& args)
	{
		constexpr char SYMBOLS[3] = { 'X', 'O', '.' };

		auto& pos = this->engine->position();
		ostringstream oss("\n  ");
		for (int32_t i = 0; i < BOARD_SIZE; i++)
			oss << static_cast<char>('A' + i) << ' ';

		for (int32_t y = BOARD_SIZE - 1; y >= 0; y--)
		{
			oss << '\n' << static_cast<char>('8' - y) << ' ';
			for (int32_t x = 0; x < BOARD_SIZE; x++)
				oss << SYMBOLS[pos.square_color_at(coordinate_2d_to_1d(x, y))] << ' ';
		}
		gtp_success(id, oss.str());
	}

	void GTP::exec_black_command(int id, istringstream& args)
	{
		ostringstream os;
		os << "black" << ' ' << args.str();
		istringstream is(os.str());
		exec_play_command(id, is);
	}

	void GTP::exec_playwhite_command(int id, istringstream& args)
	{
		ostringstream os;
		os << "white" << ' ' << args.str();
		istringstream is(os.str());
		exec_play_command(id, is);
	}

	void GTP::exec_genmove_black_command(int id, istringstream& args)
	{
		istringstream is("black");
		exec_genmove_command(id, is);
	}

	void GTP::exec_genmove_white_command(int id, istringstream& args)
	{
		istringstream is("white");
		exec_genmove_command(id, is);
	}

	void GTP::exec_rules_legal_moves_command(int id, istringstream& args)
	{
		auto& pos = this->engine->position();
		Array<Move, MAX_MOVE_NUM> moves;
		auto move_num = pos.get_next_moves(moves);
		if (!move_num)
		{
			gtp_success(id, pos.is_gameover() ? "" : "pass resign");
			return;
		}

		ostringstream os;
		for (int32_t i = 0; i < move_num; i++)
			os << coordinate_to_string(moves[i].coord) << " ";
		os << "resign";
		gtp_success(id, os.str());
	}
}