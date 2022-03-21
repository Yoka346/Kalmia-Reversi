#ifndef PCH_H
#define PCH_H

#include "config.h"

#include <iostream>
#include <random>
#include <iterator>
#include <stdio.h>

#if defined(USE_AVX2) || defined(USE_SSE41)

#include <intrin.h>
#include <immintrin.h>

#endif

#include "initialize_callback.h"

#endif //PCH_H
