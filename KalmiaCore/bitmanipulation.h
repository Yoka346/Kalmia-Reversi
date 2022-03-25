#pragma once
#include "pch.h"

#if defined(USE_AVX2) || (defined(USE_SSE41) && defined(USE_X64))
inline int32_t popcount(int64_t bits) { return (int32_t)__popcnt64(bits); }
#else

inline int32_t popcount(int64_t bits) 
{ 
	uint64_t a = (a & 0x5555555555555555ULL) + (a >> 1 & 0x5555555555555555ULL);
	a = (a & 0x3333333333333333ULL) + (a >> 2 & 0x3333333333333333ULL);
	a = (a & 0x0f0f0f0f0f0f0f0fULL) + (a >> 4 & 0x0f0f0f0f0f0f0f0fULL);
	a = (a & 0x00ff00ff00ff00ffULL) + (a >> 8 & 0x00ff00ff00ff00ffULL);
	a = (a & 0x0000ffff0000ffffULL) + (a >> 16 & 0x0000ffff0000ffffULL);
	return (int32_t)a + (int32_t)(a >> 32);
}

#endif


