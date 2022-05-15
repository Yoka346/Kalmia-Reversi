#pragma once

#include "pch.h"

#if defined(USE_SSE42) && defined(USE_X64)

#define popcount(bits) (int)__popcnt64(bits)

#elif (defined(__GNUC__) && __GNUC__ >= 4)

#define popcount(bits) (int)__builtin_popcountll(bits)

#else

inline int popcount(uint64_t bits)
{
	bits = ((bits & 0xaaaaaaaaaaaaaaaaUL) >> 1) + (bits & 0x5555555555555555UL);
	bits = ((bits & 0xccccccccccccccccUL) >> 2) + (bits & 0x3333333333333333UL);
	bits = ((bits & 0xf0f0f0f0f0f0f0f0UL) >> 4) + (bits & 0x0f0f0f0f0f0f0f0fUL);
	bits = ((bits & 0xff00ff00ff00ff00UL) >> 8) + (bits & 0x00ff00ff00ff00ffUL);
	bits = ((bits & 0xffff0000ffff0000UL) >> 16) + (bits & 0x0000ffff0000ffffUL);
	bits = ((bits & 0xffffffff00000000UL) >> 32) + (bits & 0x00000000ffffffffUL);
	return bits;
}

#endif

#if defined(USE_BMI1) && defined(USE_X64)

#define find_first_set(bits) (int)_tzcnt_u64(bits)
#define count_leading_zero(bits) (int)_lzcnt_u64(bits);

#elif (defined(__GNUC__) && __GNUC__ >= 4)

#define find_first_set(bits) __builtin_ctzll(bits)
#define count_leading_zero(bits) __builtin_clzll(bits)

#elif defined(_MSC_VER)

inline int find_first_set(uint64_t bits)
{
	unsigned long idx;
	_BitScanForward64(&idx, bits);
	return idx;
}

inline int count_leading_zero(uint64_t bits) 
{
	unsigned long idx;
	_BitScanReverse64(&idx, bits);
	return 63 - idx;
}

#else

int find_first_set(uint64_t bits)
{
	constexpr byte DE_BRUIJN_TABLE[64] =
	{
		63, 0, 58, 1, 59, 47, 53, 2,
		60, 39, 48, 27, 54, 33, 42, 3,
		61, 51, 37, 40, 49, 18, 28, 20,
		55, 30, 34, 11, 43, 14, 22, 4,
		62, 57, 46, 52, 38, 26, 32, 41,
		50, 36, 17, 19, 29, 10, 13, 21,
		56, 45, 25, 31, 35, 16, 9, 12,
		44, 24, 15, 8, 23, 7, 6, 5
	};

	return DE_BRUIJN_TABLE[((bits & (-*(uint64_t*)&bits)) * 0x07edd5e59a4e28c2ULL) >> 58];
}

int count_leading_zero_32(uint32_t bits)
{
	constexpr byte DE_BRUIJN_TABLE[32] =
	{
		31, 22, 30, 21, 18, 10, 29, 2, 20, 17, 15, 13, 9, 6, 28, 1,
		23, 19, 11, 3, 16, 14, 7, 24, 12, 4, 8, 25, 5, 26, 27, 0
	};

	bits |= bits >> 1;	// remains msb
	bits |= bits >> 2;
	bits |= bits >> 4;
	bits |= bits >> 8;
	bits |= bits >> 16;
	return DE_BRUIJN_TABLE[((bits * 0x07c4acddU) & 0xffffffffU) >> 27];
}

int count_leading_zero(uint64_t bits)
{
	constexpr byte DE_BRUIJN_TABLE[32] =
	{
		31, 22, 30, 21, 18, 10, 29, 2, 20, 17, 15, 13, 9, 6, 28, 1,
		23, 19, 11, 3, 16, 14, 7, 24, 12, 4, 8, 25, 5, 26, 27, 0
	};

	uint32_t hi = bits >> 32;
	return hi ? count_leading_zero_32(hi) : 32 + count_leading_zero_32((uint32_t)bits);
}

#endif

#define find_next_set(bits) find_first_set(bits &= (bits - 1))
#define foreach_bit(i, bits) for (i = find_first_set(bits); bits; i = find_next_set(bits))

