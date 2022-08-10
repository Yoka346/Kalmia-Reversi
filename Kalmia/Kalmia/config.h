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
#elif defined(USE_SSE42)
#define USE_SSE41
#elif USE_SSE41
#define USE_SSSE3
#elif USE_SSSE3
#define USE_SSE2
#endif

