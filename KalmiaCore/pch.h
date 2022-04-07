#ifndef PCH_H
#define PCH_H

#include "config.h"

#include <iostream>
#include <string>
#include <sstream>
#include <random>
#include <iterator>
#include <bitset>
#include <stdio.h>

#if defined(USE_AVX2) || defined(USE_SSE41) 

#include <intrin.h>
#include <immintrin.h>

#endif

#include "initialize_callback.h"

typedef unsigned char byte;

#endif //PCH_H

