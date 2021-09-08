﻿using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
using System.Text;

namespace Kalmia.Reversi
{
    public enum InitialBoardState
    {
        Cross,
        Parallel
    }

    public enum GameResult : sbyte
    {
        Win = 1,
        Draw = 0,
        Loss = -1,
        NotOver = -2
    }

    public struct Bitboard
    {
        public ulong CurrentPlayer { get; set; }
        public ulong OpponentPlayer { get; set; }

        public Bitboard(ulong currentPlayer, ulong opponentPlayer)
        {
            this.CurrentPlayer = currentPlayer;
            this.OpponentPlayer = opponentPlayer;
        }

        public int GetEmptyCount()
        {
            return (int)BitManipulations.PopCount(~(this.CurrentPlayer | this.OpponentPlayer));
        }

        public override bool Equals(object obj)
        {
            return obj is Bitboard && (Bitboard)obj == this;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(Bitboard left, Bitboard right)
        {
            return left.CurrentPlayer == right.CurrentPlayer && left.OpponentPlayer == right.OpponentPlayer;
        }

        public static bool operator !=(Bitboard left, Bitboard right)
        {
            return !(left == right);
        }
    }
 
    // see also get_moves(p, o) in https://github.com/okuhara/edax-reversi-AVX/blob/master/src/board_sse.c 
    public class Board
    {
        public const int BOARD_SIZE = 8;
        public const int SQUARE_NUM = BOARD_SIZE * BOARD_SIZE;
        public const int MAX_MOVES_NUM = 46;

        const int BOARD_HISTORY_STACK_SIZE = 96;

        static readonly Vector256<ulong> SHIFT_1897 = Vector256.Create(1UL, 8UL, 9UL, 7UL);
        static readonly Vector256<ulong> SHIFT_1897_2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
        static readonly Vector256<ulong> FLIP_MASK = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);
        static readonly Vector128<ulong> ZEROS_128 = Vector128.Create(0UL, 0UL);
        static readonly Vector256<ulong> ZEROS_256 = Vector256.Create(0UL, 0UL, 0UL, 0UL);

        Bitboard bitboard;

        ulong currentPlayerMobility;     
        bool currentPlayerMobilityWasCalculated = false;

        Stack<Bitboard> boardHistory = new Stack<Bitboard>(BOARD_HISTORY_STACK_SIZE);

        public Color SideToMove { get; private set; }

        public Board(Color firstPlayer, InitialBoardState initState) : this(firstPlayer, 0UL, 0UL)
        {
            var secondPlayer = firstPlayer ^ Color.White;
            if(initState == InitialBoardState.Cross)
            {
                Put(firstPlayer, BoardPosition.E4);
                Put(firstPlayer, BoardPosition.D5);
                Put(secondPlayer, BoardPosition.D4);
                Put(secondPlayer, BoardPosition.E5);
            }
            else
            {
                Put(firstPlayer, BoardPosition.D5);
                Put(firstPlayer, BoardPosition.E5);
                Put(secondPlayer, BoardPosition.D4);
                Put(secondPlayer, BoardPosition.E4);
            }
        }

        public Board(Color sideToMove, Bitboard bitboard):this(sideToMove, bitboard.CurrentPlayer, bitboard.OpponentPlayer) { }

        public Board(Color sideToMove, ulong currentPlayerBoard, ulong opponentPlayerBoard)
        {
            this.SideToMove = sideToMove;
            this.bitboard.CurrentPlayer = currentPlayerBoard;
            this.bitboard.OpponentPlayer = opponentPlayerBoard;
        }

        public Board(Board board)
        {
            board.CopyTo(this);
        }

        public void Init(Color sideToMove, Bitboard bitboard)
        {
            this.SideToMove = sideToMove;
            this.bitboard = bitboard;
            this.currentPlayerMobilityWasCalculated = false;
            this.boardHistory.Clear();
        }

        public ulong GetBitboard(Color color)
        {
            return (this.SideToMove == color) ? this.bitboard.CurrentPlayer : this.bitboard.OpponentPlayer;
        }

        public Bitboard GetBitBoard()
        {
            return this.bitboard;
        }

        public int GetDiscCount(Color color)
        {
            if (color == this.SideToMove)
                return GetCurrentPlayerDiscCount();
            else
                return GetOpponentPlayerDiscCount();
        }

        public int GetEmptyCount()
        {
            return this.bitboard.GetEmptyCount();
        }

        public int GetCurrentPlayerDiscCount()
        {
            return (int)BitManipulations.PopCount(this.bitboard.CurrentPlayer);
        }

