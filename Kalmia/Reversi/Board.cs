using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

using static Kalmia.Reversi.Board;

namespace Kalmia.Reversi
{
    public enum GameResult : sbyte
    {
        Win = 1,
        Loss = -1,
        Draw = 0,
        NotOver = -2
    }

    public enum DiscColor : sbyte
    {
        Black = 0,
        Null = 2,
        White = 1
    }

    public enum BoardCoordinate : byte
    {
        A1, B1, C1, D1, E1, F1, G1, H1,
        A2, B2, C2, D2, E2, F2, G2, H2,
        A3, B3, C3, D3, E3, F3, G3, H3,
        A4, B4, C4, D4, E4, F4, G4, H4,
        A5, B5, C5, D5, E5, F5, G5, H5,
        A6, B6, C6, D6, E6, F6, G6, H6,
        A7, B7, C7, D7, E7, F7, G7, H7,
        A8, B8, C8, D8, E8, F8, G8, H8,
        Pass, Null
    }

    public struct Move
    {
        public static Move Null { get; } = new Move(DiscColor.Null, BoardCoordinate.Null);

        public DiscColor Color;
        public BoardCoordinate Coord;

        public int CoordX { get { return (byte)this.Coord % BOARD_SIZE; } }
        public int CoordY { get { return (byte)this.Coord / BOARD_SIZE; } }

        public Move(DiscColor color, string pos) : this(color, StringToPosition(pos)) { }

        public Move(DiscColor color, (int x, int y) coord) : this(color, coord.x, coord.y) { }

        public Move(DiscColor color, int x, int y) : this(color, (BoardCoordinate)(x + y * BOARD_SIZE)) { }

        public Move(DiscColor color, BoardCoordinate coord)
        {
            this.Color = color;
            this.Coord = coord;
        }

