using System;
using System.Collections.Generic;
using System.Text;

namespace Kalmia
{
    public static class SpanExtension
    {
        public static int Sum(this ReadOnlySpan<int> span)
        {
            var sum = 0;
            foreach (var n in span)
                sum += n;
            return sum;
        }
    }
}
