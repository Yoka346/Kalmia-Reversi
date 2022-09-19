/*
* ���̃t�@�C���ɂ�, ���ׂẴt�@�C���ŋ��ʂŗp����}�N���萔�Ȃǂ��`����.
*/

#pragma once

// CPU�̃o�X����64bit�̏ꍇ��define����. ���ꂪdefine����Ă��Ȃ��Ƃ���, 32bit CPU�Ƃ݂Ȃ�.
#define USE_X64

// �p���閽�߃Z�b�g�̂�define����. ���閽�߃Z�b�g��define���ꂽ��, ������Â����͎̂����I��define�����.
//#define USE_AVX2
//#define USE_SSE42
//#define USE_SSE41
//#define USE_SSSE3
//#define USE_SSE2

// BMI2��p����ꍇ��define����.
#define USE_BMI2

/*
* �ȉ�, ���������s�v
*/

// define���ꂽ���߃Z�b�g�����Â����߃Z�b�g��S��define����.
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

// �����C�����C�����Ɋւ���}�N��
#if defined(_MSC_VER) && !defined(__INTEL_COMPILER)
#define FORCE_INLINE __forceinline
#elif defined(__INTEL_COMPILER)
#define FORCE_INLINE inline
#elif defined(__GNUC__)
#define FORCE_INLINE __attribute__((always_inline)) inline
#else
#define FORCE_INLINE inline
#endif