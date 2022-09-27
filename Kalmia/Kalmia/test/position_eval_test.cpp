#pragma once
#include "position_eval_test.h"

#include "test_common.h"
#include "../reversi/mobility.h"
#include <iostream>
#include <filesystem>
#include <fstream>
#include <vector>
#include <cassert>

using namespace std;

namespace test
{
	void predict_test()
	{
		stringstream test_data_path;
		test_data_path << TEST_DATA_DIR << PREDICT_TEST_DATA_NAME;
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

		cout << "start value function prediction test.\n";

		int count = 0;
		while (getline(ifs, line))
		{
			cout << "test_case: " << ++count << endl;
			// ToDo: C#版Kalmiaでテストデータを作る.
		}
	}
}