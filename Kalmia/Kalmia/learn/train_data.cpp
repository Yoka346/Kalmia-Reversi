#include "train_data.h"

#include <fstream>
#include <sstream>
#include <stdexcept>

using namespace std;

using namespace utils;

namespace learn
{
	TrainDataItem::TrainDataItem(char* buffer, size_t len, bool swap_byte) : position(0ULL, 0ULL)
	{
		if (len < DATA_SIZE)
			throw invalid_argument("Specified buffer was small.");

		this->position.player = *reinterpret_cast<uint64_t*>(buffer);
		buffer += 8;
		this->position.opponent = *reinterpret_cast<uint64_t*>(buffer);
		buffer += 8;
		this->next_move = static_cast<reversi::BoardCoordinate>(buffer[0]);
		buffer++;
		this->final_disc_diff = buffer[0];
		buffer++;
		this->eval_score = *reinterpret_cast<float*>(buffer);

		if (swap_byte)
		{
			this->position.player = BYTE_SWAP_64(this->position.player);
			this->position.opponent = BYTE_SWAP_64(this->position.opponent);
			auto swapped = BYTE_SWAP_32(*reinterpret_cast<uint32_t*>(&this->eval_score));
			this->eval_score = *reinterpret_cast<float*>(swapped);
		}
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
}