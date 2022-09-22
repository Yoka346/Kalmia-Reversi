#pragma once

#include "feature.h"

namespace evaluation
{
	/**
	* @struct
	* @brief 価値関数のパラメータ. 
	* @detail 見やすさのためにパターンの種類ごとにパラメータを分割している. サイズは905260Bytes(約884.0KiB). 
	* この構造体を(フェーズ数 x 2)個分用意したものが価値関数の全パラメータ.
	* ToDo: パラメータを16bit浮動小数点数にも対応させる(推論時は高精度である必要はないので).
	**/
	struct ValueFuncParam
	{
		float corner3x3[PATTERN_FEATURE_NUM[PatternKind::CORNER3x3]];
		float corner_edge_x[PATTERN_FEATURE_NUM[PatternKind::CORNER_EDGE_X]];
		float edge_2x[PATTERN_FEATURE_NUM[PatternKind::EDGE_2X]];
		float corner2x5[PATTERN_FEATURE_NUM[PatternKind::CORNER2x5]];
		float line0[PATTERN_FEATURE_NUM[PatternKind::LINE0]];
		float line1[PATTERN_FEATURE_NUM[PatternKind::LINE1]];
		float line2[PATTERN_FEATURE_NUM[PatternKind::LINE2]];
		float diag_line8[PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE8]];
		float diag_line7[PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE7]];
		float diag_line6[PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE6]];
		float diag_line5[PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE5]];
		float diag_line4[PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE4]];
		float bias;
	};

	/**
	* @struct
	* @brief 圧縮した価値関数のパラメータ. 
	* @detail ファイルに保存する際に用いる. パターンを対称変換して一致する特徴の重みを1つにまとめることで圧縮する.
	* サイズは457456Bytes(約446.7KiB). この構造体をフェーズ数分用意したものが圧縮した価値関数の全パラメータ. 
	* もう一方の手番のパラメータは, 片方の手番のパラメータから生成できるので保存不要.
	**/
	struct PackedValueFuncParam
	{
		float corner3x3[PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER3x3]];
		float corner_edge_x[PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER_EDGE_X]];
		float edge_2x[PACKED_PATTERN_FEATURE_NUM[PatternKind::EDGE_2X]];
		float corner2x5[PACKED_PATTERN_FEATURE_NUM[PatternKind::CORNER2x5]];
		float line0[PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE0]];
		float line1[PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE1]];
		float line2[PACKED_PATTERN_FEATURE_NUM[PatternKind::LINE2]];
		float diag_line8[PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE8]];
		float diag_line7[PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE7]];
		float diag_line6[PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE6]];
		float diag_line5[PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE5]];
		float diag_line4[PACKED_PATTERN_FEATURE_NUM[PatternKind::DIAG_LINE4]];
		float bias;
	};

	class ValueFunction
	{

	};
}