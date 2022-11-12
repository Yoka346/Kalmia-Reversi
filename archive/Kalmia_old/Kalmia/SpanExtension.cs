using System;

namespace Kalmia
{
    public static class SpanExtension
    {
        public static int Sum(this Span<int> span)
        {
            var sum = 0;
            foreach (var n in span)
                sum += n;
            return sum;
        }

        public static int Sum(this ReadOnlySpan<int> span)
        {
            var sum = 0;
            foreach (var n in span)
                sum += n;
            return sum;
        }
    }
}
