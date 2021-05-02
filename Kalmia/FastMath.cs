using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

namespace Kalmia
{
    // I refered to this code. https://github.com/LeelaChessZero/lc0/blob/master/src/utils/fastmath.h
    public static class FastMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float Log2(float x)
        {
            var tmp = *(uint*)&x;
            var expb = tmp >> 23;
            tmp = (tmp & 0x7fffff) | (0x7f << 23);
            var output = *(float*)&tmp;
            output -= 1.0f;
            return output *(1.3465552f - 0.34655523f * output) -127 + expb;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float Log(float x)
        {
            return 0.6931471805599453f * Log2(x);
        }
    }
}
