#include "feature.h"

using namespace evaluation;
using namespace reversi;

bool BoardFeature::initialized = false;

void BoardFeature::static_initializer()
{
	
}

BoardFeature::BoardFeature(Board& board) { init(board); }
BoardFeature::BoardFeature(BoardFeature& board_feature) { board_feature.copy_to(*this); }

void BoardFeature::init(reversi::Board& board)
{
	for (int32_t i = 0; i < FEATURE_NUM; i++)
	{
		auto feature_info = FEATURE_INFO[i];
		uint16_t* pattern_ptr = &this->patterns[i];
		*pattern_ptr = 0;
		for (auto j = 0; j < feature_info.size; j++)
			*pattern_ptr = *pattern_ptr * 3 + board.get_square_color(feature_info.coordinates[j]);
	}
	this->side_to_move = board.get_side_to_move();
	this->empty_square_count = board.get_empty_square_count();
}

void BoardFeature::update(Move& move)
{
	static void (* const UPDATE[])(uint16_t*, Move&) = { BoardFeature::update_after_black_move, BoardFeature::update_after_white_move };
	
	UPDATE[move.color](this->patterns, move);
}

// private
void BoardFeature::update_after_black_move(uint16_t* patterns, Move& move)
{
	auto coord_to_f = COORD_TO_PATTERN[move.coord];
	for (int32_t i = 0; i < coord_to_f.length; i++)
	{
		auto value = coord_to_f.patterns[i];
		patterns[value.feature_id] -= DiscColor::EMPTY * value.n;
	}

	auto coord = 0;
	foreach_bit(coord, move.flipped) 
	{
		coord_to_f = COORD_TO_PATTERN[coord];
		for (int32_t i = 0; i < coord_to_f.length; i++) 
		{
			auto value = coord_to_f.patterns[i];
			patterns[value.feature_id] -= value.n;
		}
	}
}

void BoardFeature::update_after_white_move(uint16_t* patterns, Move& move)
{
	auto coord_to_f = COORD_TO_PATTERN[move.coord];
	for (int32_t i = 0; i < coord_to_f.length; i++)
	{
		auto value = coord_to_f.patterns[i];
		patterns[value.feature_id] -= value.n;
	}

	auto coord = 0;
	foreach_bit(coord, move.flipped)
	{
		coord_to_f = COORD_TO_PATTERN[coord];
		for (int32_t i = 0; i < coord_to_f.length; i++)
		{
			auto value = coord_to_f.patterns[i];
			patterns[value.feature_id] += value.n;
		}
	}
}