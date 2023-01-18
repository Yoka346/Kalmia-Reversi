#pragma once

#include <fstream>
#include <random>

#include "../config.h"
#include "feature.h"
#include "../utils/array.h"
#include "../utils/math_functions.h"
#include "../utils/bitmanip.h"

namespace evaluation
{
	struct PackedValueFuncParam;

	/**
	* @struct
	* @brief ���l�֐��̃p�����[�^. 
	* @detail ���₷���̂��߂Ƀp�^�[���̎�ނ��ƂɃp�����[�^�𕪊����Ă���. �T�C�Y��905260Bytes(��884.0KiB). 
	* ���̍\���̂�(�t�F�[�Y�� x 2)���p�ӂ������̂����l�֐��̑S�p�����[�^.
	* ToDo: �p�����[�^��16bit���������_���ɂ��Ή�������(���_���͍����x�ł���K�v�͂Ȃ��̂�).
	**/
	struct ValueFuncParam
	{
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::CORNER3x3]> corner3x3;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::CORNER_EDGE_X]> corner_edge_x;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::EDGE_2X]> edge_2x;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::CORNER2x5]> corner2x5;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::LINE0]> line0;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::LINE1]> line1;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::LINE2]> line2;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE8]> diag_line8;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE7]> diag_line7;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE6]> diag_line6;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE5]> diag_line5;
		utils::Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE4]> diag_line4;
		float bias;

		void pack(PackedValueFuncParam& packed_param);
		void to_opponent(ValueFuncParam& out);
		void clear();
		void init_with_rand(std::normal_distribution<float>& dist, std::default_random_engine& eng);

		private:
			template<PatternKind KIND>
			void pack(utils::Array<float, PATTERN_FEATURE_NUM[KIND]>& param, utils::Array<float, PACKED_PATTERN_FEATURE_NUM[KIND]>& packed)
			{
				auto offset = PATTERN_FEATURE_OFFSET[KIND];
				int32_t i = 0;
				for (int32_t f = 0; f < PATTERN_FEATURE_NUM[KIND]; f++)
				{
					auto symmetric_f = TO_SYMMETRIC_FEATURE[offset + f];
					if (f <= symmetric_f)
						packed[i++] = param[f];
				}
			}

			template<PatternKind KIND>
			void to_opponent(utils::Array<float, PATTERN_FEATURE_NUM[KIND]>& param, utils::Array<float, PATTERN_FEATURE_NUM[KIND]>& out)
			{
				auto offset = PATTERN_FEATURE_OFFSET[KIND];
				for (int32_t f = 0; f < PATTERN_FEATURE_NUM[KIND]; f++)
				{
					auto opponent_f = TO_OPPONENT_FEATURE[offset + f];
					out[opponent_f] = param[f];
				}
			}

			template<PatternKind KIND>
			void init_with_rand(utils::Array<float, PATTERN_FEATURE_NUM[KIND]>& param, std::normal_distribution<float>& dist, std::default_random_engine& eng)
			{
				auto offset = PATTERN_FEATURE_OFFSET[KIND];
				for (int32_t f = 0; f < PATTERN_FEATURE_NUM[KIND]; f++)
				{
					auto symmetric_f = TO_SYMMETRIC_FEATURE[offset + f];
					param[f] = (symmetric_f < f) ? param[symmetric_f] : dist(eng);
				}
			}
	};

	/**
	* @struct
	* @brief ���k�������l�֐��̃p�����[�^. 
	* @detail �t�@�C���ɕۑ�����ۂɗp����. �p�^�[����Ώ̕ϊ����Ĉ�v��������̏d�݂�1�ɂ܂Ƃ߂邱�Ƃň��k����.
	* �T�C�Y��457456Bytes(��446.7KiB). ���̍\���̂��t�F�[�Y�����p�ӂ������̂����k�������l�֐��̑S�p�����[�^. 
	* ��������̎�Ԃ̃p�����[�^��, �Е��̎�Ԃ̃p�����[�^���琶���ł���̂ŕۑ��s�v.
	**/
	struct PackedValueFuncParam
	{
	public:
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER3x3]> corner3x3;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER_EDGE_X]> corner_edge_x;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::EDGE_2X]> edge_2x;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER2x5]> corner2x5;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE0]> line0;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE1]> line1;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE2]> line2;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE8]> diag_line8;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE7]> diag_line7;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE6]> diag_line6;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE5]> diag_line5;
		utils::Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE4]> diag_line4;
		float bias;

		void expand(ValueFuncParam& param);

	private:
		template<PatternKind KIND>
		void expand(utils::Array<float, PACKED_PATTERN_FEATURE_NUM[KIND]>& param, utils::Array<float, PATTERN_FEATURE_NUM[KIND]>& expanded)
		{
			auto offset = PATTERN_FEATURE_OFFSET[KIND];
			int32_t i = 0;
			for (int32_t f = 0; f < PATTERN_FEATURE_NUM[KIND]; f++)
			{
				auto symmetric_f = TO_SYMMETRIC_FEATURE[offset + f];
				expanded[f] = (symmetric_f < f) ? expanded[symmetric_f] : param[i++];
			}
		}
	};

	enum ValueRepresentation
	{
		WIN_RATE,
		DISC_DIFF
	};

	/**
	* @class
	* @brief �Ֆʂ̕]�����s�����l�֐�.
	* @tparam (VALUE_REPS) ���l�֐��̏o�͒l���\���������.
	* @detail ���l�֐��̍œK���ς݃p�����[�^�̓o�C�i���t�@�C������ǂݍ���. �o�C�i���t�@�C���͈ȉ��̍\��.
	* offset = 0: ���g���G���f�B�A���Ȃ�1, �r�b�O�G���f�B�A���Ȃ�0.
	* offset = 1: move_count_per_phase
	* offset >= 2: ���l�֐��p�����[�^
	**/
	template<ValueRepresentation VALUE_REPS>
	class ValueFunction
	{
		using Weight = utils::DynamicArray<utils::Array<ValueFuncParam, 2>>;
		using PackedWeight = utils::DynamicArray<PackedValueFuncParam>;

	public:
		int32_t phase_num() { return this->_phase_num; }
		int32_t move_count_per_phase() { return this->_move_count_per_phase; }

		ValueFunction(int32_t move_count_per_phase);
		ValueFunction(const std::string path);

		void init_weight_with_rand_num() { init_weight_with_rand_num(1.0f, 0.0f); }
		void init_weight_with_rand_num(float mean, float variance);
		void save_to_file(const std::string& path);

		/**
		* @fn
		* @brief �v���C���[�̊e�d�݂�, ����̏d�݂ɃR�s�[����.
		**/
		void copy_player_weight_to_opponent();

		/**
		* @fn
		* @brief ���݂̔Ֆʂ���, �΋ǂ̌��ʂ�\������.
		* @param (pos_feature) ���݂̔Ֆʂ̓���.
		* @return �\���l. VALUE_REPS��DISC_DIFF�ł����, �ŏI�΍��̗\���l. WIN_RATE�ł����, �\�z����.
		**/
		float predict(const PositionFeature& pos_feature) const
		{
			return predict(this->empty_count_to_phase[pos_feature.empty_square_count()], pos_feature);
		}

		/**
		* @fn
		* @brief ���݂̔Ֆʂ���, �΋ǂ̌��ʂ�\������.
		* @param (phase) �Q�[���̐i�s�x.
		* @param (pos_feature) ���݂̔Ֆʂ̓���.
		* @return �\���l. VALUE_REPS��DISC_DIFF�ł����, �ŏI�΍��̗\���l. WIN_RATE�ł����, �\�z����.
		**/
		FORCE_INLINE float predict(int32_t phase, const PositionFeature& pos_feature) const
		{
			auto& w = this->weight[phase][pos_feature.side_to_move()];
			auto& f = pos_feature.features;
			auto v =
				w.corner3x3[f[0]] + w.corner3x3[f[1]] + w.corner3x3[f[2]] + w.corner3x3[f[3]]
				+ w.corner_edge_x[f[4]] + w.corner_edge_x[f[5]] + w.corner_edge_x[f[6]] + w.corner_edge_x[f[7]]
				+ w.edge_2x[f[8]] + w.edge_2x[f[9]] + w.edge_2x[f[10]] + w.edge_2x[f[11]]
				+ w.corner2x5[f[12]] + w.corner2x5[f[13]] + w.corner2x5[f[14]] + w.corner2x5[f[15]]
				+ w.line0[f[16]] + w.line0[f[17]] + w.line0[f[18]] + w.line0[f[19]]
				+ w.line1[f[20]] + w.line1[f[21]] + w.line1[f[22]] + w.line1[f[23]]
				+ w.line2[f[24]] + w.line2[f[25]] + w.line2[f[26]] + w.line2[f[27]]
				+ w.diag_line8[f[28]] + w.diag_line8[f[29]]
				+ w.diag_line7[f[30]] + w.diag_line7[f[31]] + w.diag_line7[f[32]] + w.diag_line7[f[33]]
				+ w.diag_line6[f[34]] + w.diag_line6[f[35]] + w.diag_line6[f[36]] + w.diag_line6[f[37]]
				+ w.diag_line5[f[38]] + w.diag_line5[f[39]] + w.diag_line5[f[40]] + w.diag_line5[f[41]]
				+ w.diag_line4[f[42]] + w.diag_line4[f[43]] + w.diag_line4[f[44]] + w.diag_line4[f[45]]
				+ w.bias;

			if constexpr (VALUE_REPS == ValueRepresentation::WIN_RATE)
				v = utils::std_sigmoid(v);

			return v;
		}

	private:
		// �]���Ώۂ̔Ֆʂɂ�����󂫃}�X�̍ő吔.
		static constexpr int32_t MAX_EMPTY_COUNT = reversi::SQUARE_NUM - 4;

		int32_t _phase_num;
		int32_t _move_count_per_phase;
		utils::Array<int32_t, reversi::SQUARE_NUM - 4 + 1> empty_count_to_phase;
		Weight weight;

		void init_empty_count_to_phase_table();
		void load_weight(std::ifstream& ifs, bool swap_byte);
		void expand_packed_weight(PackedWeight& packed_weight);

		template<PatternKind KIND>
		void read_param(std::ifstream& ifs, utils::Array<float, PACKED_PATTERN_FEATURE_NUM[KIND]>& param, bool swap_byte)
		{
			ifs.read(reinterpret_cast<char*>(param.as_raw_array()), sizeof(float) * param.length());
			if (swap_byte)
				for (int32_t i = 0; i < param.length(); i++)
				{
					auto swapped = BYTE_SWAP_32(*reinterpret_cast<uint32_t*>(&param[i]));
					param[i] = *reinterpret_cast<float*>(&swapped);
				}
		}

		template<PatternKind KIND>
		void write_param(std::ofstream& ofs, utils::Array<float, PACKED_PATTERN_FEATURE_NUM[KIND]>& param)
		{
			ofs.write(reinterpret_cast<char*>(param.as_raw_array()), sizeof(float) * param.length());
		}
	};

	template class ValueFunction<ValueRepresentation::DISC_DIFF>;
	template class ValueFunction<ValueRepresentation::WIN_RATE>;
}