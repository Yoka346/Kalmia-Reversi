#pragma once
#include "usi.h"

#include <cmath>
#include <chrono>

#include "../utils/string_to_type.h"
#include "../utils/exception.h"

using namespace std;
using namespace std::chrono;

using namespace utils;
using namespace io;
using namespace engine;
using namespace reversi;

namespace protocol
{
	bool parse_sfen(const std::string sfen, Position& pos)
	{
		if (sfen.size() < static_cast<size_t>(SQUARE_NUM + 1))	// 少なくとも 盤面 + 手番 の長さは必要.
			return false;

		auto* sfen_array = sfen.c_str();
		for (auto i = 0; i < SQUARE_NUM; i++)
		{
			auto& c = sfen_array[i];
			if (c == 'X')
				pos.put_player_disc_at(static_cast<BoardCoordinate>(i));
			else if (c == 'O')
				pos.put_opponent_disc_at(static_cast<BoardCoordinate>(i));
			else if (c != '-')
				return false;
		}

		auto& player = sfen_array[SQUARE_NUM];
		if (player == 'B' || player == 'b')
		{
			if (pos.side_to_move() != DiscColor::BLACK)
				pos.pass();
		}
		else if (player == 'W' || player == 'w')
		{
			if (pos.side_to_move() != DiscColor::WHITE)
				pos.pass();
		}
		else
			return false;

		// 本当は手番を表す数字も与えられるが, 特に必要ないので無視する.

		return true;
	}

	string USI::position_to_sfen(Position& pos)
	{
		constexpr char DISC_SYMBOLS[3] = { 'X', 'O', '-' };
		constexpr char DISC_COLOR[2] = { 'b', 'w' };

		ostringstream oss;
		for (auto i = BoardCoordinate::A1; i <= BoardCoordinate::H8; i++)
			oss << DISC_SYMBOLS[pos.square_color_at(i)];
		oss << " " << DISC_COLOR[pos.side_to_move()];
		return oss.str();
	}

	// コマンドのテーブルを初期化.
	void USI::init()
	{
		commands["usi"] = to_handler(&USI::exec_usi_command);
		commands["isready"] = to_handler(&USI::exec_isready_command);
		commands["setoption"] = to_handler(&USI::exec_setoption_command);
		commands["usinewgame"] = to_handler(&USI::exec_usinewgame_command);
		commands["position"] = to_handler(&USI::exec_position_command);
		commands["go"] = to_handler(&USI::exec_go_command);
		commands["stop"] = to_handler(&USI::exec_stop_command);
		commands["ponderhit"] = to_handler(&USI::exec_ponderhit_command);
		commands["quit"] = to_handler(&USI::exec_quit_command);
		commands["gameover"] = to_handler(&USI::exec_gameover_command);
		commands["score_scale_and_type"] = to_handler(&USI::exec_score_scale_and_type_command);
	}

	void USI::mainloop(Engine* engine, const string& log_file_path)
	{
		if (!engine)
			throw invalid_operation("Specified engine was null.");

		if (!this->mainloop_mutex.try_lock())
			throw invalid_operation("Cannnot execute mainloop before another mainloop has been quit.");

		init_engine(engine);

		this->quit_flag = false;
		this->logger = ofstream(log_file_path);

		string cmd_name;
		string line;
		while (!this->quit_flag)
		{
			getline(*this->usi_in, line);

			this->logger << "Input " << line << endl;

			istringstream iss(line);
			iss >> cmd_name;

			auto handler = this->commands.find(cmd_name);
			if (handler != this->commands.end())
				handler->second(iss);
			else
			{
				ostringstream oss;
				oss << "unknown command: " << cmd_name;
				usi_failure(oss.str());
			}
		}

		this->mainloop_mutex.unlock();
	}

	void USI::usi_success(const string& responce)
	{
		this->usi_out << IOLock::LOCK << responce << "\n";
		this->usi_out.flush();
	}

	void USI::usi_failure(const string& msg)
	{
		this->usi_out << IOLock::LOCK << "info string Error!: " << msg << "\n";
		this->usi_out.flush();
	}

