using static Kalmia.Board;

namespace Kalmia
{
    public enum Color
    {
        Black = 1,
        Blank = 0,
        White = -1
    }

    public struct Move
    {
        public Color Color;
        public int Pos;

        public Move(Color color, string pos) : this(color, StringToPos(pos)) { }

        public Move(Color color, int posX, int posY):this(color, posX + posY * LINE_LENGTH) { }

        public Move(Color color, int pos)
        {
            this.Color = color;
            this.Pos = pos;
        }

        public override string ToString()
        {
            var posX = (char)(this.Pos % LINE_LENGTH);
            var posY = this.Pos / LINE_LENGTH;
            return $"{char.ToUpper(posX)}{posY}";
        }

        static int StringToPos(string pos)
        {
            var posX = char.ToLower(pos[0]) - 'a';
            var posY = int.Parse(pos[1].ToString());
            return posX + posY * LINE_LENGTH;
        }
    }
}
