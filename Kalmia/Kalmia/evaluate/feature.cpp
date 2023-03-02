#include "feature.h"

#include "../utils/unroller.h"

using namespace std;

using namespace reversi;

namespace evaluation
{
	PositionFeature::PositionFeature(const Position& pos) : _features(), features(_features.t_splitted.features), _side_to_move(Player::FIRST), update_callbacks()
	{
		init_features(pos);
		init_update_callbacks();
	}

	PositionFeature::PositionFeature(const PositionFeature& src) : _features(), features(_features.t_splitted.features), _side_to_move(src._side_to_move), update_callbacks()
	{
		this->_features = src._features;
		this->_side_to_move = src._side_to_move;
		this->_empty_square_count = src._empty_square_count;
		init_update_callbacks();
	}

	void PositionFeature::init_features(const Position& pos)
	{
		auto& features = this->_features.t_splitted.features;
		for (int32_t i = 0; i < features.length(); i++)
		{
			auto& pat_loc = PATTERN_LOCATION[i];
			features[i] = 0;
			for (int32_t j = 0; j < pat_loc.size; j++)
				features[i] = features[i] * 3 + pos.square_owner_at(pat_loc.coordinates[j]);
		}
		this->_side_to_move = Player::FIRST;	// �^����ꂽ�Ֆʂ̌��݂̎�Ԃ���Ƃ���.
		this->_empty_square_count = pos.empty_square_count();
	}

	void PositionFeature::init_update_callbacks()
	{
		using namespace placeholders;
		this->update_callbacks[Player::FIRST] = [this](const Move& move) { update_after_first_player_move(move); };
		this->update_callbacks[Player::SECOND] = [this](const Move& move) { update_after_second_player_move(move); };
	}

	void PositionFeature::update(const Move& move)
	{
		this->update_callbacks[this->_side_to_move](move);
		this->_empty_square_count--;
		this->_side_to_move = to_opponent_player(this->_side_to_move);
	}

	const PositionFeature& PositionFeature::operator=(const PositionFeature& right)
	{
		this->_features = right._features;
		this->_side_to_move = right._side_to_move;
		this->_empty_square_count = right._empty_square_count;
		return *this;
	}

#ifdef USE_AVX2

