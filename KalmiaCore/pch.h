#ifndef PCH_H
#define PCH_H

#include "config.h"

#include <iostream>
#include <random>
#include <iterator>
#include <stdio.h>

#if defined(USE_AVX2) || defined(USE_SSE41) || defined(USE_SSE2)

#include <intrin.h>
#include <immintrin.h>

#endif

#include "initialize_callback.h"

typedef unsigned char byte;

#endif //PCH_H

