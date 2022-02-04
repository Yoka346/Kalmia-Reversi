using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

using Kalmia.Reversi;

namespace Kalmia.IO
{
    internal class SGFNode
    {
        public int Depth { get; }
        public SGFNode Parent { get; }
        public List<SGFNode> ChildNodes { get; }
        public Dictionary<string, string> Properties { get; }

        public SGFNode(SGFNode parent, int depth)
        {
            this.Depth = depth;
            this.Parent = parent;
            this.ChildNodes = new List<SGFNode>();
            this.Properties = new Dictionary<string, string>();
        }

        public bool HasMove(DiscColor color)
        {
            if (color == DiscColor.Null)
                return false;
            return this.Properties.ContainsKey((color == DiscColor.Black) ? "B" : "W");
        }

        public string GetMove(DiscColor color)
        {
            if (color == DiscColor.Null)
                return string.Empty;
            return this.Properties[(color == DiscColor.Black) ? "B" : "W"];
        }
    }

    internal static class SGFFile
    {
        public static int SGFVersion = 1;

        public static SGFNode LoadSGFFile(string path)
        {
            var text = $"{File.ReadAllText(path)}\0";
            var loc = 0;

            int ch;
            do
                ch = text[loc++];
            while (ch != '(' && ch != '\0');
            if (ch == '\0')
                throw new FormatException();

            var root = new SGFNode(null, 0);
            LoadNodes(root, text, ref loc);
            return root;
        }

        public static BoardPosition SGFCoordinateToBoardPos(string sgfCoord)
        {
            if (sgfCoord.Length != 2)
                return BoardPosition.Null;

            if (sgfCoord == "tt")
                return BoardPosition.Pass;

            var chars = sgfCoord.ToLower().ToCharArray();
            var x = chars[0] - 'a';
            var y = chars[1] - 'a';
            if (x < 0 || y < 0 || x >= Board.BOARD_SIZE || y >= Board.BOARD_SIZE)
                return BoardPosition.Null;
            return (BoardPosition)(x + y * Board.BOARD_SIZE);
        }

        static void LoadNodes(SGFNode node, string text, ref int loc)
        {
            var sb = new StringBuilder();
            char ch;
            while ((ch = text[loc++]) != ';' && ch != '\0') ;
            if (ch == '\0')
                throw new FormatException();

            while (true)
            {
                while ((ch = text[loc++]) != '[' && ch != ';' && ch != '(' && ch != ')' && ch != '\0')
                    sb.Append(ch);

                if (ch == '\0')
                    throw new FormatException();

                if (ch == ')')
                    return;

                SGFNode childNode;
                switch (ch)
                {
                    case ';':
                        loc--;
                        childNode = new SGFNode(node, node.Depth + 1);
                        LoadNodes(childNode, text, ref loc);
                        node.ChildNodes.Add(childNode);
                        return;

                    case '[':
                        var propertyName = sb.Replace(" ", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty).ToString();
                        sb.Clear();

                        while ((ch = text[loc++]) != ']')
                        {
                            if (ch == '\0')
                                throw new FormatException();

                            if (ch == '\\')
                            {
                                ch = text[loc++];
                                if (ch == '\0')
                                    throw new FormatException();
                            }
                            sb.Append(ch);
                        }
                        node.Properties[propertyName] = sb.ToString();
                        sb.Clear();
                        break;

                    case '(':
                        childNode = new SGFNode(node, node.Depth + 1);
                        LoadNodes(childNode, text, ref loc);
                        node.ChildNodes.Add(childNode);
                        break;
                }
            }
        }
    }
}
