using static Kalmia.Reversi.Board;

namespace Kalmia.Reversi
{
    public enum Color : sbyte
    {
        Black = 0,
        Empty = 2,
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
        public Color Color;
        public BoardPosition Pos;

        public int PosX { get { return (byte)this.Pos % BOARD_SIZE; } }
        public int PosY { get { return (byte)this.Pos / BOARD_SIZE; } }

        public Move(Color color, string pos) : this(color, StringToCoordinate(pos)) { }

        public Move(Color color, (int posX, int posY) coord) : this(color, coord.posX, coord.posY) { }

        public Move(Color color, int posX, int posY) : this(color, (BoardPosition)(posX + posY * BOARD_SIZE)) { }

        public Move(Color color, BoardPosition pos)
        {
            this.Color = color;
            this.Pos = pos;
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
            var move = (Move)obj;
            return (move.Color == this.Color) && (move.Pos == this.Pos);
        }

        public override int GetHashCode()   // This method will not be used. I implemented this just to suppress a caution.
        {
            return (int)this.Color * (byte)this.Pos;
        }

        public static bool operator ==(Move left, Move right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Move left, Move right)
        {
            return !(left == right);
        }

        static BoardPosition StringToCoordinate(string pos)
        {
            if (pos.ToLower() == "pass")
                return BoardPosition.Pass;
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return (BoardPosition)(posX + posY * BOARD_SIZE);
        }
    }
}
