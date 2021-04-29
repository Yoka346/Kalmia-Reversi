using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

using static Kalmia.BitManipulations;
using static Kalmia.Board;

namespace Kalmia
{
    public static class FlipTable
    {
        static readonly byte[,] OUTFLANK_TABLE = new byte[LINE_LENGTH, 256];
        static readonly byte[,] FLIP_TABLE = new byte[LINE_LENGTH, 256];

        static FlipTable()
        {
            CreateOutflankTable();
            CreateFlipTable();
        }

        static void CreateOutflankTable()
        {
            byte calcOutflank(byte pos, byte o) { return (byte)((pos << 1) + o); }
            for (var x = 0; x < OUTFLANK_TABLE.GetLength(0); x++)
                for (var o = 0; o < OUTFLANK_TABLE.GetLength(1); o++)
                {
                    byte outflank = 0;
                    var pos = (byte)(1 << x);
                    outflank |= calcOutflank(pos, (byte)o);
                    outflank |= Reverse(calcOutflank(Reverse(pos), Reverse((byte)o)));
                    OUTFLANK_TABLE[x, o] = outflank;
                }
        }

        static void CreateFlipTable()
        {
            for(var x = 0; x < FLIP_TABLE.GetLength(0); x++)
                for(var outflank = 1; outflank < OUTFLANK_TABLE.GetLength(1); outflank++)
                {
                    byte flipped = 0;
                    var mask0 = 1;
                    for(var i = 0; i < LINE_LENGTH; i++)
                    {
                        if((outflank & mask0) != 0)
                        {
                            var mask1 = (byte)(1 << i);
                            if(i < x)
                                for(var j = i + 1; j < x; j++)
                                {
                                    mask1 <<= 1;
                                    flipped |= mask1;
                                }
                            else
                                for (var j = i - 1; j > x; j--)
                                {
                                    mask1 >>= 1;
                                    flipped |= mask1;
                                }
                        }
                        mask0 <<= 1;
                    }
                    FLIP_TABLE[x, outflank] = flipped;
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetFlippedPattern(int linePos, byte currentPlayerBoardLine, byte opponentPlayerBoardLine)
        {
            var outflank = OUTFLANK_TABLE[linePos, opponentPlayerBoardLine] & currentPlayerBoardLine;
            return FLIP_TABLE[linePos, outflank];
        }
    }
}
