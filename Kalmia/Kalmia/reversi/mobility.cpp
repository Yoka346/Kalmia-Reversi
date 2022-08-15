#pragma once

#include "mobility.h"

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
uint64_t reversi::calc_mobility(const uint64_t& p, const uint64_t& o)
{
	static const __m256i SHIFT = _mm256_set_epi64x(7ULL, 9ULL, 8ULL, 1ULL);
	static const __m256i SHIFT_2 = _mm256_set_epi64x(14ULL, 18ULL, 16ULL, 2ULL);
	static const __m256i MASK = _mm256_set_epi64x(0x7e7e7e7e7e7e7e7eULL, 0x7e7e7e7e7e7e7e7eULL, 0xffffffffffffffffULL, 0x7e7e7e7e7e7e7e7eULL);

	auto p_4 = _mm256_broadcastq_epi64(_mm_cvtsi64_si128(p));
	auto masked_o_4 = _mm256_and_si256(_mm256_broadcastq_epi64(_mm_cvtsi64_si128(o)), MASK);

	auto flip_left = _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(p_4, SHIFT));
	auto flip_right = _mm256_and_si256(masked_o_4, _mm256_srlv_epi64(p_4, SHIFT));
	flip_left = _mm256_or_si256(flip_left, _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(flip_left, SHIFT)));
	flip_right = _mm256_or_si256(flip_right, _mm256_and_si256(masked_o_4, _mm256_srlv_epi64(flip_right, SHIFT)));

	auto prefix_left = _mm256_and_si256(masked_o_4, _mm256_sllv_epi64(masked_o_4, SHIFT));
	auto prefix_right = _mm256_srlv_epi64(prefix_left, SHIFT);

	flip_left = _mm256_or_si256(flip_left, _mm256_and_si256(prefix_left, _mm256_sllv_epi64(flip_left, SHIFT_2)));
	flip_right = _mm256_or_si256(flip_right, _mm256_and_si256(prefix_right, _mm256_srlv_epi64(flip_right, SHIFT_2)));
	flip_left = _mm256_or_si256(flip_left, _mm256_and_si256(prefix_left, _mm256_sllv_epi64(flip_left, SHIFT_2)));
	flip_right = _mm256_or_si256(flip_right, _mm256_and_si256(prefix_right, _mm256_srlv_epi64(flip_right, SHIFT_2)));

	auto mobility_4 = _mm256_sllv_epi64(flip_left, SHIFT);
	mobility_4 = _mm256_or_si256(mobility_4, _mm256_srlv_epi64(flip_right, SHIFT));
	auto mobility_2 = _mm_or_si128(_mm256_extractf128_si256(mobility_4, 0), _mm256_extractf128_si256(mobility_4, 1));
	mobility_2 = _mm_or_si128(mobility_2, _mm_unpackhi_epi64(mobility_2, mobility_2));
	return _mm_cvtsi128_si64(mobility_2) & ~(p | o);
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
uint64_t reversi::calc_mobility(const uint64_t& p, const uint64_t& o)
{
	auto p_2 = _mm_set_epi64x(BYTE_SWAP_64(p), p);
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;
	auto masked_o_2 = _mm_set_epi64x(BYTE_SWAP_64(masked_o), masked_o);

	auto flip_diag_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(p_2, 7));
	auto flip_horizontal = masked_o & (p << 1);
	auto flip_vertical = o & (p << 8);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(masked_o_2, _mm_slli_epi64(flip_diag_2, 7)));
	flip_horizontal |= masked_o & (flip_horizontal << 1);
	flip_vertical |= o & (flip_vertical << 8);

	auto prefix_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(masked_o_2, 7));
	auto prefix_horizontal = masked_o & (masked_o << 1);
	auto prefix_vertical = o & (o << 8);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flip_diag_2, 14)));
	flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
	flip_vertical |= prefix_vertical & (flip_vertical << 16);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flip_diag_2, 14)));
	flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
	flip_vertical |= prefix_vertical & (flip_vertical << 16);

	auto mobility_2 = _mm_slli_epi64(flip_diag_2, 7);
	auto mobility = (flip_horizontal << 1) | (flip_vertical << 8);

	flip_diag_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(p_2, 9));
	flip_horizontal = masked_o & (p >> 1);
	flip_vertical = o & (p >> 8);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(masked_o_2, _mm_slli_epi64(flip_diag_2, 9)));
	flip_horizontal |= masked_o & (flip_horizontal >> 1);
	flip_vertical |= o & (flip_vertical >> 8);

	prefix_2 = _mm_and_si128(masked_o_2, _mm_slli_epi64(masked_o_2, 9));
	prefix_horizontal >>= 1;
	prefix_vertical >>= 8;

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flip_diag_2, 18)));
	flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
	flip_vertical |= prefix_vertical & (flip_vertical >> 16);

	flip_diag_2 = _mm_or_si128(flip_diag_2, _mm_and_si128(prefix_2, _mm_slli_epi64(flip_diag_2, 18)));
	flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
	flip_vertical |= prefix_vertical & (flip_vertical >> 16);

	mobility_2 = _mm_or_si128(mobility_2, _mm_slli_epi64(flip_diag_2, 9));
	mobility |= (flip_horizontal >> 1) | (flip_vertical >> 8);

