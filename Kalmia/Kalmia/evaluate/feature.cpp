#pragma once
#include "feature.h"

#include "../utils/unroller.h"

using namespace std;

using namespace reversi;

namespace evaluation
{
	PositionFeature::PositionFeature(Position& pos) : _features(), features(_features.t_splitted.features), _side_to_move(Player::FIRST), update_callbacks()
	{
		init_features(pos);
		init_update_callbacks();
	}

	PositionFeature::PositionFeature(const PositionFeature& src) : _features(), features(_features.t_splitted.features), _side_to_move(src._side_to_move), update_callbacks()
	{
		this->_features = src._features;
		init_update_callbacks();
	}

	void PositionFeature::init_features(Position& pos)
	{
		auto& features = this->_features.t_splitted.features;
		for (int32_t i = 0; i < features.length(); i++)
		{
			auto& pat_loc = PATTERN_LOCATION[i];
			features[i] = 0;
			for (int32_t j = 0; j < pat_loc.size; j++)
				features[i] = features[i] * 3 + pos.square_owner_at(pat_loc.coordinates[j]);
		}
		this->_side_to_move = Player::FIRST;
		this->empty_square_count = pos.empty_square_count();
	}

	void PositionFeature::init_update_callbacks()
	{
		using namespace placeholders;
		this->update_callbacks[DiscColor::BLACK] = bind(&PositionFeature::update_after_black_move, this, _1);
		this->update_callbacks[DiscColor::WHITE] = bind(&PositionFeature::update_after_black_move, this, _1);
	}

	void PositionFeature::update(const Move& move)
	{
		this->update_callbacks[this->_side_to_move](move);
		this->empty_square_count--;
		this->_side_to_move = to_opponent_player(this->_side_to_move);
	}

	const PositionFeature& PositionFeature::operator=(const PositionFeature& right)
	{
		this->_features = right._features;
		this->_side_to_move = right._side_to_move;
		this->empty_square_count = right.empty_square_count;
		return *this;
	}

#ifdef USE_AVX2

	/**
	* @fn
	* @brief 与えられた黒の着手から, 特徴を差分更新する.
	* @detail AVX2を用いて, 48個の特徴(うち2つはパディング)を16要素まとめて更新する.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_black_move(const Move& move)
	{
		auto& features = this->_features.t_v16;

		// まずはディスクを置いた場所について更新.
		LoopUnroller<FeatureTable::V16_LEN>()(
			[&](const int32_t i) { features[i] = _mm256_sub_epi16(features[i], _mm256_slli_epi16(FEATURE_TABLE_DIFF[move.coord].t_v16[i], 1)); });

		// 次に裏返ったディスクについて更新.
		int32_t coord;
		auto flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::V16_LEN>()(
				[&](const int32_t i) { features[i] = _mm256_sub_epi16(features[i], FEATURE_TABLE_DIFF[coord].t_v16[i]); });
	}

	/**
	* @fn
	* @brief 与えられた白の着手から, 特徴を差分更新する.
	* @detail AVX2を用いて, 48個の特徴(うち2つはパディング)を16要素まとめて更新する.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_white_move(const Move& move)
	{
		auto& features = this->_features.t_v16;

		// まずはディスクを置いた場所について更新.
		LoopUnroller<FeatureTable::V16_LEN>()(
			[&](const int32_t i) { features[i] = _mm256_sub_epi16(features[i], FEATURE_TABLE_DIFF[move.coord].t_v16[i]); });

		// 次に裏返ったディスクについて更新.
		int32_t coord;
		auto flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::V16_LEN>()(
				[&](const int32_t i) { features[i] = _mm256_add_epi16(features[i], FEATURE_TABLE_DIFF[move.coord].t_v16[i]); });
	}

#elif defined(USE_SSE2)

	/**
	* @fn
	* @brief 与えられた黒の着手から, 特徴を差分更新する.
	* @detail SSE2を用いて, 48個の特徴(うち2つはパディング)を8要素まとめて更新する.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_black_move(const Move& move)
	{
		auto& features = this->_features.t_v8;

		// まずはディスクを置いた場所について更新.
		LoopUnroller<FeatureTable::V8_LEN>()(
			[&](const int32_t i) { features[i] = _mm_sub_epi16(features[i], _mm_slli_epi16(FEATURE_TABLE_DIFF[move.coord].t_v8[i], 1)); });

		// 次に裏返ったディスクについて更新.
		int32_t coord;
		auto flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::V8_LEN>()(
				[&](const int32_t i) { features[i] = _mm_sub_epi16(features[i], FEATURE_TABLE_DIFF[coord].t_v8[i]); });
	}

	/**
	* @fn
	* @brief 与えられた白の着手から, 特徴を差分更新する.
	* @detail SSE2を用いて, 48個の特徴(うち2つはパディング)を8要素まとめて更新する.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_white_move(const Move& move)
	{
		auto& features = this->_features.t_v8;

		// まずはディスクを置いた場所について更新.
		LoopUnroller<FeatureTable::V8_LEN>()(
			[&](const int32_t i) { features[i] = _mm_sub_epi16(features[i], FEATURE_TABLE_DIFF[move.coord].t_v8[i]); });

		// 次に裏返ったディスクについて更新.
		int32_t coord;
		auto flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::V8_LEN>()(
				[&](const int32_t i) { features[i] = _mm_add_epi16(features[i], FEATURE_TABLE_DIFF[move.coord].t_v8[i]); });
	}

#else

	/**
	* @fn
	* @brief 与えられた黒の着手から, 特徴を差分更新する.
	* @detail SIMD命令を用いる実装よりかなり遅くなるので, もしかしたらEdaxのeval_update関数のように条件分岐で選択的に特徴を更新する方が高速かもしれない.
	* ただし, コンパイラの最適化による.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_black_move(const Move& move)
	{
		auto& features = this->_features.t;

		// まずはディスクを置いた場所について更新.
		LoopUnroller<FeatureTable::LEN>()(
			[&](const int32_t i) { features[i] -= FEATURE_TABLE_DIFF[move.coord].t[i] << 1; });

		// 次に裏返ったディスクについて更新.
		int32_t coord;
		auto flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::LEN>()(
				[&](const int32_t i) { features[i] -= FEATURE_TABLE_DIFF[coord].t[i]; });
	}

	/**
	* @fn
	* @brief 与えられた白の着手から, 特徴を差分更新する.
	* @detail SIMD命令を用いる実装よりかなり遅くなるので, もしかしたらEdaxのeval_update関数のように条件分岐で選択的に特徴を更新する方が高速かもしれない.
	* ただし, コンパイラの最適化による.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_white_move(const Move& move)
	{
		auto& features = this->_features.t;

		// まずはディスクを置いた場所について更新.
		LoopUnroller<FeatureTable::LEN>()(
			[&](const int32_t i) { features[i] -= FEATURE_TABLE_DIFF[move.coord].t[i]; });

		// 次に裏返ったディスクについて更新.
		int32_t coord;
		auto flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::LEN>()(
				[&](const int32_t i) { features[i] += FEATURE_TABLE_DIFF[coord].t[i]; });
	}

#endif
}