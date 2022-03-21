#pragma once
#include "board.h"

using namespace reversi;

bool Board::initialized = false;
uint64_t Board::hash_rank[16][256];

void Board::static_initializer()
{
	if (Board::initialized)
		return;

	std::random_device seed;
	std::mt19937_64 rand(seed());
	for (auto i = 0; i < 16; i++)
		for (auto j = 0; j < 256; j++)
			hash_rank[i][j] = rand();
	Board::initialized = true;
}

INIT_CALLBACK(Board, Board::static_initializer);