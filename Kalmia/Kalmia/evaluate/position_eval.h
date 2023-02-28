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
	template<class T>
	struct PackedValueFuncParam;

	/**
	* @struct
	* @brief 価値関数のパラメータ. 
	* @detail 見やすさのためにパターンの種類ごとにパラメータを分割している. サイズは905260Bytes(約884.0KiB). 
	* この構造体を(フェーズ数 x 2)個分用意したものが価値関数の全パラメータ.
	**/
	template<class T>
	struct ValueFuncParam
	{
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::CORNER3x3]> corner3x3;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::CORNER_EDGE_X]> corner_edge_x;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::EDGE_2X]> edge_2x;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::CORNER2x5]> corner2x5;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::LINE0]> line0;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::LINE1]> line1;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::LINE2]> line2;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE8]> diag_line8;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE7]> diag_line7;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE6]> diag_line6;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE5]> diag_line5;
		utils::Array<T, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE4]> diag_line4;
		T bias = 0.0f;


		void pack(PackedValueFuncParam<T>& packed_param)
		{
			pack<PatternKind::CORNER3x3>(this->corner3x3, packed_param.corner3x3);
			pack<PatternKind::CORNER_EDGE_X>(this->corner_edge_x, packed_param.corner_edge_x);
			pack<PatternKind::EDGE_2X>(this->edge_2x, packed_param.edge_2x);
			pack<PatternKind::CORNER2x5>(this->corner2x5, packed_param.corner2x5);
			pack<PatternKind::LINE0>(this->line0, packed_param.line0);
			pack<PatternKind::LINE1>(this->line1, packed_param.line1);
			pack<PatternKind::LINE2>(this->line2, packed_param.line2);
			pack<PatternKind::DIAG_LINE8>(this->diag_line8, packed_param.diag_line8);
			pack<PatternKind::DIAG_LINE7>(this->diag_line7, packed_param.diag_line7);
			pack<PatternKind::DIAG_LINE6>(this->diag_line6, packed_param.diag_line6);
			pack<PatternKind::DIAG_LINE5>(this->diag_line5, packed_param.diag_line5);
			pack<PatternKind::DIAG_LINE4>(this->diag_line4, packed_param.diag_line4);
			packed_param.bias = this->bias;
		}

		void to_opponent(ValueFuncParam<T>& dest)
		{
			to_opponent<PatternKind::CORNER3x3>(this->corner3x3, dest.corner3x3);
			to_opponent<PatternKind::CORNER_EDGE_X>(this->corner_edge_x, dest.corner_edge_x);
			to_opponent<PatternKind::EDGE_2X>(this->edge_2x, dest.edge_2x);
			to_opponent<PatternKind::CORNER2x5>(this->corner2x5, dest.corner2x5);
			to_opponent<PatternKind::LINE0>(this->line0, dest.line0);
			to_opponent<PatternKind::LINE1>(this->line1, dest.line1);
			to_opponent<PatternKind::LINE2>(this->line2, dest.line2);
			to_opponent<PatternKind::DIAG_LINE8>(this->diag_line8, dest.diag_line8);
			to_opponent<PatternKind::DIAG_LINE7>(this->diag_line7, dest.diag_line7);
			to_opponent<PatternKind::DIAG_LINE6>(this->diag_line6, dest.diag_line6);
			to_opponent<PatternKind::DIAG_LINE5>(this->diag_line5, dest.diag_line5);
			to_opponent<PatternKind::DIAG_LINE4>(this->diag_line4, dest.diag_line4);
			dest.bias = this->bias;
		}

		void clear()
		{
			this->corner3x3.clear();
			this->corner_edge_x.clear();
			this->edge_2x.clear();
			this->corner2x5.clear();
			this->line0.clear();
			this->line1.clear();
			this->line2.clear();
			this->diag_line8.clear();
			this->diag_line7.clear();
			this->diag_line6.clear();
			this->diag_line5.clear();
			this->diag_line4.clear();
			this->bias = 0.0f;
		}

		/**
		* @fn
		* @brief パラメーターをスケーリングして, DestTypeに変換する.
		* @tparam (DestType) 変換先のパラメータの型
		* @detail 例えば, float型のパラメータを1024倍して, int16_t型のパラメータに変換したいときは,
		*		  param.scale<int16_t>(dest, 1024.0f); のように書く.
		**/
		template<class DestType>
		void scale(ValueFuncParam<DestType>& dest, float scale_rate)
		{
			scale<PatternKind::CORNER3x3, DestType>(this->corner3x3, dest.corner3x3, scale_rate);
			scale<PatternKind::CORNER_EDGE_X, DestType>(this->corner_edge_x, dest.corner_edge_x, scale_rate);
			scale<PatternKind::EDGE_2X, DestType>(this->edge_2x, dest.edge_2x, scale_rate);
			scale<PatternKind::CORNER2x5, DestType>(this->corner2x5, dest.corner2x5, scale_rate);
			scale<PatternKind::LINE0, DestType>(this->line0, dest.line0, scale_rate);
			scale<PatternKind::LINE1, DestType>(this->line1, dest.line1, scale_rate);
			scale<PatternKind::LINE2, DestType>(this->line2, dest.line2, scale_rate);
			scale<PatternKind::DIAG_LINE8, DestType>(this->diag_line8, dest.diag_line8, scale_rate);
			scale<PatternKind::DIAG_LINE7, DestType>(this->diag_line7, dest.diag_line7, scale_rate);
			scale<PatternKind::DIAG_LINE6, DestType>(this->diag_line6, dest.diag_line6, scale_rate);
			scale<PatternKind::DIAG_LINE5, DestType>(this->diag_line5, dest.diag_line5, scale_rate);
			scale<PatternKind::DIAG_LINE4, DestType>(this->diag_line4, dest.diag_line4, scale_rate);
			dest.bias = static_cast<DestType>(this->bias * scale_rate);
		}

		private:
			template<PatternKind KIND>
			void pack(utils::Array<T, PATTERN_FEATURE_NUM[KIND]>& param, utils::Array<T, PACKED_PATTERN_FEATURE_NUM[KIND]>& packed)
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
			void to_opponent(utils::Array<T, PATTERN_FEATURE_NUM[KIND]>& param, utils::Array<T, PATTERN_FEATURE_NUM[KIND]>& dest)
			{
				auto offset = PATTERN_FEATURE_OFFSET[KIND];
				for (int32_t f = 0; f < PATTERN_FEATURE_NUM[KIND]; f++)
				{
					auto opponent_f = TO_OPPONENT_FEATURE[offset + f];
					dest[opponent_f] = param[f];
				}
			}

			template<PatternKind KIND, class DestType>
			void scale(utils::Array<T, PATTERN_FEATURE_NUM[KIND]>& param, utils::Array<DestType, PATTERN_FEATURE_NUM[KIND]>& dest, float scale_rate)
			{
				auto offset = PATTERN_FEATURE_OFFSET[KIND];
				for (int32_t f = 0; f < PATTERN_FEATURE_NUM[KIND]; f++)
				{
					auto symmetric_f = TO_SYMMETRIC_FEATURE[offset + f];
					dest[f] = (symmetric_f < f) ? dest[symmetric_f] : static_cast<DestType>(param[f] * scale_rate);
				}
			}
	};

	/**
	* @struct
	* @brief 圧縮した価値関数のパラメータ. 
	* @detail ファイルに保存する際に用いる. パターンを対称変換して一致する特徴の重みを1つにまとめることで圧縮する.
	* サイズは457456Bytes(約446.7KiB). この構造体をフェーズ数分用意したものが圧縮した価値関数の全パラメータ. 
	* もう一方の手番のパラメータは, 片方の手番のパラメータから生成できるので保存不要.
	**/
	template<class T>
	struct PackedValueFuncParam
	{
	public:
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER3x3]> corner3x3;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER_EDGE_X]> corner_edge_x;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::EDGE_2X]> edge_2x;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER2x5]> corner2x5;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE0]> line0;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE1]> line1;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE2]> line2;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE8]> diag_line8;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE7]> diag_line7;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE6]> diag_line6;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE5]> diag_line5;
		utils::Array<T, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE4]> diag_line4;
		T bias = 0.0f;

		void expand(ValueFuncParam<T>& param)
		{
			expand<PatternKind::CORNER3x3>(this->corner3x3, param.corner3x3);
			expand<PatternKind::CORNER_EDGE_X>(this->corner_edge_x, param.corner_edge_x);
			expand<PatternKind::EDGE_2X>(this->edge_2x, param.edge_2x);
			expand<PatternKind::CORNER2x5>(this->corner2x5, param.corner2x5);
			expand<PatternKind::LINE0>(this->line0, param.line0);
			expand<PatternKind::LINE1>(this->line1, param.line1);
			expand<PatternKind::LINE2>(this->line2, param.line2);
			expand<PatternKind::DIAG_LINE8>(this->diag_line8, param.diag_line8);
			expand<PatternKind::DIAG_LINE7>(this->diag_line7, param.diag_line7);
			expand<PatternKind::DIAG_LINE6>(this->diag_line6, param.diag_line6);
			expand<PatternKind::DIAG_LINE5>(this->diag_line5, param.diag_line5);
			expand<PatternKind::DIAG_LINE4>(this->diag_line4, param.diag_line4);
			param.bias = this->bias;
		}

	private:
		template<PatternKind KIND>
		void expand(utils::Array<T, PACKED_PATTERN_FEATURE_NUM[KIND]>& param, utils::Array<T, PATTERN_FEATURE_NUM[KIND]>& expanded)
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
	* @brief 盤面の評価を行う価値関数.
	* @tparam (VALUE_REPS) 価値関数の出力値が表現するもの.
	* @detail 価値関数の最適化済みパラメータはバイナリファイルから読み込む. バイナリファイルは以下の構成.
	* offset = 0: リトルエンディアンなら1, ビッグエンディアンなら0.
	* offset = 1: move_count_per_phase
	* offset >= 2: 価値関数パラメータ
	**/
	template<ValueRepresentation VALUE_REPS>
	class ValueFunction
	{
		using Weight = utils::DynamicArray<utils::Array<ValueFuncParam<int16_t>, 2>>;
		using PackedWeight = utils::DynamicArray<PackedValueFuncParam<float>>;

	public:
		static constexpr int32_t WEIGHT_SCALE = 1024;
		Weight weight;

		int32_t phase_num() { return this->_phase_num; }
		int32_t move_count_per_phase() { return this->_move_count_per_phase; }

		ValueFunction(int32_t move_count_per_phase);
		ValueFunction(const std::string path);

		void save_to_file(const std::string& path);

		/**
		* @fn
		* @brief プレイヤーの各重みを, 相手の重みにコピーする.
		**/
		void copy_player_weight_to_opponent();

		/**
		* @fn
		* @brief 現在の盤面から, 対局の結果を予測する.
		* @param (pos_feature) 現在の盤面の特徴.
		* @return 予測値. VALUE_REPSがDISC_DIFFであれば, 最終石差の予測値. WIN_RATEであれば, 予想勝率.
		**/
		float predict(const PositionFeature& pos_feature) const
		{
			return predict(this->empty_count_to_phase[pos_feature.empty_square_count()], pos_feature);
		}

		/**
		* @fn
		* @brief 現在の盤面から, 対局の結果を予測する.
		* @param (phase) ゲームの進行度.
		* @param (pos_feature) 現在の盤面の特徴.
		* @return 予測値. VALUE_REPSがDISC_DIFFであれば, 最終石差の予測値. WIN_RATEであれば, 予想勝率.
		**/
		FORCE_INLINE float predict(int32_t phase, const PositionFeature& pos_feature) const
		{
			auto& w = this->weight[phase][pos_feature.side_to_move()];
			auto& f = pos_feature.features;
			int32_t v =
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

			float fv;
			if constexpr (VALUE_REPS == ValueRepresentation::WIN_RATE)
				fv = utils::std_sigmoid(v / WEIGHT_SCALE);
			else
				fv = static_cast<float>(v);

			return fv;
		}

	private:
		// 評価対象の盤面における空きマスの最大数.
		static constexpr int32_t MAX_EMPTY_COUNT = reversi::SQUARE_NUM - 4;

		int32_t _phase_num;
		int32_t _move_count_per_phase;
		utils::Array<int32_t, reversi::SQUARE_NUM - 4 + 1> empty_count_to_phase;

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