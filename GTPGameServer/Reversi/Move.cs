using static GTPGameServer.Reversi.Board;

namespace GTPGameServer.Reversi
{
    public enum Color : sbyte
    {
        Black = 1,
        Empty = 0,
        White = -1
    }

    public struct Move
    {
        public const int PASS = 64;

        public Color Color;
        public byte Pos;

        public int PosX { get { return this.Pos % BOARD_SIZE; } }
        public int PosY { get { return this.Pos / BOARD_SIZE; } }

        public Move(Color color, string pos) : this(color, StringToPos(pos)) { }

        public Move(Color color, (int posX, int posY) coord) : this(color, coord.posX, coord.posY) { }

        public Move(Color color, int posX, int posY) : this(color, posX + posY * BOARD_SIZE) { }

        public Move(Color color, int pos)
        {
            this.Color = color;
            this.Pos = (byte)pos;
        }

        public override string ToString()
        {
            if (this.Pos == PASS)
                return "PASS";

            var posX = (char)('A' + this.Pos % BOARD_SIZE);
            var posY = this.Pos / BOARD_SIZE;
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
            return (int)this.Color * this.Pos;
        }

        public static bool operator ==(Move left, Move right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Move left, Move right)
        {
            return !(left == right);
        }

        static int StringToPos(string pos)
        {
            if (pos.ToLower() == "pass")
                return PASS;
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString()) - 1;
            return posX + posY * BOARD_SIZE;
        }
    }
}
