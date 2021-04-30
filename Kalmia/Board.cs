using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Kalmia
{
    public enum InitialBoardState
    {
        Cross,
        Parallel
    }

    public class Board
    {
        public const int LINE_LENGTH = 8;
        public const int GRID_NUM = LINE_LENGTH * LINE_LENGTH;

        static readonly Vector256<ulong> SHIFT_1897 = Vector256.Create(1UL, 8UL, 9UL, 7UL);
        static readonly Vector256<ulong> SHIFT_1897_2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
        static readonly Vector256<ulong> FLIP_MASK = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

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

        public Color[,] GetDiscsArray()
        {
            var discs = new Color[LINE_LENGTH, LINE_LENGTH];
            var currentPlayer = this.Turn;
            var opponentPlayer = (Color)(-(int)this.Turn);

            var mask = 1UL;
            for(var x = 0; x < discs.Length; x++)
                for(var y = 0; y < discs.Length; y++)
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

        public void Update(Move move)
        {
            this.mobilityWasCalculated = false;
        }

        public int GetNextMoves(Move[] moves)
        {
            var mobility = CalculateMobility();
            var mask = 1UL;
            var moveNum = BitManipulations.PopCount(mobility);
            if(moveNum == 0)
            {
                moves[0].Color = this.Turn;
                moves[0].Pos = Move.PASS;
                return 1;
            }
            // kokokara
        }

        ulong CalculateMobility()
        {
            if (this.mobilityWasCalculated)
                return this.mobility;

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
        }

        ulong CalculateFlippedDiscs()
        {

        }

        static ulong CalculateMobility_AVX2(ulong p, ulong o)   // p is current player's board      o is opponent player's board
        {
            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), FLIP_MASK);
            var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, SHIFT_1897));
            var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, SHIFT_1897);

            var flipLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(p4, SHIFT_1897));
            var flipRight = Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(p4, SHIFT_1897));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, SHIFT_1897)));
            flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, SHIFT_1897)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, SHIFT_1897_2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, SHIFT_1897_2)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, SHIFT_1897_2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, SHIFT_1897_2)));

            var mobility4 = Avx2.ShiftLeftLogicalVariable(flipLeft, SHIFT_1897);
            mobility4 = Avx2.Or(mobility4, Avx2.ShiftRightLogicalVariable(flipRight, SHIFT_1897));
            var mobility2 = Sse2.Or(Avx2.ExtractVector128(mobility4, 0), Avx2.ExtractVector128(mobility4, 1));
            mobility2 = Sse2.Or(mobility2, Sse2.UnpackHigh(mobility2, mobility2));
            return Sse2.X64.ConvertToUInt64(mobility2) & ~(p | o);
        }

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
            mobility2 = Sse2.Or(mobility2, Sse2.ShiftLeftLogical(flip, 9));
            mobility |= (flip1 >> 1) | (flip8 >> 8);

            if(Sse2.X64.IsSupported)
                mobility |= Sse2.X64.ConvertToUInt64(mobility2) | BitManipulations.ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(mobility2, mobility2)));
            else
                mobility |= mobility2.GetElement(0) | BitManipulations.ByteSwap(Sse2.UnpackHigh(mobility2, mobility2).GetElement(0));
            return mobility & ~(p | o);
        }

        static int StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return posX + posY * LINE_LENGTH;
        }
    }
}
