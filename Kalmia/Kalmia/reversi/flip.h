#pragma once

#include "../common.h"
#include "../utils/bitmanip.h"
#include "constant.h"
#include "types.h"

namespace reversi
{
	uint64_t calc_flipped_discs(uint64_t& p, uint64_t& o, BoardCoordinate& coord);
}

