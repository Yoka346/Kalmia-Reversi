using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using static Kalmia.BitManipulations;

namespace Kalmia.Reversi
{
    public struct Bitboard
    {
        public ulong CurrentPlayer { get; set; }
        public ulong OpponentPlayer { get; set; }
        public ulong Empty { get { return ~(this.CurrentPlayer | this.OpponentPlayer); } }

        public Bitboard(ulong currentPlayer, ulong opponentPlayer)
        {
            this.CurrentPlayer = currentPlayer;
            this.OpponentPlayer = opponentPlayer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCurrentPlayerDiscCount()
        {
            return (int)PopCount(this.CurrentPlayer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOpponentPlayerDiscCount()
        {
            return (int)PopCount(this.OpponentPlayer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEmptyCount()
        {
            return (int)PopCount(~(this.CurrentPlayer | this.OpponentPlayer));
        }

        public Bitboard Mirror()
        {
            return new Bitboard(MirrorByte(this.CurrentPlayer), MirrorByte(this.OpponentPlayer));
        }

        public Bitboard FlipVertical()
        {
            return new Bitboard(ByteSwap(this.CurrentPlayer), ByteSwap(this.OpponentPlayer));
        }

        public Bitboard Rotate90Clockwise()
        {
            return new Bitboard(BitManipulations.Rotate90Clockwise(this.CurrentPlayer), BitManipulations.Rotate90Clockwise(this.OpponentPlayer));
        }

        public Bitboard Rotate90AntiClockwise()
        {
            return new Bitboard(BitManipulations.Rotate90AntiClockwise(this.CurrentPlayer), BitManipulations.Rotate90AntiClockwise(this.OpponentPlayer));
        }

        public Bitboard Rotate180Clockwise()
        {
            var p = BitManipulations.Rotate90Clockwise(BitManipulations.Rotate90Clockwise(this.CurrentPlayer));
            var o = BitManipulations.Rotate90Clockwise(BitManipulations.Rotate90Clockwise(this.OpponentPlayer));
            return new Bitboard(p, o);
        }

        public override bool Equals(object obj)
        {
            return obj is Bitboard && (Bitboard)obj == this;
        }

        public override int GetHashCode()   // This method is just for suppressing a caution.
        {
            return base.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Bitboard left, Bitboard right)
        {
            return left.CurrentPlayer == right.CurrentPlayer && left.OpponentPlayer == right.OpponentPlayer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Bitboard left, Bitboard right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    /// Provides fast reversi board for searching.
    /// Source code reference: https://github.com/okuhara/edax-reversi-AVX/blob/master/src/board_sse.c
    /// See also: http://www.amy.hi-ho.ne.jp/okuhara/bitboard.htm (Japanese document)
    /// </summary>
    public class FastBoard     // board for searching
    {
        static readonly ulong[] HASH_RANK;
        const int HASH_RANK_DIM_0_LEN = Board.BOARD_SIZE * 2;
        const int HASH_RANK_DIM_1_LEN = 256;

        // The index 0 represents square where player plays, the index 1 represents player's discs pattern on the line,
        // and the element represents twice the number of discs flipped when there are no empty squares except for a square where a disc put.
        // Why double ? The final discs diffrence is brought by following formula:
        // final_disc_diff = (current_player_disc_count + last_flipped_disc_count + 1) - (current_opponent_disc_count - last_flipped_disc_count)
        // When rearranging this formula:
        // final_disc_diff = (currrent_player_disc_count - current_opponent_disc_count) + 2 * last_flipped_disc_count + 1
        // that's why, this table has double last flipped discs count.
        static byte[][] LAST_FLIP_DISC_DOUBLE_COUNT;

        // The index 0 represents square position(A1 ~ H8), the each element reprensents mask of diagonal line, vertical line, and all lines which contain specified square(index 0).
        // These masks are from the following source code.
        // https://github.com/okuhara/edax-reversi-AVX/blob/master/src/count_last_flip_bmi2.c
        static ulong[][] LINE_MASK = new ulong[64][]
        {
            new ulong[4] { 0x0000000000000001UL, 0x8040201008040201UL, 0x0101010101010101UL, 0x81412111090503ffUL },
            new ulong[4] { 0x0000000000000102UL, 0x0080402010080402UL, 0x0202020202020202UL, 0x02824222120a07ffUL },
            new ulong[4] { 0x0000000000010204UL, 0x0000804020100804UL, 0x0404040404040404UL, 0x0404844424150effUL },
            new ulong[4] { 0x0000000001020408UL, 0x0000008040201008UL, 0x0808080808080808UL, 0x08080888492a1cffUL },
            new ulong[4] { 0x0000000102040810UL, 0x0000000080402010UL, 0x1010101010101010UL, 0x10101011925438ffUL },
            new ulong[4] { 0x0000010204081020UL, 0x0000000000804020UL, 0x2020202020202020UL, 0x2020212224a870ffUL },
            new ulong[4] { 0x0001020408102040UL, 0x0000000000008040UL, 0x4040404040404040UL, 0x404142444850e0ffUL },
            new ulong[4] { 0x0102040810204080UL, 0x0000000000000080UL, 0x8080808080808080UL, 0x8182848890a0c0ffUL },
            new ulong[4] { 0x0000000000000102UL, 0x4020100804020104UL, 0x0101010101010101UL, 0x412111090503ff03UL },
            new ulong[4] { 0x0000000000010204UL, 0x8040201008040201UL, 0x0202020202020202UL, 0x824222120a07ff07UL },
            new ulong[4] { 0x0000000001020408UL, 0x0080402010080402UL, 0x0404040404040404UL, 0x04844424150eff0eUL },
            new ulong[4] { 0x0000000102040810UL, 0x0000804020100804UL, 0x0808080808080808UL, 0x080888492a1cff1cUL },
            new ulong[4] { 0x0000010204081020UL, 0x0000008040201008UL, 0x1010101010101010UL, 0x101011925438ff38UL },
            new ulong[4] { 0x0001020408102040UL, 0x0000000080402010UL, 0x2020202020202020UL, 0x20212224a870ff70UL },
            new ulong[4] { 0x0102040810204080UL, 0x0000000000804020UL, 0x4040404040404040UL, 0x4142444850e0ffe0UL },
            new ulong[4] { 0x0204081020408001UL, 0x0000000000008040UL, 0x8080808080808080UL, 0x82848890a0c0ffc0UL },
            new ulong[4] { 0x0000000000010204UL, 0x201008040201000aUL, 0x0101010101010101UL, 0x2111090503ff0305UL },
            new ulong[4] { 0x0000000001020408UL, 0x4020100804020101UL, 0x0202020202020202UL, 0x4222120a07ff070aUL },
            new ulong[4] { 0x0000000102040810UL, 0x8040201008040201UL, 0x0404040404040404UL, 0x844424150eff0e15UL },
            new ulong[4] { 0x0000010204081020UL, 0x0080402010080402UL, 0x0808080808080808UL, 0x0888492a1cff1c2aUL },
            new ulong[4] { 0x0001020408102040UL, 0x0000804020100804UL, 0x1010101010101010UL, 0x1011925438ff3854UL },
            new ulong[4] { 0x0102040810204080UL, 0x0000008040201008UL, 0x2020202020202020UL, 0x212224a870ff70a8UL },
            new ulong[4] { 0x0204081020408001UL, 0x0000000080402010UL, 0x4040404040404040UL, 0x42444850e0ffe050UL },
            new ulong[4] { 0x0408102040800003UL, 0x0000000000804020UL, 0x8080808080808080UL, 0x848890a0c0ffc0a0UL },
            new ulong[4] { 0x0000000001020408UL, 0x1008040201000016UL, 0x0101010101010101UL, 0x11090503ff030509UL },
            new ulong[4] { 0x0000000102040810UL, 0x2010080402010005UL, 0x0202020202020202UL, 0x22120a07ff070a12UL },
            new ulong[4] { 0x0000010204081020UL, 0x4020100804020101UL, 0x0404040404040404UL, 0x4424150eff0e1524UL },
            new ulong[4] { 0x0001020408102040UL, 0x8040201008040201UL, 0x0808080808080808UL, 0x88492a1cff1c2a49UL },
            new ulong[4] { 0x0102040810204080UL, 0x0080402010080402UL, 0x1010101010101010UL, 0x11925438ff385492UL },
            new ulong[4] { 0x0204081020408001UL, 0x0000804020100804UL, 0x2020202020202020UL, 0x2224a870ff70a824UL },
            new ulong[4] { 0x0408102040800003UL, 0x0000008040201008UL, 0x4040404040404040UL, 0x444850e0ffe05048UL },
            new ulong[4] { 0x0810204080000007UL, 0x0000000080402010UL, 0x8080808080808080UL, 0x8890a0c0ffc0a090UL },
            new ulong[4] { 0x0000000102040810UL, 0x080402010000002eUL, 0x0101010101010101UL, 0x090503ff03050911UL },
            new ulong[4] { 0x0000010204081020UL, 0x100804020100000dUL, 0x0202020202020202UL, 0x120a07ff070a1222UL },
            new ulong[4] { 0x0001020408102040UL, 0x2010080402010003UL, 0x0404040404040404UL, 0x24150eff0e152444UL },
            new ulong[4] { 0x0102040810204080UL, 0x4020100804020101UL, 0x0808080808080808UL, 0x492a1cff1c2a4988UL },
            new ulong[4] { 0x0204081020408002UL, 0x8040201008040201UL, 0x1010101010101010UL, 0x925438ff38549211UL },
            new ulong[4] { 0x0408102040800005UL, 0x0080402010080402UL, 0x2020202020202020UL, 0x24a870ff70a82422UL },
            new ulong[4] { 0x081020408000000bUL, 0x0000804020100804UL, 0x4040404040404040UL, 0x4850e0ffe0504844UL },
            new ulong[4] { 0x1020408000000017UL, 0x0000008040201008UL, 0x8080808080808080UL, 0x90a0c0ffc0a09088UL },
            new ulong[4] { 0x0000010204081020UL, 0x040201000000005eUL, 0x0101010101010101UL, 0x0503ff0305091121UL },
            new ulong[4] { 0x0001020408102040UL, 0x080402010000001dUL, 0x0202020202020202UL, 0x0a07ff070a122242UL },
            new ulong[4] { 0x0102040810204080UL, 0x100804020100000bUL, 0x0404040404040404UL, 0x150eff0e15244484UL },
            new ulong[4] { 0x0204081020408001UL, 0x2010080402010003UL, 0x0808080808080808UL, 0x2a1cff1c2a498808UL },
            new ulong[4] { 0x0408102040800003UL, 0x4020100804020101UL, 0x1010101010101010UL, 0x5438ff3854921110UL },
            new ulong[4] { 0x081020408000000eUL, 0x8040201008040201UL, 0x2020202020202020UL, 0xa870ff70a8242221UL },
            new ulong[4] { 0x102040800000001dUL, 0x0080402010080402UL, 0x4040404040404040UL, 0x50e0ffe050484442UL },
            new ulong[4] { 0x204080000000003bUL, 0x0000804020100804UL, 0x8080808080808080UL, 0xa0c0ffc0a0908884UL },
            new ulong[4] { 0x0001020408102040UL, 0x02010000000000beUL, 0x0101010101010101UL, 0x03ff030509112141UL },
            new ulong[4] { 0x0102040810204080UL, 0x040201000000003dUL, 0x0202020202020202UL, 0x07ff070a12224282UL },
            new ulong[4] { 0x0204081020408001UL, 0x080402010000001bUL, 0x0404040404040404UL, 0x0eff0e1524448404UL },
            new ulong[4] { 0x0408102040800003UL, 0x1008040201000007UL, 0x0808080808080808UL, 0x1cff1c2a49880808UL },
            new ulong[4] { 0x0810204080000007UL, 0x2010080402010003UL, 0x1010101010101010UL, 0x38ff385492111010UL },
            new ulong[4] { 0x102040800000000fUL, 0x4020100804020101UL, 0x2020202020202020UL, 0x70ff70a824222120UL },
            new ulong[4] { 0x204080000000003eUL, 0x8040201008040201UL, 0x4040404040404040UL, 0xe0ffe05048444241UL },
            new ulong[4] { 0x408000000000007dUL, 0x0080402010080402UL, 0x8080808080808080UL, 0xc0ffc0a090888482UL },
            new ulong[4] { 0x0102040810204080UL, 0x010000000000027eUL, 0x0101010101010101UL, 0xff03050911214181UL },
            new ulong[4] { 0x0204081020408001UL, 0x020100000000007dUL, 0x0202020202020202UL, 0xff070a1222428202UL },
            new ulong[4] { 0x0408102040800003UL, 0x040201000000003bUL, 0x0404040404040404UL, 0xff0e152444840404UL },
            new ulong[4] { 0x0810204080000007UL, 0x0804020100000017UL, 0x0808080808080808UL, 0xff1c2a4988080808UL },
            new ulong[4] { 0x102040800000000fUL, 0x1008040201000007UL, 0x1010101010101010UL, 0xff38549211101010UL },
            new ulong[4] { 0x204080000000001fUL, 0x2010080402010003UL, 0x2020202020202020UL, 0xff70a82422212020UL },
            new ulong[4] { 0x408000000000003fUL, 0x4020100804020101UL, 0x4040404040404040UL, 0xffe0504844424140UL },
            new ulong[4] { 0x800000000000017eUL, 0x8040201008040201UL, 0x8080808080808080UL, 0xffc0a09088848281UL }
        };

        Bitboard bitboard;
        public DiscColor SideToMove { get; private set; }
        public DiscColor Opponent { get { return this.SideToMove ^ DiscColor.White; } }
        bool mobilityWasCalculated = false;
        ulong mobility;

        static FastBoard()
        {
            HASH_RANK = new ulong[HASH_RANK_DIM_0_LEN * HASH_RANK_DIM_1_LEN];
            var rand = new Random();
            for (var i = 0; i < HASH_RANK_DIM_0_LEN; i++)
            {
                for (var j = 0; j < HASH_RANK_DIM_1_LEN; j++)
                    HASH_RANK[i * HASH_RANK_DIM_0_LEN + j] = (ulong)rand.NextInt64();
            }

            LAST_FLIP_DISC_DOUBLE_COUNT = (from _ in Enumerable.Range(0, 8) select new byte[256]).ToArray();
            InitLastFlippedDiscCountTable();
        }

        static void InitLastFlippedDiscCountTable()
        {
            for (var pos = 0; pos < LAST_FLIP_DISC_DOUBLE_COUNT.Length; pos++)
            {
                var x = 1u << pos;
                var flipCount = LAST_FLIP_DISC_DOUBLE_COUNT[pos];
                for (var playerPattern = 0u; playerPattern < flipCount.Length; playerPattern++)
                {
                    var opponentPattern = ~playerPattern & 0x000000ffu;
                    if ((opponentPattern & x) != 0)
                        opponentPattern ^= x;

                    var flipped0 = (x << 1) & opponentPattern;
                    flipped0 |= (flipped0 << 1) & opponentPattern;
                    var prefix0 = opponentPattern & (opponentPattern << 1);
                    flipped0 |= (flipped0 << 2) & prefix0;
                    flipped0 |= (flipped0 << 2) & prefix0;
                    var outflank0 = playerPattern & (flipped0 << 1);
                    if (outflank0 == 0)
                        flipped0 = 0u;

                    var flipped1 = (x >> 1) & opponentPattern;
                    flipped1 |= (flipped1 >> 1) & opponentPattern;
                    var prefix1 = opponentPattern & (opponentPattern >> 1);
                    flipped1 |= (flipped1 >> 2) & prefix1;
                    flipped1 |= (flipped1 >> 2) & prefix1;
                    var outflank1 = playerPattern & (flipped1 >> 1);
                    if (outflank1 == 0)
                        flipped1 = 0u;
                    flipCount[playerPattern] = (byte)(PopCount(flipped0 | flipped1) << 1);
                }
            }
        }

        public FastBoard() : this(new Board(DiscColor.Black, InitialBoardState.Cross)) { }

        public FastBoard(Board board) : this(board.SideToMove, board.GetBitBoard()) { }

        public FastBoard(DiscColor sideToMove, Bitboard bitboard)
        {
            Init(sideToMove, bitboard);
        }

        public FastBoard(FastBoard board)
        {
            this.bitboard = board.bitboard;
            this.SideToMove = board.SideToMove;
            this.mobilityWasCalculated = board.mobilityWasCalculated;
            this.mobility = board.mobility;
        }

        public static DiscColor GetOpponentColor(DiscColor color)
        {
            return color ^ DiscColor.White;
        }

        public void Init(DiscColor sideToMove, Bitboard bitboard)
        {
            this.bitboard = bitboard;
            this.SideToMove = sideToMove;
        }

        public Bitboard GetBitboard()
        {
            return this.bitboard;
        }

        public void SetBitboard(Bitboard bitboard)
        {
            this.bitboard = bitboard;
            this.mobilityWasCalculated = false;
        }

        public void CopyTo(FastBoard dest)
        {
            dest.bitboard = this.bitboard;
            dest.SideToMove = this.SideToMove;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetCurrentPlayerDiscCount()
        {
            return this.bitboard.GetCurrentPlayerDiscCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOpponentPlayerDiscCount()
        {
            return this.bitboard.GetOpponentPlayerDiscCount();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetEmptyCount()
        {
            return this.bitboard.GetEmptyCount();
        }

        /// <summary>
        /// Counts flipped discs when current player plays last one empty square.
        /// Source code reference: https://github.com/okuhara/edax-reversi-AVX/blob/master/src/count_last_flip_bmi2.c
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetLastFlippedDiscDoubleCount(BoardPosition pos)
        {
            var colIdx = (int)pos & 7;  // same as (int)pos % 8
            var rowIdx = (int)pos >> 3; // same as (int)pos / 8
            var mask = LINE_MASK[(int)pos];
            var p = this.bitboard.CurrentPlayer & mask[3];
            var count = LAST_FLIP_DISC_DOUBLE_COUNT[colIdx][(byte)(p >> ((int)pos & 0x38))];    // (int)pos & 0x38 is same as ((int)pos / 8) * 8
            count += LAST_FLIP_DISC_DOUBLE_COUNT[rowIdx][ParallelBitExtract(p, mask[0])];
            count += LAST_FLIP_DISC_DOUBLE_COUNT[rowIdx][ParallelBitExtract(p, mask[1])];
            count += LAST_FLIP_DISC_DOUBLE_COUNT[rowIdx][ParallelBitExtract(p, mask[2])];
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DiscColor GetDiscColor(BoardPosition pos)
        {
            var x = (int)pos;
            var sideToMove = (ulong)this.SideToMove + 1UL;
            var color = sideToMove * ((this.bitboard.CurrentPlayer >> x) & 1) + (sideToMove ^ 3) * ((this.bitboard.OpponentPlayer >> x) & 1);
            return (color != 0) ? (DiscColor)(color - 1) : DiscColor.Null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PutCurrentPlayerDisc(BoardPosition pos)
        {
            var x = 1UL << (int)pos;
            this.bitboard.CurrentPlayer |= x;
            this.bitboard.OpponentPlayer &= ~x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PutOpponentPlayerDisc(BoardPosition pos)
        {
            var x = 1UL << (int)pos;
            this.bitboard.OpponentPlayer |= x;
            this.bitboard.CurrentPlayer &= ~x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLegalPosition(BoardPosition pos)
        {
            var x = 1UL << (int)pos;
            var mobility = GetCurrentPlayerMobility();
            return (PopCount(mobility) == 0 && pos == BoardPosition.Pass) || (mobility & x) != 0UL;
        }

        public void PutStoneWithoutFlip(DiscColor color, BoardPosition pos)
        {
            if (this.SideToMove == color)
            {
                var mask = 1UL << (int)pos;
                this.bitboard.CurrentPlayer |= mask;
                if ((this.bitboard.OpponentPlayer & mask) != 0)
                    this.bitboard.OpponentPlayer ^= mask;
            }
            else
            {
                var mask = 1UL << (int)pos;
                this.bitboard.OpponentPlayer |= mask;
                if ((this.bitboard.CurrentPlayer & mask) != 0)
                    this.bitboard.CurrentPlayer ^= mask;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Update(BoardPosition pos)
        {
            var flipped = 0UL;
            if (pos != BoardPosition.Pass)
            {
                var x = 1UL << (byte)pos;
                flipped = CalculateFlippedDiscs((byte)pos);
                this.bitboard.OpponentPlayer ^= flipped;
                this.bitboard.CurrentPlayer |= (flipped | x);
            }
            SwitchSideToMove();
            return flipped;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GameResult GetGameResult()
        {
            if (this.bitboard.GetEmptyCount() != 0)
            {
                var mobility = GetCurrentPlayerMobility();
                if (PopCount(mobility) != 0 || PopCount(CalculateMobility(this.bitboard.OpponentPlayer, this.bitboard.CurrentPlayer)) != 0)
                    return GameResult.NotOver;
            }

            var diff = (int)PopCount(this.bitboard.CurrentPlayer) - (int)PopCount(this.bitboard.OpponentPlayer);
            if (diff > 0)
                return GameResult.Win;
            if (diff < 0)
                return GameResult.Loss;
            return GameResult.Draw;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SwitchSideToMove()
        {
            var tmp = this.bitboard.CurrentPlayer;
            this.bitboard.CurrentPlayer = this.bitboard.OpponentPlayer;
            this.bitboard.OpponentPlayer = tmp;
            this.mobilityWasCalculated = false;
            this.SideToMove ^= DiscColor.White;
        }

        public int GetNextPositionCandidates(BoardPosition[] positions) => GetNextPositionCandidates(positions.AsSpan());

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextPositionCandidates(Span<BoardPosition> positions)
        {
            var mobility = GetCurrentPlayerMobility();
            var posCount = (int)PopCount(mobility);
            if (posCount == 0)
            {
                positions[0] = BoardPosition.Pass;
                return 1;
            }

            var mask = 1UL;
            var idx = 0;
            for (byte i = 0; idx < posCount; i++)
            {
                if ((mobility & mask) != 0)
                    positions[idx++] = (BoardPosition)i;
                mask <<= 1;
            }
            return posCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetNextPositionsCandidatesNumAfter(BoardPosition pos)
        {
            var bitboard = this.bitboard;
            if (pos != BoardPosition.Pass)
            {
                var flipped = CalculateFlippedDiscs((int)pos);
                bitboard.CurrentPlayer |= (flipped | (1UL << (int)pos));
                bitboard.OpponentPlayer ^= flipped;
            }
            return (int)PopCount(CalculateMobility(bitboard.OpponentPlayer, bitboard.CurrentPlayer));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetCurrentPlayerMobility()
        {
            if (!this.mobilityWasCalculated)
            {
                this.mobility = CalculateMobility(this.bitboard.CurrentPlayer, this.bitboard.OpponentPlayer);
                this.mobilityWasCalculated = true;
            }
            return this.mobility;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetCurrentPlayerMobility(out int mobilityNum)
        {
            GetCurrentPlayerMobility();
            mobilityNum = (int)PopCount(this.mobility);
            return this.mobility;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe new ulong GetHashCode()
        {
            var bb = this.bitboard;
            var p = (byte*)&bb;
            ulong hashCode;

            fixed (ulong* hash_rank = &HASH_RANK[0])
            {
                var h0 = Sse2.LoadHigh(Sse2.LoadScalarVector128(&hash_rank[p[0]]).AsDouble(), (double*)&hash_rank[4 * HASH_RANK_DIM_0_LEN + p[4]]);
                var h1 = Sse2.LoadHigh(Sse2.LoadScalarVector128(&hash_rank[HASH_RANK_DIM_0_LEN + p[1]]).AsDouble(), (double*)&hash_rank[5 * HASH_RANK_DIM_0_LEN + p[5]]);
                var h2 = Sse2.LoadHigh(Sse2.LoadScalarVector128(&hash_rank[2 * HASH_RANK_DIM_0_LEN + p[2]]).AsDouble(), (double*)&hash_rank[6 * HASH_RANK_DIM_0_LEN + p[6]]);
                var h3 = Sse2.LoadHigh(Sse2.LoadScalarVector128(&hash_rank[3 * HASH_RANK_DIM_0_LEN + p[3]]).AsDouble(), (double*)&hash_rank[7 * HASH_RANK_DIM_0_LEN + p[7]]);
                h0 = Sse2.Xor(h0, h2);
                h1 = Sse2.Xor(h1, h3);
                h2 = Sse2.LoadHigh(Sse2.LoadScalarVector128(&hash_rank[8 * HASH_RANK_DIM_0_LEN + p[8]]).AsDouble(), (double*)&hash_rank[10 * HASH_RANK_DIM_0_LEN + p[10]]);
                h3 = Sse2.LoadHigh(Sse2.LoadScalarVector128(&hash_rank[9 * HASH_RANK_DIM_0_LEN + p[9]]).AsDouble(), (double*)&hash_rank[11 * HASH_RANK_DIM_0_LEN + p[11]]);
                h0 = Sse2.Xor(h0, h2);
                h1 = Sse2.Xor(h1, h3);
                h2 = Sse2.LoadHigh(Sse2.LoadScalarVector128(&hash_rank[12 * HASH_RANK_DIM_0_LEN + p[12]]).AsDouble(), (double*)&hash_rank[14 * HASH_RANK_DIM_0_LEN + p[14]]);
                h3 = Sse2.LoadHigh(Sse2.LoadScalarVector128(&hash_rank[13 * HASH_RANK_DIM_0_LEN + p[13]]).AsDouble(), (double*)&hash_rank[15 * HASH_RANK_DIM_0_LEN + p[15]]);
                h0 = Sse2.Xor(h0, h2);
                h1 = Sse2.Xor(h1, h3);
                h0 = Sse2.Xor(h0, h1);
                h0 = Sse2.Xor(h0, Sse.MoveHighToLow(h1.AsSingle(), h0.AsSingle()).AsDouble());
                if (Sse2.X64.IsSupported)
                    hashCode = Sse2.X64.ConvertToUInt64(h0.AsUInt64());
                else
                    hashCode = h0.AsUInt64().GetElement(0);
            }
            return hashCode;
        }

        // This method is for test.
        public unsafe ulong GetHashCode_CPU()
        {
            var bb = this.bitboard;
            var p = (byte*)&bb;
            var h0 = HASH_RANK[p[0]];
            var h1 = HASH_RANK[HASH_RANK_DIM_0_LEN + p[1]];
            h0 ^= HASH_RANK[2 * HASH_RANK_DIM_0_LEN + p[2]];
            h1 ^= HASH_RANK[3 * HASH_RANK_DIM_0_LEN + p[3]];
            h0 ^= HASH_RANK[4 * HASH_RANK_DIM_0_LEN + p[4]];
            h1 ^= HASH_RANK[5 * HASH_RANK_DIM_0_LEN + p[5]];
            h0 ^= HASH_RANK[6 * HASH_RANK_DIM_0_LEN + p[6]];
            h1 ^= HASH_RANK[7 * HASH_RANK_DIM_0_LEN + p[7]];
            h0 ^= HASH_RANK[8 * HASH_RANK_DIM_0_LEN + p[8]];
            h1 ^= HASH_RANK[9 * HASH_RANK_DIM_0_LEN + p[9]];
            h0 ^= HASH_RANK[10 * HASH_RANK_DIM_0_LEN + p[10]];
            h1 ^= HASH_RANK[11 * HASH_RANK_DIM_0_LEN + p[11]];
            h0 ^= HASH_RANK[12 * HASH_RANK_DIM_0_LEN + p[12]];
            h1 ^= HASH_RANK[13 * HASH_RANK_DIM_0_LEN + p[13]];
            h0 ^= HASH_RANK[14 * HASH_RANK_DIM_0_LEN + p[14]];
            h1 ^= HASH_RANK[15 * HASH_RANK_DIM_0_LEN + p[15]];
            return h0 ^ h1;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility_AVX2(ulong p, ulong o)   // p is current player's board      o is opponent player's board
        {
            var shift = Vector256.Create(1UL, 8UL, 9UL, 7UL);
            var shift2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
            var flipMask = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), flipMask);
            var prefixLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(maskedO4, shift));
            var prefixRight = Avx2.ShiftRightLogicalVariable(prefixLeft, shift);

            var flipLeft = Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(p4, shift));
            var flipRight = Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(p4, shift));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(maskedO4, Avx2.ShiftLeftLogicalVariable(flipLeft, shift)));
            flipRight = Avx2.Or(flipRight, Avx2.And(maskedO4, Avx2.ShiftRightLogicalVariable(flipRight, shift)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));
            flipLeft = Avx2.Or(flipLeft, Avx2.And(prefixLeft, Avx2.ShiftLeftLogicalVariable(flipLeft, shift2)));
            flipRight = Avx2.Or(flipRight, Avx2.And(prefixRight, Avx2.ShiftRightLogicalVariable(flipRight, shift2)));

            var mobility4 = Avx2.ShiftLeftLogicalVariable(flipLeft, shift);
            mobility4 = Avx2.Or(mobility4, Avx2.ShiftRightLogicalVariable(flipRight, shift));
            var mobility2 = Sse2.Or(Avx2.ExtractVector128(mobility4, 0), Avx2.ExtractVector128(mobility4, 1));
            mobility2 = Sse2.Or(mobility2, Sse2.UnpackHigh(mobility2, mobility2));
            return Sse2.X64.ConvertToUInt64(mobility2) & ~(p | o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateMobility_SSE(ulong p, ulong o)    // p is current player's board      o is opponent player's board
        {
            var maskedO = o & 0x7e7e7e7e7e7e7e7eUL;
            var p2 = Vector128.Create(p, ByteSwap(p));   // byte swap = vertical mirror
            var maskedO2 = Vector128.Create(maskedO, ByteSwap(maskedO));
            var prefix = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(maskedO2, 7));
            var prefix1 = maskedO & (maskedO << 1);
            var prefix8 = o & (o << 8);

            var flip = Sse2.And(maskedO2, Sse2.ShiftLeftLogical(p2, 7));
            var flip1 = maskedO & (p << 1);
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
                mobility |= Sse2.X64.ConvertToUInt64(mobility2) | ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(mobility2, mobility2)));
            else
                mobility |= mobility2.GetElement(0) | ByteSwap(Sse2.UnpackHigh(mobility2, mobility2).GetElement(0));
            return mobility & ~(p | o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateFilippedDiscs_AVX2(ulong p, ulong o, int pos)    // p is current player's board      o is opponent player's board
        {
            var shift = Vector256.Create(1UL, 8UL, 9UL, 7UL);
            var shift2 = Vector256.Create(2UL, 16UL, 18UL, 14UL);
            var flipMask = Vector256.Create(0x7e7e7e7e7e7e7e7eUL, 0xffffffffffffffffUL, 0x7e7e7e7e7e7e7e7eUL, 0x7e7e7e7e7e7e7e7eUL);

            var x = 1UL << pos;
            var x4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(x));
            var p4 = Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(p));
            var maskedO4 = Avx2.And(Avx2.BroadcastScalarToVector256(Sse2.X64.ConvertScalarToVector128UInt64(o)), flipMask);
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
            flipLeft = Avx2.AndNot(Avx2.CompareEqual(outflankLeft, Vector256<ulong>.Zero), flipLeft);
            flipRight = Avx2.AndNot(Avx2.CompareEqual(outflankRight, Vector256<ulong>.Zero), flipRight);
            var flip4 = Avx2.Or(flipLeft, flipRight);
            var flip2 = Sse2.Or(Avx2.ExtractVector128(flip4, 0), Avx2.ExtractVector128(flip4, 1));
            flip2 = Sse2.Or(flip2, Sse2.UnpackHigh(flip2, flip2));
            return Sse2.X64.ConvertToUInt64(flip2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong CalculateFlippedDiscs_SSE(ulong p, ulong o, int pos)    // p is current player's board      o is opponent player's board
        {
            var x = 1UL << pos;
            var maskedO = o & 0x7e7e7e7e7e7e7e7eUL;
            var x2 = Vector128.Create(x, ByteSwap(x));   // byte swap = vertical mirror
            var p2 = Vector128.Create(p, ByteSwap(p));
            var maskedO2 = Vector128.Create(maskedO, ByteSwap(maskedO));
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
                flip7 = Sse2.AndNot(Sse41.CompareEqual(outflank7, Vector128<ulong>.Zero), flip7);
                flip9 = Sse2.AndNot(Sse41.CompareEqual(outflank9, Vector128<ulong>.Zero), flip9);
            }
            else
            {
                flip7 = Sse2.And(Sse2.CompareNotEqual(outflank7.AsDouble(), Vector128<ulong>.Zero.AsDouble()).AsUInt64(), flip7);
                flip9 = Sse2.And(Sse2.CompareNotEqual(outflank9.AsDouble(), Vector128<ulong>.Zero.AsDouble()).AsUInt64(), flip9);
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
                             | ByteSwap(Sse2.X64.ConvertToUInt64(Sse2.UnpackHigh(flippedDiscs2, flippedDiscs2)));
            else
                flippedDiscs |= flippedDiscs2.GetElement(0) | ByteSwap(Sse2.UnpackHigh(flippedDiscs2, flippedDiscs2).GetElement(0));
            return flippedDiscs;
        }
    }
}
