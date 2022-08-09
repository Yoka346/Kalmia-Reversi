#pragma once

#ifdef _DEBUG

#include "test_common.h"

#include "../common.h"
#include "../reversi/flip.h"

#include <fstream>
#include <vector>

#define FLIP_TEST_DATA_NAME "flip_test_data.csv"

namespace test
{
	void calc_flipped_discs_test();
}

#endif