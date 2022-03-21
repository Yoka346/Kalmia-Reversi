#pragma once
#include "pch.h"

#if defined(USE_AVX2) || defined(USE_SSE41)
inline int32_t popcount(int64_t bits) { return (int32_t)__popcnt64(bits); }
#else
inline int32_t popcount(int64_t bits) 
{ 
	a = (a & UINT64_C(0x5555555555555555)) + (a >> 1 & UINT64_C(0x5555555555555555));
	a = (a & UINT64_C(0x3333333333333333)) + (a >> 2 & UINT64_C(0x3333333333333333));
	a = (a & UINT64_C(0x0f0f0f0f0f0f0f0f)) + (a >> 4 & UINT64_C(0x0f0f0f0f0f0f0f0f));
	a = (a & UINT64_C(0x00ff00ff00ff00ff)) + (a >> 8 & UINT64_C(0x00ff00ff00ff00ff));
	a = (a & UINT64_C(0x0000ffff0000ffff)) + (a >> 16 & UINT64_C(0x0000ffff0000ffff));
	return (int32_t)a + (int32_t)(a >> 32);
}
#endif
