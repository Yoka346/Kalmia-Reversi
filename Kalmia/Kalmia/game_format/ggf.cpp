#include "ggf.h"

#include <sstream>
#include <vector>
#include <locale>
#include <algorithm>
#include <stdexcept>

#include "../utils/string_to_type.h"
#include "../utils/array.h"
#include "../utils/string_to_type.h"

using namespace std;
using namespace std::chrono;

using namespace utils;
using namespace reversi;

namespace game_format
{
	GGFReversiGame::GGFReversiGame(const string& ggf_str)
	{
		istringstream iss(ggf_str);

		if (!find_game_start_delimiter(iss))
			throw GGFParserException("Invalid format: GGF must start with \"(;\".");

		parse_properties(iss);
	}

	bool GGFReversiGame::find_game_start_delimiter(istringstream& iss)
	{
		char ch = 0;
		while (!iss.eof())
		{
			iss >> ch;
			if (ch == '(')
			{
				iss >> ch;
				if (ch == ';')
					return true;
			}
		}
		return false;
	}

	void GGFReversiGame::parse_properties(istringstream& iss)
	{
		ostringstream oss;
		char ch = 0;
		while (!iss.eof())
		{
			iss >> ch;
			if (ch == ';')
			{
				iss >> ch;
				if (ch == ')')	// found end delimiter.
					return;
				throw GGFParserException("Invalid format: Unexcepted token \";\". Maybe \";)\"?");
			}

			if (ch >= 'A' && ch <= 'Z')
			{
				oss.str("");
				oss << ch;
				while (!iss.eof())
				{
					iss >> ch;

					if (ch == '[')
						break;

					if (ch < 'A' || ch > 'Z')
					{
						ostringstream msg;
						msg << "Invalid format: The property name contains invalid character \'" << ch << '\'';
						throw GGFParserException(msg.str());
					}
					oss << ch;
				}

				if(iss.eof())
					throw GGFParserException("Invalid format: GGF must end with \";)\"");

				parse_property(oss.str(), iss);
			}
		}
	}

	void GGFReversiGame::parse_property(const string& property_name, istringstream& iss)
	{
		string value;
		getline(iss, value, ']');

		if (property_name == "GM")
		{
			transform(value.begin(), value.end(), value.begin(), [](char c) { return tolower(c); });
			if (value != "othello")
			{
				ostringstream msg;
				msg << "Game \"" << value << "\" is not supported.";
				throw GGFParserException(msg.str());
			}
			return;
		}

		if (property_name == "PC")
		{
			this->place = value;
			return;
		}

		if (property_name == "DT")
		{
			this->date = value;
			return;
		}

		if (property_name == "PB")
		{
			this->black_player_name = value;
			return;
		}

		if (property_name == "PW")
		{
			this->white_player_name = value;
			return;
		}

		if (property_name == "RB")
		{
			auto rating = 0.0f;
			if(!try_stof(value, rating))
			   throw GGFParserException("Invalid format: The value of RB must be a real number.");
			this->black_player_rating = rating;
			return;
		}

		if (property_name == "RW")
		{
			auto rating = 0.0f;
			if (!try_stof(value, rating))
				throw GGFParserException("Invalid format: The value of WB must be a real number.");
			this->white_player_rating = rating;
			return;
		}

		if (property_name == "TI")
		{
			parse_time(value, this->black_thinking_time);
			this->white_thinking_time = this->black_thinking_time;
			return;
		}

		if (property_name == "TB")
		{
			parse_time(value, this->black_thinking_time);
			return;
		}

		if (property_name == "TW")
		{
			parse_time(value, this->white_thinking_time);
			return;
		}

		if (property_name == "RE")
		{
			parse_result(value);
			return;
		}

		if (property_name == "BO")
		{
			parse_position(value);
			return;
		}

		if (property_name == "B")
		{
			parse_move(DiscColor::BLACK, value);
			return;
		}

		if (property_name == "W")
		{
			parse_move(DiscColor::WHITE, value);
			return;
		}
	}

