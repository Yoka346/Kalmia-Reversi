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
		static constexpr int32_t DATA_SIZE = sizeof(uint64_t) * 2 + sizeof(reversi::BoardCoordinate) + sizeof(int16_t) * 2 + sizeof(float);

		reversi::Bitboard position;
		reversi::BoardCoordinate next_move;
		int16_t final_disc_diff;		// (player - opponent) * 10    
		int16_t wld;	// win = 1000, loss = 0, draw = 500, 
		float eval_score;		// from player's view

		TrainDataItem() : position(0ULL, 0ULL), next_move(reversi::BoardCoordinate::NULL_COORD), final_disc_diff(0), wld(0), eval_score(0.0f) {}
		TrainDataItem(char* buffer, size_t len,  bool swap_byte = false); 

		void write_to(std::ofstream& ofs);
	};

	using TrainData = std::vector<TrainDataItem>;

	/**
	* @fn
	* @brief 訓練データをファイルから読み込む.
	* @detail 訓練データファイルのフォーマットは以下の通り.
	* offset = 0: リトルエンディアンなら1, ビッグエンディアンなら0.
	* offset >= 1: TrainDataItemの各メンバーが隙間なく敷き詰められる.
	**/
	void load_train_data_from_file(const std::string& path, TrainData& train_data, int32_t min_empty_count = 0, int32_t max_empty_count = 60);

	/**
	* @fn
	* @brief GGFファイルを訓練データファイルに変換する.
	* @param (ggf_path) GGFファイルのパス.
	* @param (out_path) 出力先のパス.
	* @param (min_player_rating) プレイヤーのレーティングの最小値(このレーティングを下回るプレイヤーによる対局は除外).
	**/
	void convert_ggf_file_to_train_data_file(const std::string& ggf_path, const std::string& out_path, double min_player_rating);

	/**
	* @fn
	* @brief 訓練データに含まれる重複局面を統合する. 石差と勝敗には平均値を用いる.
	* @param (in_path) 入力する訓練データのパス.
	* @param (out_path) 出力先のパス.
	**/
	void merge_duplicated_position_in_train_data(const std::string& in_path, const std::string& out_path);
}