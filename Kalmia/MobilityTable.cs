using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.CompilerServices;

using static Kalmia.BitManipulations;

namespace Kalmia
{
    public static class MobilityTable
    {
        static readonly byte[,] MOBILITY_TABLE = new byte[256, 256];

        static MobilityTable()
        {
            // Calculate mobility of all patterns of bit.
            static byte calcMobility(byte p, byte o) { var shiftedP = p << 1; return (byte)(~(shiftedP | p | o) & (shiftedP + o)); }
            for (var p = 0; p < MOBILITY_TABLE.GetLength(0); p++)
                for (var o = 0; o < MOBILITY_TABLE.GetLength(1); o++)
                {
                    MOBILITY_TABLE[p, o] = calcMobility((byte)p, (byte)o);
                    MOBILITY_TABLE[p, o] |= Reverse(calcMobility(Reverse((byte)p), Reverse((byte)o)));
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetMobilityPattern(byte currentPlayerBoardLine, byte opponentPlayerBoardLine)
        {
            return MOBILITY_TABLE[currentPlayerBoardLine, opponentPlayerBoardLine];
        }
    }
}