	void USI::send_search_info(const ThinkInfo& info)
	{
		ostringstream oss;
		oss << "info ";

		if (info.nps.has_value())
			oss << "nps " << lround(info.nps.value()) << " ";

		if (info.ellapsed_ms.has_value())
			oss << "time " << info.ellapsed_ms.value().count() << " ";

		if (info.node_count.has_value())
			oss << "nodes " << info.node_count.value() << " ";

		if (info.depth.has_value())
			oss << "depth " << info.depth.value() << " ";

		if (info.selected_depth.has_value())
			oss << "seldepth " << info.depth.value() << " ";

		if (info.eval_score.has_value())
			oss << "score " << info.eval_score.value() << " ";

		if (info.pv.has_value())
		{
			auto& pv = info.pv.value();
			if (!pv.empty())
			{
				oss << "pv ";
				for (auto move : pv)
					oss << coordinate_to_string(move) << " ";
			}
		}

		oss << "\n";
		
		this->usi_out << IOLock::LOCK << oss.str();
		this->usi_out.flush();
	}

	void USI::send_multi_pv(const MultiPV& multi_pv)
	{
		ostringstream oss;

		auto pv_count = 0;
		for (auto& item : multi_pv)
		{
			oss << "info ";
			if (item.eval_score.has_value())
				oss << "score " << item.eval_score.value() << " ";

			if (item.node_count.has_value())
				oss << "nodes " << item.node_count.value() << " ";

			auto& pv = item.pv;
			if (!pv.empty())
			{
				oss << "multipv " << ++pv_count << " ";
				oss << "pv ";
				for (auto move : pv)
					oss << coordinate_to_string(move) << " ";
			}
			oss << "\n";
		}

		this->usi_out << IOLock::LOCK << oss.str() << "\n";
		this->usi_out.flush();
	}

	void USI::init_engine(Engine* engine)
	{
		this->engine = engine;
		this->engine->on_err_message_was_sent = [this](const string& msg) { usi_failure(msg); };
		this->engine->on_think_info_was_sent = [this](const ThinkInfo& ti) { send_search_info(ti); };
		this->engine->on_multi_pv_was_sent = [this](const MultiPV& mpv) { send_multi_pv(mpv); };
	}

	USI::CommandHandler USI::to_handler(void (USI::* exec_cmd)(istringstream&))
	{
		return bind(exec_cmd, this, placeholders::_1);
	}

	void USI::set_time(const string time_kind, const string time)
	{
		int32_t t;
		if (!try_stoi(time, t))
			return;

		if (time_kind == "btime")
			this->engine->set_main_time(DiscColor::BLACK, milliseconds(t));
		else if (time_kind == "wtime")
			this->engine->set_main_time(DiscColor::WHITE, milliseconds(t));
		else if (time_kind == "byoyomi")
			this->engine->set_byoyomi(this->engine->position().side_to_move(), milliseconds(t));
		else if (time_kind == "binc")
			this->engine->set_time_inc(DiscColor::BLACK, milliseconds(t));
		else if (time_kind == "winc")
			this->engine->set_time_inc(DiscColor::WHITE, milliseconds(t));
	}

	void USI::exec_usi_command(istringstream& iss) 
	{
		ostringstream oss;
		oss << "id name " << this->engine->name() <<"\n";
		oss << "id author " << this->engine->author() << "\n";

		EngineOptions options;
		this->engine->get_options(options);
		for (auto& option : options)
			oss << "option name " << option.first 
			<< " type " << option.second.type()
			<< " default " << option.second.default_value() 
			<< "\n";
		oss << "usiok\n";
		usi_success(oss.str());
	}

	void USI::exec_isready_command(istringstream& iss) 
	{
		if (this->engine->ready())
			usi_success("readyok");
		else
			usi_failure("Engine initialization was failed.");
	}

	void USI::exec_setoption_command(istringstream& iss) 
	{
		if (iss.eof())
		{
			usi_failure("specify name and value of option.");
			return;
		}

		string token;
		string option_name;

		iss >> token;
		if (token == "name")
		{	// USIの仕様には無いが利便性のためにnameを省略できるようにする.
			if (!iss.eof())
				iss >> option_name;
			else
			{
				usi_failure("specify a name of option.");
				return;
			}
		}
		else
			option_name = token;

		if (iss.eof())
		{
			usi_failure("specify a value.");
			return;
		}

		iss >> token;
		if (token == "value")	// USIの仕様には無いが利便性のためにvalueを省略できるようにする.
			if (!iss.eof())
				iss >> token;
			else
			{
				usi_failure("specify a value.");
				return;
			}

		string err_msg;
		this->engine->set_option(option_name, token, err_msg);
		if (!err_msg.empty())
			usi_failure(err_msg);
	}