	void GGFReversiGame::parse_time(const string& time_str, GameTimerOptions& timer_options)
	{
		stringstream time_ss(time_str);
		string buffer;
		vector<string> times;
		while (getline(time_ss, buffer, '/'))
			times.emplace_back(buffer);

		if(times.size() > 3)
			throw GGFParserException("Invalid format: The representation of time was invalid. Valid format is \"[main_time]/[increment_time]/[extension_time]\".");

		vector<int32_t> times_ms;
		for (size_t i = 0; i < times.size(); i++)
		{
			vector<string> clock_time;
			stringstream ss(times.at(i));
			while (getline(ss, buffer, ':'))
			{
				auto pos = buffer.find(',', 0);	// GGFの時間の仕様によれば, コロンの後にいくつかオプションが指定されるが, 
												// リバーシで用いている棋譜は少ないので無視する.
				if (pos != string::npos)
					buffer = buffer.substr(0, pos);
				clock_time.emplace_back(buffer);
			}
			
			if (clock_time.size() > 3)
				throw GGFParserException("Invalid format: The representation of main time. Valid format is \"[hour]:[minute]:[second]\".");

			int32_t time_ms = 0;
			int32_t unit = 1000; 
			for (auto s = clock_time.rbegin(), e = clock_time.rend(); s != e; s++)
			{
				int32_t ms = 0;
				if (!try_stoi(*s, ms))
					throw GGFParserException("Invalid format: The value of hour, minute and second must be an integer.");
				time_ms += ms * unit;
				unit *= 60;
			}
			times_ms.emplace_back(time_ms);
		}

		timer_options.main_time_ms = milliseconds(times_ms[0]);

		if(times_ms.size() > 1)
			timer_options.increment_ms = milliseconds(times_ms[1]);

		// extension timeは本プログラムでは未対応なので無視.
	}

	void GGFReversiGame::parse_result(const string& res_str)
	{
		stringstream ss(res_str);
		string buffer;
		getline(ss, buffer, ':');

		// 対局途中の棋譜などは, REの値に'?'などを指定しているケースがあるので, 
		// 読めなかったら例外を投げずに関数を抜ける.
		auto score = 0.0f;
		if (!try_stof(buffer, score))	
			return;
		this->game_result.first_player_score = score;

		char flag = 0;
		ss >> flag;
		switch (flag)
		{
		case 'r':
			this->game_result.is_resigned = true;
			return;

		case 't':
			this->game_result.is_timeout = true;
			return;

		case 's':
			this->game_result.is_mutual = true;
			return;
		}
	}

	void GGFReversiGame::parse_position(const string& pos_str)
	{
		istringstream iss(pos_str);
		char ch = 0;
		iss >> ch;
		if (ch != '8')
			throw GGFParserException("Invalid board size: This program only supports 8x8 board.");

		this->position.set_bitboard(Bitboard(0ULL, 0ULL));
		auto coord = BoardCoordinate::A1;
		while (coord <= BoardCoordinate::H8 && !iss.eof())
		{
			iss >> ch;
			if (ch == '*')
				this->position.put_disc_at<DiscColor::BLACK>(coord++);
			else if (ch == 'O')
				this->position.put_disc_at<DiscColor::WHITE>(coord++);
			else if(ch == '-')
				coord++;
			else
			{
				ostringstream oss;
				oss << "Invalid board: Unexcepted symbol \'" << ch << "\'.";
				throw GGFParserException(oss.str());
			}
		}

		if (iss.eof())
			throw GGFParserException("Invalid board: Missing side to move.");

		iss >> ch;
		if (ch == '*')
			this->position.set_side_to_move(DiscColor::BLACK);
		else if (ch == 'O')
			this->position.set_side_to_move(DiscColor::WHITE);
		else
		{
			ostringstream oss;
			oss << "Invalid board: Unexcepted symbol \'" << ch << "\'.";
			throw GGFParserException(oss.str());
		}
	}

	void GGFReversiGame::parse_move(DiscColor color, const string& move_str)
	{
		GGFMove move;
		move.color = color;
		vector<string> move_info;
		stringstream ss(move_str);
		string buffer;
		while (getline(ss, buffer, '/'))
			move_info.emplace_back(buffer);

		if (move_info.size() == 0)
			throw GGFParserException("Invalid move: Coordinate was empty.");

		auto coord = (move_info[0] == "PA") ? BoardCoordinate::PASS : parse_coordinate(move_info[0]);
		if (coord == BoardCoordinate::NULL_COORD)
		{
			ostringstream oss;
			oss << "Invalid move: Cannot parse \"" << move_info[0] << "\" as a coordinate.";
			throw GGFParserException(oss.str());
		}
		move.coord = coord;

		auto value = 0.0f;
		if (move_info.size() > 1)
			if (try_stof(move_info[1], value))
				move.eval_score = value;

		if (move_info.size() > 2)
			if (try_stof(move_info[2], value))
				move.time = value;

		this->moves.emplace_back(move);
	}
}