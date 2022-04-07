#pragma once
#pragma warning(disable : 4146)

#include "pch.h"

#if defined(USE_BMI2) || defined(USE_X64)

#define popcount(bits) (int)__popcnt64(bits)

#elif (defined(__GNUC__) && __GNUC__ >= 4)

#define popcount(bits) (int)__builtin_popcountll(bits)

#else

#define popcount(bits) std::bitset<64>(bits).count()

#endif

#if defined(USE_BMI2) && defined(USE_X64)

#define find_first_set(bits) (int)_tzcnt_u64(bits)

#elif (defined(__GNUC__) && __GNUC__ >= 4)

#define find_first_set(bits) __builtin_ctzll(bits)

#elif defined(_MSC_VER)

inline int find_first_set(uint64_t bits)
{
	unsigned long idx;
	_BitScanForward64(&idx, bits);
	return idx;
}

#else

static constexpr byte TRAILING_ZERO_COUNT_TABLE[64] = 
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

int find_first_set(uint64_t bits)
{
	return TRAILING_ZERO_COUNT_TABLE[((bits & (-bits)) * 0x07EDD5E59A4E28C2ULL) >> 58];
}

#endif

#define foreach_bit(i, bits) for (i = find_first_set(bits); bits; i = find_first_set(bits &= (bits - 1)))

