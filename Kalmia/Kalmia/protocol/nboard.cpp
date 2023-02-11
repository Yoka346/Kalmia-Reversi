#include "nboard.h"

#include <chrono>

#include "../utils/string_to_type.h"
#include "../game_format/ggf.h"

using namespace std;
using namespace std::chrono;

using namespace utils;
using namespace io;
using namespace reversi;
using namespace game_format;

namespace protocol
{
	void NBoard::init()
	{
		commands["nboard"] = to_handler(&NBoard::exec_nboard_command);
		commands["set"] = to_handler(&NBoard::exec_set_command);
		commands["move"] = to_handler(&NBoard::exec_move_command);
		commands["hint"] = to_handler(&NBoard::exec_hint_command);
		commands["go"] = to_handler(&NBoard::exec_go_command);
		commands["ping"] = to_handler(&NBoard::exec_ping_command);
		commands["learn"] = to_handler(&NBoard::exec_learn_command);
		commands["analyze"] = to_handler(&NBoard::exec_analyze_command);
	}

	void NBoard::mainloop(engine::Engine* engine, const std::string& log_file_path)
	{

	}

	NBoard::CommandHandler NBoard::to_handler(void (NBoard::* exec_cmd)(istringstream&))
	{
		return bind(exec_cmd, this, placeholders::_1);
	}

	void NBoard::nboard_success(const string& responce)
	{
		this->nb_out << IOLock::LOCK << responce << "\n";
		this->nb_out.flush();
	}

	void NBoard::nboard_failure(const string& msg)
	{
		this->nb_err << IOLock::LOCK << "Error: " << msg << "\n";
		this->nb_err.flush();
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
		}

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
}