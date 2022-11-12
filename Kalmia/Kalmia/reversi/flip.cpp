#include "flip.h"
#include "../utils/bitmanip.h"
#include "constant.h"

using namespace reversi;

#if defined(USE_AVX2) && defined(USE_X64) 

/**
* @fn
* @brief AVX2を用いて, 着手によって裏返る石を計算する.
* @param (p) 現在のプレイヤーの盤面.
* @param (o) 相手の盤面.
* @return 裏返った石の位置を表すビットボード.
* @detail
* 着手可能位置はparallel prefix アルゴリズム(1段 kogge stone)を用いて生成される.
* 4方向について, AVX2を用いて同時に計算する.
*
* @cite
*  https://www.chessprogramming.org/Parallel_Prefix_Algorithms
*  http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility
**/
uint64_t reversi::calc_flipped_discs(const uint64_t& p, const uint64_t& o, const BoardCoordinate& coord)
{
	static const __m256i SHIFT = _mm256_set_epi64x(7ULL, 9ULL, 8ULL, 1ULL);
	static const __m256i SHIFT_2 = _mm256_set_epi64x(14ULL, 18ULL, 16ULL, 2ULL);
	static const __m256i MASK = _mm256_set_epi64x(0x7e7e7e7e7e7e7e7eULL, 0x7e7e7e7e7e7e7e7eULL, 0xffffffffffffffffULL, 0x7e7e7e7e7e7e7e7eULL);
	static const __m256i ZERO = _mm256_setzero_si256();

	auto coord_bit = COORD_TO_BIT[coord];
	auto coord_bit_4 = _mm256_broadcastq_epi64(_mm_cvtsi64_si128(coord_bit));
	auto p_4 = _mm256_broadcastq_epi64(_mm_cvtsi64_si128(p));
	auto masked_o_4 = _mm256_and_si256(_mm256_broadcastq_epi64(_mm_cvtsi64_si128(o)), MASK);

	auto flipped_left_4 = _mm256_and_si256(_mm256_sllv_epi64(coord_bit_4, SHIFT), masked_o_4);
	auto flipped_right_4 = _mm256_and_si256(_mm256_srlv_epi64(coord_bit_4, SHIFT), masked_o_4);

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT), masked_o_4));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT), masked_o_4));

	auto prefix_left = _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(masked_o_4, SHIFT));
	auto prefix_right = _mm256_srlv_epi64(prefix_left, SHIFT);

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT_2), prefix_left));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT_2), prefix_right));

	flipped_left_4 = _mm256_or_si256(flipped_left_4, _mm256_and_si256(_mm256_sllv_epi64(flipped_left_4, SHIFT_2), prefix_left));
	flipped_right_4 = _mm256_or_si256(flipped_right_4, _mm256_and_si256(_mm256_srlv_epi64(flipped_right_4, SHIFT_2), prefix_right));

	auto outflank_left_4 = _mm256_and_si256(p_4, _mm256_sllv_epi64(flipped_left_4, SHIFT));
	auto outflank_right_4 = _mm256_and_si256(p_4, _mm256_srlv_epi64(flipped_right_4, SHIFT));

	flipped_left_4 = _mm256_andnot_si256(_mm256_cmpeq_epi64(outflank_left_4, ZERO), flipped_left_4);	
	flipped_right_4 = _mm256_andnot_si256(_mm256_cmpeq_epi64(outflank_right_4, ZERO), flipped_right_4);

	auto flipped_4 = _mm256_or_si256(flipped_left_4, flipped_right_4);
	auto flipped_2 = _mm_or_si128(_mm256_extracti128_si256(flipped_4, 0), _mm256_extracti128_si256(flipped_4, 1));
	flipped_2 = _mm_or_si128(flipped_2, _mm_unpackhi_epi64(flipped_2, flipped_2));
	return _mm_cvtsi128_si64(flipped_2);
}

#elif defined(USE_SSE42) || defined(USE_SSE41)

