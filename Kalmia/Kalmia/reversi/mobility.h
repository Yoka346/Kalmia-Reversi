#pragma once

#include "../common.h"
#include "../utils/bitmanip.h"
#include "enum.h"

namespace reversi
{
	uint64_t calc_mobility(uint64_t& p, uint64_t& o, BoardCoordinate& coord);
}

