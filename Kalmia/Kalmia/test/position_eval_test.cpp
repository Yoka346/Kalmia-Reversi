#ifdef _DEBUG

#include "position_eval_test.h"

#include "test_common.h"

#include <iostream>
#include <sstream>
#include <filesystem>
#include <fstream>
#include <vector>
#include <cassert>
#include <cmath>
#include <algorithm>

#include "../reversi/types.h"
#include "../evaluate/feature.h"
#include "../evaluate/position_eval.h"

using namespace std;
using namespace reversi;
using namespace evaluation;

namespace test
{
	void predict_test(ValueFunction<ValueRepresentation::WIN_RATE>&);

	void predict_test()
	{
		stringstream weight_path;
		weight_path << TEST_DATA_DIR << VALUE_FUNC_WEIGHT_FILE_NAME;
		ValueFunction<ValueRepresentation::WIN_RATE> value_func(weight_path.str());
		predict_test(value_func);
		cout << "done.";
	}

	void predict_test(ValueFunction<ValueRepresentation::WIN_RATE>& value_func)
	{
		constexpr float EPSILON = 1.0e-4f;

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
			stringstream ss(line);
			vector<string> items;
			string item;
			while (getline(ss, item, ','))
				items.push_back(item);

			uint64_t p = strtoull(items[0].c_str(), nullptr, 10);
			uint64_t o = strtoull(items[1].c_str(), nullptr, 10);
			BoardCoordinate coord = static_cast<BoardCoordinate>(stoi(items[2].c_str(), nullptr, 10));
			float v0 = strtof(items[3].c_str(), nullptr);
			float v1 = strtof(items[4].c_str(), nullptr);

			Position pos(Bitboard(p, o), DiscColor::BLACK);
			PositionFeature pf(pos);
			assert(fabsf(value_func.predict(pf) - v0) < EPSILON);

			Move move(coord, 0ULL);
			pos.calc_flipped_discs(move);
			pf.update(move);
			assert(fabsf(value_func.predict(pf) - v1) < EPSILON);
		}
	}

	void save_to_file_test()
	{
		ostringstream oss;
		oss << TEST_DATA_DIR << VALUE_FUNC_WEIGHT_FILE_NAME;
		auto weight_path = oss.str();
		ValueFunction<ValueRepresentation::WIN_RATE> value_func(weight_path);

		cout << "start value function params save to file test.\n";

		oss << ".saved";
		auto saved_path = oss.str();
		cout << "save to \"" << saved_path << "\".\n";
		value_func.save_to_file(saved_path);
		cout << "done.\n\n";

		cout << "check saved file validation.\n";

		ValueFunction<ValueRepresentation::WIN_RATE> saved_value_func(saved_path);
		predict_test(saved_value_func);

		cout << "done.\n";
	}
}

#endif