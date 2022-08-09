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

#define FIND_FIRST_SET(bits) std::countr_zero(bits)
#define FIND_NEXT_SET(bits) FIND_FIRST_SET(bits &= (bits - 1))

// LSBから順に立っているビットの位置を列挙する
#define FOREACH_BIT(i, bits) for (i = FIND_FIRST_SET(bits); bits; i = FIND_NEXT_SET(bits))

#if defined(USE_AVX2) && defined(USE_BMI2)

#define PEXT_32(bits, mask) _pext_u32(bits, mask)

#ifdef USE_X64
#define PEXT_64(bits, mask) _pext_u64(bits, mask)
#else
#define PEXT_64(bits, mask) (uint64_t)(PEXT_32(bits >> 32, mask >> 32) << std::popcount((uint32_t)mask)) | (uint64_t)PEXT_32((uint32_t)bits, (uint32_t)mask)
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

#define PEXT_32(bits, mask) (uint32_t)pext(bits, mask)
#define PEXT_64(bits, mask) pext(bits, mask)

#endif

#ifdef _MSC_VER
#define BYTE_SWAP_64(bits) _byteswap_uint64(bits)
#elif defined(__GNUC__)
#define BYTE_SWAP_64(bits) __builtin_bswap64(bits)
#else

inline uint64_t byte_swap_64(uint64_t bits)
{
    uint64_t swapped = (bits >> 32) | (bits << 32);
    swapped = ((swapped & 0xffff0000ffff0000ULL) >> 16) | ((swapped & 0x0000ffff0000ffffULL) << 16);
    swapped = ((swapped & 0xff00ff00ff00ff00ULL) >> 8) | ((swapped & 0x00ff00ff00ff00ffULL) << 8);
    return swapped;
}

#define BYTE_SWAP_64(bits) byte_swap_64(bits)

#endif
