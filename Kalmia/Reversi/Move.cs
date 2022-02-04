using System;

using static Kalmia.Reversi.Board;

namespace Kalmia.Reversi
{
    public enum DiscColor : sbyte
    {
        Black = 0,
        Null = 2,
        White = 1
    }

    public enum BoardPosition : byte
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
        public static Move Null { get; } = new Move(DiscColor.Null, BoardPosition.Null);

        public DiscColor Color;
        public BoardPosition Pos;

        public int PosX { get { return (byte)this.Pos % BOARD_SIZE; } }
        public int PosY { get { return (byte)this.Pos / BOARD_SIZE; } }

        public Move(DiscColor color, string pos) : this(color, StringToPosition(pos)) { }

        public Move(DiscColor color, (int posX, int posY) coord) : this(color, coord.posX, coord.posY) { }

        public Move(DiscColor color, int posX, int posY) : this(color, (BoardPosition)(posX + posY * BOARD_SIZE)) { }

        public Move(DiscColor color, BoardPosition pos)
        {
            this.Color = color;
            this.Pos = pos;
        }

        public Move Mirror()
        {
            return new Move(this.Color, MirrorPosition(this.Pos));
        }

        public Move FlipVertical()
        {
            return new Move(this.Color, FlipPositionVertical(this.Pos));
        }

        public Move Rotate90Clockwise()
        {
            return new Move(this.Color, RotatePosition90Clockwise(this.Pos));
        }

        public Move Rotate90AntiClockwise()
        {
            return new Move(this.Color, RotatePosition90AntiClockwise(this.Pos));
        }

        public Move Rotate180Clockwise()
        {
            return new Move(this.Color, RotatePosition180Clockwise(this.Pos));
        }

        public override string ToString()
        {
            if (this.Pos == BoardPosition.Pass)
                return "PASS";

            var posX = (char)('A' + (byte)this.Pos % BOARD_SIZE);
            var posY = (byte)this.Pos / BOARD_SIZE;
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
            return (int)this.Color * (byte)this.Pos;
        }

        public static bool operator ==(Move left, Move right)
        {
            return (left.Color == right.Color) && (left.Pos == right.Pos);
        }

        public static bool operator !=(Move left, Move right)
        {
            return !(left == right);
        }

        public static BoardPosition MirrorPosition(BoardPosition pos)
        {
            (var posX, var posY) = ((int)pos % BOARD_SIZE, (int)pos / BOARD_SIZE);
            return (BoardPosition)((BOARD_SIZE - posX - 1) + posY * BOARD_SIZE);
        }

        public static BoardPosition FlipPositionVertical(BoardPosition pos)
        {
            (var posX, var posY) = ((int)pos % BOARD_SIZE, (int)pos / BOARD_SIZE);
            return (BoardPosition)(posX + (BOARD_SIZE - posY - 1) * BOARD_SIZE);
        }

        public static BoardPosition RotatePosition90Clockwise(BoardPosition pos)
        {
            (var posX, var posY) = ((int)pos % BOARD_SIZE, (int)pos / BOARD_SIZE);
            return (BoardPosition)((BOARD_SIZE - posY - 1) + posX * BOARD_SIZE);
        }

        public static BoardPosition RotatePosition90AntiClockwise(BoardPosition pos)
        {
            (var posX, var posY) = ((int)pos % BOARD_SIZE, (int)pos / BOARD_SIZE);
            return (BoardPosition)(posY + (BOARD_SIZE - posX - 1) * BOARD_SIZE);
        }

        public static BoardPosition RotatePosition180Clockwise(BoardPosition pos)
        {
            (var posX, var posY) = ((int)pos % BOARD_SIZE, (int)pos / BOARD_SIZE);
            return (BoardPosition)((BOARD_SIZE - posX - 1) + (BOARD_SIZE - posY - 1) * BOARD_SIZE);
        }

        public static BoardPosition StringToPosition(string pos)
        {
            if (pos.ToLower() == "pass")
                return BoardPosition.Pass;
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return (BoardPosition)(posX + posY * BOARD_SIZE);
        }
    }
}
