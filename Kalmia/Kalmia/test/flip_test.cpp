#pragma once
#include "flip_test.h"

using namespace std;

void test::calc_flipped_discs_test()
{
	stringstream test_data_path;
	test_data_path << TEST_DATA_DIR << FLIP_TEST_DATA_NAME;
	ifstream ifs(test_data_path.str());
	string line;
	getline(ifs, line);

	int count = 0;
	while (getline(ifs, line))
	{
		cout << "test_case: " << ++count << endl;
		stringstream ss(line);
		vector<string> items;
		string item;
		while (getline(ss, item, ','))
			items.push_back(item);

		uint64_t p = atoll(items[0].c_str());
		uint64_t o = atoll(items[1].c_str());
		auto move = static_cast<reversi::BoardCoordinate>(atoi(items[2].c_str()));
		uint64_t expected = atoll(items[3].c_str());
		uint64_t actual = reversi::calc_flipped_discs(p, o, move);
		assert(expected == actual);
	}
}