/**
* @fn
* @brief SSEを用いて, 着手によって裏返る石を計算する.
* @param (p) 現在のプレイヤーの盤面.
* @param (o) 相手の盤面.
* @return 裏返った石の位置を表すビットボード.
* @detail
* 着手可能位置はparallel prefix アルゴリズム(1段 kogge stone)を用いて生成される.
* 2方向について, SSEを用いて同時に計算する.
*
* @cite
*  https://www.chessprogramming.org/Parallel_Prefix_Algorithms
*  http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility
**/
uint64_t reversi::calc_flipped_discs(const uint64_t& p, const uint64_t& o, const BoardCoordinate& coord)
{
	auto coord_bit = COORD_TO_BIT[coord];
	auto coord_bit_2 = _mm_set_epi64x(BYTE_SWAP_64(coord_bit), coord_bit);
	auto p_2 = _mm_set_epi64x(BYTE_SWAP_64(p), p);
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;
	auto masked_o_2 = _mm_set_epi64x(BYTE_SWAP_64(masked_o), masked_o);

	// left
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

	// right
	auto flipped_diag_right_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(coord_bit_2, 9));
	auto flipped_horizontal_right = masked_o & (coord_bit >> 1);
	auto flipped_vertical_right = o & (coord_bit >> 8);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(masked_o_2, _mm_slli_epi64(flipped_diag_right_2, 9)));
	flipped_horizontal_right |= masked_o & (flipped_horizontal_right >> 1);
	flipped_vertical_right |= o & (flipped_vertical_right >> 8);

	prefix_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(masked_o_2, 9));
	prefix_horizontal = masked_o & (masked_o >> 1);
	prefix_vertical = o & (o >> 8);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flipped_diag_right_2, 18)));
	flipped_horizontal_right |= prefix_horizontal & (flipped_horizontal_right >> 2);
	flipped_vertical_right |= prefix_vertical & (flipped_vertical_right >> 16);

	flipped_diag_right_2 = _mm_or_si128(flipped_diag_right_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flipped_diag_right_2, 18)));
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
	flipped_horizontal_left &= -static_cast<int>(outflank_horizontal_left != 0);
	flipped_vertical_left &= -static_cast<int>(outflank_vertical_left != 0);

	flipped_diag_right_2 = _mm_andnot_si128(_mm_cmpeq_epi64(outflank_diag_right_2, _mm_setzero_si128()), flipped_diag_right_2);
	flipped_horizontal_right &= -static_cast<int>(outflank_horizontal_right != 0);
	flipped_vertical_right &= -static_cast<int>(outflank_vertical_right != 0);
#else
	flipped_diag_left_2 = _mm_and_si128(_mm_castpd_si128(_mm_cmpneq_pd(_mm_castsi128_pd(outflank_diag_left_2), _mm_setzero_pd())), flipped_diag_left_2);
	flipped_horizontal_left &= -static_cast<int>(outflank_horizontal_left != 0);
	flipped_vertical_left &= -static_cast<int>(outflank_vertical_left != 0);

	flipped_diag_right_2 = _mm_and_si128(_mm_castpd_si128(_mm_cmpneq_pd(_mm_castsi128_pd(outflank_diag_right_2), _mm_setzero_pd())), flipped_diag_right_2);
	flipped_horizontal_right &= -static_cast<int>(outflank_horizontal_right != 0);
	flipped_vertical_right &= -static_cast<int>(outflank_vertical_right != 0);
#endif
	auto flipped_2 = _mm_or_si128(flipped_diag_left_2, flipped_diag_right_2);
	auto flipped = flipped_horizontal_left | flipped_horizontal_right | flipped_vertical_left | flipped_vertical_right;
#ifdef USE_X64
	flipped |= _mm_cvtsi128_si64(flipped_2) | BYTE_SWAP_64(_mm_cvtsi128_si64(_mm_unpackhi_epi64(flipped_2, flipped_2)));
#else
	uint64_t data[2];
	std::memcpy(data, &flipped_2, 16);
	flipped |= data[0] | BYTE_SWAP_64(data[1]);
#endif
	return flipped;
}

#else

