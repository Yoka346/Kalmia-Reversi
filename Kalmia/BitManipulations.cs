using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

namespace Kalmia
{
    public static class BitManipulations
    {
        static readonly Vector128<byte> BYTE_SWAP_SHUFFLE_TABLE_128;
        static readonly Vector256<byte> BYTE_SWAP_SHUFFLE_TABLE_256;
        static readonly Vector128<byte>[] BYTE_ROTATE_RIGHT_SHUFFLE_TABLES_128;
        static readonly Vector256<byte>[] BYTE_ROTATE_RIGHT_SHUFFLE_TABLES_256;

        static BitManipulations()
        {
            BYTE_SWAP_SHUFFLE_TABLE_128 = Vector128.Create(7, 6, 5, 4, 3, 2, 1, 0, 
                                                          15, 14, 13, 12, 11, 10, 9, 8).AsByte();

            BYTE_SWAP_SHUFFLE_TABLE_256 = Vector256.Create(7, 6, 5, 4, 3, 2, 1, 0,
                                                          15, 14, 13, 12, 11, 10, 9, 8,
                                                          23, 22, 21, 20, 19, 18, 17, 16,
                                                          31, 30, 29, 28, 27, 26, 25, 24).AsByte();

            BYTE_ROTATE_RIGHT_SHUFFLE_TABLES_128 = new Vector128<byte>[8];
            BYTE_ROTATE_RIGHT_SHUFFLE_TABLES_256 = new Vector256<byte>[8];
            for (int i = 0; i < 8; i++)
            {
                var values = new byte[16];
                for (var j = 0; j < 16; j++)
                    values[j] = (byte)((((j % 8) + i) % 8) + 8 * (j / 8));
                BYTE_ROTATE_RIGHT_SHUFFLE_TABLES_128[i] = Vector128.Create(values[0], values[1], values[2], values[3],
                                                                           values[4], values[5], values[6], values[7],
                                                                           values[8], values[9], values[10], values[11],
                                                                           values[12], values[13], values[14], values[15]);

                values = new byte[32];
                for (var j = 0; j < 32; j++)
                    values[j] = (byte)((((j % 8) + i) % 8) + 8 * (j / 8));
                BYTE_ROTATE_RIGHT_SHUFFLE_TABLES_256[i] = Vector256.Create(values[0], values[1], values[2], values[3],
                                                                           values[4], values[5], values[6], values[7],
                                                                           values[8], values[9], values[10], values[11],
                                                                           values[12], values[13], values[14], values[15],
                                                                           values[16], values[17], values[18], values[19],
                                                                           values[20], values[21], values[22], values[23],
                                                                           values[24], values[25], values[26], values[27],
                                                                           values[28], values[29], values[30], values[31]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PopCount(ulong bits)
        {
            if (Popcnt.X64.IsSupported)
                return Popcnt.X64.PopCount(bits);
            else if (Popcnt.IsSupported)
            {
                const ulong LOWER_MASK = 0x00000000ffffffffUL;
                var lower = (uint)(bits & LOWER_MASK);
                var upper = (uint)(bits >> 32);
                return Popcnt.PopCount(lower) + Popcnt.PopCount(upper);
            }
            else
                return (ulong)BitOperations.PopCount(bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ParallelBitExtract(ulong bits, ulong mask)
        { 
            if(Bmi2.X64.IsSupported)
                return Bmi2.X64.ParallelBitExtract(bits, mask);     // On ZEN2 architecture, this is slower than software emulation code...
            else
            {
                var res = 0UL;
                for (var bb = 1UL; mask != 0UL; bb += bb)
                {
                    if ((bits & mask & (~mask + 1)) != 0UL)
                        res |= bb;
                    mask &= mask - 1;
                }
                return res;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte BitSwap(byte bits)
        {
            int n = bits;
            n = (n & 0x55) << 1 | (n >> 1 & 0x55);
            n = (n & 0x33) << 2 | (n >> 2 & 0x33);
            n = (n & 0x0f) << 4 | (n >> 4 & 0x0f);
            return (byte)n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ByteSwap(ulong bits)
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
        public static Vector128<ulong> ByteSwap(Vector128<ulong> bits)
        {
            if (Ssse3.IsSupported)
                return Ssse3.Shuffle(bits.AsByte(), BYTE_SWAP_SHUFFLE_TABLE_128).AsUInt64();
            else
                return Vector128.Create(ByteSwap(bits.GetElement(0)), ByteSwap(bits.GetElement(1)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> ByteSwap(Vector256<ulong> bits)
        {
            if (Avx2.IsSupported)
                return Avx2.Shuffle(bits.AsByte(), BYTE_SWAP_SHUFFLE_TABLE_256).AsUInt64();
            else
                return Vector256.Create(ByteSwap(bits.GetLower()), ByteSwap(bits.GetUpper()));
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
            return ByteSwap(Transpose(bitboard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> Rotate90AntiClockwise(Vector128<ulong> bitboard)
        {
            return ByteSwap(Transpose(bitboard));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> Rotate90AntiClockwise(Vector256<ulong> bitboard)
        {
            return ByteSwap(Transpose(bitboard));
        }

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PseudoRotate45Clockwise(ulong bitboard)
        {
            const ulong MASK0 = 0xaaaaaaaaaaaaaaaaUL;
            const ulong MASK1 = 0xccccccccccccccccUL;
            const ulong MASK2 = 0xf0f0f0f0f0f0f0f0UL;
            bitboard ^= MASK0 & (bitboard ^ BitOperations.RotateRight(bitboard, 8));
            bitboard ^= MASK1 & (bitboard ^ BitOperations.RotateRight(bitboard, 16));
            return bitboard ^ MASK2 & (bitboard ^ BitOperations.RotateRight(bitboard, 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> PseudoRotate45Clockwise(Vector128<ulong> bitboard)
        {
            var mask0 = Vector128.Create((byte)0xaa).AsUInt64();
            var mask1 = Vector128.Create((byte)0xcc).AsUInt64();
            var mask2 = Vector128.Create((byte)0xf0).AsUInt64();
            var data = Sse2.Xor(bitboard, Sse2.And(mask0, Sse2.Xor(bitboard, ByteRotateRight(bitboard, 1))));
            data = Sse2.Xor(data, Sse2.And(mask1, Sse2.Xor(data, ByteRotateRight(data, 2))));
            return Sse2.Xor(data, Sse2.And(mask2, Sse2.Xor(data, ByteRotateRight(data, 4))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> PseudoRotate45Clockwise(Vector256<ulong> bitboard)
        {
            if (Avx2.IsSupported)
            {
                var mask0 = Vector256.Create((byte)0xaa).AsUInt64();
                var mask1 = Vector256.Create((byte)0xcc).AsUInt64();
                var mask2 = Vector256.Create((byte)0xf0).AsUInt64();
                var data = Avx2.Xor(bitboard, Avx2.And(mask0, Avx2.Xor(bitboard, ByteRotateRight(bitboard, 1))));
                data = Avx2.Xor(data, Avx2.And(mask1, Avx2.Xor(data, ByteRotateRight(data, 2))));
                return Avx2.Xor(data, Avx2.And(mask2, Avx2.Xor(data, ByteRotateRight(data, 4))));
            }
            else
                return Vector256.Create(PseudoRotate45Clockwise(bitboard.GetLower()), PseudoRotate45Clockwise(bitboard.GetUpper()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PseudoRotate45AntiClockwise(ulong bitboard)
        {
            const ulong MASK0 = 0x5555555555555555UL;
            const ulong MASK1 = 0x3333333333333333UL;
            const ulong MASK2 = 0x0f0f0f0f0f0f0f0fUL;
            bitboard ^= (MASK0 & (bitboard ^ BitOperations.RotateRight(bitboard, 8)));
            bitboard ^= (MASK1 & (bitboard ^ BitOperations.RotateRight(bitboard, 16)));
            return bitboard ^ (MASK2 & (bitboard ^ BitOperations.RotateRight(bitboard, 32)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> PseudoRotate45AntiClockwise(Vector128<ulong> bitboard)
        {
            var mask0 = Vector128.Create((byte)0x55).AsUInt64();
            var mask1 = Vector128.Create((byte)0x33).AsUInt64();
            var mask2 = Vector128.Create((byte)0x0f).AsUInt64();
            var data = Sse2.Xor(bitboard, Sse2.And(mask0, Sse2.Xor(bitboard, ByteRotateRight(bitboard, 1))));
            data = Sse2.Xor(data, Sse2.And(mask1, Sse2.Xor(data, ByteRotateRight(data, 2))));
            return Sse2.Xor(data, Sse2.And(mask2, Sse2.Xor(data, ByteRotateRight(data, 4))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> PseudoRotate45AntiClockwise(Vector256<ulong> bitboard)
        {
            if (Avx2.IsSupported)
            {
                var mask0 = Vector256.Create((byte)0x55).AsUInt64();
                var mask1 = Vector256.Create((byte)0x33).AsUInt64();
                var mask2 = Vector256.Create((byte)0x0f).AsUInt64();
                var data = Avx2.Xor(bitboard, Avx2.And(mask0, Avx2.Xor(bitboard, ByteRotateRight(bitboard, 1))));
                data = Avx2.Xor(data, Avx2.And(mask1, Avx2.Xor(data, ByteRotateRight(data, 2))));
                return Avx2.Xor(data, Avx2.And(mask2, Avx2.Xor(data, ByteRotateRight(data, 4))));
            }
            else
                return Vector256.Create(PseudoRotate45AntiClockwise(bitboard.GetLower()), PseudoRotate45AntiClockwise(bitboard.GetUpper()));
        }*/

        static Vector128<ulong> RotateRight(Vector128<ulong> bits, int n)
        {
            return Sse2.Or(Sse2.ShiftRightLogical(bits, (byte)n), Sse2.ShiftLeftLogical(bits, (byte)(64 - n)));
        }

        static Vector256<ulong> RotateRight(Vector256<ulong> bits, int n)
        {
            return Avx2.Or(Avx2.ShiftRightLogical(bits, (byte)n), Avx2.ShiftLeftLogical(bits, (byte)(64 - n)));
        }

        static ulong ByteRotateRight(ulong bits, int i)
        {
            return BitOperations.RotateRight(bits, i * 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<ulong> ByteRotateRight(Vector128<ulong> bits, int i)
        {
            if (Ssse3.IsSupported)
                return Ssse3.Shuffle(bits.AsByte(), BYTE_ROTATE_RIGHT_SHUFFLE_TABLES_128[i]).AsUInt64();
            else
                return Vector128.Create(ByteRotateRight(bits.GetElement(0), i), ByteRotateRight(bits.GetElement(1), i));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<ulong> ByteRotateRight(Vector256<ulong> bits, int i)
        {
            if (Avx2.IsSupported)
                return Avx2.Shuffle(bits.AsByte(), BYTE_ROTATE_RIGHT_SHUFFLE_TABLES_256[i]).AsUInt64();
            else
                return Vector256.Create(ByteRotateRight(bits.GetLower(), i), ByteRotateRight(bits.GetLower(), i));
        }

        /*[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PseudoRotate45Clockwise(ulong bitboard)
        {
            const ulong MASK0 = 0xaaaaaaaaaaaaaaaaUL;
            const ulong MASK1 = 0xccccccccccccccccUL;
            const ulong MASK2 = 0xf0f0f0f0f0f0f0f0UL;
            bitboard ^= MASK0 & (bitboard ^ BitOperations.RotateRight(bitboard, 8));
            bitboard ^= MASK1 & (bitboard ^ BitOperations.RotateRight(bitboard, 16));
            return bitboard ^ MASK2 & (bitboard ^ BitOperations.RotateRight(bitboard, 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> PseudoRotate45Clockwise(Vector128<ulong> bitboard)
        {
            var mask0 = Vector128.Create((byte)0xaa).AsUInt64();
            var mask1 = Vector128.Create((byte)0xcc).AsUInt64();
            var mask2 = Vector128.Create((byte)0xf0).AsUInt64();
            var data = Sse2.Xor(bitboard, Sse2.And(mask0, Sse2.Xor(bitboard, ByteRotateRight(bitboard, 1))));
            data = Sse2.Xor(data, Sse2.And(mask1, Sse2.Xor(data, ByteRotateRight(data, 2))));
            return Sse2.Xor(data, Sse2.And(mask2, Sse2.Xor(data, ByteRotateRight(data, 4))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> PseudoRotate45Clockwise(Vector256<ulong> bitboard)
        {
            if (Avx2.IsSupported)
            {
                var mask0 = Vector256.Create((byte)0xaa).AsUInt64();
                var mask1 = Vector256.Create((byte)0xcc).AsUInt64();
                var mask2 = Vector256.Create((byte)0xf0).AsUInt64();
                var data = Avx2.Xor(bitboard, Avx2.And(mask0, Avx2.Xor(bitboard, ByteRotateRight(bitboard, 1))));
                data = Avx2.Xor(data, Avx2.And(mask1, Avx2.Xor(data, ByteRotateRight(data, 2))));
                return Avx2.Xor(data, Avx2.And(mask2, Avx2.Xor(data, ByteRotateRight(data, 4))));
            }
            else
                return Vector256.Create(PseudoRotate45Clockwise(bitboard.GetLower()), PseudoRotate45Clockwise(bitboard.GetUpper()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong PseudoRotate45AntiClockwise(ulong bitboard)
        {
            const ulong MASK0 = 0x5555555555555555UL;
            const ulong MASK1 = 0x3333333333333333UL;
            const ulong MASK2 = 0x0f0f0f0f0f0f0f0fUL;
            bitboard ^= (MASK0 & (bitboard ^ BitOperations.RotateRight(bitboard, 8)));
            bitboard ^= (MASK1 & (bitboard ^ BitOperations.RotateRight(bitboard, 16)));
            return bitboard ^ (MASK2 & (bitboard ^ BitOperations.RotateRight(bitboard, 32)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ulong> PseudoRotate45AntiClockwise(Vector128<ulong> bitboard)
        {
            var mask0 = Vector128.Create((byte)0x55).AsUInt64();
            var mask1 = Vector128.Create((byte)0x33).AsUInt64();
            var mask2 = Vector128.Create((byte)0x0f).AsUInt64();
            var data = Sse2.Xor(bitboard, Sse2.And(mask0, Sse2.Xor(bitboard, ByteRotateRight(bitboard, 1))));
            data = Sse2.Xor(data, Sse2.And(mask1, Sse2.Xor(data, ByteRotateRight(data, 2))));
            return Sse2.Xor(data, Sse2.And(mask2, Sse2.Xor(data, ByteRotateRight(data, 4))));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ulong> PseudoRotate45AntiClockwise(Vector256<ulong> bitboard)
        {
            if (Avx2.IsSupported)
            {
                var mask0 = Vector256.Create((byte)0x55).AsUInt64();
                var mask1 = Vector256.Create((byte)0x33).AsUInt64();
                var mask2 = Vector256.Create((byte)0x0f).AsUInt64();
                var data = Avx2.Xor(bitboard, Avx2.And(mask0, Avx2.Xor(bitboard, ByteRotateRight(bitboard, 1))));
                data = Avx2.Xor(data, Avx2.And(mask1, Avx2.Xor(data, ByteRotateRight(data, 2))));
                return Avx2.Xor(data, Avx2.And(mask2, Avx2.Xor(data, ByteRotateRight(data, 4))));
            }
            else
                return Vector256.Create(PseudoRotate45AntiClockwise(bitboard.GetLower()), PseudoRotate45AntiClockwise(bitboard.GetUpper()));
        }*/

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
