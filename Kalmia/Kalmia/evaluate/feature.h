#pragma once

#include "../reversi/constant.h"
#include "../reversi/types.h"
#include "../reversi/position.h"

namespace evaluate
{	
    enum PatternKind
    {
        Corner3x3,
        CornerEdgeX,
        Edge2X,
        Edge4x2AndCorner,
        Line2,
        Line3,
        Line4,
        DiagonalLine0,
        DiagonalLine1,
        DiagonalLine2,
        DiagonalLine3,
        DiagonalLine4
    };

	// Kalmiaが局面評価に用いるパターンはEdaxと同じ12種類のパターン.
	constexpr int32_t PATTERN_KIND_NUM = 12;	

	// 局面から抽出するパターンは全部で46個(Bias項も含む).
	constexpr int32_t ALL_PATTERN_NUM = 46;

	constexpr int32_t MAX_PATTERN_SIZE = 10;

    // 各パターンを構成するマスの数.
    constexpr ConstantArray<int32_t, PATTERN_KIND_NUM> PATTERN_SIZE = { 9, 10, 10, 10, 8, 8, 8, 8, 7, 6, 5, 4 };

    // 各パターンの特徴の数((e.g. Corner3x3パターンであれば, サイズが9, マスが取りうる状態は黒, 白, 空の3つなので, 3^9個の特徴).
    constexpr ConstantArray<int32_t, PATTERN_KIND_NUM> PATTERN_FEATURE_NUM = {19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81};

    // 対称変換したら一致するパターンを除いた際の各パターンの特徴の数.
    constexpr ConstantArray<int32_t, PATTERN_KIND_NUM> PACKED_PATTERN_FEATURE_NUM = { 10206, 29889, 29646, 29646, 3321, 3321, 3321, 3321, 1134, 378, 135, 45 };

    // 3^n(n <= MAX_PATTERN_SIZE)の結果を格納したテーブル. 特徴の計算でよく使うのでコンパイル時に事前計算しておく.
    constexpr ConstantArray<uint16_t, MAX_PATTERN_SIZE + 1> POW_3([](uint16_t* data, size_t len)
        {
            for (size_t i = 0; i < len; i++)
            {
                uint16_t pow3 = 1;
                for (uint16_t n = 0; n < i; n++)
                    pow3 *= 3;
                data[i] = pow3;
            }
        });

	struct PatternInfo
	{
		// パターンを構成するマスの数.
		int32_t size;

		// パターンを構成するマスの座標.
        ConstantArray<reversi::BoardCoordinate, MAX_PATTERN_SIZE + 1> coordinates;
	};

