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
#elif defined(USE_SSE42)
#define USE_SSE41
#elif USE_SSE41
#define USE_SSSE3
#elif USE_SSSE3
#define USE_SSE2
#endif

