#pragma once
#include "../pch.h"

#ifdef USE_AVX2

union Vector256
{
	byte element_byte[32];
	uint64_t element_uint64[4];
	__m256i data;
};

#elif defined(USE_SSE41) 

union Vector128
{
	byte element_byte[16];
	uint64_t element_uint64[2];
	__m128i data;
};

#endif
