using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            Assert.AreEqual(BitManipulations.Reverse(bits), revBits);
        }

        [TestMethod]
        public void ByteSwap_Test()
        {
            var bits = 1728371736416UL; // test data
            Assert.AreEqual(BitManipulations.ByteSwap64(bits), ByteSwap(bits));

            var bits128 = Vector128.Create(12717298721UL, bits);    // test data
            Assert.AreEqual(BitManipulations.ByteSwap64(bits128), ByteSwap(bits128));

            var bits256 = Vector256.Create(Vector128.Create(121718731289UL, 42352523525UL), bits128);    // test data
            Assert.AreEqual(BitManipulations.ByteSwap64(bits256), ByteSwap(bits256));
        }

        [TestMethod]
        public void MirrorByte_Test()
        {
            var bits = 0xaf34b90dcf45e467UL;    // test data
            Assert.AreEqual(BitManipulations.MirrorByte(bits), MirrorByte(bits));

            var bits128 = Vector128.Create(0x656bb052e1cecef1UL, bits);     // test data
            Assert.AreEqual(BitManipulations.MirrorByte(bits128), MirrorByte(bits128));

            var bits256 = Vector256.Create(Vector128.Create(0x4e3cd7be8f25cfb8UL, 0x2518a7d5e6c923a6UL), bits128);      // test data
            Assert.AreEqual(BitManipulations.MirrorByte(bits256), MirrorByte(bits256));
        }

        [TestMethod]
        public void Transpose_Test()
        {
            var bitboard = 0xaf34b90dcf45e467UL;    // test data
            Assert.AreEqual(BitManipulations.Transpose(bitboard), Transpose(bitboard));

            var bitboard128 = Vector128.Create(0x656bb052e1cecef1UL, bitboard);     // test data
            Assert.AreEqual(BitManipulations.Transpose(bitboard128), Transpose(bitboard128));

            var bitboard256 = Vector256.Create(Vector128.Create(0x4e3cd7be8f25cfb8UL, 0x2518a7d5e6c923a6UL), bitboard128);      // test data
            Assert.AreEqual(BitManipulations.Transpose(bitboard256), Transpose(bitboard256));
        }

        [TestMethod]
        public void Rotate90Clockwise_Test()
        {
            var bitboard = 0xaf34b90dcf45e467UL;    // test data
            Assert.AreEqual(BitManipulations.Rotate90Clockwise(bitboard), Rotate90Clockwise(bitboard));

            var bitboard128 = Vector128.Create(0x656bb052e1cecef1UL, bitboard);     // test data
            Assert.AreEqual(BitManipulations.Rotate90Clockwise(bitboard128), Rotate90Clockwise(bitboard128));

            var bitboard256 = Vector256.Create(Vector128.Create(0x4e3cd7be8f25cfb8UL, 0x2518a7d5e6c923a6UL), bitboard128);      // test data
            Assert.AreEqual(BitManipulations.Rotate90Clockwise(bitboard256), Rotate90Clockwise(bitboard256));
        }

        [TestMethod]
        public void Rotate90AntiClockwise_Test()
        {
            var bitboard = 0xaf34b90dcf45e467UL;    // test data
            Assert.AreEqual(BitManipulations.Rotate90AntiClockwise(bitboard), Rotate90AntiClockwise(bitboard));

            var bitboard128 = Vector128.Create(0x656bb052e1cecef1UL, bitboard);     // test data
            Assert.AreEqual(BitManipulations.Rotate90AntiClockwise(bitboard128), Rotate90AntiClockwise(bitboard128));

            var bitboard256 = Vector256.Create(Vector128.Create(0x4e3cd7be8f25cfb8UL, 0x2518a7d5e6c923a6UL), bitboard128);      // test data
            Assert.AreEqual(BitManipulations.Rotate90AntiClockwise(bitboard256), Rotate90AntiClockwise(bitboard256));
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
    }
}
