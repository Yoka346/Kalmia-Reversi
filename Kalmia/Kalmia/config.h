/*
* このファイルには, すべてのファイルで共通で用いるマクロ定数などを定義する.
*/

#pragma once

// CPUのバス幅が64bitの場合にdefineする. これがdefineされていないときは, 32bit CPUとみなす.
#define USE_X64

// 用いる命令セットのみdefineする. ある命令セットがdefineされたら, それより古いものは自動的にdefineされる.
//#define USE_AVX2
//#define USE_SSE42
//#define USE_SSE41
//#define USE_SSSE3
//#define USE_SSE2

// BMI2を用いる場合にdefineする.
#define USE_BMI2

/*
* 以下, 書き換え不要
*/

// defineされた命令セットよりも古い命令セットを全てdefineする.
#ifdef USE_AVX2
#define USE_SSE42
#endif

#ifdef USE_SSE42
#define USE_SSE41
#endif

#ifdef USE_SSE41
#define USE_SSSE3
#endif

#ifdef USE_SSSE3
#define USE_SSE2
#endif

// 強制インライン化に関するマクロ
#if defined(_MSC_VER) && !defined(__INTEL_COMPILER)
#define FORCE_INLINE __forceinline
#elif defined(__INTEL_COMPILER)
#define FORCE_INLINE inline
#elif defined(__GNUC__)
#define FORCE_INLINE __attribute__((always_inline)) inline
#else
#define FORCE_INLINE inline
#endif