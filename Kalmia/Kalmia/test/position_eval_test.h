#pragma once

#ifdef _DEBUG

#define VALUE_FUNC_WEIGHT_FILE_NAME "value_func_weight_for_test.bin";
#define PREDICT_TEST_DATA_NAME "value_func_predict_test_data.csv"

namespace test
{
	void predict_test();
	void save_to_file_test();
}

#endif