#include "train_data.h"

#include <iostream>
#include <fstream>
#include <sstream>
#include <map>
#include <algorithm>
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
		this->final_disc_diff = *reinterpret_cast<int16_t*>(buffer);
		buffer += 2;
		this->wld = *reinterpret_cast<int16_t*>(buffer);
		buffer += 2;
		this->eval_score = *reinterpret_cast<float*>(buffer);

		if (swap_byte)
		{
			this->position.player = BYTE_SWAP_64(this->position.player);
			this->position.opponent = BYTE_SWAP_64(this->position.opponent);
			this->final_disc_diff = byte_swap_16(this->final_disc_diff);
			this->wld = byte_swap_16(this->wld);
			auto swapped = BYTE_SWAP_32(*reinterpret_cast<uint32_t*>(&this->eval_score));
			this->eval_score = *reinterpret_cast<float*>(&swapped);
		}
	}

	void TrainDataItem::write_to(ofstream& ofs)
	{
		ofs.write(reinterpret_cast<char*>(&this->position.player), sizeof(uint64_t));
		ofs.write(reinterpret_cast<char*>(&this->position.opponent), sizeof(uint64_t));
		ofs.write(reinterpret_cast<char*>(&this->next_move), sizeof(BoardCoordinate));
		ofs.write(reinterpret_cast<char*>(&this->final_disc_diff), sizeof(int16_t));
		ofs.write(reinterpret_cast<char*>(&this->wld), sizeof(int16_t));
		ofs.write(reinterpret_cast<char*>(&this->eval_score), sizeof(float));
	}

	void load_train_data_from_file(const std::string& path, TrainData& train_data, int32_t min_empty_count, int32_t max_empty_count)
	{
		ifstream ifs(path, ios_base::binary);
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
			TrainDataItem item(buffer, TrainDataItem::DATA_SIZE, file_endian != endian::native);
			auto empty_count = item.position.empty_count();
			if (empty_count >= min_empty_count && empty_count <= max_empty_count)
				train_data.emplace_back(item);
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
				for (const GGFMove& move : game.moves)
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

					TrainDataItem& item = items[loc++];
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
				int16_t disc_diff = items[loc].final_disc_diff = pos.get_disc_diff() * 10;
				items[loc].wld = (disc_diff == 0) ? 500 : (disc_diff > 0) ? 1000 : 0;
				if (pos.side_to_move() != game.position.side_to_move())
					disc_diff = -disc_diff;

				auto i = 0;
				for (const GGFMove& move : game.moves)
				{
					if (move.coord != BoardCoordinate::PASS)
					{
						TrainDataItem& item = items[i++];
						item.final_disc_diff = disc_diff;
						item.wld = (disc_diff == 0) ? 500 : (disc_diff > 0) ? 1000 : 0;
						item.write_to(ofs);
					}
					disc_diff = -disc_diff;
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

	void merge_duplicated_position_in_train_data(const string& in_path, const string& out_path)
	{
		size_t in_count = 0;
		size_t out_count = 0;
		TrainData train_data;
		ofstream ofs(out_path, ios::binary);
		char endian_flag = (endian::native == endian::little) ? 1 : 0;
		ofs.write(&endian_flag, 1);

		for (auto i = 0; i <= SQUARE_NUM; i++)
		{
			train_data.clear();
			load_train_data_from_file(in_path, train_data, i, i); 
			in_count += train_data.size();

			// 盤面のハッシュ値で分類.
			unordered_map<uint64_t, vector<TrainDataItem*>> train_data_set;
			for (TrainDataItem& item : train_data)
			{
				uint64_t hash_code = item.position.calc_hash_code();
				auto ptr = train_data_set.find(hash_code);
				if (ptr != train_data_set.end())
					ptr->second.emplace_back(&item);
				else
					train_data_set[hash_code].emplace_back(&item);
			}

			for (auto& [_, data] : train_data_set)
			{
				TrainDataItem* head_item = data[0];

				int64_t disc_diff_sum = 0;
				uint64_t wld_sum = 0;
				auto eval_score_sum = 0.0;
				for (const TrainDataItem* item : data)
				{
					assert(item->position == head_item->position);
					disc_diff_sum += item->final_disc_diff;
					wld_sum += item->wld;
					eval_score_sum += item->eval_score;
				}

				auto count = data.size();
				head_item->final_disc_diff = static_cast<int16_t>(disc_diff_sum / count);
				head_item->wld = static_cast<int16_t>(wld_sum / count);
				head_item->eval_score = static_cast<float>(eval_score_sum / count);
				head_item->write_to(ofs);
				out_count++;
			}
		}

		cout << "Original train data size: " << in_count << endl;
		cout << "Merged train data size: " << out_count << endl;
	}
}