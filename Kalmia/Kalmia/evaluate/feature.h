#pragma once

#include <algorithm>
#include <functional>

#include "../utils/bitmanip.h"
#include "../reversi/constant.h"
#include "../reversi/types.h"
#include "../reversi/position.h"

namespace evaluation
{	
    enum PatternKind
    {
        CORNER3x3,
        CORNER_EDGE_X,
        EDGE_2X,
        CORNER2x5,
        LINE0,
        LINE1,
        LINE2,
        DIAG_LINE8,
        DIAG_LINE7,
        DIAG_LINE6,
        DIAG_LINE5,
        DIAG_LINE4
    };

	// Kalmiaが盤面評価に用いるパターンはEdaxと同じ12種類のパターン.
	constexpr int32_t PATTERN_KIND_NUM = 12;	

	// 盤面から抽出するパターンは全部で46個.
	constexpr int32_t ALL_PATTERN_NUM = 46;

	constexpr int32_t MAX_PATTERN_SIZE = 10;

    // 各パターンを構成するマスの数.
    constexpr utils::Array<int32_t, PATTERN_KIND_NUM> PATTERN_SIZE = { 9, 10, 10, 10, 8, 8, 8, 8, 7, 6, 5, 4 };

    // それぞれのパターンの出現数. 例えば, Corner3x3パターンであれば, 4隅なので4.
    constexpr utils::Array<int32_t, PATTERN_KIND_NUM> PATTERN_NUM = { 4, 4, 4, 4, 4, 4, 4, 2, 4, 4, 4, 4 };

    // 各パターンの特徴の数((e.g. Corner3x3パターンであれば, サイズが9, マスが取りうる状態は黒, 白, 空の3つなので, 3^9個の特徴).
    constexpr utils::Array<int32_t, PATTERN_KIND_NUM> PATTERN_FEATURE_NUM = {19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81};

    // 対称変換したら一致するパターンを除いた際の各パターンの特徴の数.
    constexpr utils::Array<int32_t, PATTERN_KIND_NUM> PACKED_PATTERN_FEATURE_NUM = { 10206, 29889, 29646, 29646, 3321, 3321, 3321, 3321, 1134, 378, 135, 45 };

    constexpr int32_t ALL_PATTERN_FEATURE_NUM = 
        ([]
            {
                int32_t sum = 0;
                for (size_t i = 0; i < PATTERN_SIZE.length(); i++)
                    sum += PATTERN_FEATURE_NUM[i];
                return sum;
            })();

    constexpr utils::Array<int32_t, PATTERN_KIND_NUM> PATTERN_FEATURE_OFFSET([](int32_t* data, size_t len)
        {
            int32_t offset = 0;
            for (int32_t kind = 0; kind < PATTERN_KIND_NUM; kind++)
            {
                data[kind] = offset;
                offset += PATTERN_FEATURE_NUM[kind];
            }
        });

    // 3^n(n <= MAX_PATTERN_SIZE)の結果を格納したテーブル. 特徴の計算でよく使うのでコンパイル時に事前計算しておく.
    constexpr utils::Array<uint16_t, MAX_PATTERN_SIZE + 1> POW_3([](uint16_t* data, size_t len)
        {
            for (size_t i = 0; i < len; i++)
            {
                uint16_t pow3 = 1;
                for (uint16_t n = 0; n < i; n++)
                    pow3 *= 3;
                data[i] = pow3;
            }
        });

    // パターンの位置を表す構造体.
	struct PatternLocation
	{
		// パターンを構成するマスの数.
		int32_t size;

		// パターンを構成するマスの座標.
        utils::Array<reversi::BoardCoordinate, MAX_PATTERN_SIZE + 1> coordinates;
	};

