#ifdef _DEBUG

#include "mobility_test.h"
#include "test_common.h"
#include "../reversi/mobility.h"
#include <iostream>
#include <filesystem>
#include <fstream>
#include <vector>
#include <cassert>

using namespace std;

void test::calc_mobility_test()
{
	stringstream test_data_path;
	test_data_path << TEST_DATA_DIR << MOBILITY_TEST_DATA_NAME; 
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

	cout << "start mobility calculation test.\n";

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
		auto expected = strtoull(items[2].c_str(), nullptr, 10);
		uint64_t actual = reversi::calc_mobility(p, o);
		assert(expected == actual);
	}
}

#endif