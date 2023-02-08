#include "ggf.h"

#include <sstream>
#include <locale>
#include <algorithm>
#include <stdexcept>

#include "../utils/string_to_type.h"
#include "../utils/array.h"
#include "../utils/string_to_type.h"

using namespace std;

using namespace utils;
using namespace reversi;

namespace game_format
{
	GGFGameType::GGFGameType(istringstream& iss)
	{
		char ch = 0;
		iss >> ch;
		if (ch != '[')
			throw GGFParserException("Invalid format.");

		parse_board_size(iss);
		parse_options(iss);
	}

	void GGFGameType::parse_board_size(istringstream& iss)
	{
		ostringstream oss;
		char ch = 0;
		while (true)
		{
			iss >> ch;
			if (iss.eof() || ch < '0' || ch > '9')
				break;
			oss << ch;
		}

		if (iss.eof() || !try_stoi(oss.str(), this->board_size))
			throw GGFParserException("Invalid format: Board size must be integer.");
		oss.str("");

		if (this->board_size != reversi::BOARD_SIZE)
		{
			ostringstream msg;
			msg << "Invalid format: Board size " << this->board_size << " is not supported.";
			throw GGFParserException(msg.str());
		}
	}

	void GGFGameType::parse_options(istringstream& iss)
	{
		ostringstream oss;
		char ch;
		iss >> ch;
		while (ch != ']')
		{
			if (iss.eof())
				throw GGFParserException("invalid format.");

			ch = tolower(ch);
			switch (ch)
			{
			case 's':
				throw GGFParserException("Synchro mode is not supported.");

			case 'k':
				throw GGFParserException("Komi is not supported.");

			case 'r':
				while (true)
				{
					iss >> ch;
					if (iss.eof() || ch < '0' || ch > '9')
						break;
					oss << ch;
				}

				if (iss.eof())
					throw GGFParserException("Invalid format");

				this->random_disc_count = stoi(oss.str());
				break;

			case 'a':
				this->is_anti_game = true;
				break;

			case 'b':
				this->prefered_color = DiscColor::BLACK;
				break;

			case 'w':
				this->prefered_color = DiscColor::WHITE;
				break;
			}

			iss >> ch;
		}
	}

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
				throw GGFParserException("Invalid format: unexcepted token \";\". Maybe \";)\"?");
			}

			if (ch >= 'A' && ch <= 'Z')
			{
				oss.str("");
				oss << ch;
				while (!iss.eof())
				{
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

				auto propety_name = oss.str();
				parse_property(propety_name, iss);
			}
		}
	}

	void GGFReversiGame::parse_property(const string& property_name, istringstream& iss)
	{
		stringstream ss;
		char ch = 0;
		while (ch != ']' && !iss.eof())
		{
			if (ch == ']')
				break;
			ss << ch;
		}

		if (property_name == "GM")
		{
			if (ss.str() != "Othello")
			{
				ostringstream msg;
				msg << "Game \"" << ss.str() << "\" is not supported.";
				throw GGFParserException(msg.str());
			}
			return;
		}

		if (property_name == "PC")
		{
			this->place = ss.str();
			return;
		}

		if (property_name == "DT")
		{
			this->date = ss.str();
			return;
		}

		if (property_name == "PB")
		{
			this->black_player_name = ss.str();
			return;
		}

		if (property_name == "PW")
		{
			this->white_player_name = ss.str();
			return;
		}

		if (property_name == "RB")
		{
			auto rating = 0.0f;
			if(!try_stof(ss.str(), rating))
			   throw GGFParserException("Invalid format: the value of RB must be real number.");
			this->black_player_rating = rating;
			return;
		}

		if (property_name == "RW")
		{
			auto rating = 0.0f;
			if (!try_stof(ss.str(), rating))
				throw GGFParserException("Invalid format: the value of WB must be real number.");
			this->white_player_rating = rating;
			return;
		}

		if (property_name == "TI")
		{
			
		}
	}
}