#include "edax_book.h"

#include <iostream>
#include <sstream>
#include <filesystem>

using namespace std;
using namespace reversi;

namespace book
{
	EdaxBook::EdaxBook(const string& path) : _positions(0)
	{
		ifstream ifs(path, ios::binary);
		if (!ifs)
		{
			ostringstream oss;
			oss << "Cannnot open \"" << path << "\".";
			throw invalid_argument(oss.str());
		}

		if (!check_endian(ifs))
		{
			ostringstream oss;
			oss << "\"" << path << "\" is not a file for Edax.";
			throw invalid_argument(oss.str());
		}

		if (!check_if_book(ifs))
		{
			ostringstream oss;
			oss << "\"" << path << "\" is not book file.";
			throw invalid_argument(oss.str());
		}

		load_header(ifs);
		load_positions(ifs);
	}

	bool EdaxBook::check_endian(ifstream& ifs)
	{
		char buffer[LABEL_SIZE];
		ifs.read(buffer, sizeof(buffer));
		
		if (strcmp(buffer, LITTLE_ENDIAN_LABEL) == 0)
		{
			this->file_endian = endian::little;
			return true;
		}

		if (strcmp(buffer, BIG_ENDIAN_LABEL) == 0)
		{
			this->file_endian = endian::big;
			return true;
		}

		return false;
	}

	bool EdaxBook::check_if_book(ifstream& ifs)
	{
		char buffer[DATA_KIND_SIZE];
		ifs.read(buffer, sizeof(buffer));

		return (this->file_endian == endian::little && strcmp(buffer, LITTLE_ENDIAN_DATA_KIND))
			|| strcmp(buffer, BIG_ENDIAN_DATA_KIND);
	}

	void EdaxBook::load_header(ifstream& ifs)
	{
		read_file(ifs, this->_version);
		read_file(ifs, this->_release_num);
		read_file(ifs, this->_date.year);
		read_file(ifs, this->_date.month);
		read_file(ifs, this->_date.day);
		read_file(ifs, this->_date.hour);
		read_file(ifs, this->_date.minute);
		read_file(ifs, this->_date.second);
		ifs.seekg(1, ios::cur);	// ƒoƒCƒg‹«ŠE
		read_file(ifs, this->_options.level);
		read_file(ifs, this->_options.empty_num);
		read_file(ifs, this->_options.midgame_error);
		read_file(ifs, this->_options.endgame_error);
		read_file(ifs, this->_options.verbosity);

		read_file(ifs, this->_stats.node_count);
		this->_positions.reset(this->_stats.node_count);
	}

	void EdaxBook::load_positions(ifstream& ifs)
	{
		this->_stats.link_count = 0;
		for (size_t i = 0; i < this->_positions.length(); i++)
		{
			auto& pos = this->_positions[i];

			read_file(ifs, pos.board.player);
			read_file(ifs, pos.board.opponent);
			read_file(ifs, pos.win_count);
			read_file(ifs, pos.draw_count);
			read_file(ifs, pos.loss_count);
			read_file(ifs, pos.unterminated_line_count);
			read_file(ifs, pos.score.value);
			read_file(ifs, pos.score.lower);
			read_file(ifs, pos.score.upper);

			int8_t link_num = 0;
			read_file(ifs, link_num);
			pos.links.reset(link_num);
			this->_stats.link_count += link_num;

			read_file(ifs, pos.level);

			for (auto i = 0; i < link_num; i++)
			{
				auto& link = pos.links[i];
				read_file(ifs, link.score);
				read_file(ifs, link.move);
			}

			read_file(ifs, pos.leaf.score);
			read_file(ifs, pos.leaf.move);
		}
	}

	void EdaxBook::adjust_endian(char* buffer, size_t len)
	{
		if(this->file_endian != endian::native)
			for (size_t i = 0; i < len / 2; i++)
			{
				auto tmp = buffer[i];
				buffer[i] = buffer[len - i - 1];
				buffer[len - i - 1] = tmp;
			}
	}

	void EdaxBook::read_file(ifstream& ifs, char* buffer, size_t count)
	{
		ifs.read(buffer, count);
		adjust_endian(buffer, count);
	}
}