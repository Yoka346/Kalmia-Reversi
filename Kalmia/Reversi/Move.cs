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
        A1, A2, A3, A4, A5, A6, A7, A8,
        B1, B2, B3, B4, B5, B6, B7, B8,
        C1, C2, C3, C4, C5, C6, C7, C8,
        D1, D2, D3, D4, D5, D6, D7, D8,
        E1, E2, E3, E4, E5, E6, E7, E8,
        F1, F2, F3, F4, F5, F6, F7, F8,
        G1, G2, G3, G4, G5, G6, G7, G8,
        H1, H2, H3, H4, H5, H6, H7, H8,
        Pass
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
