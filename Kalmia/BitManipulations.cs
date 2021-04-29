using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

namespace Kalmia
{
    public static class BitManipulations
    {
        static readonly Vector128<byte> BYTE_SWAP_SHUFFLE_TABLE_128;
        static readonly Vector256<byte> BYTE_SWAP_SHUFFLE_TABLE_256;

        static BitManipulations()
        {
            BYTE_SWAP_SHUFFLE_TABLE_128 = Vector128.Create(7, 6, 5, 4, 3, 2, 1, 0, 
                                                          15, 14, 13, 12, 11, 10, 9, 8).AsByte();

            BYTE_SWAP_SHUFFLE_TABLE_256 = Vector256.Create(7, 6, 5, 4, 3, 2, 1, 0,
                                                          15, 14, 13, 12, 11, 10, 9, 8,
                                                          23, 22, 21, 20, 19, 18, 17, 16,
                                                          31, 30, 29, 28, 27, 26, 25, 24).AsByte();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Reverse(byte bits)
        {
            int n = bits;
            n = (n & 0x55) << 1 | (n >> 1 & 0x55);
            n = (n & 0x33) << 2 | (n >> 2 & 0x33);
            n = (n & 0x0f) << 4 | (n >> 4 & 0x0f);
            return (byte)n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ByteSwap64(ulong bits)
        {
            var ret = bits << 56;
            ret |= (bits & 0x000000000000ff00) << 40;
            ret |= (bits & 0x0000000000ff0000) << 24;
            ret |= (bits & 0x00000000ff000000) << 8;
            ret |= (bits & 0x000000ff00000000) >> 8;
            ret |= (bits & 0x0000ff0000000000) >> 24;
            ret |= (bits & 0x00ff000000000000) >> 40;
            return ret | (bits >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> ByteSwap64(Vector128<ulong> bits)
        {
            if (Ssse3.IsSupported)
                return Ssse3.Shuffle(bits.AsByte(), BYTE_SWAP_SHUFFLE_TABLE_128).AsUInt64();
            else
                return Vector128.Create(ByteSwap64(bits.GetElement(0)), ByteSwap64(bits.GetElement(1)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> ByteSwap64(Vector256<ulong> bits)
        {
            if (Avx2.IsSupported)
                return Avx2.Shuffle(bits.AsByte(), BYTE_SWAP_SHUFFLE_TABLE_256).AsUInt64();
            else
                return Vector256.Create(ByteSwap64(bits.GetLower()), ByteSwap64(bits.GetUpper()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MirrorByte(ulong bits)
        {
            const ulong MASK0 = 0x5555555555555555UL;
            const ulong MASK1 = 0x3333333333333333UL;
            const ulong MASK2 = 0x0f0f0f0f0f0f0f0f;
            bits = ((bits >> 1) & MASK0) | ((bits & MASK0) << 1);
            bits = ((bits >> 2) & MASK1) | ((bits & MASK1) << 2);
            bits = ((bits >> 4) & MASK2) | ((bits & MASK2) << 4);
            return bits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> MirrorByte(Vector128<ulong> bits)
        {
            var mask0 = Vector128.Create((byte)0x55).AsUInt64();
            var mask1 = Vector128.Create((byte)0x33).AsUInt64();
            var mask2 = Vector128.Create((byte)0x0f).AsUInt64();

            var left = Sse2.And(Sse2.ShiftRightLogical(bits, 1), mask0);
            var right = Sse2.ShiftLeftLogical(Sse2.And(bits, mask0), 1);
            bits = Sse2.Or(left, right);

            left = Sse2.And(Sse2.ShiftRightLogical(bits, 2), mask1);
            right = Sse2.ShiftLeftLogical(Sse2.And(bits, mask1), 2);
            bits = Sse2.Or(left, right);

            left = Sse2.And(Sse2.ShiftRightLogical(bits, 4), mask2);
            right = Sse2.ShiftLeftLogical(Sse2.And(bits, mask2), 4);
            bits = Sse2.Or(left, right);
            return bits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> MirrorByte(Vector256<ulong> bits)
        {
            if (Avx2.IsSupported) 
            {
                var mask0 = Vector256.Create((byte)0x55).AsUInt64();
                var mask1 = Vector256.Create((byte)0x33).AsUInt64();
                var mask2 = Vector256.Create((byte)0x0f).AsUInt64();

                var left = Avx2.And(Avx2.ShiftRightLogical(bits, 1), mask0);
                var right = Avx2.ShiftLeftLogical(Avx2.And(bits, mask0), 1);
                bits = Avx2.Or(left, right);

                left = Avx2.And(Avx2.ShiftRightLogical(bits, 2), mask1);
                right = Avx2.ShiftLeftLogical(Avx2.And(bits, mask1), 2);
                bits = Avx2.Or(left, right);

                left = Avx2.And(Avx2.ShiftRightLogical(bits, 4), mask2);
                right = Avx2.ShiftLeftLogical(Avx2.And(bits, mask2), 4);
                bits = Avx2.Or(left, right);
                return bits;
            }
            else
                return Vector256.Create(MirrorByte(bits.GetLower()), MirrorByte(bits.GetUpper()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Transpose(ulong bitboard)
        {
            if (Avx2.IsSupported)
            {
                var v = Avx2.ShiftLeftLogicalVariable(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(bitboard)), 
                                                      Vector256.Create(3UL, 2UL, 1UL, 0UL));
                return ((ulong)Avx2.MoveMask(v.AsByte()) << 32) | (uint)Avx2.MoveMask(Avx2.ShiftLeftLogical(v, 4).AsByte());
            }
            else
            {
                bitboard = DeltaSwap(bitboard, 0x00000000F0F0F0F0UL, 28);
                bitboard = DeltaSwap(bitboard, 0x0000CCCC0000CCCCUL, 14);
                return DeltaSwap(bitboard, 0x00AA00AA00AA00AAUL, 7);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> Transpose(Vector128<ulong> bitboard)
        {
            if (Avx2.IsSupported)
            {
                var lower = Sse2.X64.ConvertToUInt64(bitboard);
                var upper = Sse41.X64.Extract(bitboard, 1);
                return Vector128.Create(Transpose(lower), Transpose(upper));
            }
            else
            {
                bitboard = DeltaSwap(bitboard, Vector128.Create(0x00000000F0F0F0F0UL), 28);
                bitboard = DeltaSwap(bitboard, Vector128.Create(0x0000CCCC0000CCCCUL), 14);
                return DeltaSwap(bitboard, Vector128.Create(0x00AA00AA00AA00AAUL), 7);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> Transpose(Vector256<ulong> bitboard)
        {
            if (Avx2.IsSupported)
            {
                bitboard = DeltaSwap(bitboard, Vector256.Create(0x00000000F0F0F0F0UL), 28);
                bitboard = DeltaSwap(bitboard, Vector256.Create(0x0000CCCC0000CCCCUL), 14);
                return DeltaSwap(bitboard, Vector256.Create(0x00AA00AA00AA00AAUL), 7);
            }
            else
                return Vector256.Create(Transpose(bitboard.GetLower()), Transpose(bitboard.GetUpper()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Rotate90Clockwise(ulong bitboard)
        {
            return MirrorByte(Transpose(bitboard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> Rotate90Clockwise(Vector128<ulong> bitboard)
        {
            return MirrorByte(Transpose(bitboard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> Rotate90Clockwise(Vector256<ulong> bitboard)
        {
            return MirrorByte(Transpose(bitboard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Rotate90AntiClockwise(ulong bitboard)
        {
            return ByteSwap64(Transpose(bitboard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> Rotate90AntiClockwise(Vector128<ulong> bitboard)
        {
            return ByteSwap64(Transpose(bitboard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> Rotate90AntiClockwise(Vector256<ulong> bitboard)
        {
            return ByteSwap64(Transpose(bitboard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong DeltaSwap(ulong bits, ulong mask, int delta)
        {
            var x = (bits ^ (bits >> delta)) & mask;
            return bits ^ x ^ (x << delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<ulong> DeltaSwap(Vector128<ulong> bits, Vector128<ulong> mask, int delta)
        {
            var x = Sse2.Xor(bits, Sse2.ShiftRightLogical(bits, (byte)delta));
            x = Sse2.And(x, mask);
            return Sse2.Xor(Sse2.Xor(bits, x), Sse2.ShiftLeftLogical(x, (byte)delta));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<ulong> DeltaSwap(Vector256<ulong> bits, Vector256<ulong> mask, int delta)
        {
            var x = Avx2.Xor(bits, Avx2.ShiftRightLogical(bits, (byte)delta));
            x = Avx2.And(x, mask);
            return Avx2.Xor(Avx2.Xor(bits, x), Avx2.ShiftLeftLogical(x, (byte)delta));
        }
    }
}
