#pragma once
#include "../common.h"

#include <bit>

#ifdef USE_AVX2
#include <immintrin.h>
#elif defined(USE_SSE42)
#include <nmmintrin.h>
#elif defined(USE_SSE41)
#include <smmintrin.h>
#elif defined(USE_SSSE3)
#include <tmmintrin.h>
#elif defined(USE_SSE2)
#include <emmintrin.h>
#endif

#define find_first_set(bits) std::countr_zero(bits)
#define find_next_set(bits) find_first_set(bits &= (bits - 1))

// LSBから順に立っているビットの位置を列挙する
#define foreach_bit(i, bits) for (i = find_first_set(bits); bits; i = find_next_set(bits))

#if defined(USE_AVX2) && defined(USE_BMI2)

#define pext_32(bits, mask) _pext_u32(bits, mask)

#ifdef X64
#define pext_64(bits, mask) _pext_u64(bits, mask)
#else
#define pext_64(bits, mask) (uint64_t)(pext_32(bits >> 32, mask >> 32) << std::popcount((uint32_t)mask)) | (uint64_t)pext_32((uint32_t)bits, (uint32_t)mask)
#endif

#else

/**
* @fn
* @brief    PEXT命令のソフトウェアエミュレーションコード
* @detail   YaneuraOuのbitop.h内で実装されていたPEXT命令のソフトウェアエミュレーションより引用.
* @sa       https://github.com/yaneurao/YaneuraOu/blob/599378d420fa9a8cdae9b1b816615313d41ccf6e/source/extra/bitop.h#L93
**/
inline uint64_t pext(uint64_t bits, uint64_t mask)
{
    uint64_t res = 0;
    for (uint64_t bb = 1; mask; bb += bb)
    {
        if ((int64_t)bits & (int64_t)mask & -(int64_t)mask)
            res |= bb;
        mask &= mask - 1;
    }
    return res;
}

#define pext_32(bits, mask) (uint32_t)pext(bits, mask)
#define pext_64(bits, mask) pext(bits, mask)

#endif
