using System.Collections.Generic;
using System.Linq;
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

        public Board(DiscColor sideToMove, InitialBoardState initState) : this(sideToMove, 0UL, 0UL)
        {
            if(initState == InitialBoardState.Cross)
            {
                this.fastBoard.PutCurrentPlayerDisc(BoardPosition.E4);
                this.fastBoard.PutCurrentPlayerDisc(BoardPosition.D5);
                this.fastBoard.PutOpponentPlayerDisc(BoardPosition.D4);
                this.fastBoard.PutOpponentPlayerDisc(BoardPosition.E5);
            }
            else
            {
                this.fastBoard.PutCurrentPlayerDisc(BoardPosition.D5);
                this.fastBoard.PutCurrentPlayerDisc(BoardPosition.E5);
                this.fastBoard.PutOpponentPlayerDisc(BoardPosition.D4);
                this.fastBoard.PutOpponentPlayerDisc(BoardPosition.E4);
            }
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
            return GetDiscColor((BoardPosition)(posX + posY * BOARD_SIZE));
        }

        public DiscColor GetDiscColor(BoardPosition pos)
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
            Put(color, (BoardPosition)(posX + posY * BOARD_SIZE));
        }

        public void Put(DiscColor color, BoardPosition pos)
        {
            this.boardHistory.Push(this.fastBoard.GetBitboard());
            if (color == this.SideToMove)
                this.fastBoard.PutCurrentPlayerDisc(pos);
            else
                this.fastBoard.PutOpponentPlayerDisc(pos);
        }

        public bool Update(Move move)
        {
            if (move.Color != this.SideToMove || !this.fastBoard.IsLegalPosition(move.Pos))
                return false;
            this.boardHistory.Push(this.fastBoard.GetBitboard());
            this.fastBoard.Update(move.Pos);
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
            return fastBoard.IsLegalPosition(move.Pos);
        }

        public Move[] GetNextMoves()
        {
            var positions = new BoardPosition[MAX_MOVE_CANDIDATE_COUNT];
            var count = this.fastBoard.GetNextPositions(positions);
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
            var mask = 1UL;
            for (var y = 0; y < BOARD_SIZE; y++)
            {
                sb.Append($"\n{y + 1} ");
                for (var x = 0; x < BOARD_SIZE; x++)
                {
                    if ((p & mask) != 0)
                        sb.Append((this.SideToMove == DiscColor.Black) ? "X " : "O ");
                    else if ((o & mask) != 0)
                        sb.Append((this.SideToMove != DiscColor.Black) ? "X " : "O ");    
                    else
                        sb.Append(". ");
                    mask <<= 1;
                }
            }
            return sb.ToString();
        }

        static BoardPosition StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return (BoardPosition)(posX + posY * BOARD_SIZE);
        }
    }
}
