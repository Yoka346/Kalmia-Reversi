#include "app.h"

//#define DEVELOP

void dev_test();

int main(int argc, char* argv[])
{
#ifndef DEVELOP
	Application::instance().run(argv, argc);
#else
	dev_test();
#endif
}

#ifdef DEVELOP

#include <iostream>
#include <fstream>

#include "utils/array.h"
#include "reversi/types.h"
#include "reversi/position.h"
#include "book/edax_book.h"
#include "protocol/usi.h"
#include "protocol/nboard.h"
#include "protocol/protocol.h"
#include "game_format/ggf.h"
#include "engine/kalmia.h"
#include "learn/train_data.h"
#include "evaluate/position_eval.h"
#include "evaluate/feature.h"

using namespace std;

using namespace book;
using namespace reversi;
using namespace protocol;
using namespace engine;
using namespace game_format;
using namespace learn;
using namespace evaluation;

// 開発時のテストコードなどをここに書く.
void dev_test()
{
	/*convert_ggf_file_to_train_data_file("C:\\Users\\yu_ok\\Documents\\GGS\\Othello\\merged.ggf", "out.bin", 1500.0);*/

	auto path = "C:\\Users\\yu_ok\\source\\repos\\Yoka346\\Kalmia-Reversi\\Kalmia\\train_data\\train_data_1500.bin";
	merge_duplicated_position_in_train_data(path, "out.bin");
}

#endif


