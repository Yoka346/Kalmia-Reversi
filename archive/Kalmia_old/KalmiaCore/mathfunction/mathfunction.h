#pragma once
#include "../pch.h"

namespace mathfunction
{
	inline float std_sigmoid(float x) { return 1.0f / (1.0f + expf(-x)); }
}
