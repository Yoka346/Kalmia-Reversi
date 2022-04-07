#pragma once
#include "../pch.h"
#include "../reversi/board.h"


namespace evaluation
{
    static constexpr int MAX_FEATURE_SIZE = 10;
    static constexpr int FEATURE_KIND_NUM = 47;
    static constexpr int PACKED_FEATURE_KIND_NUM = 13;

	struct FeatureInfo
	{
		int size;
		reversi::BoardCoordinate coordinates[MAX_FEATURE_SIZE];
	};

	struct Feature
	{
		int index;
		uint16_t value;
	};

    constexpr static FeatureInfo FEATURE_INFO[FEATURE_KIND_NUM] =
    {
        // corner3x3 
        { 9, {reversi::A1, reversi::B1, reversi::A2, reversi::B2, reversi::C1, reversi::A3, reversi::C2, reversi::B3, reversi::C3}},
        { 9, {reversi::H1, reversi::G1, reversi::H2, reversi::G2, reversi::F1, reversi::H3, reversi::F2, reversi::G3, reversi::F3}},
        { 9, {reversi::A8, reversi::A7, reversi::B8, reversi::B7, reversi::A6, reversi::C8, reversi::B6, reversi::C7, reversi::C6}},
        { 9, {reversi::H8, reversi::H7, reversi::G8, reversi::G7, reversi::H6, reversi::F8, reversi::G6, reversi::F7, reversi::F6}},

        // corner edge x 
        {10, {reversi::A5, reversi::A4, reversi::A3, reversi::A2, reversi::A1, reversi::B2, reversi::B1, reversi::C1, reversi::D1, reversi::E1}},
        {10, {reversi::H5, reversi::H4, reversi::H3, reversi::H2, reversi::H1, reversi::G2, reversi::G1, reversi::F1, reversi::E1, reversi::D1}},
        {10, {reversi::A4, reversi::A5, reversi::A6, reversi::A7, reversi::A8, reversi::B7, reversi::B8, reversi::C8, reversi::D8, reversi::E8}},
        {10, {reversi::H4, reversi::H5, reversi::H6, reversi::H7, reversi::H8, reversi::G7, reversi::G8, reversi::F8, reversi::E8, reversi::D8}},

        // edge 2x 
        {10, {reversi::B2, reversi::A1, reversi::B1, reversi::C1, reversi::D1, reversi::E1, reversi::F1, reversi::G1, reversi::H1, reversi::G2}},
        {10, {reversi::B7, reversi::A8, reversi::B8, reversi::C8, reversi::D8, reversi::E8, reversi::F8, reversi::G8, reversi::H8, reversi::G7}},
        {10, {reversi::B2, reversi::A1, reversi::A2, reversi::A3, reversi::A4, reversi::A5, reversi::A6, reversi::A7, reversi::A8, reversi::B7}},
        {10, {reversi::G2, reversi::H1, reversi::H2, reversi::H3, reversi::H4, reversi::H5, reversi::H6, reversi::H7, reversi::H8, reversi::G7}},

        // edge4x2 2x 
        {10, {reversi::A1, reversi::C1, reversi::D1, reversi::C2, reversi::D2, reversi::E2, reversi::F2, reversi::E1, reversi::F1, reversi::H1}},
        {10, {reversi::A8, reversi::C8, reversi::D8, reversi::C7, reversi::D7, reversi::E7, reversi::F7, reversi::E8, reversi::F8, reversi::H8}},
        {10, {reversi::A1, reversi::A3, reversi::A4, reversi::B3, reversi::B4, reversi::B5, reversi::B6, reversi::A5, reversi::A6, reversi::A8}},
        {10, {reversi::H1, reversi::H3, reversi::H4, reversi::G3, reversi::G4, reversi::G5, reversi::G6, reversi::H5, reversi::H6, reversi::H8}},

        // horizontal and vertical line (row = 2 or column = 2)
        { 8, {reversi::A2, reversi::B2, reversi::C2, reversi::D2, reversi::E2, reversi::F2, reversi::G2, reversi::H2}},
        { 8, {reversi::A7, reversi::B7, reversi::C7, reversi::D7, reversi::E7, reversi::F7, reversi::G7, reversi::H7}},
        { 8, {reversi::B1, reversi::B2, reversi::B3, reversi::B4, reversi::B5, reversi::B6, reversi::B7, reversi::B8}},
        { 8, {reversi::G1, reversi::G2, reversi::G3, reversi::G4, reversi::G5, reversi::G6, reversi::G7, reversi::G8}},

        // horizontal and vertical line (row = 3 or column = 3)
        { 8, {reversi::A3, reversi::B3, reversi::C3, reversi::D3, reversi::E3, reversi::F3, reversi::G3, reversi::H3}},
        { 8, {reversi::A6, reversi::B6, reversi::C6, reversi::D6, reversi::E6, reversi::F6, reversi::G6, reversi::H6}},
        { 8, {reversi::C1, reversi::C2, reversi::C3, reversi::C4, reversi::C5, reversi::C6, reversi::C7, reversi::C8}},
        { 8, {reversi::F1, reversi::F2, reversi::F3, reversi::F4, reversi::F5, reversi::F6, reversi::F7, reversi::F8}},

        // horizontal and vertical line (row = 4 or column = 4)
        { 8, {reversi::A4, reversi::B4, reversi::C4, reversi::D4, reversi::E4, reversi::F4, reversi::G4, reversi::H4}},
        { 8, {reversi::A5, reversi::B5, reversi::C5, reversi::D5, reversi::E5, reversi::F5, reversi::G5, reversi::H5}},
        { 8, {reversi::D1, reversi::D2, reversi::D3, reversi::D4, reversi::D5, reversi::D6, reversi::D7, reversi::D8}},
        { 8, {reversi::E1, reversi::E2, reversi::E3, reversi::E4, reversi::E5, reversi::E6, reversi::E7, reversi::E8}},

        // diagonal line 0
        { 8, {reversi::A1, reversi::B2, reversi::C3, reversi::D4, reversi::E5, reversi::F6, reversi::G7, reversi::H8}},
        { 8, {reversi::A8, reversi::B7, reversi::C6, reversi::D5, reversi::E4, reversi::F3, reversi::G2, reversi::H1}},

        // diagonal line 1
        { 7, {reversi::B1, reversi::C2, reversi::D3, reversi::E4, reversi::F5, reversi::G6, reversi::H7}},
        { 7, {reversi::H2, reversi::G3, reversi::F4, reversi::E5, reversi::D6, reversi::C7, reversi::B8}},
        { 7, {reversi::A2, reversi::B3, reversi::C4, reversi::D5, reversi::E6, reversi::F7, reversi::G8}},
        { 7, {reversi::G1, reversi::F2, reversi::E3, reversi::D4, reversi::C5, reversi::B6, reversi::A7}},

        // diagonal line 2
        { 6, {reversi::C1, reversi::D2, reversi::E3, reversi::F4, reversi::G5, reversi::H6}},
        { 6, {reversi::A3, reversi::B4, reversi::C5, reversi::D6, reversi::E7, reversi::F8}},
        { 6, {reversi::F1, reversi::E2, reversi::D3, reversi::C4, reversi::B5, reversi::A6}},
        { 6, {reversi::H3, reversi::G4, reversi::F5, reversi::E6, reversi::D7, reversi::C8}},

        // diagonal line 3
        { 5, {reversi::D1, reversi::E2, reversi::F3, reversi::G4, reversi::H5}},
        { 5, {reversi::A4, reversi::B5, reversi::C6, reversi::D7, reversi::E8}},
        { 5, {reversi::E1, reversi::D2, reversi::C3, reversi::B4, reversi::A5}},
        { 5, {reversi::H4, reversi::G5, reversi::F6, reversi::E7, reversi::D8}},

        // diagonal line 4
        { 4, {reversi::D1, reversi::C2, reversi::B3, reversi::A4}},
        { 4, {reversi::A5, reversi::B6, reversi::C7, reversi::D8}},
        { 4, {reversi::E1, reversi::F2, reversi::G3, reversi::H4}},
        { 4, {reversi::H5, reversi::G6, reversi::F7, reversi::E8}},

        // bias
        { 0, { }}
    };

	class BoardFeature
	{
    public:
        uint16_t feature_values[FEATURE_KIND_NUM];

        inline reversi::DiscColor get_side_to_move() { return this->side_to_move; }
        inline int get_empty_squares_count() { return this->empty_square_count; }

        BoardFeature(reversi::Board& board);
        BoardFeature(BoardFeature& board_feature);
        void init(reversi::Board& board);
        void update(reversi::Move& move);
        void pass();
        void copy_to(BoardFeature& dest);

    private:
        reversi::DiscColor side_to_move;
        int empty_square_count;

        void update_after_black_move(reversi::Move& move);
        void update_after_white_move(reversi::Move& move);
	};
}
