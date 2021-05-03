#define DEBUG_AVX2
//#define DEBUG_SSE  

using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

namespace Kalmia.Reversi
{
    public enum InitialBoardState
    {
        Cross,
        Parallel
    }

    // I reffered to the code below to implement mobility and flipped discs calculation. 
    // get_moves(p, o) in https://github.com/okuhara/edax-reversi-AVX/blob/master/src/board_sse.c 
    public class Board
    {
        public const int LINE_LENGTH = 8;
        public const int GRID_NUM = LINE_LENGTH * LINE_LENGTH;
        public const int MAX_MOVES_NUM = 46;

        static readonly Vector256<ulong> SHIFT_1897 = Vector256.Create(1UL, 8UL, 9UL, 7UL);
        static readonly Vector256<ulong> SHIFT_1897_2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
        static readonly Vector256<ulong> FLIP_MASK = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);
        static readonly Vector128<ulong> ZEROS_128 = Vector128.Create(0UL, 0UL);
        static readonly Vector256<ulong> ZEROS_256 = Vector256.Create(0UL, 0UL, 0UL, 0UL);

        ulong currentPlayersBoard;
        ulong opponentPlayersBoard;

        ulong mobility;     // reuse the mobility in same turn.
        bool mobilityWasCalculated = false; 

        public Color Turn { get; private set; }

        public Board(Color firstPlayer, InitialBoardState initState) : this(firstPlayer, 0UL, 0UL)
        {
            var secondPlayer = (Color)(-(int)firstPlayer);
            if(initState == InitialBoardState.Cross)
            {
                Put(firstPlayer, "E4");
                Put(firstPlayer, "D5");
                Put(secondPlayer, "D4");
                Put(secondPlayer, "E5");
            }
            else
            {
                Put(firstPlayer, "D5");
                Put(firstPlayer, "E5");
                Put(secondPlayer, "D4");
                Put(secondPlayer, "E4");
            }
        }

        public Board(Color firstPlayer, ulong firstPlayersBoard, ulong secondPlayersBoard)
        {
            this.Turn = firstPlayer;
            this.currentPlayersBoard = firstPlayersBoard;
            this.opponentPlayersBoard = secondPlayersBoard;
        }

        public Board(Board board)
        {
            this.Turn = board.Turn;
            this.currentPlayersBoard = board.currentPlayersBoard;
            this.opponentPlayersBoard = board.opponentPlayersBoard;
        }

        public Color[,] GetDiscsArray()
        {
            var discs = new Color[LINE_LENGTH, LINE_LENGTH];
            var currentPlayer = this.Turn;
            var opponentPlayer = (Color)(-(int)this.Turn);

            var mask = 1UL;
            for(var y = 0; y < discs.GetLength(0); y++)
                for(var x = 0; x < discs.GetLength(1); x++)
                {
                    if ((this.currentPlayersBoard & mask) != 0)
                        discs[x, y] = currentPlayer;
                    else if ((this.opponentPlayersBoard & mask) != 0)
                        discs[x, y] = opponentPlayer;
                    mask <<= 1;
                }
            return discs;
        }

        public void SwitchTurn()
        {
            // swap bitboard
            var tmp = this.currentPlayersBoard;
            this.currentPlayersBoard = this.opponentPlayersBoard;
            this.opponentPlayersBoard = tmp;
            this.mobilityWasCalculated = false;
            this.Turn = (Color)(-(int)this.Turn);
        }

        public void Put(Color color, string pos)
        {
            Put(color, StringToPos(pos));
        }

        public void Put(Color color, int posX, int posY)
        {
            Put(color, posX + LINE_LENGTH * posY);
        }

        public void Put(Color color, int pos)
        {
            var putPat = 1UL << pos;
            if (color == this.Turn)
                this.currentPlayersBoard |= putPat;
            else
                this.opponentPlayersBoard |= putPat;
            this.mobilityWasCalculated = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Move move)
        {
            if (move.Pos != Move.PASS)
            {
                var x = 1UL << move.Pos;
                var flipped = CalculateFlippedDiscs(move.Pos);
                this.opponentPlayersBoard ^= flipped;
                this.currentPlayersBoard |= (flipped | x);
            }

            SwitchTurn();
        }

