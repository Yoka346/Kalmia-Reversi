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
		if (sfen.size() < SQUARE_NUM + 1)	// 少なくとも 盤面 + 手番 の長さは必要.
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

	void USI::mainloop(Engine* engine, const std::string& log_file_path)
	{
		if (!engine)
			throw invalid_operation("Specified engine was null.");

		if (!this->mainloop_mutex.try_lock())
			throw invalid_operation("Cannnot execute mainloop before another mainloop has been quit.");

		this->engine = engine;
		this->quit_flag = false;
		this->logger = ofstream(log_file_path);

		using namespace placeholders;
		this->engine->on_err_message_is_sent = [this](string msg) { usi_failure(msg); };
		this->engine->on_think_info_is_sent = bind(&USI::on_think_info_is_sent, this, _1);
		this->engine->on_multi_pv_is_sent = bind(&USI::on_multi_pv_is_sent, this, _1);

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

	void USI::on_think_info_is_sent(const ThinkInfo& info)
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

	void USI::on_multi_pv_is_sent(const MultiPV& multi_pv)
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

	USI::CommandHandler USI::to_handler(void (USI::* exec_cmd)(istringstream&))
	{
		return bind(exec_cmd, this, placeholders::_1);
	}

	void USI::usi_failure(const string msg)
	{
		this->usi_out << IOLock::LOCK << "info string Error!: " << msg << "\n";
		this->usi_out.flush();
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
		auto& u_out = this->usi_out << IOLock::LOCK;
		u_out << "id name " << this->engine->name() <<"\n";
		u_out << "id author " << this->engine->author() << "\n";

		EngineOptions options;
		this->engine->get_options(options);
		for (auto& option : options)
			u_out << "option name " << option.first 
			<< " type " << option.second.type()
			<< " default " << option.second.default_value() 
			<< "\n";
		u_out << "usiok\n";
		u_out.flush();
	}

	void USI::exec_isready_command(istringstream& iss) 
	{
		if (this->engine->ready())
		{
			this->usi_out << IOLock::LOCK << "readyok\n";
			this->usi_out.flush();
		}
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
		{
			ostringstream oss;
			oss << "" << err_msg;
			usi_failure(oss.str());
		}
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

			if (!pos.update<true>(move))
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
			current_pos.update<false>(move);

			if (current_pos == pos)
			{
				current_pos.undo(move);
				this->engine->update_position(current_pos.side_to_move(), move.coord);
				return;
			}

			current_pos.undo(move);
		}

		this->engine->set_position(pos);
	}

	void USI::exec_go_command(istringstream& iss)
	{
		if (!go_command_has_done())
		{
			future<BoardCoordinate>& go_future = this->go_command_future;
			if (go_future.valid() && !go_command_has_done())
			{
				usi_failure("Cannnot execute multiple go commands");
				return;
			}
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

		this->go_command_future = std::async(
			[=, this]() 
			{ 
				BoardCoordinate move = this->engine->go(ponder);
				if (!ponder && !stop_go_flag)
				{
					usi_out << IOLock::LOCK << "bestmove " << coordinate_to_string(move) << "\n";
					usi_out.flush();
				}
				return move;
			});
	}

	void USI::exec_stop_command(istringstream& iss) 
	{
		static const milliseconds TIMEOUT(10000);

		if (!this->go_command_future.valid())
			return;

		if (!this->go_command_has_done())
		{
			this->stop_go_flag = true;
			this->engine->stop_thinking(TIMEOUT);
			auto status = this->go_command_future.wait_for(TIMEOUT);
			this->stop_go_flag = false;
			if (status == future_status::timeout)
			{
				usi_failure("Timeout!!");
				return;
			}
		}
		usi_out << IOLock::LOCK << "bestmove " << coordinate_to_string(this->go_command_future.get()) << "\n";
		usi_out.flush();
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

		this->go_command_future = std::async(
			[=, this]()
			{
				BoardCoordinate move = this->engine->go(false);
				usi_out << IOLock::LOCK << "bestmove " << coordinate_to_string(move) << "\n";
				usi_out.flush();
				return move;
			});
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