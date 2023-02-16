#pragma once

#include <string>
#include <vector>

#include "../utils/array.h"
#include "../utils/bitmanip.h"
#include "../reversi/bitboard.h"
#include "../reversi/types.h"

namespace learn
{
	struct TrainDataItem
	{
		static constexpr int32_t DATA_SIZE = 16 + 1 + 1 + 4;

		reversi::Bitboard position;
		reversi::BoardCoordinate next_move;
		int8_t final_disc_diff;
		float eval_score;

		TrainDataItem() : position(0ULL, 0ULL) {}
		TrainDataItem(char* buffer, size_t len,  bool swap_byte = false); 
	};

	using TrainData = std::vector<TrainDataItem>;

	/**
	* @fn
	* @brief 訓練データをファイルから読み込む.
	* @detail 訓練データファイルのフォーマットは以下の通り.
	* offset = 0: リトルエンディアンなら1, ビッグエンディアンなら0.
	* offset >= 1: TrainDataItemの各メンバーが隙間なく敷き詰められる.
	**/
	void load_train_data_from_file(const std::string& path, TrainData& train_data);
}