        public int GetOpponentPlayerDiscCount()
        {
            return (int)BitManipulations.PopCount(this.bitboard.OpponentPlayer);
        }

        public Color GetColor(int posX, int posY)
        {
            return GetDiscColor((BoardPosition)(posX + posY * BOARD_SIZE));
        }

        public Color GetDiscColor(BoardPosition pos)
        {
            var x = (int)pos;
            var sideToMove = (ulong)this.SideToMove + 1UL;
            var color = sideToMove * ((this.bitboard.CurrentPlayer >> x) & 1) + (sideToMove ^ 3) * ((this.bitboard.OpponentPlayer >> x) & 1);
            return (color != 0) ? (Color)(color - 1) : Color.Empty;
        }

        public Color[,] GetDiscsArray()
        {
            var discs = new Color[BOARD_SIZE, BOARD_SIZE];
            for (var i = 0; i < discs.GetLength(0); i++)
                for (var j = 0; j < discs.GetLength(1); j++)
                    discs[i, j] = Color.Empty;
            var currentPlayer = this.SideToMove;
            var opponentPlayer = this.SideToMove ^ Color.White;

            var mask = 1UL;
            for(var y = 0; y < discs.GetLength(0); y++)
                for(var x = 0; x < discs.GetLength(1); x++)
                {
                    if ((this.bitboard.CurrentPlayer & mask) != 0)
                        discs[x, y] = currentPlayer;
                    else if ((this.bitboard.OpponentPlayer & mask) != 0)
                        discs[x, y] = opponentPlayer;
                    mask <<= 1;
                }
            return discs;
        }

        public void SwitchSideToMove()
        {
            // swap bitboard
            var tmp = this.bitboard.CurrentPlayer;
            this.bitboard.CurrentPlayer = this.bitboard.OpponentPlayer;
            this.bitboard.OpponentPlayer = tmp;
            this.currentPlayerMobilityWasCalculated = false;
            this.SideToMove ^= Color.White;
        }

        public void Put(Color color, string pos)
        {
            Put(color, StringToPos(pos));
        }

        public void Put(Color color, int posX, int posY)
        {
            Put(color, (BoardPosition)(posX + posY * BOARD_SIZE));
        }