#ifdef USE_X64
	mobility |= _mm_cvtsi128_si64(mobility_2) | BYTE_SWAP_64(_mm_cvtsi128_si64(_mm_unpackhi_epi64(mobility_2, mobility_2)));
#else
	uint64_t data[2];
	std::memcpy(data, &mobility_2, 16);
	mobility |= data[0] | BYTE_SWAP_64(data[1]);
#endif
	return mobility & ~(p | o);
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
uint64_t reversi::calc_mobility(const uint64_t& p, const uint64_t& o)
{
	auto masked_o = o & 0x7e7e7e7e7e7e7e7eULL;

	// left
	auto flip_horizontal = masked_o & (p << 1);
	auto flip_diag_A1H8 = masked_o & (p << 9);
	auto flip_diag_A8H1 = masked_o & (p << 7);
	auto flip_vertical = o & (p << 8);

	flip_horizontal |= masked_o & (flip_horizontal << 1);
	flip_diag_A1H8 |= masked_o & (flip_diag_A1H8 << 9);
	flip_diag_A8H1 |= masked_o & (flip_diag_A8H1 << 7);
	flip_vertical |= o & (flip_vertical << 8);

	auto prefix_horizontal = masked_o & (masked_o << 1);
	auto prefix_diag_A1H8 = masked_o & (masked_o << 9);
	auto prefix_diag_A8H1 = masked_o & (masked_o << 7);
	auto prefix_vertical = o & (o << 8);

	flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
	flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 << 18);
	flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 << 14);
	flip_vertical |= prefix_vertical & (flip_vertical << 16);

	flip_horizontal |= prefix_horizontal & (flip_horizontal << 2);
	flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 << 18);
	flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 << 14);
	flip_vertical |= prefix_vertical & (flip_vertical << 16);

	auto mobility = (flip_horizontal << 1) | (flip_diag_A1H8 << 9) | (flip_diag_A8H1 << 7) | (flip_vertical << 8);

	// right
	flip_horizontal = masked_o & (p >> 1);
	flip_diag_A1H8 = masked_o & (p >> 9);
	flip_diag_A8H1 = masked_o & (p >> 7);
	flip_vertical = o & (p >> 8);

	flip_horizontal |= masked_o & (flip_horizontal >> 1);
	flip_diag_A1H8 |= masked_o & (flip_diag_A1H8 >> 9);
	flip_diag_A8H1 |= masked_o & (flip_diag_A8H1 >> 7);
	flip_vertical |= o & (flip_vertical >> 8);

	prefix_horizontal = masked_o & (masked_o >> 1);
	prefix_diag_A1H8 = masked_o & (masked_o >> 9);
	prefix_diag_A8H1 = masked_o & (masked_o >> 7);
	prefix_vertical = o & (o >> 8);

	flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
	flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 >> 18);
	flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 >> 14);
	flip_vertical |= prefix_vertical & (flip_vertical >> 16);

	flip_horizontal |= prefix_horizontal & (flip_horizontal >> 2);
	flip_diag_A1H8 |= prefix_diag_A1H8 & (flip_diag_A1H8 >> 18);
	flip_diag_A8H1 |= prefix_diag_A8H1 & (flip_diag_A8H1 >> 14);
	flip_vertical |= prefix_vertical & (flip_vertical >> 16);

	mobility |= (flip_horizontal >> 1) | (flip_diag_A1H8 >> 9) | (flip_diag_A8H1 >> 7) | (flip_vertical >> 8);
	return mobility & ~(p | o);
}

#endif