    constexpr PatternLocation PATTERN_LOCATION[ALL_PATTERN_NUM] =
    {
        { PATTERN_SIZE[CORNER3x3], {reversi::A1, reversi::B1, reversi::A2, reversi::B2, reversi::C1, reversi::A3, reversi::C2, reversi::B3, reversi::C3}},
        { PATTERN_SIZE[CORNER3x3], {reversi::H1, reversi::G1, reversi::H2, reversi::G2, reversi::F1, reversi::H3, reversi::F2, reversi::G3, reversi::F3}},
        { PATTERN_SIZE[CORNER3x3], {reversi::A8, reversi::A7, reversi::B8, reversi::B7, reversi::A6, reversi::C8, reversi::B6, reversi::C7, reversi::C6}},
        { PATTERN_SIZE[CORNER3x3], {reversi::H8, reversi::H7, reversi::G8, reversi::G7, reversi::H6, reversi::F8, reversi::G6, reversi::F7, reversi::F6}},

        { PATTERN_SIZE[CORNER_EDGE_X], {reversi::A5, reversi::A4, reversi::A3, reversi::A2, reversi::A1, reversi::B2, reversi::B1, reversi::C1, reversi::D1, reversi::E1}},
        { PATTERN_SIZE[CORNER_EDGE_X], {reversi::H5, reversi::H4, reversi::H3, reversi::H2, reversi::H1, reversi::G2, reversi::G1, reversi::F1, reversi::E1, reversi::D1}},
        { PATTERN_SIZE[CORNER_EDGE_X], {reversi::A4, reversi::A5, reversi::A6, reversi::A7, reversi::A8, reversi::B7, reversi::B8, reversi::C8, reversi::D8, reversi::E8}},
        { PATTERN_SIZE[CORNER_EDGE_X], {reversi::H4, reversi::H5, reversi::H6, reversi::H7, reversi::H8, reversi::G7, reversi::G8, reversi::F8, reversi::E8, reversi::D8}},

        { PATTERN_SIZE[EDGE_2X], {reversi::B2, reversi::A1, reversi::B1, reversi::C1, reversi::D1, reversi::E1, reversi::F1, reversi::G1, reversi::H1, reversi::G2}},
        { PATTERN_SIZE[EDGE_2X], {reversi::B7, reversi::A8, reversi::B8, reversi::C8, reversi::D8, reversi::E8, reversi::F8, reversi::G8, reversi::H8, reversi::G7}},
        { PATTERN_SIZE[EDGE_2X], {reversi::B2, reversi::A1, reversi::A2, reversi::A3, reversi::A4, reversi::A5, reversi::A6, reversi::A7, reversi::A8, reversi::B7}},
        { PATTERN_SIZE[EDGE_2X], {reversi::G2, reversi::H1, reversi::H2, reversi::H3, reversi::H4, reversi::H5, reversi::H6, reversi::H7, reversi::H8, reversi::G7}},

        { PATTERN_SIZE[CORNER2x5], {reversi::A1, reversi::C1, reversi::D1, reversi::C2, reversi::D2, reversi::E2, reversi::F2, reversi::E1, reversi::F1, reversi::H1}},
        { PATTERN_SIZE[CORNER2x5], {reversi::A8, reversi::C8, reversi::D8, reversi::C7, reversi::D7, reversi::E7, reversi::F7, reversi::E8, reversi::F8, reversi::H8}},
        { PATTERN_SIZE[CORNER2x5], {reversi::A1, reversi::A3, reversi::A4, reversi::B3, reversi::B4, reversi::B5, reversi::B6, reversi::A5, reversi::A6, reversi::A8}},
        { PATTERN_SIZE[CORNER2x5], {reversi::H1, reversi::H3, reversi::H4, reversi::G3, reversi::G4, reversi::G5, reversi::G6, reversi::H5, reversi::H6, reversi::H8}},

        { PATTERN_SIZE[LINE0], {reversi::A2, reversi::B2, reversi::C2, reversi::D2, reversi::E2, reversi::F2, reversi::G2, reversi::H2}},
        { PATTERN_SIZE[LINE0], {reversi::A7, reversi::B7, reversi::C7, reversi::D7, reversi::E7, reversi::F7, reversi::G7, reversi::H7}},
        { PATTERN_SIZE[LINE0], {reversi::B1, reversi::B2, reversi::B3, reversi::B4, reversi::B5, reversi::B6, reversi::B7, reversi::B8}},
        { PATTERN_SIZE[LINE0], {reversi::G1, reversi::G2, reversi::G3, reversi::G4, reversi::G5, reversi::G6, reversi::G7, reversi::G8}},

        { PATTERN_SIZE[LINE1], {reversi::A3, reversi::B3, reversi::C3, reversi::D3, reversi::E3, reversi::F3, reversi::G3, reversi::H3}},
        { PATTERN_SIZE[LINE1], {reversi::A6, reversi::B6, reversi::C6, reversi::D6, reversi::E6, reversi::F6, reversi::G6, reversi::H6}},
        { PATTERN_SIZE[LINE1], {reversi::C1, reversi::C2, reversi::C3, reversi::C4, reversi::C5, reversi::C6, reversi::C7, reversi::C8}},
        { PATTERN_SIZE[LINE1], {reversi::F1, reversi::F2, reversi::F3, reversi::F4, reversi::F5, reversi::F6, reversi::F7, reversi::F8}},

        { PATTERN_SIZE[LINE2], {reversi::A4, reversi::B4, reversi::C4, reversi::D4, reversi::E4, reversi::F4, reversi::G4, reversi::H4}},
        { PATTERN_SIZE[LINE2], {reversi::A5, reversi::B5, reversi::C5, reversi::D5, reversi::E5, reversi::F5, reversi::G5, reversi::H5}},
        { PATTERN_SIZE[LINE2], {reversi::D1, reversi::D2, reversi::D3, reversi::D4, reversi::D5, reversi::D6, reversi::D7, reversi::D8}},
        { PATTERN_SIZE[LINE2], {reversi::E1, reversi::E2, reversi::E3, reversi::E4, reversi::E5, reversi::E6, reversi::E7, reversi::E8}},

        { PATTERN_SIZE[DIAG_LINE8], {reversi::A1, reversi::B2, reversi::C3, reversi::D4, reversi::E5, reversi::F6, reversi::G7, reversi::H8}},
        { PATTERN_SIZE[DIAG_LINE8], {reversi::A8, reversi::B7, reversi::C6, reversi::D5, reversi::E4, reversi::F3, reversi::G2, reversi::H1}},

        { PATTERN_SIZE[DIAG_LINE7], {reversi::B1, reversi::C2, reversi::D3, reversi::E4, reversi::F5, reversi::G6, reversi::H7}},
        { PATTERN_SIZE[DIAG_LINE7], {reversi::H2, reversi::G3, reversi::F4, reversi::E5, reversi::D6, reversi::C7, reversi::B8}},
        { PATTERN_SIZE[DIAG_LINE7], {reversi::A2, reversi::B3, reversi::C4, reversi::D5, reversi::E6, reversi::F7, reversi::G8}},
        { PATTERN_SIZE[DIAG_LINE7], {reversi::G1, reversi::F2, reversi::E3, reversi::D4, reversi::C5, reversi::B6, reversi::A7}},

        { PATTERN_SIZE[DIAG_LINE6], {reversi::C1, reversi::D2, reversi::E3, reversi::F4, reversi::G5, reversi::H6}},
        { PATTERN_SIZE[DIAG_LINE6], {reversi::A3, reversi::B4, reversi::C5, reversi::D6, reversi::E7, reversi::F8}},
        { PATTERN_SIZE[DIAG_LINE6], {reversi::F1, reversi::E2, reversi::D3, reversi::C4, reversi::B5, reversi::A6}},
        { PATTERN_SIZE[DIAG_LINE6], {reversi::H3, reversi::G4, reversi::F5, reversi::E6, reversi::D7, reversi::C8}},

        { PATTERN_SIZE[DIAG_LINE5], {reversi::D1, reversi::E2, reversi::F3, reversi::G4, reversi::H5}},
        { PATTERN_SIZE[DIAG_LINE5], {reversi::A4, reversi::B5, reversi::C6, reversi::D7, reversi::E8}},
        { PATTERN_SIZE[DIAG_LINE5], {reversi::E1, reversi::D2, reversi::C3, reversi::B4, reversi::A5}},
        { PATTERN_SIZE[DIAG_LINE5], {reversi::H4, reversi::G5, reversi::F6, reversi::E7, reversi::D8}},

        { PATTERN_SIZE[DIAG_LINE4], {reversi::D1, reversi::C2, reversi::B3, reversi::A4}},
        { PATTERN_SIZE[DIAG_LINE4], {reversi::A5, reversi::B6, reversi::C7, reversi::D8}},
        { PATTERN_SIZE[DIAG_LINE4], {reversi::E1, reversi::F2, reversi::G3, reversi::H4}},
        { PATTERN_SIZE[DIAG_LINE4], {reversi::H5, reversi::G6, reversi::F7, reversi::E8}}
    };