        public int GetNextMovesNum()
        {
            return (int)BitManipulations.PopCount(CalculateMobility());
        }

        public int GetNextMoves(Move[] moves)
        {
            var mobility = CalculateMobility();
            var moveNum = (int)BitManipulations.PopCount(mobility);
            if(moveNum == 0)
            {
                moves[0] = new Move(this.Turn, Move.PASS);
                return 1;
            }

            var mask = 1UL;
            var idx = 0;
            for(var i = 0; idx < moveNum; i++)
            {
                if ((mobility & mask) != 0)
                    moves[idx++] = new Move(this.Turn, i);
                mask <<= 1;
            }
            return moveNum;
        }

        public Move GetNextMove(int idx)
        {
            var mobility = CalculateMobility();
            var mask = 1UL;
            var i = 0;
            int pos;
            for(pos = 0; i < idx; pos++)
            {
                if ((mobility & mask) != 0UL)
                    i++;
                mask <<= 1;
            }
            return new Move(this.Turn, pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong CalculateMobility()
        {
            if (this.mobilityWasCalculated)
                return this.mobility;

#if RELEASE
            if (Avx2.IsSupported)
            {
                this.mobility = CalculateMobility_AVX2(this.currentPlayersBoard, this.opponentPlayersBoard);
                this.mobilityWasCalculated = true;
                return this.mobility;
            }
            else
            {
                this.mobility = CalculateMobility_SSE(this.currentPlayersBoard, this.opponentPlayersBoard);
                this.mobilityWasCalculated = true;
                return this.mobility;
            }
#elif DEBUG_AVX2
                this.mobility = CalculateMobility_AVX2(this.currentPlayersBoard, this.opponentPlayersBoard);
                this.mobilityWasCalculated = true;
                return this.mobility;
#elif DEBUG_SSE
            this.mobility = CalculateMobility_SSE(this.currentPlayersBoard, this.opponentPlayersBoard);
            this.mobilityWasCalculated = true;
            return this.mobility;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong CalculateFlippedDiscs(int pos)
        {
#if RELEASE
            if (Avx2.IsSupported)
                return CalculateFilippedDiscs_AVX2(this.currentPlayersBoard, this.opponentPlayersBoard, pos);
            else
                return CalculateFlippedDiscs_SSE(this.currentPlayersBoard, this.opponentPlayersBoard, pos);
#elif DEBUG_AVX2
            return CalculateFilippedDiscs_AVX2(this.currentPlayersBoard, this.opponentPlayersBoard, pos);
#elif DEBUG_SSE
            return CalculateFlippedDiscs_SSE(this.currentPlayersBoard, this.opponentPlayersBoard, pos);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility_AVX2(ulong p, ulong o)   // p is current player's board      o is opponent player's board
        {
            var shift = SHIFT_1897;
            var shift2 = SHIFT_1897_2;

            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), FLIP_MASK);
            var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, SHIFT_1897));
            var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, SHIFT_1897);

