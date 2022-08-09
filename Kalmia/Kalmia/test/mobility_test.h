#pragma once

#ifdef _DEBUG

#include "test_common.h"

#include "../common.h"
#include "../reversi/mobility.h"

#include <fstream>
#include <vector>

#define MOBILITY_TEST_DATA_NAME "mobility_test_data.csv"

namespace test
{
	void calc_mobility_test();
}

#endif