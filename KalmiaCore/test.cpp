#pragma once
#include "test.h"
#include <iostream>

using namespace reversi;
using namespace std;

void KalmiaCore_Test()
{
	// write some test code.
	Mobility mobility(3ULL);
	BoardCoordinate coord;
	while(mobility.move_to_next_coord(coord))
	{
		cout << (int)coord << endl;
	}
	getchar();
}