using System.Runtime.CompilerServices;
using System.Threading;

namespace Kalmia
{
    public static class AtomicOperations
    {
        public static void Add(ref int target, int value)
        {
            Interlocked.Add(ref target, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(ref float target, float value)
        {
            if (float.IsNaN(target) || float.IsNaN(value))
                throw new System.ArgumentException("atomic add does not support NaN.");
            float expected;
            do
                expected = target;
            while (expected != Interlocked.CompareExchange(ref target, expected + value, expected));
        }

        public static void Increment(ref int value)
        {
            Interlocked.Increment(ref value);
        }

        public static void Decrement(ref int value)
        {
            Interlocked.Decrement(ref value);
        }

        public static void Substitute(ref int target, int value)
        {
            Interlocked.Exchange(ref target, value);
        }

        public static void Substitute(ref float target, int value)
        {
            Interlocked.Exchange(ref target, value);
        }
    }
}
