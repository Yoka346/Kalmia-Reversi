using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Kalmia
{
    internal static class VectorOperator
    {
        public static float Dot(ref Vector256<float> left, ref Vector256<float> right)
        {
            var product = Avx.Multiply(left, right);
            var productHi = Avx.ExtractVector128(product, 1);
            var productLow = Avx.ExtractVector128(product, 0);
            var quadSum = Sse.Add(productLow, productHi);
            var dualSumLow = quadSum;
            var dualSumHi = Sse.MoveHighToLow(quadSum, quadSum);
            var dualSum = Sse.Add(dualSumLow, dualSumHi);
            var sumLow = dualSum;
            var sumHi = Sse.Shuffle(dualSum, dualSum, 0x1);
            return Sse.Add(sumLow, sumHi).GetElement(0);
        }

        public static float Nrm(ref Vector256<float> vec)
        {
            return MathF.Sqrt(Dot(ref vec, ref vec));
        }
    }
}
