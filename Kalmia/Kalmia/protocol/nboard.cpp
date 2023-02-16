#include "nboard.h"

#include <iomanip>
#include <chrono>
#include <cmath>

#include "../utils/text.h"
#include "../utils/string_to_type.h"
#include "../utils/exception.h"
#include "../game_format/ggf.h"


using namespace std;
using namespace std::chrono;

using namespace utils;
using namespace io;
using namespace reversi;
using namespace game_format;
using namespace engine;

namespace protocol
{
	void NBoard::init_handlers()
	{
		commands["nboard"] = to_handler(&NBoard::exec_nboard_command);
		commands["set"] = to_handler(&NBoard::exec_set_command);
		commands["move"] = to_handler(&NBoard::exec_move_command);
		commands["hint"] = to_handler(&NBoard::exec_hint_command);
		commands["go"] = to_handler(&NBoard::exec_go_command);
		commands["ping"] = to_handler(&NBoard::exec_ping_command);
		commands["learn"] = to_handler(&NBoard::exec_learn_command);
		commands["analyze"] = to_handler(&NBoard::exec_analyze_command);
		commands["quit"] = to_handler(&NBoard::exec_quit_command);
	}

	void NBoard::mainloop(Engine* engine, const string& log_file_path)
	{
		if (!engine)
			throw invalid_argument("Specified engine was null.");

		if (!this->mainloop_mutex.try_lock())
			throw invalid_operation("Cannnot execute mainloop before another mainloop has been quit.");

		init_engine(engine);

		this->quit_flag = false;

		ofstream ofs(log_file_path);
		SyncOutStream logger(&ofs);
		this->logger = &logger;

		string cmd_name;
		string line;
		while (!this->quit_flag && getline(*this->nb_in, line))
		{
			*this->logger << IOLock::LOCK << "< " << line << "\n";
			this->logger->flush();

			istringstream iss(line);
			iss >> cmd_name;

			auto handler = this->commands.find(cmd_name);
			if (handler != this->commands.end())
				handler->second(iss);
			else
			{
				ostringstream oss;
				oss << "Unknown command: " << cmd_name;
				nboard_failure(oss.str());
			}
		}

		this->mainloop_mutex.unlock();
	}

	void NBoard::init_engine(engine::Engine* engine)
	{
		this->engine = engine;
		this->engine->on_err_message_was_sent = [this](const auto& msg) { nboard_failure(msg); };
		this->engine->on_think_info_was_sent = [this](const auto& think_info) { send_node_stats(think_info); };
		this->engine->on_multi_pv_was_sent = [this](const auto& multi_pv) { send_hints(multi_pv); };
		this->engine->on_move_was_sent = [this](const auto& move) { send_move(move); };
		this->engine->on_analysis_ended = [this]() { nboard_success("status"); };
	}

	NBoard::CommandHandler NBoard::to_handler(void (NBoard::* exec_cmd)(istringstream&))
	{
		return bind(exec_cmd, this, placeholders::_1);
	}

	void NBoard::nboard_success(const string& responce)
	{
		this->nb_out << IOLock::LOCK << responce << "\n";
		this->nb_out.flush();

		*this->logger << IOLock::LOCK << "> " << responce << "\n";
		this->logger->flush();
	}

	void NBoard::nboard_failure(const string& msg)
	{
		this->nb_err << IOLock::LOCK << "Error: " << msg << "\n";
		this->nb_err.flush();

		*this->logger << IOLock::LOCK << ">! " << msg << "\n";
		this->logger->flush();
	}

	void NBoard::send_node_stats(const ThinkInfo& think_info)
	{
		if (!think_info.node_count.has_value())
			return;

		ostringstream oss;
		oss << "nodestats " << think_info.node_count.value() << " ";
		if (think_info.ellapsed_ms.has_value())
			oss << think_info.ellapsed_ms.value().count() * 1.0e-3f;
		nboard_success(oss.str());
	}

	void NBoard::send_hints(const MultiPV& multi_pv)
	{
		ostringstream oss;
		oss << fixed;
		for (size_t i = 0; i < hint_num && i < multi_pv.size(); i++)
		{
			auto& item = multi_pv[i];

			oss << "search ";
			for (auto& move : item.pv)
				if (move != BoardCoordinate::PASS)
					oss << coordinate_to_string(move);
				else
					oss << "PA";

			if (item.eval_score.has_value())
				oss << " " << setprecision(2) << item.eval_score.value();
			else
				oss << " " << 0;

			oss << " 0 ";

			if (item.eval_score_is_exact)
				oss << "100%";
			else if (item.exact_wld != GameResult::NOT_OVER)
				oss << "100%W";
			else
				oss << item.pv.size();

			oss << "\n";
		}
		nboard_success(oss.str());
	}

