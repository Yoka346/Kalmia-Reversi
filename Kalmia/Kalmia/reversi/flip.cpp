#pragma once

#include "flip.h"

using namespace reversi;

#if defined(USE_AVX2) && defined(USE_X64) 

/**
* @fn
* @brief AVX2を用いて, 着手可能位置を計算する.
* @param (p) 現在のプレイヤーの盤面.
* @param (o) 相手の盤面.
* @return 着手可能位置を示すビットボード
* @detail
* 着手可能位置はparallel prefix アルゴリズムを用いて生成される.
* 4方向について, AVX2を用いて同時に計算する.
*
* @cite
*  https://www.chessprogramming.org/Parallel_Prefix_Algorithms
*  http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility
**/
uint64_t reversi::calc_flipped_discs(uint64_t& p, uint64_t& o, BoardCoordinate& coord)
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

	flipped_left_4 = _mm256_andnot_si256(_mm256_cmpeq_epi64(outflank_left_4, ZERO), flipped_left_4);	// ������error
	flipped_right_4 = _mm256_andnot_si256(_mm256_cmpeq_epi64(outflank_right_4, ZERO), flipped_right_4);

	auto flipped_4 = _mm256_or_si256(flipped_left_4, flipped_right_4);
	auto flipped_2 = _mm_or_si128(_mm256_extracti128_si256(flipped_4, 0), _mm256_extracti128_si256(flipped_4, 1));
	flipped_2 = _mm_or_si128(flipped_2, _mm_unpackhi_epi64(flipped_2, flipped_2));
	return _mm_cvtsi128_si64(flipped_2);
}

#elif defined(USE_SSE42) || defined(USE_SSE41)

/**
* @fn
* @brief SSEを用いて, 着手可能位置を計算する.
* @param (p) 現在のプレイヤーの盤面.
* @param (o) 相手の盤面.
* @return 着手可能位置を示すビットボード
* @detail
* 着手可能位置はparallel prefix アルゴリズムを用いて生成される.
* 2方向について, SSEを用いて同時に計算する.
*
* @cite
*  https://www.chessprogramming.org/Parallel_Prefix_Algorithms
*  http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility
**/
uint64_t reversi::calc_flipped_discs(uint64_t& p, uint64_t& o, BoardCoordinate& coord)
}
{
	
}

#else

/**
* @fn
* @brief 着手可能位置を計算する.
* @param (p) 現在のプレイヤーの盤面.
* @param (o) 相手の盤面.
* @return 着手可能位置を示すビットボード
* @detail
* 着手可能位置はparallel prefix アルゴリズムを用いて生成される.
*
* @cite
*  https://www.chessprogramming.org/Parallel_Prefix_Algorithms
*  http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm#mobility
**/
uint64_t reversi::calc_flipped_discs(uint64_t& p, uint64_t& o, BoardCoordinate& coord)
{
	
}

#endif
