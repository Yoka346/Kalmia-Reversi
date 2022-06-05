#pragma once
#include "gtest/gtest.h"
#include <vector>

#include "pch.h"
#include "reversi/board.h"

reversi::BoardCoordinate sample_move(reversi::MoveCoordinateIterator& mobility, std::mt19937& rand);

int count_true(bool* b);

void assert_mobility_equal(reversi::Board board, bool* expected, reversi::MoveCoordinateIterator& actual, bool& success);

void assert_board_are_equal(reversi::DiscColor* expected, reversi::Board actual, bool& success);

bool is_out_of_board(int coord);

void board_init(reversi::DiscColor* board);

#define coord_2D_to_1D(x, y) x + y * BOARD_SIZE

void calc_flipped_discs(reversi::DiscColor* board, reversi::DiscColor color, reversi::BoardCoordinate coord, int dir_x, int dir_y, bool* flipped);

void board_calc_flipped_discs(reversi::DiscColor* board, reversi::DiscColor color, reversi::BoardCoordinate coord, bool* flipped);

#define out_of_range(x, y)  x < 0 || x >= BOARD_SIZE || y < 0 || y >= BOARD_SIZE

bool check_mobility(reversi::DiscColor* board, reversi::DiscColor color, reversi::BoardCoordinate coord, int dir_x, int dir_y);

bool check_mobility(reversi::DiscColor* board, reversi::DiscColor color, reversi::BoardCoordinate coord);

void board_calc_mobility(reversi::DiscColor* board, reversi::DiscColor color, bool* mobility);

void board_update(reversi::DiscColor* board, reversi::DiscColor color, reversi::BoardCoordinate coord, bool* flipped);

std::string board_to_string(reversi::DiscColor* board);