    constexpr PatternInfo PATTERN_INFO[ALL_PATTERN_NUM] =
    {
        // corner3x3 
        { PATTERN_SIZE[Corner3x3], {reversi::A1, reversi::B1, reversi::A2, reversi::B2, reversi::C1, reversi::A3, reversi::C2, reversi::B3, reversi::C3}},
        { PATTERN_SIZE[Corner3x3], {reversi::H1, reversi::G1, reversi::H2, reversi::G2, reversi::F1, reversi::H3, reversi::F2, reversi::G3, reversi::F3}},
        { PATTERN_SIZE[Corner3x3], {reversi::A8, reversi::A7, reversi::B8, reversi::B7, reversi::A6, reversi::C8, reversi::B6, reversi::C7, reversi::C6}},
        { PATTERN_SIZE[Corner3x3], {reversi::H8, reversi::H7, reversi::G8, reversi::G7, reversi::H6, reversi::F8, reversi::G6, reversi::F7, reversi::F6}},

        // corner edge x 
        { PATTERN_SIZE[CornerEdgeX], {reversi::A5, reversi::A4, reversi::A3, reversi::A2, reversi::A1, reversi::B2, reversi::B1, reversi::C1, reversi::D1, reversi::E1}},
        { PATTERN_SIZE[CornerEdgeX], {reversi::H5, reversi::H4, reversi::H3, reversi::H2, reversi::H1, reversi::G2, reversi::G1, reversi::F1, reversi::E1, reversi::D1}},
        { PATTERN_SIZE[CornerEdgeX], {reversi::A4, reversi::A5, reversi::A6, reversi::A7, reversi::A8, reversi::B7, reversi::B8, reversi::C8, reversi::D8, reversi::E8}},
        { PATTERN_SIZE[CornerEdgeX], {reversi::H4, reversi::H5, reversi::H6, reversi::H7, reversi::H8, reversi::G7, reversi::G8, reversi::F8, reversi::E8, reversi::D8}},

        // edge 2x 
        { PATTERN_SIZE[Edge2X], {reversi::B2, reversi::A1, reversi::B1, reversi::C1, reversi::D1, reversi::E1, reversi::F1, reversi::G1, reversi::H1, reversi::G2}},
        { PATTERN_SIZE[Edge2X], {reversi::B7, reversi::A8, reversi::B8, reversi::C8, reversi::D8, reversi::E8, reversi::F8, reversi::G8, reversi::H8, reversi::G7}},
        { PATTERN_SIZE[Edge2X], {reversi::B2, reversi::A1, reversi::A2, reversi::A3, reversi::A4, reversi::A5, reversi::A6, reversi::A7, reversi::A8, reversi::B7}},
        { PATTERN_SIZE[Edge2X], {reversi::G2, reversi::H1, reversi::H2, reversi::H3, reversi::H4, reversi::H5, reversi::H6, reversi::H7, reversi::H8, reversi::G7}},

        // edge4x2 2x 
        { PATTERN_SIZE[Edge4x2AndCorner], {reversi::A1, reversi::C1, reversi::D1, reversi::C2, reversi::D2, reversi::E2, reversi::F2, reversi::E1, reversi::F1, reversi::H1}},
        { PATTERN_SIZE[Edge4x2AndCorner], {reversi::A8, reversi::C8, reversi::D8, reversi::C7, reversi::D7, reversi::E7, reversi::F7, reversi::E8, reversi::F8, reversi::H8}},
        { PATTERN_SIZE[Edge4x2AndCorner], {reversi::A1, reversi::A3, reversi::A4, reversi::B3, reversi::B4, reversi::B5, reversi::B6, reversi::A5, reversi::A6, reversi::A8}},
        { PATTERN_SIZE[Edge4x2AndCorner], {reversi::H1, reversi::H3, reversi::H4, reversi::G3, reversi::G4, reversi::G5, reversi::G6, reversi::H5, reversi::H6, reversi::H8}},

        // horizontal and vertical line (row = 2 or column = 2)
        { PATTERN_SIZE[Line2], {reversi::A2, reversi::B2, reversi::C2, reversi::D2, reversi::E2, reversi::F2, reversi::G2, reversi::H2}},
        { PATTERN_SIZE[Line2], {reversi::A7, reversi::B7, reversi::C7, reversi::D7, reversi::E7, reversi::F7, reversi::G7, reversi::H7}},
        { PATTERN_SIZE[Line2], {reversi::B1, reversi::B2, reversi::B3, reversi::B4, reversi::B5, reversi::B6, reversi::B7, reversi::B8}},
        { PATTERN_SIZE[Line2], {reversi::G1, reversi::G2, reversi::G3, reversi::G4, reversi::G5, reversi::G6, reversi::G7, reversi::G8}},

        // horizontal and vertical line (row = 3 or column = 3)
        { PATTERN_SIZE[Line3], {reversi::A3, reversi::B3, reversi::C3, reversi::D3, reversi::E3, reversi::F3, reversi::G3, reversi::H3}},
        { PATTERN_SIZE[Line3], {reversi::A6, reversi::B6, reversi::C6, reversi::D6, reversi::E6, reversi::F6, reversi::G6, reversi::H6}},
        { PATTERN_SIZE[Line3], {reversi::C1, reversi::C2, reversi::C3, reversi::C4, reversi::C5, reversi::C6, reversi::C7, reversi::C8}},
        { PATTERN_SIZE[Line3], {reversi::F1, reversi::F2, reversi::F3, reversi::F4, reversi::F5, reversi::F6, reversi::F7, reversi::F8}},

        // horizontal and vertical line (row = 4 or column = 4)
        { PATTERN_SIZE[Line4], {reversi::A4, reversi::B4, reversi::C4, reversi::D4, reversi::E4, reversi::F4, reversi::G4, reversi::H4}},
        { PATTERN_SIZE[Line4], {reversi::A5, reversi::B5, reversi::C5, reversi::D5, reversi::E5, reversi::F5, reversi::G5, reversi::H5}},
        { PATTERN_SIZE[Line4], {reversi::D1, reversi::D2, reversi::D3, reversi::D4, reversi::D5, reversi::D6, reversi::D7, reversi::D8}},
        { PATTERN_SIZE[Line4], {reversi::E1, reversi::E2, reversi::E3, reversi::E4, reversi::E5, reversi::E6, reversi::E7, reversi::E8}},

        // diagonal line 0
        { PATTERN_SIZE[DiagonalLine0], {reversi::A1, reversi::B2, reversi::C3, reversi::D4, reversi::E5, reversi::F6, reversi::G7, reversi::H8}},
        { PATTERN_SIZE[DiagonalLine0], {reversi::A8, reversi::B7, reversi::C6, reversi::D5, reversi::E4, reversi::F3, reversi::G2, reversi::H1}},

        // diagonal line 1
        { PATTERN_SIZE[DiagonalLine1], {reversi::B1, reversi::C2, reversi::D3, reversi::E4, reversi::F5, reversi::G6, reversi::H7}},
        { PATTERN_SIZE[DiagonalLine1], {reversi::H2, reversi::G3, reversi::F4, reversi::E5, reversi::D6, reversi::C7, reversi::B8}},
        { PATTERN_SIZE[DiagonalLine1], {reversi::A2, reversi::B3, reversi::C4, reversi::D5, reversi::E6, reversi::F7, reversi::G8}},
        { PATTERN_SIZE[DiagonalLine1], {reversi::G1, reversi::F2, reversi::E3, reversi::D4, reversi::C5, reversi::B6, reversi::A7}},

        // diagonal line 2
        { PATTERN_SIZE[DiagonalLine2], {reversi::C1, reversi::D2, reversi::E3, reversi::F4, reversi::G5, reversi::H6}},
        { PATTERN_SIZE[DiagonalLine2], {reversi::A3, reversi::B4, reversi::C5, reversi::D6, reversi::E7, reversi::F8}},
        { PATTERN_SIZE[DiagonalLine2], {reversi::F1, reversi::E2, reversi::D3, reversi::C4, reversi::B5, reversi::A6}},
        { PATTERN_SIZE[DiagonalLine2], {reversi::H3, reversi::G4, reversi::F5, reversi::E6, reversi::D7, reversi::C8}},

        // diagonal line 3
        { PATTERN_SIZE[DiagonalLine3], {reversi::D1, reversi::E2, reversi::F3, reversi::G4, reversi::H5}},
        { PATTERN_SIZE[DiagonalLine3], {reversi::A4, reversi::B5, reversi::C6, reversi::D7, reversi::E8}},
        { PATTERN_SIZE[DiagonalLine3], {reversi::E1, reversi::D2, reversi::C3, reversi::B4, reversi::A5}},
        { PATTERN_SIZE[DiagonalLine3], {reversi::H4, reversi::G5, reversi::F6, reversi::E7, reversi::D8}},

        // diagonal line 4
        { PATTERN_SIZE[DiagonalLine4], {reversi::D1, reversi::C2, reversi::B3, reversi::A4}},
        { PATTERN_SIZE[DiagonalLine4], {reversi::A5, reversi::B6, reversi::C7, reversi::D8}},
        { PATTERN_SIZE[DiagonalLine4], {reversi::E1, reversi::F2, reversi::G3, reversi::H4}},
        { PATTERN_SIZE[DiagonalLine4], {reversi::H5, reversi::G6, reversi::F7, reversi::E8}}
    };

