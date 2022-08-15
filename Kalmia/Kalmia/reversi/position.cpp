#pragma once

#include "position.h"

using namespace reversi;

template<bool CHECK_LEGALITY>
bool Position::update(BoardCoordinate& coord)
{
	if constexpr (CHECK_LEGALITY)
	{
		uint64_t moves = this->_bitboard.calc_player_mobility();
		if (!is_legal(coord))
			return false;
	}

	uint64_t flipped = this->_bitboard.calc_flipped_discs(coord);
	Move move(coord, flipped);
	update(move);
}

int Position::get_next_moves(Array<Move, MAX_MOVE_NUM>& moves)
{
	uint64_t mobility = this->_bitboard.calc_player_mobility();
	auto move_count = 0;
	int coord;
	FOREACH_BIT(coord, mobility)
		moves[move_count++].coord = static_cast<BoardCoordinate>(coord);
	return move_count;
}