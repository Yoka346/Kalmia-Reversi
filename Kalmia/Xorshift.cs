using System;

namespace Kalmia
{
    public class Xorshift
    {
        static readonly Xorshift SEED_GENERATOR = new Xorshift((uint)Environment.TickCount);
        static readonly object LOCK_OBJ = new object();

        uint y;

        public Xorshift()
        {
            lock (LOCK_OBJ)
                this.y = SEED_GENERATOR.Next();
        }

        public Xorshift(uint seed)
        {
            this.y = seed;
        }

        public uint Next()
        {
            this.y ^= this.y << 13;
            this.y ^= this.y >> 17;
            this.y ^= this.y << 15;
            return this.y;
        }

        public uint Next(uint maxValue)
        {
            return Next() / (uint.MaxValue / maxValue);
        }

        public ulong Next(uint minValue, uint maxValue)
        {
            return Next(maxValue - minValue) + minValue;
        }

        public float NextFloat()
        {
            return (float)Next() / uint.MaxValue;
        }

        public void Shuffle<T>(T[] array)
        {
            var n = (uint)array.Length;
            while (n > 1u)
            {
                n--;
                var k = this.Next(n + 1u);
                var tmp = array[k];
                array[k] = array[n];
                array[n] = tmp;
            }
        }
    }
}