    constexpr uint16_t mirror_feature(uint16_t feature, int32_t size)
    {
        uint16_t mirrored = 0;
        for (int32_t i = 0; i < size; i++)
            mirrored += ((feature / POW_3[size - (i + 1)]) % 3) * POW_3[i];
        return mirrored;
    }

    template<int TABLE_LEN>
    constexpr uint16_t shuffle_feature_with_table(uint16_t feature, const ConstantArray<int32_t, TABLE_LEN>& table)
    {
        uint16_t shuffled = 0;
        for (size_t i = 0; i < table.length(); i++)
        {
            auto idx = table[i];
            uint16_t tmp = (feature / POW_3[idx]) % 3;
            shuffled += tmp * POW_3[i];
        }
        return shuffled;
    }

    constexpr uint16_t to_symmetric_feature(PatternKind kind, uint16_t feature)
    {
        constexpr ConstantArray<int32_t, PATTERN_SIZE[PatternKind::Corner3x3]> TABLE_FOR_CORNER_3X3 = { 0, 2, 1, 4, 3, 5, 7, 6, 8 };
        constexpr ConstantArray<int32_t, PATTERN_SIZE[PatternKind::CornerEdgeX]> TABLE_FOR_CORNER_EDGE_X = { 9, 8, 7, 6, 4, 5, 3, 2, 1, 0 };

        if (kind == PatternKind::Corner3x3)
            return shuffle_feature_with_table(feature, TABLE_FOR_CORNER_3X3);

        if (kind == PatternKind::CornerEdgeX)
            return shuffle_feature_with_table(feature, TABLE_FOR_CORNER_EDGE_X);

        return mirror_feature(feature, PATTERN_SIZE[kind]);
    }

	/**
	* @class
	* @brief 局面の特徴を表現するクラス.
	* @detail 局面の評価はこの特徴に基づいて行われる.
	**/
	class PositionFeature
	{

	};
}
