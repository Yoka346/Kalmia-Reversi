#pragma once

#include "../config.h"
#include "feature.h"
#include "../utils/array.h"
#include "../utils/math_functions.h"

namespace evaluation
{
	struct PackedValueFuncParam;

	/**
	* @struct
	* @brief 価値関数のパラメータ. 
	* @detail 見やすさのためにパターンの種類ごとにパラメータを分割している. サイズは905260Bytes(約884.0KiB). 
	* この構造体を(フェーズ数 x 2)個分用意したものが価値関数の全パラメータ.
	* ToDo: パラメータを16bit浮動小数点数にも対応させる(推論時は高精度である必要はないので).
	**/
	struct ValueFuncParam
	{
		Array<float, PATTERN_FEATURE_NUM[PatternKind::CORNER3x3]> corner3x3;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::CORNER_EDGE_X]> corner_edge_x;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::EDGE_2X]> edge_2x;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::CORNER2x5]> corner2x5;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::LINE0]> line0;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::LINE1]> line1;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::LINE2]> line2;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE8]> diag_line8;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE7]> diag_line7;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE6]> diag_line6;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE5]> diag_line5;
		Array<float, PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE4]> diag_line4;
		float bias;

		void pack(PackedValueFuncParam& packed_param);
	};

	using ValueFuncParamArray = Array<float, sizeof(ValueFuncParam) / sizeof(float)>;

	inline void value_func_param_as_array(ValueFuncParam& param, ValueFuncParamArray* out) { out = reinterpret_cast<ValueFuncParamArray*>(&param); }	

	/**
	* @struct
	* @brief 圧縮した価値関数のパラメータ. 
	* @detail ファイルに保存する際に用いる. パターンを対称変換して一致する特徴の重みを1つにまとめることで圧縮する.
	* サイズは457456Bytes(約446.7KiB). この構造体をフェーズ数分用意したものが圧縮した価値関数の全パラメータ. 
	* もう一方の手番のパラメータは, 片方の手番のパラメータから生成できるので保存不要.
	**/
	struct PackedValueFuncParam
	{
	public:
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER3x3]> corner3x3;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER_EDGE_X]> corner_edge_x;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::EDGE_2X]> edge_2x;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER2x5]> corner2x5;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE0]> line0;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE1]> line1;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE2]> line2;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE8]> diag_line8;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE7]> diag_line7;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE6]> diag_line6;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE5]> diag_line5;
		Array<float, PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE4]> diag_line4;
		float bias;

		void expand(ValueFuncParam& param);
	};

	using PackedValueFuncParamArray = Array<float, sizeof(PackedValueFuncParam) / sizeof(float)>;

	inline void packed_value_func_param_as_array(PackedValueFuncParam& param, PackedValueFuncParamArray* out) { out = reinterpret_cast<PackedValueFuncParamArray*>(&param); }

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
		using Weight = utils::DynamicArray<utils::Array<ValueFuncParam, 2>>;
		using PackedWeight = utils::DynamicArray<PackedValueFuncParam>;

	private:
		// 評価対象の盤面における空きマスの最大数.
		static constexpr int32_t MAX_EMPTY_COUNT = reversi::SQUARE_NUM - 4;

		int32_t _phase_num;
		int32_t _move_count_per_phase;
		utils::Array<int32_t, reversi::SQUARE_NUM - 4 + 1> empty_count_to_phase;
		Weight weight;

		void init_empty_count_to_phase_table();
		void expand_packed_weight(PackedWeight& packed_weight);

	public:
		inline int32_t phase_num() { return this->_phase_num; }
		inline int32_t move_count_per_phase() { return this->_move_count_per_phase; }

		ValueFunction(int32_t move_count_per_phase);
		ValueFunction(const std::string path);

		inline void init_weight_with_rand_num() { init_weight_with_rand_num(1.0f, 0.0f); }
		void init_weight_with_rand_num(float mean, float variance);
		void save_to_file(const std::string& path);

		/**
		* @fn
		* @brief プレイヤーの各重みを, 対象変換して一致するパターンの特徴の重みにコピーする.
		**/
		void copy_player_weight_to_symmetric_pattern_feature();

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
		inline float predict(const PositionFeature& pos_feature) const 
		{
			return predict(this->empty_count_to_phase[pos_feature.empty_count()], pos_feature);
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
	};

	template class ValueFunction<ValueRepresentation::DISC_DIFF>;
	template class ValueFunction<ValueRepresentation::WIN_RATE>;
}