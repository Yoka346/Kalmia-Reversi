#pragma once
#include "board_test.h"

using namespace reversi;

TEST(Board_Test, FlippAndMobilityTest)
{
	const int SAMPLE_NUM = 100;
	DiscColor board_test[SQUARE_NUM];
	bool mobility_test[SQUARE_NUM];
	bool flipped_test[SQUARE_NUM];
	Mobility mobility;
	Move move;
	std::random_device seed;
	auto rand = std::mt19937(seed());

	for (auto i = 0; i < SAMPLE_NUM; i++)
	{
		Board board;
		board_init(board_test);

		auto side_to_move = DiscColor::BLACK;
		auto pass_count = 0;
		auto loop_count = 0;
		bool success;
		while (pass_count != 2)
		{
			ASSERT_TRUE(++loop_count < 100) << "Infinite loop!!";

			success = false;
			assert_board_are_equal(board_test, board, success);
			if (!success)
				return;

			board.get_current_player_mobility(mobility);
			board_calc_mobility(board_test, side_to_move, mobility_test);

			success = false;
			assert_mobility_equal(board, mobility_test, mobility, success);
			if (!success)
				return;

			if (!mobility.count())
			{
				board.pass();
				side_to_move = opponent_disc_color(side_to_move);
				pass_count++;
				continue;
			}
			pass_count = 0;

			auto coord = sample_move(mobility, rand);
			board.get_move(coord, move);
			board_calc_flipped_discs(board_test, side_to_move, coord, flipped_test);
			board_update(board_test, side_to_move, coord, flipped_test);
			board.update(move);
			side_to_move = opponent_disc_color(side_to_move);
		}
	}
}

BoardCoordinate sample_move(Mobility& mobility, std::mt19937& rand)
{
	auto idx = rand() % mobility.count();
	auto count = 0;
	BoardCoordinate coord = BoardCoordinate::A1;
	foreach_mobility(coord, mobility)
		if (count++ == idx)
			return coord;
}

int count_true(bool* b)
{
	auto count = 0;
	for (auto i = 0; i < SQUARE_NUM; i++)
		if (b[i])
			count++;
	return count;
}

std::string mobility_vector_to_string(std::vector<BoardCoordinate> mobility)
{
	std::stringstream ss;
	for (auto coord : mobility)
		ss << coordinate_to_string(coord) << " ";
	return ss.str();
}

void assert_board_are_equal(DiscColor* expected, Board actual, bool& success)
{
	for(auto coord = BoardCoordinate::A1; coord <= BoardCoordinate::H8; coord++)
		if (expected[coord] != actual.get_square_color(coord))
			ASSERT_TRUE(false) << "expected board is\n" << board_to_string(expected) << "\nbut, actual board is\n" << actual.to_string() << "\n";
	success = true;
}

void assert_mobility_equal(Board board, bool* expected, Mobility& actual, bool& success)
{
	BoardCoordinate coord;

	std::vector<BoardCoordinate> actual_mobility;
	foreach_mobility(coord, actual)
		actual_mobility.push_back(coord);
	std::sort(actual_mobility.begin(), actual_mobility.end());

	std::vector<BoardCoordinate> expected_mobility;
	for (auto i = 0; i < SQUARE_NUM; i++)
		if (expected[i])
			expected_mobility.push_back((BoardCoordinate)i);
	std::sort(expected_mobility.begin(), expected_mobility.end());

	for(auto m : actual_mobility)
		if (!expected[m])
		{
			ASSERT_TRUE(false) << "expected mobility is" << "{" << mobility_vector_to_string(expected_mobility) << "}" << std::endl
				<< "but, actual mobility is" << "{" << mobility_vector_to_string(actual_mobility) << "}" << std::endl
				<< board.to_string() << std::endl;
		}
	success = true;
}

bool is_out_of_board(int coord)
{
	return coord < BoardCoordinate::A1 && coord >= BoardCoordinate::PASS;
}

void board_init(DiscColor* board)
{
	for (auto i = 0; i < SQUARE_NUM; i++)
		board[i] = DiscColor::EMPTY;

	board[BoardCoordinate::E4] = DiscColor::BLACK;
	board[BoardCoordinate::D5] = DiscColor::BLACK;
	board[BoardCoordinate::D4] = DiscColor::WHITE;
	board[BoardCoordinate::E5] = DiscColor::WHITE;
}

