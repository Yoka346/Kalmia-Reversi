#pragma once
#include "board.h"

using namespace reversi;

inline bool Mobility::move_to_next_coord(BoardCoordinate& coord)
{
	while (this->mobility_count != this->mobility_num)
	{
		if (this->mobility & this->mask)
		{
			this->mobility_count++;
			coord = this->current_coordinate++;
			this->mask <<= 1;
			return true;
		}
		this->current_coordinate++;
		this->mask <<= 1;
	}
	return false;
}

bool Board::initialized = false;
uint64_t Board::hash_rank[Board::HASH_RANK_LEN_0][Board::HASH_RANK_LEN_1];

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

inline uint64_t Board::calc_flipped_discs(uint64_t p, uint64_t o, BoardCoordinate coord)
{

}

#ifdef USE_AVX2

inline static uint64_t calc_flipped_discs_AVX2(uint64_t p, uint64_t o, BoardCoordinate coord)
{
	const static __m256i SHIFT = _mm256_set_epi64x(7, 9, 8, 1);
	const static __m256i SHIFT_2 = _mm256_set_epi64x(14, 18, 16, 2);
	const static __m256i MASK = _mm256_set_epi64x(0x7e7e7e7e7e7e7e7e, 0x7e7e7e7e7e7e7e7e, 0xffffffffffffffff, 0x7e7e7e7e7e7e7e7e);

	auto coord_bit = COORD_TO_BIT[coord];
	auto coord_bit_4 = _mm256_broadcastq_epi64(_mm_cvtsi64_si128(coord_bit));
	auto p_4 = _mm256_broadcastq_epi64(_mm_cvtsi64_si128(p));
	auto masked_o_4 = _mm256_and_si256(_mm256_broadcastq_epi64(_mm_cvtsi64_si128(o)), MASK);

	auto flipped_left_4 = _mm256_and_si256(_mm256_sllv_epi64(coord_bit_4, SHIFT), masked_o_4);
	auto flipped_right_4 = _mm256_and_si256(_mm256_srlv_epi64(coord_bit_4, SHIFT), masked_o_4);

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT), masked_o_4));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT), masked_o_4));

	auto prefix_left = _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(masked_o_4, SHIFT));
	auto prefix_right = _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(masked_o_4, SHIFT));

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT_2), prefix_left));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT_2), prefix_right));

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT_2), prefix_left));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT_2), prefix_right));

	auto outflank_left_4 = _mm256_and_si256(p_4, _mm256_sllv_epi64(flipped_left_4, SHIFT));
	auto outflank_right_4 = _mm256_and_si256(p_4, _mm256_srlv_epi64(flipped_right_4, SHIFT));

	flipped_left_4 = _mm256_andnot_epi64(_mm256_cmpeq_epi64(outflank_left_4, _mm256_setzero_si256()), flipped_left_4);
	flipped_right_4 = _mm256_andnot_epi64(_mm256_cmpeq_epi64(outflank_left_4, _mm256_setzero_si256()), flipped_right_4);

	auto flipped_4 = _mm256_or_epi64(flipped_left_4, flipped_right_4);
	auto flipped_2 = _mm_or_epi64(_mm256_extracti128_si256(flipped_4, 0), _mm256_extracti128_si256(flipped_4, 1));
	flipped_2 = _mm_or_epi64(flipped_2, _mm_unpackhi_epi64(flipped_2, flipped_2));
	return _mm_cvtsi128_si64(flipped_2);
}

#elif defined(USE_SSE42) || defined(USE_SSE2)

static uint64_t calc_flipped_discs_SSE(uint64_t p, uint64_t o, BoardCoordinate coord)
{
	const static __m128i MASK = _mm_set_epi64x(0x7e7e7e7e7e7e7e7e, 0x7e7e7e7e7e7e7e7e);
	auto coord_bit = COORD_TO_BIT[coord];
	auto coord_bit_2 = _mm_broadcastq_epi64(_mm_cvtsi64_si128(coord_bit));
	auto p_2 = _mm_broadcastq_epi64(_mm_cvtsi64_si128(p));
	auto masked_o_2 = _mm_and_si128(_mm_broadcastq_epi64(_mm_cvtsi64_si128(o)), MASK);

	//auto flipped_horizontal = _byteswap_uint64	–¾“ú‘‚­
}

#else

static uint64_t calc_flipped_discs_CPU(uint64_t p, uint64_t o, BoardCoordinate coord)
{
	
}

#endif

DiscColor Board::get_square_color(BoardCoordinate coord)
{
	auto side_to_move = this->side_to_move + 1;
	auto color = side_to_move * ((this->bitboard.current_player >> coord) & 1) + (side_to_move ^ 3) * ((this->bitboard.opponent_player >> coord) & 1);
	return (color != 0) ? static_cast<DiscColor>(color - 1) : DiscColor::NONE;
}