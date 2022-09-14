#pragma once
#include "feature.h"

#include "../utils/unroller.h"

using namespace std;

using namespace reversi;

namespace evaluation
{
	PositionFeature::PositionFeature(Position& pos) : _features(), features(_features.t_splitted.features), _side_to_move(pos.side_to_move()), update_callbacks()
	{
		init_features(pos);
		init_update_callbacks();
	}

	PositionFeature::PositionFeature(const PositionFeature& src) : _features(), features(_features.t_splitted.features), _side_to_move(src._side_to_move), update_callbacks()
	{
		LoopUnroller<FeatureTable::V16_LEN>()([&](const int32_t i) { this->_features.t_v16[i] = src._features.t_v16[i]; });
		init_update_callbacks();
	}

	void PositionFeature::init_features(Position& pos)
	{
		auto features = this->_features.t_splitted.features;
		for (int32_t i = 0; i < features.length(); i++)
		{
			auto& pat_loc = PATTERN_LOCATION[i];
			features[i] = 0;
			for (int32_t j = 0; j < pat_loc.size; j++)
				features[i] = features[i] * 3 + pos.square_owner_at(pat_loc.coordinates[j]);
		}
		this->_side_to_move = pos.side_to_move();
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
		this->_side_to_move = to_opponent_color(this->_side_to_move);
	}

	const PositionFeature& PositionFeature::operator=(const PositionFeature& right)
	{
		LoopUnroller<FeatureTable::V16_LEN>()([&](const int32_t i) { this->_features.t_v16[i] = right._features.t_v16[i]; });
		this->_side_to_move = right._side_to_move;
		this->empty_square_count = right.empty_square_count;
		return *this;
	}

	void PositionFeature::update_after_black_move(const Move& move)
	{

	}

	void PositionFeature::update_after_white_move(const Move& move)
	{

	}

}