#define coord_2D_to_1D(x, y) x + y * BOARD_SIZE

void calc_flipped_discs(DiscColor* board, DiscColor color, BoardCoordinate coord, int dir_x, int dir_y, bool* flipped)
{
	if (!check_mobility(board, color, coord, dir_x, dir_y))
		return;

	auto opponent_color = opponent_disc_color(color);
	auto x = coord % BOARD_SIZE;
	auto y = coord / BOARD_SIZE;
	auto next_x = x + dir_x;
	auto next_y = y + dir_y;
	while (next_x >= 0 && next_x < BOARD_SIZE && next_y >= 0 && next_y < BOARD_SIZE && board[coord_2D_to_1D(next_x, next_y)] == opponent_color)
	{
		flipped[coord_2D_to_1D(next_x, next_y)] = true;
		next_x += dir_x;
		next_y += dir_y;
	}
}

void board_calc_flipped_discs(DiscColor* board, DiscColor color, BoardCoordinate coord, bool* flipped)
{
	for (auto i = 0; i < SQUARE_NUM; i++)
		flipped[i] = false;
	calc_flipped_discs(board, color, coord, 1, 0, flipped);
	calc_flipped_discs(board, color, coord, -1, 0, flipped);
	calc_flipped_discs(board, color, coord, 0, 1, flipped);
	calc_flipped_discs(board, color, coord, 0, -1, flipped);
	calc_flipped_discs(board, color, coord, 1, 1, flipped);
	calc_flipped_discs(board, color, coord, -1, -1, flipped);
	calc_flipped_discs(board, color, coord, -1, 1, flipped);
	calc_flipped_discs(board, color, coord, 1, -1, flipped);
}

#define out_of_range(x, y)  x < 0 || x >= BOARD_SIZE || y < 0 || y >= BOARD_SIZE

bool check_mobility(DiscColor* board, DiscColor color, BoardCoordinate coord, int dir_x, int dir_y)
{
	auto opponent_color = opponent_disc_color(color);
	auto x = coord % BOARD_SIZE;
	auto y = coord / BOARD_SIZE;

	auto next_x = x + dir_x;
	auto next_y = y + dir_y;
	if (out_of_range(next_x, next_y) || board[coord_2D_to_1D(next_x, next_y)] != opponent_color)
		return false;

	do
	{
		next_x += dir_x;
		next_y += dir_y;
	} while (!(out_of_range(next_x, next_y)) && board[coord_2D_to_1D(next_x, next_y)] == opponent_color);

	if (!(out_of_range(next_x, next_y)) && board[coord_2D_to_1D(next_x, next_y)] == color)
		return true;
	return false;
}

bool check_mobility(DiscColor* board, DiscColor color, BoardCoordinate coord)
{
	return check_mobility(board, color, coord, 1, 0)
		|| check_mobility(board, color, coord, -1, 0)
		|| check_mobility(board, color, coord, 0, 1)
		|| check_mobility(board, color, coord, 0, -1)
		|| check_mobility(board, color, coord, 1, 1)
		|| check_mobility(board, color, coord, -1, -1)
		|| check_mobility(board, color, coord, -1, 1)
		|| check_mobility(board, color, coord, 1, -1);
}

void board_calc_mobility(DiscColor* board, DiscColor color, bool* mobility)
{
	for (auto coord = BoardCoordinate::A1; coord <= BoardCoordinate::H8; coord++)
	{
		if (board[coord] != DiscColor::EMPTY)
		{
			mobility[coord] = false;
			continue;
		}
		mobility[coord] = check_mobility(board, color, coord);
	}
}

void board_update(DiscColor* board, DiscColor color, BoardCoordinate coord, bool* flipped)
{
	board[coord] = color;
	for (auto i = 0; i < SQUARE_NUM; i++)
		if (flipped[i])
			board[i] = color;
}

std::string board_to_string(DiscColor* board)
{
	std::stringstream ss;
	for (auto i = 0; i < SQUARE_NUM; i++)
	{
		if (board[i] == DiscColor::EMPTY)
			ss << ". ";
		else if (board[i] == DiscColor::BLACK)
			ss << "X ";
		else
			ss << "O ";
		if (i != 0 && i % BOARD_SIZE == BOARD_SIZE - 1)
			ss << std::endl;
	}
	return ss.str();
}

