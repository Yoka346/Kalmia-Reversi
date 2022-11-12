#pragma once
#include "../pch.h"
#include "../reversi/board.h"

namespace evaluation
{
    constexpr int MAX_FEATURE_SIZE = 10;
    constexpr int FEATURE_NUM = 47;
    constexpr int FEATURE_KIND_NUM = 13;
    constexpr int EACH_FEATURE_NUM[FEATURE_KIND_NUM] = { 4, 4, 4, 4, 4, 4, 4, 2, 4, 4, 4, 4, 1 };
    constexpr int FEATURE_SIZE[FEATURE_KIND_NUM] = { 9, 10, 10, 10, 8, 8, 8, 8, 7, 6, 5, 4, 0 };
    constexpr int PATTERN_NUM[FEATURE_KIND_NUM] = { 19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81, 1 };
    constexpr int PACKED_PATTERN_NUM[FEATURE_KIND_NUM] = { 10206, 29889, 29646, 29646, 3321, 3321, 3321, 3321, 1134, 378, 135, 45, 1 };

    enum FeatureKind
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
        DiagonalLine4,
        Bias
    };

	struct FeatureInfo
	{
		int size;
		reversi::BoardCoordinate coordinates[MAX_FEATURE_SIZE];
	};

    struct Pattern 
    {
        int feature_id;
        uint16_t n;

        constexpr Pattern() :feature_id(-1), n(0) { ; }
        constexpr Pattern(int feature_id, uint16_t n) : feature_id(feature_id), n(n) { ; }
    };

    constexpr FeatureInfo FEATURE_INFO[FEATURE_NUM] =
    {
        // corner3x3 
        { FEATURE_SIZE[Corner3x3], {reversi::A1, reversi::B1, reversi::A2, reversi::B2, reversi::C1, reversi::A3, reversi::C2, reversi::B3, reversi::C3}},
        { FEATURE_SIZE[Corner3x3], {reversi::H1, reversi::G1, reversi::H2, reversi::G2, reversi::F1, reversi::H3, reversi::F2, reversi::G3, reversi::F3}},
        { FEATURE_SIZE[Corner3x3], {reversi::A8, reversi::A7, reversi::B8, reversi::B7, reversi::A6, reversi::C8, reversi::B6, reversi::C7, reversi::C6}},
        { FEATURE_SIZE[Corner3x3], {reversi::H8, reversi::H7, reversi::G8, reversi::G7, reversi::H6, reversi::F8, reversi::G6, reversi::F7, reversi::F6}},

        // corner edge x 
        { FEATURE_SIZE[CornerEdgeX], {reversi::A5, reversi::A4, reversi::A3, reversi::A2, reversi::A1, reversi::B2, reversi::B1, reversi::C1, reversi::D1, reversi::E1}},
        { FEATURE_SIZE[CornerEdgeX], {reversi::H5, reversi::H4, reversi::H3, reversi::H2, reversi::H1, reversi::G2, reversi::G1, reversi::F1, reversi::E1, reversi::D1}},
        { FEATURE_SIZE[CornerEdgeX], {reversi::A4, reversi::A5, reversi::A6, reversi::A7, reversi::A8, reversi::B7, reversi::B8, reversi::C8, reversi::D8, reversi::E8}},
        { FEATURE_SIZE[CornerEdgeX], {reversi::H4, reversi::H5, reversi::H6, reversi::H7, reversi::H8, reversi::G7, reversi::G8, reversi::F8, reversi::E8, reversi::D8}},

        // edge 2x 
        { FEATURE_SIZE[Edge2X], {reversi::B2, reversi::A1, reversi::B1, reversi::C1, reversi::D1, reversi::E1, reversi::F1, reversi::G1, reversi::H1, reversi::G2}},
        { FEATURE_SIZE[Edge2X], {reversi::B7, reversi::A8, reversi::B8, reversi::C8, reversi::D8, reversi::E8, reversi::F8, reversi::G8, reversi::H8, reversi::G7}},
        { FEATURE_SIZE[Edge2X], {reversi::B2, reversi::A1, reversi::A2, reversi::A3, reversi::A4, reversi::A5, reversi::A6, reversi::A7, reversi::A8, reversi::B7}},
        { FEATURE_SIZE[Edge2X], {reversi::G2, reversi::H1, reversi::H2, reversi::H3, reversi::H4, reversi::H5, reversi::H6, reversi::H7, reversi::H8, reversi::G7}},

        // edge4x2 2x 
        { FEATURE_SIZE[Edge4x2AndCorner], {reversi::A1, reversi::C1, reversi::D1, reversi::C2, reversi::D2, reversi::E2, reversi::F2, reversi::E1, reversi::F1, reversi::H1}},
        { FEATURE_SIZE[Edge4x2AndCorner], {reversi::A8, reversi::C8, reversi::D8, reversi::C7, reversi::D7, reversi::E7, reversi::F7, reversi::E8, reversi::F8, reversi::H8}},
        { FEATURE_SIZE[Edge4x2AndCorner], {reversi::A1, reversi::A3, reversi::A4, reversi::B3, reversi::B4, reversi::B5, reversi::B6, reversi::A5, reversi::A6, reversi::A8}},
        { FEATURE_SIZE[Edge4x2AndCorner], {reversi::H1, reversi::H3, reversi::H4, reversi::G3, reversi::G4, reversi::G5, reversi::G6, reversi::H5, reversi::H6, reversi::H8}},

        // horizontal and vertical line (row = 2 or column = 2)
        { FEATURE_SIZE[Line2], {reversi::A2, reversi::B2, reversi::C2, reversi::D2, reversi::E2, reversi::F2, reversi::G2, reversi::H2}},
        { FEATURE_SIZE[Line2], {reversi::A7, reversi::B7, reversi::C7, reversi::D7, reversi::E7, reversi::F7, reversi::G7, reversi::H7}},
        { FEATURE_SIZE[Line2], {reversi::B1, reversi::B2, reversi::B3, reversi::B4, reversi::B5, reversi::B6, reversi::B7, reversi::B8}},
        { FEATURE_SIZE[Line2], {reversi::G1, reversi::G2, reversi::G3, reversi::G4, reversi::G5, reversi::G6, reversi::G7, reversi::G8}},

        // horizontal and vertical line (row = 3 or column = 3)
        { FEATURE_SIZE[Line3], {reversi::A3, reversi::B3, reversi::C3, reversi::D3, reversi::E3, reversi::F3, reversi::G3, reversi::H3}},
        { FEATURE_SIZE[Line3], {reversi::A6, reversi::B6, reversi::C6, reversi::D6, reversi::E6, reversi::F6, reversi::G6, reversi::H6}},
        { FEATURE_SIZE[Line3], {reversi::C1, reversi::C2, reversi::C3, reversi::C4, reversi::C5, reversi::C6, reversi::C7, reversi::C8}},
        { FEATURE_SIZE[Line3], {reversi::F1, reversi::F2, reversi::F3, reversi::F4, reversi::F5, reversi::F6, reversi::F7, reversi::F8}},

        // horizontal and vertical line (row = 4 or column = 4)
        { FEATURE_SIZE[Line4], {reversi::A4, reversi::B4, reversi::C4, reversi::D4, reversi::E4, reversi::F4, reversi::G4, reversi::H4}},
        { FEATURE_SIZE[Line4], {reversi::A5, reversi::B5, reversi::C5, reversi::D5, reversi::E5, reversi::F5, reversi::G5, reversi::H5}},
        { FEATURE_SIZE[Line4], {reversi::D1, reversi::D2, reversi::D3, reversi::D4, reversi::D5, reversi::D6, reversi::D7, reversi::D8}},
        { FEATURE_SIZE[Line4], {reversi::E1, reversi::E2, reversi::E3, reversi::E4, reversi::E5, reversi::E6, reversi::E7, reversi::E8}},

        // diagonal line 0
        { FEATURE_SIZE[DiagonalLine0], {reversi::A1, reversi::B2, reversi::C3, reversi::D4, reversi::E5, reversi::F6, reversi::G7, reversi::H8}},
        { FEATURE_SIZE[DiagonalLine0], {reversi::A8, reversi::B7, reversi::C6, reversi::D5, reversi::E4, reversi::F3, reversi::G2, reversi::H1}},

        // diagonal line 1
        { FEATURE_SIZE[DiagonalLine1], {reversi::B1, reversi::C2, reversi::D3, reversi::E4, reversi::F5, reversi::G6, reversi::H7}},
        { FEATURE_SIZE[DiagonalLine1], {reversi::H2, reversi::G3, reversi::F4, reversi::E5, reversi::D6, reversi::C7, reversi::B8}},
        { FEATURE_SIZE[DiagonalLine1], {reversi::A2, reversi::B3, reversi::C4, reversi::D5, reversi::E6, reversi::F7, reversi::G8}},
        { FEATURE_SIZE[DiagonalLine1], {reversi::G1, reversi::F2, reversi::E3, reversi::D4, reversi::C5, reversi::B6, reversi::A7}},

        // diagonal line 2
        { FEATURE_SIZE[DiagonalLine2], {reversi::C1, reversi::D2, reversi::E3, reversi::F4, reversi::G5, reversi::H6}},
        { FEATURE_SIZE[DiagonalLine2], {reversi::A3, reversi::B4, reversi::C5, reversi::D6, reversi::E7, reversi::F8}},
        { FEATURE_SIZE[DiagonalLine2], {reversi::F1, reversi::E2, reversi::D3, reversi::C4, reversi::B5, reversi::A6}},
        { FEATURE_SIZE[DiagonalLine2], {reversi::H3, reversi::G4, reversi::F5, reversi::E6, reversi::D7, reversi::C8}},

        // diagonal line 3
        { FEATURE_SIZE[DiagonalLine3], {reversi::D1, reversi::E2, reversi::F3, reversi::G4, reversi::H5}},
        { FEATURE_SIZE[DiagonalLine3], {reversi::A4, reversi::B5, reversi::C6, reversi::D7, reversi::E8}},
        { FEATURE_SIZE[DiagonalLine3], {reversi::E1, reversi::D2, reversi::C3, reversi::B4, reversi::A5}},
        { FEATURE_SIZE[DiagonalLine3], {reversi::H4, reversi::G5, reversi::F6, reversi::E7, reversi::D8}},

        // diagonal line 4
        { FEATURE_SIZE[DiagonalLine4], {reversi::D1, reversi::C2, reversi::B3, reversi::A4}},
        { FEATURE_SIZE[DiagonalLine4], {reversi::A5, reversi::B6, reversi::C7, reversi::D8}},
        { FEATURE_SIZE[DiagonalLine4], {reversi::E1, reversi::F2, reversi::G3, reversi::H4}},
        { FEATURE_SIZE[DiagonalLine4], {reversi::H5, reversi::G6, reversi::F7, reversi::E8}},

        // bias
        { FEATURE_SIZE[Bias], {}}
    };

    struct CoordinateToPattern
    {
        int length;
        Pattern patterns[FEATURE_NUM];

        constexpr CoordinateToPattern() :length(0), patterns() { ; }
    };

    constexpr ConstantArray<CoordinateToPattern, reversi::SQUARE_NUM + 1> COORD_TO_PATTERN(
        [](CoordinateToPattern* table, size_t length)
        {
            for (auto coord = reversi::BoardCoordinate::A1; coord <= reversi::BoardCoordinate::PASS; coord++)
            {
                auto count = 0;
                for (auto featureID = 0; featureID < FEATURE_NUM; featureID++)
                {
                    auto feature_info = FEATURE_INFO[featureID];
                    auto coords = feature_info.coordinates;
                    auto size = feature_info.size;
                    auto idx = arraymanipulation::index_of(coords, 0, size, coord);
                    if (idx == -1)
                        continue;
                    table[coord].patterns[count++] = Pattern(featureID, fastmath::pow3(size - idx - 1));
                }
                table[coord].length = count;
            }
        }
    );

    constexpr uint16_t calc_opponent_pattern(uint16_t feature, int size)
    {
        uint16_t pattern_inv = 0;
        for (auto i = 0; i < size; i++)
        {
            auto color = static_cast<reversi::DiscColor>((feature / fastmath::pow3(i)) % 3);
            if (color == reversi::DiscColor::EMPTY)
                pattern_inv += static_cast<uint16_t>(color) * fastmath::pow3(i);
            else
                pattern_inv += static_cast<uint16_t>(opponent_disc_color(color)) * fastmath::pow3(i);
        }
        return pattern_inv;
    }

    constexpr uint16_t mirror_pattern(uint16_t feature, int size)
    {
        uint16_t mirrored = 0;
        for (auto i = 0; i < size; i++)
            mirrored += ((feature / fastmath::pow3(size - (i + 1))) % 3) * fastmath::pow3(i);
        return mirrored;
    }

    constexpr uint16_t shuffle_pattern_with_table(uint16_t feature, const int* table, int size)
    {
        uint16_t shuffled = 0;
        for (auto i = 0; i < size; i++)
        {
            auto idx = table[i];
            auto tmp = (feature / fastmath::pow3(idx)) % 3;
            shuffled += tmp * fastmath::pow3(i);
        }
        return shuffled;
    }

    constexpr uint16_t to_symmetric_pattern(FeatureKind kind, uint16_t feature)
    {
        constexpr int TABLE_FOR_CORNER_3X3[9] = { 0, 2, 1, 4, 3, 5, 7, 6, 8 };
        constexpr int TABLE_FOR_CORNER_EDGE_X[10] = { 9, 8, 7, 6, 4, 5, 3, 2, 1, 0 };

        if (kind == FeatureKind::Corner3x3)
            return shuffle_pattern_with_table(feature, TABLE_FOR_CORNER_3X3, FEATURE_SIZE[kind]);
        if (kind == FeatureKind::CornerEdgeX)
            return shuffle_pattern_with_table(feature, TABLE_FOR_CORNER_EDGE_X, FEATURE_SIZE[kind]);
        return mirror_pattern(feature, FEATURE_SIZE[kind]);
    }

    /**
     * @class
     * @brief	Represents the feature of board.
     * @detail	The object of this class has feature values of board which is calculated from disc positions.
     *          The feature of board is same as Edax's.
     * 
     * @cite    https://github.com/abulmo/edax-reversi/blob/master/src/eval.h
    */
	class BoardFeature
	{
    public:
        static bool initialized;

        uint16_t patterns[FEATURE_NUM];

        inline reversi::DiscColor get_side_to_move() { return this->side_to_move; }
        inline int get_empty_square_count() { return this->empty_square_count; }

        static void static_initializer();

        DLL_EXPORT BoardFeature(reversi::Board& board);
        DLL_EXPORT BoardFeature(BoardFeature& board_feature);

        DLL_EXPORT void init(reversi::Board& board);
        DLL_EXPORT void update(reversi::Move& move);
        DLL_EXPORT void pass() { this->side_to_move = opponent_disc_color(this->side_to_move); }
        DLL_EXPORT void copy_to(BoardFeature& dest) { memmove(&dest, this, sizeof(BoardFeature)); }

    private:
        reversi::DiscColor side_to_move;
        int empty_square_count;

        static void update_after_black_move(uint16_t* patterns, reversi::Move& move);
        static void update_after_white_move(uint16_t* patterns, reversi::Move& move);
	};
}
