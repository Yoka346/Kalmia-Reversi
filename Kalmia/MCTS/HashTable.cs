using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Kalmia.Evaluation;

namespace Kalmia.MCTS
{
    public struct HashTableItem
    {
        public int EmptyCount;
        public float BoardValue;

#if DEBUG
        public BoardFeature Feature;
#endif

#if RELEASE
        public HashTableItem(int emptyCount, float value)
        {
            this.EmptyCount = emptyCount;
            this.Value = value;
        }

        public void Init(int emptyCount, float value)
        {
            this.EmptyCount = emptyCount;
            this.Value = value;
        }
#endif

#if DEBUG
        public HashTableItem(BoardFeature feature, int emptyCount, float value)
        {
            this.Feature = new BoardFeature(feature);
            this.EmptyCount = emptyCount;
            this.BoardValue = value;
        }

        public void Init(BoardFeature feature, int emptyCount, float value)
        {
            this.Feature = new BoardFeature(feature);
            this.EmptyCount = emptyCount;
            this.BoardValue = value;
        }
#endif
    }

    public class HashTable
    {
        const ulong BASE = 17UL;
        const ulong BASE_POW_2 = BASE * BASE;
        const ulong BASE_POW_3 = BASE_POW_2 * BASE;
        const ulong BASE_POW_4 = BASE_POW_3 * BASE;
        const ulong BASE_POW_5 = BASE_POW_4 * BASE;
        const ulong BASE_POW_6 = BASE_POW_5 * BASE;
        const ulong BASE_POW_7 = BASE_POW_6 * BASE;

        readonly int TABLE_SIZE;
        HashTableItem[][] table;

#if DEBUG
        public int HashClashCount { get; private set; } = 0;
#endif

        public HashTable(int tableSize)
        {
            if (tableSize % (int)BASE == 0)
                throw new ArgumentException($"The size of hash table cannnot be multiple of BASE(={BASE}).");

            this.TABLE_SIZE = tableSize;
            this.table = new HashTableItem[2][];
            this.table[0] = new HashTableItem[this.TABLE_SIZE];
            this.table[1] = new HashTableItem[this.TABLE_SIZE];

            for (var i = 0; i < 2; i++)
                for (var j = 0; j < this.table[i].Length; j++)
                    this.table[i][j].BoardValue = float.NaN;
        }

        public void Add(ulong hash, BoardFeature feature, float value)
        {
#if DEBUG
            this.table[(int)feature.SideToMove][hash].Init(feature, feature.EmptyCount, value);
#elif RELEASE
            this.table[(int)feature.SideToMove][hash].Init(feature.EmptyCount, value);
#endif
        }

        public HashTableItem? GetItem(BoardFeature feature, out ulong hashCode)
        {
            hashCode = CalcHash(feature);
            var item = this.table[(int)feature.SideToMove][hashCode];
            if (float.IsNaN(item.BoardValue))
                return null;

#if DEBUG
            if (!feature.Equals(item.Feature))
                this.HashClashCount++;
#endif

            return item;
        }

        ulong CalcHash(BoardFeature feature)      // Calculates hash code by rolling hash.
        {
            var hash = (ulong)feature.Features[8];
            hash += (ulong)feature.Features[9] * BASE;
            hash += (ulong)feature.Features[16] * BASE_POW_2;
            hash += (ulong)feature.Features[17] * BASE_POW_3;
            hash += (ulong)feature.Features[20] * BASE_POW_4;
            hash += (ulong)feature.Features[21] * BASE_POW_5;
            hash += (ulong)feature.Features[24] * BASE_POW_6;
            hash += (ulong)feature.Features[25] * BASE_POW_7;
            return hash % (ulong)this.TABLE_SIZE;
        }
    }
}
