#pragma once
#include "mobility_test.h"

using namespace std;

void test::calc_mobility_test()
{
	stringstream test_data_path;
	test_data_path << TEST_DATA_DIR << MOBILITY_TEST_DATA_NAME;
	ifstream ifs(test_data_path.str());
	string line;
	getline(ifs, line);

	while (getline(ifs, line))
	{
		stringstream ss(line);
		vector<string> items;
		string item;
		while (getline(ss, item, ','))
			items.push_back(item);

		uint64_t p = atoll(items[0].c_str());
		uint64_t o = atoll(items[1].c_str());
		uint64_t expected = atoll(items[2].c_str());
		uint64_t actual = reversi::calc_mobility(p, o);
		assert(expected == actual);
	}
}