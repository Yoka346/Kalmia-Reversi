using System;
using System.Linq;
using System.Runtime.Intrinsics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Kalmia;

namespace KalmiaTest
{
    [TestClass]
    public class BitManipulations_Test
    {
        [TestMethod]
        public void Reverse_Test()
        {
            byte bits = 0b11010110; // test data
            byte revBits = 0b01101011;  // accurate result
            Assert.AreEqual(revBits, BitManipulations.BitSwap(bits));
        }

        [TestMethod]
        public void ByteSwap_Test()
        {
            var bits = 1728371736416UL; // test data
            Assert.AreEqual(ByteSwap(bits), BitManipulations.ByteSwap(bits));

            var bits128 = Vector128.Create(12717298721UL, bits);    // test data
            Assert.AreEqual(ByteSwap(bits128), BitManipulations.ByteSwap(bits128));

            var bits256 = Vector256.Create(Vector128.Create(121718731289UL, 42352523525UL), bits128);    // test data
            Assert.AreEqual(ByteSwap(bits256), BitManipulations.ByteSwap(bits256));
        }

        [TestMethod]
        public void MirrorByte_Test()
        {
            var bits = 0xaf34b90dcf45e467UL;    // test data
            Assert.AreEqual(MirrorByte(bits), BitManipulations.MirrorByte(bits));

            var bits128 = Vector128.Create(0x656bb052e1cecef1UL, bits);     // test data
            Assert.AreEqual(MirrorByte(bits128), BitManipulations.MirrorByte(bits128));

            var bits256 = Vector256.Create(Vector128.Create(0x4e3cd7be8f25cfb8UL, 0x2518a7d5e6c923a6UL), bits128);      // test data
            Assert.AreEqual(MirrorByte(bits256), BitManipulations.MirrorByte(bits256));
        }

        [TestMethod]
        public void Transpose_Test()
        {
            var bitboard = 0xaf34b90dcf45e467UL;    // test data
            Assert.AreEqual(Transpose(bitboard), BitManipulations.Transpose(bitboard));

            var bitboard128 = Vector128.Create(0x656bb052e1cecef1UL, bitboard);     // test data
            Assert.AreEqual(Transpose(bitboard128), BitManipulations.Transpose(bitboard128));

            var bitboard256 = Vector256.Create(Vector128.Create(0x4e3cd7be8f25cfb8UL, 0x2518a7d5e6c923a6UL), bitboard128);      // test data
            Assert.AreEqual(Transpose(bitboard256), BitManipulations.Transpose(bitboard256));
        }

        [TestMethod]
        public void Rotate90Clockwise_Test()
        {
            var bitboard = 0xaf34b90dcf45e467UL;    // test data
            Assert.AreEqual(Rotate90Clockwise(bitboard), BitManipulations.Rotate90Clockwise(bitboard));

            var bitboard128 = Vector128.Create(0x656bb052e1cecef1UL, bitboard);     // test data
            Assert.AreEqual(Rotate90Clockwise(bitboard128), BitManipulations.Rotate90Clockwise(bitboard128));

            var bitboard256 = Vector256.Create(Vector128.Create(0x4e3cd7be8f25cfb8UL, 0x2518a7d5e6c923a6UL), bitboard128);      // test data
            Assert.AreEqual(Rotate90Clockwise(bitboard256), BitManipulations.Rotate90Clockwise(bitboard256));
        }

        [TestMethod]
        public void Rotate90AntiClockwise_Test()
        {
            var bitboard = 0xaf34b90dcf45e467UL;    // test data
            Assert.AreEqual(Rotate90AntiClockwise(bitboard), BitManipulations.Rotate90AntiClockwise(bitboard));

            var bitboard128 = Vector128.Create(0x656bb052e1cecef1UL, bitboard);     // test data
            Assert.AreEqual(Rotate90AntiClockwise(bitboard128), BitManipulations.Rotate90AntiClockwise(bitboard128));

            var bitboard256 = Vector256.Create(Vector128.Create(0x4e3cd7be8f25cfb8UL, 0x2518a7d5e6c923a6UL), bitboard128);      // test data
            Assert.AreEqual(Rotate90AntiClockwise(bitboard256), BitManipulations.Rotate90AntiClockwise(bitboard256));
        }

        ulong ByteSwap(ulong bits)
        {
            var bytes = BitConverter.GetBytes(bits);
            return BitConverter.ToUInt64(bytes.Reverse().ToArray());
        }

        Vector128<ulong> ByteSwap(Vector128<ulong> bits)
        {
            return Vector128.Create(ByteSwap(bits.GetElement(0)), ByteSwap(bits.GetElement(1)));
        }

        Vector256<ulong> ByteSwap(Vector256<ulong> bits)
        {
            return Vector256.Create(ByteSwap(bits.GetLower()), ByteSwap(bits.GetUpper()));
        }

        ulong MirrorByte(ulong bits)
        {
            var bytes = BitConverter.GetBytes(bits);
            var ret = new byte[sizeof(ulong)];
            for(var i = 0; i < bytes.Length; i++)
            {
                byte b = 0;
                byte mask0 = 1;
                byte mask1 = 128;
                for (var j = 0; j < 8; j++)
                {
                    if ((bytes[i] & mask0) != 0)
                        b |= mask1;
                    mask0 <<= 1;
                    mask1 >>= 1;
                }
                ret[i] = b;
            }
            return BitConverter.ToUInt64(ret);
        }

        Vector128<ulong> MirrorByte(Vector128<ulong> bits)
        {
            return Vector128.Create(MirrorByte(bits.GetElement(0)), 
                                    MirrorByte(bits.GetElement(1)));
        }

