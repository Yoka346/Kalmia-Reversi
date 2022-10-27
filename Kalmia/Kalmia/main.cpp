#include <iostream>
#include <memory>
#include <filesystem>

#include "config.h"
#include "engine/kalmia.h"
#include "protocol/gtp.h"
#include "protocol/usi.h"

using namespace std;
using namespace std::filesystem;

using namespace engine;
using namespace protocol;

void create_directories();
string create_log_file(const string& file_name);

int main()
{
	create_directories();

	static const string PARAM_PATH = "../test_data/value_func_weight_for_test.bin";
	unique_ptr<Kalmia> kalmia(new Kalmia(PARAM_PATH, create_log_file("kalmia")));

	USI usi;
	usi.mainloop(kalmia.get(), create_log_file("usi"));
}

void create_directories()
{
	if (!directory_entry(EVAL_DIR).exists())
		create_directory(EVAL_DIR);

	if(!directory_entry(LOG_DIR).exists())
		create_directory(LOG_DIR);
}

string create_log_file(const string& file_name)
{
	ostringstream oss;
	oss << LOG_DIR << file_name << ".log";
	auto path = oss.str();

	if (!exists(path))
		return path;

	int32_t i = 0;
	do
	{
		oss.clear();
		oss << LOG_DIR << file_name << "(" << ++i << ").log";
		path = oss.str();
	} while (exists(path));

	ofstream ofs(path);

	return path;
}