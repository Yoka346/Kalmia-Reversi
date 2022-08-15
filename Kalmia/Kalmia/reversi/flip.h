#pragma once

#include "../common.h"
#include "../utils/bitmanip.h"
#include "constant.h"
#include "types.h"

namespace reversi
{
	uint64_t calc_flipped_discs(const uint64_t& p, const uint64_t& o, const BoardCoordinate& coord);
}