        public override string ToString()
        {
            if (this.Coord == BoardCoordinate.Pass)
                return "pass";

            var posX = (char)('A' + (byte)this.Coord % BOARD_SIZE);
            var posY = (byte)this.Coord / BOARD_SIZE;
            return $"{char.ToUpper(posX)}{posY + 1}";
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Move))
                return false;
            return this == (Move)obj;
        }

        public override int GetHashCode()   // This method will not be used. I implemented this just to suppress a caution.
        {
            return (int)this.Color * (byte)this.Coord;
        }

        public static bool operator ==(Move left, Move right)
        {
            return (left.Color == right.Color) && (left.Coord == right.Coord);
        }

        public static bool operator !=(Move left, Move right)
        {
            return !(left == right);
        }

        public static BoardCoordinate StringToPosition(string pos)
        {
            if (pos.ToLower() == "pass")
                return BoardCoordinate.Pass;
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return (BoardCoordinate)(posX + posY * BOARD_SIZE);
        }
    }

    public class Board
    {
        public const int BOARD_SIZE = 8;
        public const int SQUARE_NUM = BOARD_SIZE * BOARD_SIZE;
        public const int MAX_MOVE_CANDIDATE_COUNT = 46;
        const int BOARD_HISTORY_STACK_SIZE = 96;

        FastBoard fastBoard;
        Stack<Bitboard> boardHistory = new Stack<Bitboard>(BOARD_HISTORY_STACK_SIZE);

        public DiscColor SideToMove { get { return fastBoard.SideToMove; } }
        public DiscColor Opponent { get { return fastBoard.Opponent; } }

        public Board(DiscColor sideToMove) : this (sideToMove, 0UL, 0UL)
        {
            this.fastBoard.PutCurrentPlayerDisc(BoardCoordinate.E4);
            this.fastBoard.PutCurrentPlayerDisc(BoardCoordinate.D5);
            this.fastBoard.PutOpponentPlayerDisc(BoardCoordinate.D4);
            this.fastBoard.PutOpponentPlayerDisc(BoardCoordinate.E5);
        }

        public Board(DiscColor sideToMove, Bitboard bitboard):this(sideToMove, bitboard.CurrentPlayer, bitboard.OpponentPlayer) { }

        public Board(DiscColor sideToMove, ulong currentPlayerBoard, ulong opponentPlayerBoard)
        {
            this.fastBoard = new FastBoard(sideToMove, new Bitboard(currentPlayerBoard, opponentPlayerBoard));
        }

        public Board(Board board)
        {
            this.fastBoard = new FastBoard(board.fastBoard);
            this.boardHistory = board.boardHistory.Copy();
        }

        public void Init(DiscColor sideToMove, Bitboard bitboard)
        {
            this.fastBoard = new FastBoard(sideToMove, bitboard);
        }

        public FastBoard GetFastBoard()
        {
            return new FastBoard(this.fastBoard);
        }

        public ulong GetBitboard(DiscColor color)
        {
            var bitboard = this.fastBoard.GetBitboard();
            return (this.SideToMove == color) ? bitboard.CurrentPlayer : bitboard.OpponentPlayer;
        }

        public Bitboard GetBitBoard()
        {
            return this.fastBoard.GetBitboard();
        }

        public int GetDiscCount(DiscColor color)
        {
            return (this.SideToMove == color) ? this.fastBoard.GetCurrentPlayerDiscCount() : this.fastBoard.GetOpponentPlayerDiscCount();
        }

        public int GetEmptyCount()
        {
            return this.fastBoard.GetEmptyCount();
        }

        public DiscColor GetColor(int posX, int posY)
        {
            return GetDiscColor((BoardCoordinate)(posX + posY * BOARD_SIZE));
        }

        public DiscColor GetDiscColor(BoardCoordinate pos)
        {
            return this.fastBoard.GetDiscColor(pos);
        }

        public DiscColor[,] GetDiscsArray()
        {
            var discs = new DiscColor[BOARD_SIZE, BOARD_SIZE];
            for (var i = 0; i < discs.GetLength(0); i++)
                for (var j = 0; j < discs.GetLength(1); j++)
                    discs[i, j] = DiscColor.Null;
            var currentPlayer = this.SideToMove;
            var opponentPlayer = this.SideToMove ^ DiscColor.White;
            var bitboard = this.fastBoard.GetBitboard();
            var p = bitboard.CurrentPlayer;
            var o = bitboard.OpponentPlayer;

            var mask = 1UL;
            for(var y = 0; y < discs.GetLength(0); y++)
                for(var x = 0; x < discs.GetLength(1); x++)
                {
                    if ((p & mask) != 0)
                        discs[x, y] = currentPlayer;
                    else if ((o & mask) != 0)
                        discs[x, y] = opponentPlayer;
                    mask <<= 1;
                }
            return discs;
        }

        public void SwitchSideToMove()
        {
            this.fastBoard.SwitchSideToMove();
        }

        public void Put(DiscColor color, string pos)
        {
            Put(color, StringToPos(pos));
        }

        public void Put(DiscColor color, int posX, int posY)
        {
            Put(color, (BoardCoordinate)(posX + posY * BOARD_SIZE));
        }

        public void Put(DiscColor color, BoardCoordinate pos)
        {
            this.boardHistory.Push(this.fastBoard.GetBitboard());
            if (color == this.SideToMove)
                this.fastBoard.PutCurrentPlayerDisc(pos);
            else
                this.fastBoard.PutOpponentPlayerDisc(pos);
        }

        public bool Update(Move move)
        {
            if (move.Color != this.SideToMove || !this.fastBoard.IsLegalPosition(move.Coord))
                return false;
            this.boardHistory.Push(this.fastBoard.GetBitboard());
            this.fastBoard.Update(move.Coord);
            return true;
        }

        public bool Undo()
        {
            if (this.boardHistory.Count == 0)
                return false;
            this.SwitchSideToMove();
            this.fastBoard.SetBitboard(this.boardHistory.Pop());
            return true;
        }

        public bool IsLegalMove(Move move)
        {
            if (move.Color != this.SideToMove)
                return false;
            return fastBoard.IsLegalPosition(move.Coord);
        }

        public Move[] GetNextMoves()
        {
            var positions = new BoardCoordinate[MAX_MOVE_CANDIDATE_COUNT];
            var count = this.fastBoard.GetNextPositionCandidates(positions);
            return (from pos in positions[..count] select new Move(this.SideToMove, pos)).ToArray();
        }

        public GameResult GetGameResult(DiscColor color)
        {
            var result = this.fastBoard.GetGameResult();
            if (result == GameResult.NotOver)
                return result;
            return (color == this.SideToMove) ? result : (GameResult)(-(int)result);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("  ");
            for (var i = 0; i < BOARD_SIZE; i++)
                sb.Append($"{(char)('A' + i)} ");

            var bitboard = this.fastBoard.GetBitboard();
            var p = bitboard.CurrentPlayer;
            var o = bitboard.OpponentPlayer;
            var mask = 1UL << (SQUARE_NUM - 1);
            for (var y = BOARD_SIZE - 1; y >= 0; y--)
            {
                sb.Append($"\n{y + 1} ");
                var line = new StringBuilder();
                for (var x = 0; x < BOARD_SIZE; x++)
                {
                    if ((p & mask) != 0)
                        line.Append((this.SideToMove == DiscColor.Black) ? "X " : "O ");
                    else if ((o & mask) != 0)
                        line.Append((this.SideToMove != DiscColor.Black) ? "X " : "O ");    
                    else
                        line.Append(". ");
                    mask >>= 1;
                }
                sb.Append(line.ToString().Reverse().ToArray());
            }
            return sb.ToString();
        }

        static BoardCoordinate StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return (BoardCoordinate)(posX + posY * BOARD_SIZE);
        }
    }
}