	void NBoard::send_move(const EngineMove& move)
	{
		ostringstream oss;
		oss << "=== ";
		if (move.coord == BoardCoordinate::PASS)
			oss << "PA";
		else
			oss << coordinate_to_string(move.coord);

		if (move.eval_score.has_value())
			oss << '/' << move.eval_score.value();

		if (move.ellapsed_ms.has_value())
			oss << '/' << move.ellapsed_ms.value();

		nboard_success(oss.str());
		nboard_success("status");
	}

	void NBoard::exec_nboard_command(istringstream& iss)
	{
		string str;
		iss >> str;
		int32_t version = 0;
		if (!try_stoi(str, version))
		{
			nboard_failure("Version must be an integer.");
			return;
		}

		if (version != PROTOCOL_VERSION)
		{
			ostringstream oss;
			oss << "NBoard version " << version << " is not supported.";
			nboard_failure(oss.str());
			return;
		}

		this->engine->ready();

		ostringstream oss;
		oss << "set myname " << this->engine->name();
		nboard_success(oss.str());
	}

	void NBoard::exec_set_command(istringstream& iss)
	{
		string property_name;
		iss >> property_name;

		string str;
		if (property_name == "depth")
		{
			int32_t depth = 0;
			iss >> str;

			if (!try_stoi(str, depth))
			{
				nboard_failure("Depth must be an integer.");
				return;
			}

			if (depth < 1 || depth > 60)
			{
				nboard_failure("Depth must be within [1, 60].");
				return;
			}

			this->engine->set_level(depth);
			return;
		}

		if (property_name == "game")
		{
			try
			{
				GGFReversiGame game(iss.str().substr(static_cast<size_t>(iss.tellg()) + 1));
				for (auto& move : game.moves)
				{
					if (!game.position.update<true>(move.coord))
					{
						ostringstream oss;
						oss << "Specified moves contains an invalid move " << coordinate_to_string(move.coord) << '.';
						nboard_failure(oss.str());
						return;
					}
				}
				this->engine->set_position(game.position);

				GameTimerOptions* times[2] = { &game.black_thinking_time, &game.white_thinking_time };
				for (auto color = DiscColor::BLACK; color <= DiscColor::WHITE; color++)
				{
					auto& time = *times[color];
					if (time.main_time_ms != milliseconds::zero() && time.increment_ms != milliseconds::zero())
					{
						this->engine->set_main_time(color, time.main_time_ms);
						this->engine->set_time_inc(color, time.increment_ms);
					}
				}
			}
			catch (GGFParserException ex)
			{
				ostringstream oss;
				oss << "Cannnot parse GGF string. Detail: " << ex.what();
				nboard_failure(oss.str());
			}
			return;
		}

		if (property_name == "contempt")
		{
			int32_t contempt = 0;
			string str;
			iss >> str;

			if (!try_stoi(str, contempt))
			{
				nboard_failure("Contempt must be an integer.");
				return;
			}

			this->engine->set_book_contempt(contempt);
			return;
		}
	}

	void NBoard::exec_move_command(istringstream& iss)
	{
		if(this->engine_is_thinking)	// エンジンの思考中にmoveコマンドが飛んでくることがあるので, 思考中であれば止める.
			this->engine->stop_thinking(milliseconds(TIMEOUT_MS));

		string move_str;
		if (!getline(iss, move_str, '/'))
		{
			nboard_failure("Specify a coordinate of move.");
			return;
		}

		remove_head_whitespace(move_str);
		auto move = (move_str == "PA") ? BoardCoordinate::PASS : parse_coordinate(move_str);
		if (!this->engine->update_position(this->engine->position().side_to_move(), move))
		{
			ostringstream oss;
			oss << "move " << move_str << " is invalid.";
			nboard_failure(oss.str());
			return;
		}

		// eval や time が指定されていても使わないので無視する.
	}

	void NBoard::exec_hint_command(istringstream& iss)
	{
		string str;
		iss >> str;
		if (!try_stoi(str, this->hint_num))
		{
			nboard_failure("The number of hints must be an integer.");
			return;
		}

		if (this->hint_num < 1)
		{
			nboard_failure("The number of hints must more than or equal 1.");
			return;
		}

		nboard_success("status Analyzing");
		this->engine->analyze(this->hint_num);
	}

	void NBoard::exec_go_command(istringstream& iss)
	{
		nboard_success("status Thinking");
		engine->go(false);
	}

	void NBoard::exec_ping_command(istringstream& iss)
	{
		this->engine->stop_thinking(milliseconds(TIMEOUT_MS));
		int32_t n = 0;
		string str;
		iss >> str;
		if (!try_stoi(str, n))
			n = 0;
		ostringstream oss;
		oss << "pong " << n;
		nboard_success(oss.str());
	}

	void NBoard::exec_learn_command(istringstream& iss)
	{
		this->engine->add_current_game_to_book();
		nboard_success("leaned");
	}

	void NBoard::exec_analyze_command(istringstream& iss)
	{
		nboard_failure("Not supported.");
	}

	void NBoard::exec_quit_command(istringstream& iss)
	{
		istringstream dummy;
		exec_ping_command(dummy);
		this->quit_flag = true;
	}
}