        public void Put(Color color, BoardPosition pos)
        {
            var putPat = 1UL << (byte)pos;
            if (color == this.SideToMove)
                this.bitboard.CurrentPlayer |= putPat;
            else
                this.bitboard.OpponentPlayer |= putPat;
            this.currentPlayerMobilityWasCalculated = false;
            this.boardHistory.Clear();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Board))
                return false;
            var board = (Board)obj;
            return board.SideToMove == this.SideToMove && board.bitboard == this.bitboard;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Board destBoard, bool copyHistory = false)
        {
            destBoard.SideToMove = this.SideToMove;
            destBoard.bitboard = this.bitboard;
            destBoard.currentPlayerMobility = this.currentPlayerMobility;
            destBoard.currentPlayerMobilityWasCalculated = this.currentPlayerMobilityWasCalculated;
            if (copyHistory)
                destBoard.boardHistory = this.boardHistory.Copy(BOARD_HISTORY_STACK_SIZE);
            else
                destBoard.boardHistory.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Move move)
        {
            this.boardHistory.Push(this.bitboard);
            if (move.Pos != BoardPosition.Pass)
            {
                var x = 1UL << (byte)move.Pos;
                var flipped = CalculateFlippedDiscs((byte)move.Pos);
                this.bitboard.OpponentPlayer ^= flipped;
                this.bitboard.CurrentPlayer |= (flipped | x);
            }
            SwitchSideToMove();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Undo()
        {
            if (this.boardHistory.Count == 0)
                return false;
            this.SwitchSideToMove();
            this.bitboard = this.boardHistory.Pop();
            this.currentPlayerMobilityWasCalculated = false;
            return true;
        }

        public bool IsLegalMove(Move move)
        {
            if (move.Color != this.SideToMove)
                return false;
            if (move.Pos == BoardPosition.Pass)
                return CalculateCurrentPlayerMobility() == 0UL && GetNextMovesCount() == 1;
            return ((1UL << (byte)move.Pos) & CalculateCurrentPlayerMobility()) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextMovesCount()
        {
            var moveCount = (int)BitManipulations.PopCount(CalculateCurrentPlayerMobility());
            if (moveCount != 0)
                return moveCount;
            if (BitManipulations.PopCount(CalculateMobility(this.bitboard.OpponentPlayer, this.bitboard.CurrentPlayer)) == 0)
                return 0;
            else
                return 1;   // pass
        }

        public IEnumerable<Move> GetNextMoves()
        {
            var mobility = CalculateCurrentPlayerMobility();
            var moveCount = (int)BitManipulations.PopCount(mobility);
            if (moveCount == 0)
            {
                if (BitManipulations.PopCount(CalculateMobility(this.bitboard.OpponentPlayer, this.bitboard.CurrentPlayer)) != 0)
                    yield return new Move(this.SideToMove, BoardPosition.Pass);
            }
            else
            {
                var mask = 1UL;
                var count = 0;
                for (var i = 0; count < moveCount; i++)
                {
                    if ((mobility & mask) != 0)
                    {
                        yield return new Move(this.SideToMove, (BoardPosition)i);
                        count++;
                    }
                    mask <<= 1;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextMoves(Move[] moves)
        {
            var mobility = CalculateCurrentPlayerMobility();
            var moveCount = (int)BitManipulations.PopCount(mobility);
            if(moveCount == 0)
            {
                if (BitManipulations.PopCount(CalculateMobility(this.bitboard.OpponentPlayer, this.bitboard.CurrentPlayer)) == 0)
                    return 0;
                moves[0] = new Move(this.SideToMove, BoardPosition.Pass);
                return 1;
            }

            var mask = 1UL;
            var idx = 0;
            for(var i = 0; idx < moveCount; i++)
            {
                if ((mobility & mask) != 0)
                    moves[idx++] = new Move(this.SideToMove, (BoardPosition)i);
                mask <<= 1;
            }
            return moveCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Move GetNextMove(int idx)
        {
            var mobility = CalculateCurrentPlayerMobility();
            if (BitManipulations.PopCount(mobility) == 0)
                return new Move(this.SideToMove, BoardPosition.Pass);

            var mask = 1UL;
            var i = 0;
            int pos;
            for(pos = 0; pos < SQUARE_NUM; pos++)
            {
                if ((mobility & mask) != 0)
                    if (++i >= idx)
                        break;
                mask <<= 1;
            }
            return new Move(this.SideToMove, (BoardPosition)pos);
        }

        public GameResult GetGameResult(Color color)
        {
            if (GetNextMovesCount() != 0)
                return GameResult.NotOver;

            var currentPlayerCount = GetCurrentPlayerDiscCount();
            var opponentPlayerCount = GetOpponentPlayerDiscCount();
            if (currentPlayerCount > opponentPlayerCount)
                return (color == this.SideToMove) ? GameResult.Win : GameResult.Loss;
            if (currentPlayerCount < opponentPlayerCount)
                return (color == this.SideToMove) ? GameResult.Loss : GameResult.Win;
            return GameResult.Draw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong CalculateCurrentPlayerMobility()
        {
            if (this.currentPlayerMobilityWasCalculated)
                return this.currentPlayerMobility;

            return CalculateMobility(this.bitboard.CurrentPlayer, this.bitboard.OpponentPlayer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility(ulong p, ulong o)
        {
            if (Sse2.X64.IsSupported && Avx2.IsSupported)
                return CalculateMobility_AVX2(p, o);
            else
                return CalculateMobility_SSE(p, o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ulong CalculateFlippedDiscs(int pos)
        {
            if (Sse2.X64.IsSupported && Avx2.IsSupported)
                return CalculateFilippedDiscs_AVX2(this.bitboard.CurrentPlayer, this.bitboard.OpponentPlayer, pos);
            else
                return CalculateFlippedDiscs_SSE(this.bitboard.CurrentPlayer, this.bitboard.OpponentPlayer, pos);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("  ");
            for (var i = 0; i < BOARD_SIZE; i++)
                sb.Append($"{(char)('A' + i)} ");

            var mask = 1UL;
            for (var y = 0; y < BOARD_SIZE; y++)
            {
                sb.Append($"\n{y + 1} ");
                for (var x = 0; x < BOARD_SIZE; x++)
                {
                    if ((this.bitboard.CurrentPlayer & mask) != 0)
                        sb.Append((this.SideToMove == Color.Black) ? "X " : "O ");
                    else if ((this.bitboard.OpponentPlayer & mask) != 0)
                        sb.Append((this.SideToMove != Color.Black) ? "X " : "O ");    
                    else
                        sb.Append(". ");
                    mask <<= 1;
                }
            }
            return sb.ToString();
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

        static BoardPosition StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return (BoardPosition)(posX + posY * BOARD_SIZE);
        }
    }
}
