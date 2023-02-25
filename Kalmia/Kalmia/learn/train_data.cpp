#include "train_data.h"

#include <iostream>
#include <fstream>
#include <sstream>
#include <stdexcept>

#include "../game_format/ggf.h"

using namespace std;

using namespace utils;
using namespace reversi;
using namespace game_format;

namespace learn
{
	TrainDataItem::TrainDataItem(char* buffer, size_t len, bool swap_byte) : position(0ULL, 0ULL)
	{
		if (len < DATA_SIZE)
			throw invalid_argument("Specified buffer is small.");

		this->position.player = *reinterpret_cast<uint64_t*>(buffer);
		buffer += 8;
		this->position.opponent = *reinterpret_cast<uint64_t*>(buffer);
		buffer += 8;
		this->next_move = static_cast<BoardCoordinate>(buffer[0]);
		buffer++;
		this->final_disc_diff = buffer[0];
		buffer++;
		this->eval_score = *reinterpret_cast<float*>(buffer);

		if (swap_byte)
		{
			this->position.player = BYTE_SWAP_64(this->position.player);
			this->position.opponent = BYTE_SWAP_64(this->position.opponent);
			uint32_t swapped = BYTE_SWAP_32(*reinterpret_cast<uint32_t*>(&this->eval_score));
			this->eval_score = *reinterpret_cast<float*>(swapped);
		}
	}

	void TrainDataItem::write_to(ofstream& ofs)
	{
		ofs.write(reinterpret_cast<char*>(&this->position.player), sizeof(uint64_t));
		ofs.write(reinterpret_cast<char*>(&this->position.opponent), sizeof(uint64_t));
		ofs.write(reinterpret_cast<char*>(&this->next_move), sizeof(BoardCoordinate));
		ofs.write(reinterpret_cast<char*>(&this->final_disc_diff), sizeof(int8_t));
		ofs.write(reinterpret_cast<char*>(&this->eval_score), sizeof(float));
	}

	void load_train_data_from_file(const std::string& path, TrainData& train_data)
	{
		ifstream ifs(path, ios_base::in | ios_base::binary);
		if (!ifs)
		{
			ostringstream oss;
			oss << "Cannnot open \"" << path << "\".";
			throw invalid_argument(oss.str());
		}

		char b;
		ifs.read(&b, 1);
		endian file_endian = b ? endian::little : endian::big;

		char buffer[TrainDataItem::DATA_SIZE];
		while (!ifs.eof())
		{
			ifs.read(buffer, TrainDataItem::DATA_SIZE);
			train_data.emplace_back(TrainDataItem(buffer, TrainDataItem::DATA_SIZE));
		}
	}

	void convert_ggf_file_to_train_data_file(const string& ggf_path, const string& out_path, double min_player_rating)
	{
		ifstream ifs(ggf_path);
		ofstream ofs(out_path, ios_base::binary);
		char endian_flag = (endian::native == endian::little) ? 1 : 0;
		ofs.write(&endian_flag, 1);

		int32_t count = 0;
		int32_t draw_count = 0;
		Array<TrainDataItem, SQUARE_NUM> items;
		auto loc = 0;
		string line;
		while (!ifs.eof())
		{
			getline(ifs, line);

			try
			{
				GGFReversiGame game(line);
				if (game.black_player_rating < min_player_rating || game.white_player_rating < min_player_rating)
					continue;

				loc = 0;
				auto pos = game.position;
				auto save = true;

				// GGFのREプロパティは, 中押し勝ちであっても+64.0と記録されるケースがあるので, 実際に着手してスコアを確認する.
				for (auto& move : game.moves)
				{
					if (move.coord == BoardCoordinate::PASS)
					{
						if (!pos.can_pass())
						{
							save = false;
							break;
						}
						pos.pass();
						continue;
					}

					auto& item = items[loc++];
					item.position = pos.bitboard();
					item.next_move = move.coord;

					if (move.eval_score.has_value())
						item.eval_score = move.eval_score.value();

					if (!pos.update(move.coord))
					{
						save = false;
						break;
					}
				}

				if (!save || !pos.is_gameover())	// 最後まで打ち切っていない棋譜や非合法手を含む棋譜は排除.
					continue;

				items[loc].position = pos.bitboard();
				items[loc].next_move = BoardCoordinate::NULL_COORD;
				int8_t disc_diff = items[loc].final_disc_diff = pos.get_disc_diff() * 10;
				items[loc].wld = (disc_diff == 0) ? 500 : (disc_diff > 0) ? 1000 : 0;
				if (pos.side_to_move() == game.position.side_to_move())
					disc_diff = -disc_diff;

				auto i = 0;
				for (auto& move : game.moves)
				{
					disc_diff = -disc_diff;
					if (move.coord != BoardCoordinate::PASS)
					{
						auto& item = items[i++];
						item.final_disc_diff = disc_diff;
						item.wld = (disc_diff == 0) ? 500 : (disc_diff > 0) ? 1000 : 0;
						item.write_to(ofs);
					}
				}
				items[loc].write_to(ofs);
				count += loc + 1;
				if (disc_diff == 0)
					draw_count += loc + 1;
			}
			catch (GGFParserException ex)
			{
				cerr << "Error: " << ex.what() << endl;
			}
		}

		cout << count << " positions were saved." << endl;
		cout << "The number of draw positions: " << draw_count << "(" << static_cast<double>(draw_count) * 100.0 / count << "%)";
	}
}