#pragma once
#include "feature.h"

using namespace std;

using namespace reversi;

namespace evaluation
{
	PositionFeature::PositionFeature(Position& pos) : _features(), features(_features), _side_to_move(pos.side_to_move()), update_callbacks()
	{
		init_features(pos);
		init_update_callbacks();
	}

	PositionFeature::PositionFeature(const PositionFeature& src) : _features(), features(_features), _side_to_move(src._side_to_move), update_callbacks()
	{
		for (int32_t i = 0; i < this->_features.length(); i++)
			this->_features[i] = src._features[i];
		init_update_callbacks();
	}

	void PositionFeature::init_features(Position& pos)
	{
		for (int32_t i = 0; i < this->_features.length(); i++)
		{
			this->_features[i] = 0;
			for (auto& coord : PATTERN_LOCATION[i].coordinates)
				this->_features[i] = this->_features[i] * 3 + pos.square_owner_at(coord);
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
		for (int32_t i = 0; i < this->_features.length(); i++)
			this->_features[i] = right._features[i];
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