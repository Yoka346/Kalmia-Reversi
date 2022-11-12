using System;

namespace Kalmia
{
    public static class RandomExtension
    {
        public static void Shuffle<T>(this Random rand, T[] array)
        {
            var n = array.Length;
            while (n > 1u)
            {
                n--;
                var k = rand.Next(n + 1);
                var tmp = array[k];
                array[k] = array[n];
                array[n] = tmp;
            }
        }
    }
}
