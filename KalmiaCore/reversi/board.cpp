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
	flipped_right_4 = _mm256_andnot_si256(_mm256_cmpeq_epi64(outflank_left_4, _mm256_setzero_si256()), flipped_right_4);

	auto flipped_4 = _mm256_or_si256(flipped_left_4, flipped_right_4);
	auto flipped_2 = _mm_or_si128(_mm256_extracti128_si256(flipped_4, 0), _mm256_extracti128_si256(flipped_4, 1));
	flipped_2 = _mm_or_si128(flipped_2, _mm_unpackhi_epi64(flipped_2, flipped_2));
	return _mm_cvtsi128_si64(flipped_2);
}

#elif defined(USE_SSE41) || defined(USE_SSE2)

static uint64_t calc_flipped_discs_SSE(uint64_t p, uint64_t o, BoardCoordinate coord)
{
	auto coord_bit = COORD_TO_BIT[coord];
	auto coord_bit_2 = _mm_set_epi64x(_byteswap_uint64(coord_bit), coord_bit);
	auto p_2 = _mm_set_epi64x(_byteswap_uint64(p), p);
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;
	auto masked_o_2 = _mm_set_epi64x(_byteswap_uint64(masked_o), masked_o);

	auto flipped_diag_left_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(coord_bit_2, 7));
	auto flipped_horizontal_left = masked_o & (coord_bit << 1);
	auto flipped_vertical_left = o & (coord_bit << 8);

	flipped_diag_left_2 = _mm_or_si128(flipped_diag_left_2, _mm_and_si128(masked_o_2, _mm_slli_epi64(flipped_diag_left_2, 7)));
	flipped_horizontal_left |= masked_o & (flipped_horizontal_left << 1);
	flipped_vertical_left |= o & (flipped_vertical_left << 8);

	auto prefix_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(masked_o_2, 7));
	auto prefix_horizontal = masked_o & (masked_o << 1);
	auto prefix_vertical = o & (o << 8);

	flipped_diag_left_2 = _mm_or_si128(flipped_diag_left_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flipped_diag_left_2, 14)));
	flipped_horizontal_left |= prefix_horizontal & (flipped_horizontal_left << 2);
	flipped_vertical_left |= prefix_vertical & (flipped_vertical_left << 16);

	flipped_diag_left_2 = _mm_or_si128(flipped_diag_left_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flipped_diag_left_2, 14)));
	flipped_horizontal_left |= prefix_horizontal & (flipped_horizontal_left << 2);
	flipped_vertical_left |= prefix_vertical & (flipped_vertical_left << 16);

	auto flipped_diag_right_2 = _mm_and_si128(masked_o_2, _mm_srli_epi64(coord_bit_2, 9));
	auto flipped_horizontal_right = masked_o & (coord_bit >> 1);
	auto flipped_vertical_right = o & (coord_bit >> 8);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(masked_o_2, _mm_srli_epi64(flipped_diag_right_2, 9)));
	flipped_horizontal_right |= masked_o & (flipped_horizontal_right >> 1);
	flipped_vertical_right |= o & (flipped_vertical_right >> 8);

	prefix_2 = _mm_and_si128(masked_o_2, _mm_srli_epi64(masked_o_2, 9));
	prefix_horizontal = masked_o & (masked_o >> 1);
	prefix_vertical = o & (o >> 8);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(prefix_2, _mm_srli_epi64(flipped_diag_right_2, 18)));
	flipped_horizontal_right |= prefix_horizontal & (flipped_horizontal_right >> 2);
	flipped_vertical_right |= prefix_vertical & (flipped_vertical_right >> 16);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(prefix_2, _mm_srli_epi64(flipped_diag_right_2, 18)));
	flipped_horizontal_right |= prefix_horizontal & (flipped_horizontal_right >> 2);
	flipped_vertical_right |= prefix_vertical & (flipped_vertical_right >> 16);

	auto outflank_diag_left_2 = _mm_and_si128(p_2, _mm_slli_epi64(flipped_diag_left_2, 7));
	auto outflank_horizontal_left = p & (flipped_horizontal_left << 1);
	auto outflank_vertical_left = p & (flipped_vertical_left << 8);

	auto outflank_diag_right_2 = _mm_and_si128(p_2, _mm_slli_epi64(flipped_diag_right_2, 9));
	auto outflank_horizontal_right = p & (flipped_horizontal_right >> 1);
	auto outflank_vertical_right = p & (flipped_vertical_right >> 8);

#ifdef USE_SSE41
	flipped_diag_left_2 = _mm_andnot_si128(_mm_cmpeq_epi64(outflank_diag_left_2, _mm_setzero_si128()), flipped_diag_left_2);
	flipped_horizontal_left &= -(int)(outflank_horizontal_left != 0);
	flipped_vertical_left &= -(int)(outflank_vertical_left != 0);

	flipped_diag_right_2 = _mm_andnot_si128(_mm_cmpeq_epi64(outflank_diag_right_2, _mm_setzero_si128()), flipped_diag_right_2);
	flipped_horizontal_right &= -(int)(outflank_horizontal_right != 0);
	flipped_vertical_right &= -(int)(outflank_vertical_right != 0);
#else
	flipped_diag_left_2 = _mm_and_si128(_mm_castpd_si128(_mm_cmpneq_pd(_mm_castsi128_pd(outflank_diag_left_2), _mm_setzero_pd())), flipped_diag_left_2);
	flipped_horizontal_left &= -(int)(outflank_horizontal_left != 0);
	flipped_vertical_left &= -(int)(outflank_vertical_left != 0);

	flipped_diag_right_2 = _mm_and_si128(_mm_castpd_si128(_mm_cmpneq_pd(_mm_castsi128_pd(outflank_diag_right_2), _mm_setzero_pd())), flipped_diag_right_2);
	flipped_horizontal_right &= -(int)(outflank_horizontal_right != 0);
	flipped_vertical_right &= -(int)(outflank_vertical_right != 0);
#endif
	auto flipped_2 = _mm_or_si128(flipped_diag_left_2, flipped_diag_right_2);
	auto flipped = flipped_horizontal_left | flipped_horizontal_right | flipped_vertical_left | flipped_vertical_right;
#ifdef USE_X64
	flipped |= _mm_cvtsi128_si64(flipped_2) | _byteswap_uint64(_mm_cvtsi128_si64(_mm_unpackhi_epi64(flipped_2, flipped_2)));
#else
	uint64_t data[2];
	std::memcpy(data, &flipped_2, 16);
	flipped |= data[0] | _byteswap_uint64(data[1]);
#endif
	return flipped;
}

#else

static uint64_t calc_flipped_discs_CPU(uint64_t p, uint64_t o, BoardCoordinate coord)
{
	auto coord_bit = COORD_TO_BIT[coord];
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;


}

#endif

DiscColor Board::get_square_color(BoardCoordinate coord)
{
	auto side_to_move = this->side_to_move + 1;
	auto color = side_to_move * ((this->bitboard.current_player >> coord) & 1) + (side_to_move ^ 3) * ((this->bitboard.opponent_player >> coord) & 1);
	return (color != 0) ? static_cast<DiscColor>(color - 1) : DiscColor::NONE;
}