/**
* @fn
* @brief 着手によって裏返る石を計算する.
* @param (p) 現在のプレイヤーの盤面.
* @param (o) 相手の盤面.
* @return 裏返った石の位置を表すビットボード.
* @detail
* 着手可能位置はparallel prefix アルゴリズム(1段 kogge stone)を用いて生成される.
*
* @cite
*  https://www.chessprogramming.org/Parallel_Prefix_Algorithms
*  http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility
**/
uint64_t reversi::calc_flipped_discs(const uint64_t& p, const uint64_t& o, const BoardCoordinate& coord)
{
	auto coord_bit = COORD_TO_BIT[coord];
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;

	// left
	auto flipped_horizontal = masked_o & (coord_bit << 1);
	auto flipped_diag_A1H8 = masked_o & (coord_bit << 9);
	auto flipped_diag_A8H1 = masked_o & (coord_bit << 7);
	auto flipped_vertical = o & (coord_bit << 8);

	flipped_horizontal |= masked_o & (flipped_horizontal << 1);
	flipped_diag_A1H8 |= masked_o & (flipped_diag_A1H8 << 9);
	flipped_diag_A8H1 |= masked_o & (flipped_diag_A8H1 << 7);
	flipped_vertical |= o & (flipped_vertical << 8);

	auto prefix_horizontal = masked_o & (masked_o << 1);
	auto prefix_diag_A1H8 = masked_o & (masked_o << 9);
	auto prefix_diag_A8H1 = masked_o & (masked_o << 7);
	auto prefix_vertical = o & (o << 8);

	flipped_horizontal |= prefix_horizontal & (flipped_horizontal << 2);
	flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 << 18);
	flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 << 14);
	flipped_vertical |= prefix_vertical & (flipped_vertical << 16);

	flipped_horizontal |= prefix_horizontal & (flipped_horizontal << 2);
	flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 << 18);
	flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 << 14);
	flipped_vertical |= prefix_vertical & (flipped_vertical << 16);

	auto outflank_horizontal = p & (flipped_horizontal << 1);
	auto outflank_diag_A1H8 = p & (flipped_diag_A1H8 << 9);
	auto outflank_diag_A8H1 = p & (flipped_diag_A8H1 << 7);
	auto outflank_vertical = p & (flipped_vertical << 8);

	flipped_horizontal &= -static_cast<int>(outflank_horizontal != 0);
	flipped_diag_A1H8 &= -static_cast<int>(outflank_diag_A1H8 != 0);
	flipped_diag_A8H1 &= -static_cast<int>(outflank_diag_A8H1 != 0);
	flipped_vertical &= -static_cast<int>(outflank_vertical != 0);

	auto flipped = flipped_horizontal | flipped_diag_A1H8 | flipped_diag_A8H1 | flipped_vertical;

	// right
	flipped_horizontal = masked_o & (coord_bit >> 1);
	flipped_diag_A1H8 = masked_o & (coord_bit >> 9);
	flipped_diag_A8H1 = masked_o & (coord_bit >> 7);
	flipped_vertical = o & (coord_bit >> 8);

	flipped_horizontal |= masked_o & (flipped_horizontal >> 1);
	flipped_diag_A1H8 |= masked_o & (flipped_diag_A1H8 >> 9);
	flipped_diag_A8H1 |= masked_o & (flipped_diag_A8H1 >> 7);
	flipped_vertical |= o & (flipped_vertical >> 8);

	prefix_horizontal = masked_o & (masked_o >> 1);
	prefix_diag_A1H8 = masked_o & (masked_o >> 9);
	prefix_diag_A8H1 = masked_o & (masked_o >> 7);
	prefix_vertical = o & (o >> 8);

	flipped_horizontal |= prefix_horizontal & (flipped_horizontal >> 2);
	flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 >> 18);
	flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 >> 14);
	flipped_vertical |= prefix_vertical & (flipped_vertical >> 16);

	flipped_horizontal |= prefix_horizontal & (flipped_horizontal >> 2);
	flipped_diag_A1H8 |= prefix_diag_A1H8 & (flipped_diag_A1H8 >> 18);
	flipped_diag_A8H1 |= prefix_diag_A8H1 & (flipped_diag_A8H1 >> 14);
	flipped_vertical |= prefix_vertical & (flipped_vertical >> 16);

	auto outflank_horizontal_right = p & (flipped_horizontal >> 1);
	auto outflank_diag_A1H8_right = p & (flipped_diag_A1H8 >> 9);
	auto outflank_diag_A8H1_right = p & (flipped_diag_A8H1 >> 7);
	auto outflank_vertical_right = p & (flipped_vertical >> 8);

	flipped_horizontal &= -static_cast<int>(outflank_horizontal_right != 0);
	flipped_diag_A1H8 &= -static_cast<int>(outflank_diag_A1H8_right != 0);
	flipped_diag_A8H1 &= -static_cast<int>(outflank_diag_A8H1_right != 0);
	flipped_vertical &= -static_cast<int>(outflank_vertical_right != 0);

	return flipped | flipped_horizontal | flipped_diag_A1H8 | flipped_diag_A8H1 | flipped_vertical;
}

#endif
