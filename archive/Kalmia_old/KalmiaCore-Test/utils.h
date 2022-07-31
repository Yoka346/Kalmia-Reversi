#pragma once
#include "reversi/board.h"
#include "random.h"

inline void create_random_board(reversi::Board& board, int empty_square_count, Random& rand) 
{
	board = reversi::Board();
	reversi::MoveCoordinateIterator mobility;
	reversi::Move move;

	auto pass_count = 0;
	while (board.get_empty_square_count() > empty_square_count && pass_count != 2) 
	{
		board.get_current_player_move_coords(mobility);

		if (mobility.count() == 0)
		{
			board.pass();
			pass_count++;
			continue;
		}

		board.get_move(mobility.get_coord_at(rand.next(mobility.count())), move);
		board.update(move);
	}
}

inline void create_random_boards(reversi::Board* boards, int length, int empty_square_count, Random& rand) 
{
	for (auto i = 0; i < length; i++)
		create_random_board(boards[i], empty_square_count, rand);
}