    constexpr int32_t to_feature_idx(PatternKind kind, uint16_t feature) { return PATTERN_FEATURE_OFFSET[kind] + feature; }

    constexpr uint16_t mirror_feature(uint16_t feature, int32_t size)
    {
        uint16_t mirrored = 0;
        for (int32_t i = 0; i < size; i++)
            mirrored += ((feature / POW_3[size - (i + 1)]) % 3) * POW_3[i];
        return mirrored;
    }

    template<int TABLE_LEN>
    constexpr uint16_t shuffle_feature_with_table(uint16_t feature, const utils::Array<int32_t, TABLE_LEN>& table)
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
        constexpr utils::Array<int32_t, PATTERN_SIZE[PatternKind::CORNER3x3]> TABLE_FOR_CORNER_3X3 = { 0, 2, 1, 4, 3, 5, 7, 6, 8 };
        constexpr utils::Array<int32_t, PATTERN_SIZE[PatternKind::CORNER_EDGE_X]> TABLE_FOR_CORNER_EDGE_X = { 9, 8, 7, 6, 4, 5, 3, 2, 1, 0 };

        if (kind == PatternKind::CORNER3x3)
            return shuffle_feature_with_table(feature, TABLE_FOR_CORNER_3X3);

        if (kind == PatternKind::CORNER_EDGE_X)
            return shuffle_feature_with_table(feature, TABLE_FOR_CORNER_EDGE_X);

