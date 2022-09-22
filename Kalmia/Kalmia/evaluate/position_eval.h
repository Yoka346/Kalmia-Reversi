#pragma once

#include "feature.h"

namespace evaluation
{
	/**
	* @struct
	* @brief ���l�֐��̃p�����[�^. 
	* @detail ���₷���̂��߂Ƀp�^�[���̎�ނ��ƂɃp�����[�^�𕪊����Ă���. �T�C�Y��905260Bytes(��884.0KiB). 
	* ���̍\���̂�(�t�F�[�Y�� x 2)���p�ӂ������̂����l�֐��̑S�p�����[�^.
	* ToDo: �p�����[�^��16bit���������_���ɂ��Ή�������(���_���͍����x�ł���K�v�͂Ȃ��̂�).
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
	* @brief ���k�������l�֐��̃p�����[�^. 
	* @detail �t�@�C���ɕۑ�����ۂɗp����. �p�^�[����Ώ̕ϊ����Ĉ�v��������̏d�݂�1�ɂ܂Ƃ߂邱�Ƃň��k����.
	* �T�C�Y��457456Bytes(��446.7KiB). ���̍\���̂��t�F�[�Y�����p�ӂ������̂����k�������l�֐��̑S�p�����[�^. 
	* ��������̎�Ԃ̃p�����[�^��, �Е��̎�Ԃ̃p�����[�^���琶���ł���̂ŕۑ��s�v.
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