	void USI::exec_usinewgame_command(istringstream& iss) { this->engine->start_game(); }

	void USI::exec_position_command(istringstream& iss) 
	{
		string token;
		iss >> token;

		Position pos;
		if (token == "sfen")
		{
			iss >> token;
			if (!parse_sfen(token, pos))
			{
				usi_failure("invalid sfen.");
				return;
			}
		}
		else if (token != "startpos")
		{
			usi_failure("specify startpos or sfen.");
			return;
		}

		while (!iss.eof())
		{
			iss >> token;
			BoardCoordinate move = parse_coordinate(token);
			if (move == BoardCoordinate::NULL_COORD)
			{
				usi_failure("invalid coordinate.");
				return;
			}

			if (!pos.update(move))
			{
				ostringstream oss;
				oss << "illegal move: " << token;
				usi_failure(oss.str());
				return;
			}
		}

		Array<Move, MAX_MOVE_NUM> moves;
		Position current_pos = this->engine->position();
		auto move_num = current_pos.get_next_moves(moves);
		for (auto i = 0; i < move_num; i++)
		{
			auto& move = moves[i];
			current_pos.calc_flipped_discs(move);
			current_pos.update(move);

			if (current_pos == pos)	// 指定された局面が, 現在の局面を1手進めたものであれば, 着手して更新.
			{
				this->engine->update_position(current_pos.opponent_color(), move.coord);
				return;
			}

			current_pos.undo(move);
		}

		this->engine->set_position(pos);
	}

	void USI::exec_go_command(istringstream& iss)
	{
		if (this->go_is_running)
		{
			usi_failure("Cannnot execute multiple go commands");
			return;
		}

		string token;
		auto ponder = false;
		while (!iss.eof())
		{
			iss >> token;
			if (token == "ponder" || token == "infinite")
				ponder = true;
			else
			{
				string time_kind = token;
				iss >> token;
				set_time(time_kind, token);
			}
		}

		this->stop_go_flag = false;
		this->engine->go(ponder);
		this->go_is_running = true;
		this->engine->on_move_was_sent = [=, this](const EngineMove& move)
		{
			while (ponder && !stop_go_flag)	// ponderが有効のときは, stopコマンドが呼ばれるまでbestmoveを返してはいけない.
				this_thread::yield();
			stop_go_flag = false;
			go_is_running = false;

			ostringstream oss;
			oss << "bestmove " << coordinate_to_string(move.coord);
			usi_success(oss.str());
		};
	}

	void USI::exec_stop_command(istringstream& iss)
	{
		static const milliseconds TIMEOUT(10000);

		if (!this->go_is_running)
			return;

		this->stop_go_flag = true;
		this->engine->stop_thinking(TIMEOUT);
	}

	void USI::exec_ponderhit_command(istringstream& iss) 
	{
		string time_kind, time;
		while (!iss.eof())
		{
			// USIの原案では, ponderhitの引数に持ち時間は渡されないが, ここでは時間も送るように拡張する.
		    // 時間が指定されていなくてもエラーにはならない(互換性維持のため).
			iss >> time_kind;	
			iss >> time;
			set_time(time_kind, time);
		}

		istringstream dummy;
		exec_go_command(dummy);
	}

	void USI::exec_quit_command(istringstream& iss) 
	{
		exec_stop_command(iss);
		this->quit_flag = true;
	}

	void USI::exec_gameover_command(istringstream& iss) 
	{
		// gameoverコマンドは, 引数で対局結果も送られてくるらしいが, 今のところ必要が無いので無視.
		exec_stop_command(iss);		// stopコマンドの前にgameoverコマンドが入力される可能性を考慮.
		this->engine->end_game();
	}

	void USI::exec_score_scale_and_type_command(istringstream& iss) 
	{
		auto& u_out = this->usi_out << IOLock::LOCK;
		u_out << "scoretype ";

		switch (this->engine->score_type())
		{
		case EvalScoreType::WIN_RATE:
			u_out << "WP";
			break;

		case EvalScoreType::DISC_DIFF:
			u_out << "stone";
			break;

		case EvalScoreType::OTHER:
			u_out << "other";
			break;
		}

		u_out << " min " << this->engine->get_eval_score_min();
		u_out << " max " << this->engine->get_eval_score_max() << "\n";
		u_out.flush();
	}
}