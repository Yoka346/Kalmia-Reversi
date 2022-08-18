#pragma once
#include "types.h"

namespace reversi
{
	struct Move
	{
		BoardCoordinate coord;
		uint64_t flipped;

		Move(BoardCoordinate coord, uint64_t flipped) : coord(coord), flipped(flipped) { ; }
	};
}