            var flipLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(p4, shift));
            var flipRight = Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(p4, SHIFT_1897));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, SHIFT_1897)));
            flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, SHIFT_1897)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

            var mobility4 = Avx2.ShiftLeftLogicalVariable(flipLeft, SHIFT_1897);
            mobility4 = Avx2.Or(mobility4, Avx2.ShiftRightLogicalVariable(flipRight, SHIFT_1897));
            var mobility2 = Sse2.Or(Avx2.ExtractVector128(mobility4, 0), Avx2.ExtractVector128(mobility4, 1));
            mobility2 = Sse2.Or(mobility2, Sse2.UnpackHigh(mobility2, mobility2));
            return Sse2.X64.ConvertToUInt64(mobility2) & ~(p | o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility_SSE(ulong p, ulong o)    // p is current player's board      o is opponent player's board
        {
            var maskedO = o & 0x7e7e7e7e7e7e7e7eUL;
            var p2 = Vector128.Create(p, BitManipulations.ByteSwap(p));   // byte swap = vertical mirror
            var maskedO2 = Vector128.Create(maskedO, BitManipulations.ByteSwap(maskedO));
            var prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 7));
            var prefix1 = maskedO & (maskedO << 1);
            var prefix8 = o & (o << 8);

            var flip = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(p2, 7));    // If you write alternately SSE code and CPU code, you can expect that compiler would execute both code in parallel. 
            var flip1 = maskedO & (p << 1);                                 // However, I do not know whether this tip would works or not on C#'s compiler.
            var flip8 = o & (p << 8);
            flip = Sse2.Or(flip, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip, 7)));
            flip1 |= maskedO & (flip1 << 1);
            flip8 |= o & (flip8 << 8);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 14)));
            flip1 |= prefix1 & (flip1 << 2);
            flip8 |= prefix8 & (flip8 << 16);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 14)));
            flip1 |= prefix1 & (flip1 << 2);
            flip8 |= prefix8 & (flip8 << 16);

            var mobility2 = Sse2.ShiftLeftLogical(flip, 7);
            var mobility = (flip1 << 1) | (flip8 << 8);

            prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 9));
            prefix1 >>= 1;
            prefix8 >>= 8;
            flip = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(p2, 9));
            flip1 = maskedO & (p >> 1);
            flip8 = o & (p >> 8);
            flip = Sse2.Or(flip, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip, 9)));
            flip1 |= maskedO & (flip1 >> 1);
            flip8 |= o & (flip8 >> 8);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 18)));
            flip1 |= prefix1 & (flip1 >> 2);
            flip8 |= prefix8 & (flip8 >> 16);
            flip = Sse2.Or(flip, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip, 18)));
            flip1 |= prefix1 & (flip1 >> 2);
            flip8 |= prefix8 & (flip8 >> 16);
            mobility2 = Sse2.Or(mobility2, Sse2.ShiftLeftLogical(flip, 9));
            mobility |= (flip1 >> 1) | (flip8 >> 8);

            if (Sse2.X64.IsSupported)
                mobility |= Sse2.X64.ConvertToUInt64(mobility2) | BitManipulations.ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(mobility2, mobility2)));
            else
                mobility |= mobility2.GetElement(0) | BitManipulations.ByteSwap(Sse2.UnpackHigh(mobility2, mobility2).GetElement(0));
            return mobility & ~(p | o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateFilippedDiscs_AVX2(ulong p, ulong o, int pos)    // p is current player's board      o is opponent player's board
        {
            var shift = SHIFT_1897;
            var shift2 = SHIFT_1897_2;
            var zeros = ZEROS_256;

            var x = 1UL << pos;
            var x4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(x));
            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), FLIP_MASK);
            var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, shift));
            var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, shift);

            var flipLeft = Avx2.And(Avx2.ShiftLeftLogicalVariable(x4, shift), maskedO4);
            var flipRight = Avx2.And(Avx2.ShiftRightLogicalVariable(x4, shift), maskedO4);
            flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift)));
            flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, shift)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

            var outflankLeft = Avx2.And(p4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift));
            var outflankRight = Avx2.And(p4, Avx2.ShiftRightLogicalVariable(flipRight, shift));
            flipLeft = Avx2.AndNot(Avx2.CompareEqual(outflankLeft, zeros), flipLeft);
            flipRight = Avx2.AndNot(Avx2.CompareEqual(outflankRight, zeros), flipRight);
            var flip4 = Avx2.Or(flipLeft, flipRight);
            var flip2 = Sse2.Or(Avx2.ExtractVector128(flip4, 0), Avx2.ExtractVector128(flip4, 1));
            flip2 = Sse2.Or(flip2, Sse2.UnpackHigh(flip2, flip2));
            return Sse2.X64.ConvertToUInt64(flip2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateFlippedDiscs_SSE(ulong p, ulong o, int pos)    // p is current player's board      o is opponent player's board
        {
            var zeros = ZEROS_128;

            var x = 1UL << pos;
            var maskedO = o & 0x7e7e7e7e7e7e7e7eUL;
            var x2 = Vector128.Create(x, BitManipulations.ByteSwap(x));   // byte swap = vertical mirror
            var p2 = Vector128.Create(p, BitManipulations.ByteSwap(p));
            var maskedO2 = Vector128.Create(maskedO, BitManipulations.ByteSwap(maskedO));
            var prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 7));
            var prefix1 = maskedO & (maskedO << 1);
            var prefix8 = o & (o << 8);

            var flip7 = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(x2, 7));
            var flip1Left = maskedO & (x << 1);
            var flip8Left = o & (x << 8);
            flip7 = Sse2.Or(flip7, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip7, 7)));
            flip1Left |= maskedO & (flip1Left << 1);
            flip8Left |= o & (flip8Left << 8);
            flip7 = Sse2.Or(flip7, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip7, 14)));
            flip1Left |= prefix1 & (flip1Left << 2);
            flip8Left |= prefix8 & (flip8Left << 16);
            flip7 = Sse2.Or(flip7, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip7, 14)));
            flip1Left |= prefix1 & (flip1Left << 2);
            flip8Left |= prefix8 & (flip8Left << 16);

            prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 9));
            prefix1 >>= 1;
            prefix8 >>= 8;

            var flip9 = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(x2, 9));
            var flip1Right = maskedO & (x >> 1);
            var flip8Right = o & (x >> 8);
            flip9 = Sse2.Or(flip9, Sse2.And(maskedO2, Sse2.ShiftLeftLogical(flip9, 9)));
            flip1Right |= maskedO & (flip1Right >> 1);
            flip8Right |= o & (flip8Right >> 8);
            flip9 = Sse2.Or(flip9, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip9, 18)));
            flip1Right |= prefix1 & (flip1Right >> 2);
            flip8Right |= prefix8 & (flip8Right >> 16);
            flip9 = Sse2.Or(flip9, Sse2.And(prefix, Sse2.ShiftLeftLogical(flip9, 18)));
            flip1Right |= prefix1 & (flip1Right >> 2);
            flip8Right |= prefix8 & (flip8Right >> 16);

            var outflank7 = Sse2.And(p2, Sse2.ShiftLeftLogical(flip7, 7));
            var outflankLeft1 = p & (flip1Left << 1);
            var outflankLeft8 = p & (flip8Left << 8);
            var outflank9 = Sse2.And(p2, Sse2.ShiftLeftLogical(flip9, 9));
            var outflankRight1 = p & (flip1Right >> 1);
            var outflankRight8 = p & (flip8Right >> 8);

            if (Sse41.IsSupported)
            {
                flip7 = Sse2.AndNot(Sse41.CompareEqual(outflank7, zeros), flip7);
                flip9 = Sse2.AndNot(Sse41.CompareEqual(outflank9, zeros), flip9);
            }
            else
            {
                flip7 = Sse2.And(Sse2.CompareNotEqual(outflank7.AsDouble(), zeros.AsDouble()).AsUInt64(), flip7);
                flip9 = Sse2.And(Sse2.CompareNotEqual(outflank9.AsDouble(), zeros.AsDouble()).AsUInt64(), flip9);
            }

            if (outflankLeft1 == 0)
                flip1Left = 0UL;
            if (outflankLeft8 == 0)
                flip8Left = 0UL;
            if (outflankRight1 == 0)
                flip1Right = 0UL;
            if (outflankRight8 == 0)
                flip8Right = 0UL;

            var flippedDiscs2 = Sse2.Or(flip7, flip9);
            var flippedDiscs = flip1Left | flip8Left | flip1Right | flip8Right;
            if (Sse2.X64.IsSupported)
                flippedDiscs |= Sse2.X64.ConvertToUInt64(flippedDiscs2) 
                             | BitManipulations.ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(flippedDiscs2, flippedDiscs2)));
            else
                flippedDiscs |= flippedDiscs2.GetElement(0) | BitManipulations.ByteSwap(Sse2.UnpackHigh(flippedDiscs2, flippedDiscs2).GetElement(0));
            return flippedDiscs;
        }

        static int StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return posX + posY * LINE_LENGTH;
        }
    }
}
