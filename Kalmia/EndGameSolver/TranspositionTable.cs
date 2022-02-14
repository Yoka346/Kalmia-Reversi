using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Kalmia.Reversi;

namespace Kalmia.EndGameSolver
{
    struct TTEntry<T>
    {
        public T UpperBound { get; set; }
        public T LowerBound { get; set; }
        public ulong HashCode { get; set; }
        public bool IsUsed { get; set; }
    }

    class TranspositionTable<T>
    {
        TTEntry<T>[] entries;

        public TranspositionTable(ulong maxSize)
        {
            this.entries = new TTEntry<T>[CalcTableLength(maxSize)];
        }

        public void Clear()
        {
            for (var i = 0; i < this.entries.Length; i++)
                this.entries[i].IsUsed = false;
        }

        public TTEntry<T>? GetEntry(ulong hashCode)
        {
            var idx = hashCode & (ulong)(this.entries.Length - 1);
            if(this.entries[idx].IsUsed && this.entries[idx].HashCode == hashCode)
                return this.entries[idx];
            return null; 
        }

        public void SetEntry(ulong hashCode, T lowerBound, T upperBound)
        {
            var idx = hashCode & (ulong)(this.entries.Length - 1);
            this.entries[idx].LowerBound = lowerBound;
            this.entries[idx].UpperBound = upperBound;
            this.entries[idx].IsUsed = true;
        }

        static ulong CalcTableLength(ulong maxSize)
        {
            var entrySize = Unsafe.SizeOf<TTEntry<T>>();
            var exp = Math.ILogB((double)maxSize / entrySize);
            return Enumerable.Repeat(2UL, exp).Aggregate((x, y) => x * y);
        }
    }
}
