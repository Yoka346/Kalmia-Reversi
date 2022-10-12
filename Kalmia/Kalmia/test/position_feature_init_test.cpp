#ifdef _DEBUG

#include "position_feature_init_test.h"
#include "test_common.h"

#include <iostream>
#include <filesystem>
#include <fstream>
#include <vector>
#include <algorithm>
#include <cassert>

#include "../reversi/types.h"
#include "../reversi/position.h"
#include "../evaluate/feature.h"

using namespace std;

using namespace reversi;
using namespace evaluation;

namespace test
{
	void init_position_feature_test()
	{
		stringstream test_data_path;
		test_data_path << TEST_DATA_DIR << TEST_DATA_NAME;
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

		cout << "start position feature initialize test.\n";

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
			uint16_t expected[ALL_PATTERN_NUM];
			for (int32_t i = 0; i < ALL_PATTERN_NUM; i++)
				expected[i] = static_cast<uint16_t>(stoi(items[2 + i].c_str(), nullptr, 10));

			Bitboard bb(p, o);
			Position pos(bb, DiscColor::BLACK);
			PositionFeature pf(pos);
			assert(std::equal(pf.features.begin(), pf.features.end(), expected));
		}
	}
}

#endif