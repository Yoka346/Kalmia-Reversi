using System;
using System.Runtime.CompilerServices;

namespace Kalmia
{
    // I refered to Log2 and Log from https://github.com/LeelaChessZero/lc0/blob/master/src/utils/fastmath.h
    public static class FastMath
    {
        static readonly int[] POW3_TABLE;

        static FastMath()
        {
            POW3_TABLE = new int[10];
            var pow3 = 1;
            for (var i = 0; i < POW3_TABLE.Length; i++)
            {
                POW3_TABLE[i] = pow3;
                pow3 *= 3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float Exp2(float x)
        {
            int exp;
            if (x < 0)
            {
                if (x < -126)
                    return 0.0f;
                exp = (int)(x - 1);
            }
            else
                exp = (int)x;

            float output = x - exp;
            output = 1.0f + output *(0.6602339f + 0.33976606f * output);
            var tmp = *(int*)(&output);
            tmp += (int)((uint)exp << 23);
            return *(float*)(&tmp);
        }

        public static unsafe float Exp(float x)
        {
            return Exp2(1.442695040f * x);
        }

        public static int Pow3(int n)
        {
            return POW3_TABLE[n];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float Log2(float x)
        {
            var tmp = *(uint*)&x;
            var expb = tmp >> 23;
            tmp = (tmp & 0x7fffff) | (0x7f << 23);
            var output = *(float*)&tmp;
            output -= 1.0f;
            return output * (1.3465552f - 0.34655523f * output) - 127 + expb;
        }

        public static float Log(float x)
        {
            return 0.6931471805599453f * Log2(x);
        }
    }
}