	/**
	* @fn
	* @brief �^����ꂽ���̒��肩��, �����������X�V����.
	* @detail AVX2��p����, 48�̓���(����2�̓p�f�B���O)��16�v�f�܂Ƃ߂čX�V����.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_first_player_move(const Move& move)
	{
		auto& features = this->_features.t_v16;

		// �܂��̓f�B�X�N��u�����ꏊ�ɂ��čX�V.
		LoopUnroller<FeatureTable::V16_LEN>()(
			[&](const int32_t i) { features[i] = _mm256_sub_epi16(features[i], _mm256_slli_epi16(FEATURE_TABLE_DIFF[move.coord].t_v16[i], 1)); });

		// ���ɗ��Ԃ����f�B�X�N�ɂ��čX�V.
		int32_t coord;
		uint64_t flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::V16_LEN>()(
				[&](const int32_t i) { features[i] = _mm256_sub_epi16(features[i], FEATURE_TABLE_DIFF[coord].t_v16[i]); });
	}

	/**
	* @fn
	* @brief �^����ꂽ���̒��肩��, �����������X�V����.
	* @detail AVX2��p����, 48�̓���(����2�̓p�f�B���O)��16�v�f�܂Ƃ߂čX�V����.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_second_player_move(const Move& move)
	{
		auto& features = this->_features.t_v16;

		// �܂��̓f�B�X�N��u�����ꏊ�ɂ��čX�V.
		LoopUnroller<FeatureTable::V16_LEN>()(
			[&](const int32_t i) { features[i] = _mm256_sub_epi16(features[i], FEATURE_TABLE_DIFF[move.coord].t_v16[i]); });

		// ���ɗ��Ԃ����f�B�X�N�ɂ��čX�V.
		int32_t coord;
		uint64_t flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::V16_LEN>()(
				[&](const int32_t i) { features[i] = _mm256_add_epi16(features[i], FEATURE_TABLE_DIFF[coord].t_v16[i]); });
	}

#elif defined(USE_SSE2)

	/**
	* @fn
	* @brief �^����ꂽ���̒��肩��, �����������X�V����.
	* @detail SSE2��p����, 48�̓���(����2�̓p�f�B���O)��8�v�f�܂Ƃ߂čX�V����.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_first_player_move(const Move& move)
	{
		auto& features = this->_features.t_v8;

		// �܂��̓f�B�X�N��u�����ꏊ�ɂ��čX�V.
		LoopUnroller<FeatureTable::V8_LEN>()(
			[&](const int32_t i) { features[i] = _mm_sub_epi16(features[i], _mm_slli_epi16(FEATURE_TABLE_DIFF[move.coord].t_v8[i], 1)); });

		// ���ɗ��Ԃ����f�B�X�N�ɂ��čX�V.
		int32_t coord;
		uint64_t flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::V8_LEN>()(
				[&](const int32_t i) { features[i] = _mm_sub_epi16(features[i], FEATURE_TABLE_DIFF[coord].t_v8[i]); });
	}

	/**
	* @fn
	* @brief �^����ꂽ���̒��肩��, �����������X�V����.
	* @detail SSE2��p����, 48�̓���(����2�̓p�f�B���O)��8�v�f�܂Ƃ߂čX�V����.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_second_player_move(const Move& move)
	{
		auto& features = this->_features.t_v8;

		// �܂��̓f�B�X�N��u�����ꏊ�ɂ��čX�V.
		LoopUnroller<FeatureTable::V8_LEN>()(
			[&](const int32_t i) { features[i] = _mm_sub_epi16(features[i], FEATURE_TABLE_DIFF[move.coord].t_v8[i]); });

		// ���ɗ��Ԃ����f�B�X�N�ɂ��čX�V.
		int32_t coord;
		uint64_t flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::V8_LEN>()(
				[&](const int32_t i) { features[i] = _mm_add_epi16(features[i], FEATURE_TABLE_DIFF[coord].t_v8[i]); });
	}

#else

	/**
	* @fn
	* @brief �^����ꂽ���̒��肩��, �����������X�V����.
	* @detail SIMD���߂�p���������肩�Ȃ�x���Ȃ�̂�, ������������Edax��eval_update�֐��̂悤�ɏ�������őI��I�ɓ������X�V�������������������Ȃ�.
	* ������, �R���p�C���̍œK���ɂ��.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_first_player_move(const Move& move)
	{
		auto& features = this->_features.t;

		// �܂��̓f�B�X�N��u�����ꏊ�ɂ��čX�V.
		LoopUnroller<FeatureTable::LEN>()(
			[&](const int32_t i) { features[i] -= FEATURE_TABLE_DIFF[move.coord].t[i] << 1; });

		// ���ɗ��Ԃ����f�B�X�N�ɂ��čX�V.
		int32_t coord;
		uint64_t flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::LEN>()(
				[&](const int32_t i) { features[i] -= FEATURE_TABLE_DIFF[coord].t[i]; });
	}

	/**
	* @fn
	* @brief �^����ꂽ���̒��肩��, �����������X�V����.
	* @detail SIMD���߂�p���������肩�Ȃ�x���Ȃ�̂�, ������������Edax��eval_update�֐��̂悤�ɏ�������őI��I�ɓ������X�V�������������������Ȃ�.
	* ������, �R���p�C���̍œK���ɂ��.
	* @cite http://www.amy.hi-ho.ne.jp/okuhara/edaxopt.htm
	**/
	void PositionFeature::update_after_second_player_move(const Move& move)
	{
		auto& features = this->_features.t;

		// �܂��̓f�B�X�N��u�����ꏊ�ɂ��čX�V.
		LoopUnroller<FeatureTable::LEN>()(
			[&](const int32_t i) { features[i] -= FEATURE_TABLE_DIFF[move.coord].t[i]; });

		// ���ɗ��Ԃ����f�B�X�N�ɂ��čX�V.
		int32_t coord;
		uint64_t flipped = move.flipped;
		FOREACH_BIT(coord, flipped)
			LoopUnroller<FeatureTable::LEN>()(
				[&](const int32_t i) { features[i] += FEATURE_TABLE_DIFF[coord].t[i]; });
	}

#endif
}