        return mirror_feature(feature, PATTERN_SIZE[kind]);
    }

    constexpr uint16_t to_opponent_feature(PatternKind kind, uint16_t feature)
    {
        uint16_t opp_feature = 0;
        for (int32_t i = 0; i < PATTERN_SIZE[kind]; i++)
        {
            auto color = static_cast<reversi::DiscColor>((feature / POW_3[i]) % 3);
            if (color == reversi::DiscColor::EMPTY)
                opp_feature += static_cast<uint16_t>(color) * POW_3[i];
            else
                opp_feature += static_cast<uint16_t>(reversi::to_opponent_color(color)) * POW_3[i];
        }
        return opp_feature;
    }

    // 対象変換したパターンの特徴を格納しているテーブル.
    const utils::Array<uint16_t, ALL_PATTERN_FEATURE_NUM> TO_SYMMETRIC_FEATURE([](uint16_t* data, size_t len)
        {
            for (int32_t kind = 0; kind < PATTERN_KIND_NUM; kind++)
                for (uint16_t feature = 0; feature < PATTERN_FEATURE_NUM[kind]; feature++)
                {
                    auto k = static_cast<PatternKind>(kind);
                    data[to_feature_idx(k, feature)] = to_symmetric_feature(k, feature);
                }
        });

    // ディスクの色を反転させたパターンの特徴を格納しているテーブル.
    const utils::Array<uint16_t, ALL_PATTERN_FEATURE_NUM> TO_OPPONENT_FEATURE(
        [](uint16_t* data, size_t len)
        {
            for (int32_t kind = 0; kind < PATTERN_KIND_NUM; kind++)
                for (uint16_t feature = 0; feature < PATTERN_FEATURE_NUM[kind]; feature++)
                {
                    auto k = static_cast<PatternKind>(kind);
                    data[to_feature_idx(k, feature)] = to_opponent_feature(k, feature);
                }
        });

    // 盤面に出現する12種類(合計46個)のパターンの特徴を管理するテーブル.
    union FeatureTable
    {
        static constexpr int32_t PADDING = 2;
        static constexpr int32_t LEN = ALL_PATTERN_NUM + PADDING;
        static constexpr int32_t SIZE = sizeof(uint16_t) * LEN;
        static constexpr int32_t ULL_LEN = SIZE / sizeof(uint64_t);

        Array<uint16_t, LEN> t;  // ベクトル化のために, 配列のサイズ(バイト単位)が16Bまたは32Bで割り切れるようにパディングする.
        struct { Array<uint16_t, ALL_PATTERN_NUM> features; Array<uint16_t, PADDING> padding; } t_splitted;
        Array<uint16_t, ULL_LEN> t_ull;

#ifdef USE_SSE2
        static constexpr int32_t V8_LEN = SIZE / sizeof(__m128i);
        Array<__m128i, V8_LEN> t_v8;
#endif

#ifdef USE_AVX2
        static constexpr int32_t V16_LEN = SIZE / sizeof(__m256i);
        Array<__m256i, V16_LEN> t_v16;
#endif
        
        constexpr FeatureTable() : t() { ; }

        constexpr FeatureTable(uint16_t* t) : t(t, LEN) { ; }

        const FeatureTable& operator=(const FeatureTable& right)
        {
#ifdef USE_AVX2
            utils::LoopUnroller<V16_LEN>()(
                [&](const int32_t i) { this->t_v16.as_raw_array()[i] = right.t_v16.as_raw_array()[i]; });
#elif defined(USE_SSE2)
            utils::LoopUnroller<V8_LEN>()(
                [&](const int32_t i) { this->t_v8.as_raw_array()[i] = right.t_v8.as_raw_array()[i]; });
#else
            utils::LoopUnroller<ULL_LEN>()(
                [&](const int32_t i) { this->t_ull.as_raw_array()[i] = right.t_ull.as_raw_array()[i]; });
#endif
            return *this;
        }
    };

    // 特徴を更新する際の差分を格納しているテーブル.
    constexpr utils::Array<FeatureTable, reversi::SQUARE_NUM + 1> FEATURE_TABLE_DIFF(
        [](FeatureTable* data, size_t len)
        {
            for (auto coord = reversi::BoardCoordinate::A1; coord <= reversi::BoardCoordinate::PASS; coord++)
            {
                auto& features = data[coord].t;
                for (int32_t i = 0; i < ALL_PATTERN_NUM; i++)
                {
                    auto& pattern_loc = PATTERN_LOCATION[i];
                    auto coordinates = pattern_loc.coordinates.as_raw_array();
                    int32_t idx;
                    for (idx = 0; idx < pattern_loc.size; idx++)
                        if (coordinates[idx] == coord)
                            break;
                    if (idx != pattern_loc.size)
                        features.as_raw_array()[i] = POW_3.as_raw_array()[pattern_loc.size - idx - 1];
                    else
                        features.as_raw_array()[i] = 0;
                }
            }
        });

	/**
	* @class
	* @brief 盤面の特徴を表現するクラス.
	* @detail 盤面の評価はこの特徴に基づいて行われる.
	**/
    class PositionFeature
    {
    private:
        FeatureTable _features;
        reversi::Player _side_to_move;
        int32_t _empty_count;
        std::function<void(const reversi::Move&)> update_callbacks[2];    
        void init_update_callbacks();
        void update_after_first_player_move(const reversi::Move& move);
        void update_after_second_player_move(const reversi::Move& move);

    public:
        const utils::ReadonlyArray<uint16_t, ALL_PATTERN_NUM> features;

        PositionFeature(reversi::Position& pos);
        PositionFeature(const PositionFeature& src);
        reversi::Player side_to_move() const { return this->_side_to_move; }
        int32_t empty_count() const { return this->_empty_count; }
        void init_features(reversi::Position& pos);
        void update(const reversi::Move& move); 
        void pass() { this->_side_to_move = reversi::to_opponent_player(this->_side_to_move); }
        const PositionFeature& operator=(const PositionFeature& right);
        bool operator==(const PositionFeature& right) { return this->_side_to_move == right._side_to_move && std::equal(this->features.begin(), this->features.end(), right.features.begin()); }
    };
}
