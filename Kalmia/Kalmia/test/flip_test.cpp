#pragma once
#include "flip_test.h"

using namespace std;

void test::calc_flipped_discs_test()
{
	stringstream test_data_path;
	test_data_path << TEST_DATA_DIR << FLIP_TEST_DATA_NAME;
	ifstream ifs(test_data_path.str());
	if (!ifs)
	{
		auto path = test_data_path.str();
		path = filesystem::absolute(path).string();
		cerr << "Cannot open \"" << path << "\".";
		return;
	}

	string line;
	getline(ifs, line);

	cout << "start flipped discs calculation test.\n";

	int count = 0;
	while (getline(ifs, line))
	{
		cout << "test_case: " << ++count << endl;
		stringstream ss(line);
		vector<string> items;
		string item;
		while (getline(ss, item, ','))
			items.push_back(item);

		uint64_t p = strtoull(items[0].c_str(), nullptr, 10);
		uint64_t o = strtoull(items[1].c_str(), nullptr, 10);
		auto move = static_cast<reversi::BoardCoordinate>(strtol(items[2].c_str(), nullptr, 10));
		auto expected = strtoull(items[3].c_str(), nullptr, 10);
		uint64_t actual = reversi::calc_flipped_discs(p, o, move);
		assert(expected == actual);
	}
}