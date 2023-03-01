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
	* @brief �P���f�[�^���t�@�C������ǂݍ���.
	* @detail �P���f�[�^�t�@�C���̃t�H�[�}�b�g�͈ȉ��̒ʂ�.
	* offset = 0: ���g���G���f�B�A���Ȃ�1, �r�b�O�G���f�B�A���Ȃ�0.
	* offset >= 1: TrainDataItem�̊e�����o�[�����ԂȂ��~���l�߂���.
	**/
	void load_train_data_from_file(const std::string& path, TrainData& train_data, int32_t min_empty_count = 0, int32_t max_empty_count = 60);

	/**
	* @fn
	* @brief GGF�t�@�C�����P���f�[�^�t�@�C���ɕϊ�����.
	* @param (ggf_path) GGF�t�@�C���̃p�X.
	* @param (out_path) �o�͐�̃p�X.
	* @param (min_player_rating) �v���C���[�̃��[�e�B���O�̍ŏ��l(���̃��[�e�B���O�������v���C���[�ɂ��΋ǂ͏��O).
	**/
	void convert_ggf_file_to_train_data_file(const std::string& ggf_path, const std::string& out_path, double min_player_rating);

	/**
	* @fn
	* @brief �P���f�[�^�Ɋ܂܂��d���ǖʂ𓝍�����. �΍��Ə��s�ɂ͕��ϒl��p����.
	* @param (in_path) ���͂���P���f�[�^�̃p�X.
	* @param (out_path) �o�͐�̃p�X.
	**/
	void merge_duplicated_position_in_train_data(const std::string& in_path, const std::string& out_path);
}