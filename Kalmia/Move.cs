using static Kalmia.Board;

namespace Kalmia
{
    public enum Color : sbyte
    {
        Black = 1,
        Blank = 0,
        White = -1
    }

    public struct Move
    {
        public const int PASS = 64;

        public Color Color;
        public byte Pos;

        public Move(Color color, string pos) : this(color, StringToPos(pos)) { }

        public Move(Color color, int posX, int posY):this(color, posX + posY * LINE_LENGTH) { }

        public Move(Color color, int pos)
        {
            this.Color = color;
            this.Pos = (byte)pos;
        }

        public override string ToString()
        {
            if (this.Pos == PASS)
                return "PASS";

            var posX = (char)('A' + this.Pos % LINE_LENGTH);
            var posY = this.Pos / LINE_LENGTH;
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

        public static bool operator==(Move left, Move right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(Move left, Move right)
        {
            return !(left == right);
        }

        static int StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString());
            return posX + posY * LINE_LENGTH;
        }
    }
}
