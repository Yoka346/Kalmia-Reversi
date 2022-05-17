#ifndef PCH_H
#define PCH_H

#include "config.h"

#include <stddef.h>
#include <iostream>
#include <string>
#include <sstream>
#include <random>
#include <iterator>
#include <algorithm>
#include <bitset>
#include <stdio.h>
#include <assert.h>

#if defined(USE_AVX2) || defined(USE_SSE41) || defined(USE_SSE42) || defined(USE_BMI1) || defined(USE_BMI2)

#include <intrin.h>
#include <immintrin.h>

#endif

#include "initialize_callback.h"
#include "constantarray.h"
#include "fastmath/fastmath.h"
#include "arraymanipulation/arraymanipulation.h"

typedef unsigned char byte;

#endif //PCH_H

