#include "app.h"

#define DEVELOP

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

#include "reversi/types.h"
#include "reversi/position.h"
#include "book/edax_book.h"

using namespace book;

// 開発時のテストコードなどをここに書く.
void dev_test()
{
	auto count = 0;
	EdaxBook book("book/book.bin");
	for (auto& pos : book)
		if (pos.score.value == 0 && pos.board.empty_count() >= 50)
			count++;
	std::cout << count;
}

#endif


