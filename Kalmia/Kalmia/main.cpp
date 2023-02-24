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

#include "test/position_eval_test.h"

using namespace std;

using namespace book;
using namespace reversi;
using namespace protocol;
using namespace engine;

// 開発時のテストコードなどをここに書く.
void dev_test()
{
	
}

#endif