        Vector256<ulong> MirrorByte(Vector256<ulong> bits)
        {
            return Vector256.Create(MirrorByte(bits.GetLower()),
                                    MirrorByte(bits.GetUpper()));
        }

        ulong Transpose(ulong bitboard)
        {
            var ret = 0UL;
            for(var i = 0; i < 8; i++)
                for(var j = 0; j < 8; j++)
                {
                    var mask = 1UL << (i + j * 8);
                    mask &= bitboard;
                    mask >>= (i + j * 8);
                    ret |= mask << (i * 8 + j);
                }
            return ret;
        }

        Vector128<ulong> Transpose(Vector128<ulong> bitboard)
        {
            return Vector128.Create(Transpose(bitboard.GetElement(0)), Transpose(bitboard.GetElement(1)));
        }

        Vector256<ulong> Transpose(Vector256<ulong> bitboard)
        {
            return Vector256.Create(Transpose(bitboard.GetLower()), Transpose(bitboard.GetUpper()));
        }

        ulong Rotate90Clockwise(ulong bitboard)
        {
            var ret = 0UL;
            for (var i = 0; i < 8; i++)
                for (var j = 0; j < 8; j++)
                {
                    var mask = 1UL << (i + j * 8);
                    mask &= bitboard;
                    mask >>= (i + j * 8);
                    ret |= mask << (i * 8 + (7 - j));
                }
            return ret;
        }

        Vector128<ulong> Rotate90Clockwise(Vector128<ulong> bitboard)
        {
            return Vector128.Create(Rotate90Clockwise(bitboard.GetElement(0)), Rotate90Clockwise(bitboard.GetElement(1)));
        }

        Vector256<ulong> Rotate90Clockwise(Vector256<ulong> bitboard)
        {
            return Vector256.Create(Rotate90Clockwise(bitboard.GetLower()), Rotate90Clockwise(bitboard.GetUpper()));
        }

        ulong Rotate90AntiClockwise(ulong bitboard)
        {
            var ret = 0UL;
            for (var i = 0; i < 8; i++)
                for (var j = 0; j < 8; j++)
                {
                    var mask = 1UL << (i + j * 8);
                    mask &= bitboard;
                    mask >>= (i + j * 8);
                    ret |= mask << (j + (7 - i) * 8);
                }
            return ret;
        }

        Vector128<ulong> Rotate90AntiClockwise(Vector128<ulong> bitboard)
        {
            return Vector128.Create(Rotate90AntiClockwise(bitboard.GetElement(0)), Rotate90AntiClockwise(bitboard.GetElement(1)));
        }

        Vector256<ulong> Rotate90AntiClockwise(Vector256<ulong> bitboard)
        {
            return Vector256.Create(Rotate90AntiClockwise(bitboard.GetLower()), Rotate90AntiClockwise(bitboard.GetUpper()));
        }

        ulong PseudoRotate45Clockwise(ulong bitboard)
        {
            var table = new int[]
            {
                0, 57, 50, 43, 36, 29, 22, 15,
                8, 1, 58, 51, 44, 37, 30, 23,
               16, 9, 2, 59, 52, 45, 38, 31,
               24, 17, 10, 3, 60, 53, 46, 39,
               32, 25, 18, 11, 4, 61, 54, 47,
               40, 33, 26, 19, 12, 5, 62, 55,
               48, 41, 34, 27, 20, 13, 6, 63,
               56, 49, 42, 35, 28, 21, 14, 7
            };

            var ret = 0UL;
            for(var i =0; i < table.Length; i++)
            {
                if ((bitboard & (1UL << table[i])) != 0)
                    ret |= 1UL << i;
            }
            return ret;
        }

        Vector128<ulong> PseudoRotate45Clockwise(Vector128<ulong> bitboard)
        {
            return Vector128.Create(PseudoRotate45Clockwise(bitboard.GetElement(0)), PseudoRotate45Clockwise(bitboard.GetElement(1)));
        }

        Vector256<ulong> PseudoRotate45Clockwise(Vector256<ulong> bitboard)
        {
            return Vector256.Create(PseudoRotate45Clockwise(bitboard.GetLower()), PseudoRotate45Clockwise(bitboard.GetUpper()));
        }

        ulong PseudoRotate45AntiClockwise(ulong bitboard)
        {
            var table = new int[]
            {
                8, 17, 26, 35, 44, 53, 62, 7,
               16, 25, 34, 43, 52, 61, 6, 15,
               24, 33, 42, 51, 60, 5, 14, 19,
               32, 41, 50, 59, 4, 13, 22, 31,
               40, 49, 58, 3, 12, 21, 30, 39,
               48, 57, 2, 11, 20, 29, 38, 47,
               56, 1, 10, 19, 28, 37, 46, 55,
                0, 9, 18, 27, 36, 45, 54, 63
            };

            var ret = 0UL;
            for (var i = 0; i < table.Length; i++)
            {
                if ((bitboard & (1UL << table[i])) != 0)
                    ret |= 1UL << i;
            }
            return ret;
        }

        Vector128<ulong> PseudoRotate45AntiClockwise(Vector128<ulong> bitboard)
        {
            return Vector128.Create(PseudoRotate45AntiClockwise(bitboard.GetElement(0)), PseudoRotate45AntiClockwise(bitboard.GetElement(1)));
        }

        Vector256<ulong> PseudoRotate45AntiClockwise(Vector256<ulong> bitboard)
        {
            return Vector256.Create(PseudoRotate45AntiClockwise(bitboard.GetLower()), PseudoRotate45AntiClockwise(bitboard.GetUpper()));
        }
    }
}
