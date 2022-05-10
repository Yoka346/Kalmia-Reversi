#include "feature.h"

using namespace evaluation;
using namespace reversi;

bool BoardFeature::initialized = false;

void BoardFeature::static_initializer()
{
	
}

uint16_t BoardFeature::symmetric_transform_feature(FeatureInfo info, uint16_t feature)
{
	constexpr int TABLE_FOR_CORNER_3X3[9] = { 0, 2, 1, 4, 3, 5, 7, 6, 8 };
	constexpr int TABLE_FOR_CORNER_EDGE_X[10] = { 9, 8, 7, 6, 4, 5, 3, 2, 1, 0 };

	if (info.kind == FeatureKind::Corner3x3)
		return shuffle_feature_with_table(feature, TABLE_FOR_CORNER_3X3, info.size);
	if(info.kind == FeatureKind::CornerEdgeX)
		return shuffle_feature_with_table(feature, TABLE_FOR_CORNER_EDGE_X, info.size);
	return mirror_feature(feature, info.size);
}

void BoardFeature::init(reversi::Board& board)
{
	for (auto i = 0; i < FEATURE_NUM; i++)
	{
		auto feature_info = FEATURE_INFO[i];
		auto feature_value = &this->feature_values[i];
		for (auto j = 0; j < feature_info.size; j++)
			*feature_value = *feature_value * 3 + board.get_square_color(feature_info.coordinates[j]);
	}
	this->side_to_move = board.get_side_to_move();
	this->empty_square_count = board.get_empty_square_count();
}

void BoardFeature::update(Move& move)
{
	static void (* const UPDATE[])(uint16_t*, uint64_t) = { BoardFeature::update_after_black_move, BoardFeature::update_after_white_move };
	
	UPDATE[move.color](this->feature_values, move.flipped);
}

// private
uint16_t BoardFeature::calc_opponent_feature(uint16_t feature, int size)
{
	uint16_t feature_inv = 0;
	for (auto i = 0; i < size; i++)
	{
		auto color = static_cast<DiscColor>((feature / fastmath::pow3(i)) % 3);
		if (color == DiscColor::NONE)
			feature_inv += static_cast<uint16_t>(color) * fastmath::pow3(i);
		else
			feature_inv += static_cast<uint16_t>(opponent_disc_color(color)) * fastmath::pow3(i);
	}
	return feature_inv;
}

uint16_t BoardFeature::mirror_feature(uint16_t feature, int size)
{
	uint16_t mirrored = 0;
	for (auto i = 0; i < size; i++)
		mirrored += ((feature / fastmath::pow3(size - (i + 1))) % 3) * fastmath::pow3(i);
	return mirrored;
}

uint16_t BoardFeature::shuffle_feature_with_table(uint16_t feature, const int* table, int size)
{
	uint16_t shuffled = 0;
	for (auto i = 0; i < size; i ++)
	{
		auto idx = table[i];
		auto tmp = (feature / fastmath::pow3(idx)) % 3;
		shuffled += tmp * fastmath::pow3(i);
	}
	return shuffled;
}

void BoardFeature::update_after_black_move(uint16_t* feature_values, uint64_t flipped)
{

}

void BoardFeature::update_after_white_move(uint16_t* feature_values, uint64_t flipped)
{

}