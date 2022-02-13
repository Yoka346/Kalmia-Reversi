using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;

using Kalmia.Reversi;

namespace Kalmia.MCTS
{
    internal class TranspositionTable
    {
        TTEntry[] entries;

        public TranspositionTable(ulong maxSize)
        {
            this.entries = new TTEntry[CalcTableLength(maxSize)];
        }

        public void Clear()
        {
            for (var i = 0; i < this.entries.Length; i++)
                entries[i].Node = null;
        }

        public void RegisterNode(FastBoard board, Node node)
        {
            var hashCode = board.GetHashCode();
            var idx = board.GetHashCode() & (ulong)(this.entries.Length - 1);
            this.entries[idx].Lock.Enter();
            this.entries[idx].Node = node;
            this.entries[idx].Board = board.GetBitboard();
            this.entries[idx].Lock.Exit();
        }

        public Node GetNode(FastBoard board)
        {
            var hashCode = board.GetHashCode();
            var idx = board.GetHashCode() & (ulong)(this.entries.Length - 1);

            this.entries[idx].Lock.Enter();
            if (this.entries[idx].Board == board.GetBitboard())
            {
                var node = this.entries[idx].Node;
                this.entries[idx].Lock.Exit();
                return node;
            }
            return null;
        }

        static ulong CalcTableLength(ulong maxSize)
        {
            var entrySize = Marshal.SizeOf<TTEntry>();
            var exp = Math.ILogB((double)maxSize / entrySize);
            return Enumerable.Repeat(2UL, exp).Aggregate((x, y) => x * y);
        }

        struct TTEntry
        {
            public Node Node { get; set; }
            public BoardPosition Pos { get; set; }
            public Bitboard Board { get; set; }
            public FastSpinLock Lock { get; set; }
            public bool IsUsed { get { return this.Node is not null; } }
        }
    }
}
