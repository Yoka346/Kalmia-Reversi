using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.Evaluate
{
    public class FeatureBoard
    {
        public const int FEATURE_TYPE_NUM = 13;
        public const int FEATURE_NUM = 47;
        static readonly BoardPosition[][] FEATURE_POSITIONS;

        // The number of square of each feature. e.g.) corner3x3 has 9 squares.
        public static ReadOnlyCollection<int> FeatureSize = new ReadOnlyCollection<int>(new int[] { 9, 10, 10, 10, 8, 8, 8, 8, 7, 6, 5, 4, 0 });

        // The number of each feature. e.g.) The number of corner3x3 is 4, because there are 4 corners for one board.
        public static ReadOnlyCollection<int> FeatureNum = new ReadOnlyCollection<int>(new int[] { 4, 4, 4, 4, 4, 4, 4, 2, 4, 4, 4, 4, 1 });

        // the number of possible patterns. e.g.) corner3x3 has 3^9(= 19683) possible patterns.
        public static ReadOnlyCollection<int> PatternNum { get; } 
            = new ReadOnlyCollection<int>(new int[] { 19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81, 1 });

        // the number of packed patterns by symmetries.
        // e.g.) corner3x3 has 3^9 possible patterns, however 3^9 - 3^6 patterns are asymmetric so total is (3^9 - 3^6) / 2 + 3^6 = 10206 patterns.
        public static ReadOnlyCollection<int> PackedPatternNum { get; }
            = new ReadOnlyCollection<int>(new int[] { 10206, 29889, 29646, 29646, 3321, 3321, 3321, 3321, 1134, 378, 135, 45, 1 });

        // the mapping which flips the pattern about appropriate axis.
        // e.g.) corner3x3 is flipped about diagonal axis.
        public static ReadOnlyCollection<Func<int, int>> FlipPatternMap { get; }

        public int[] Features { get; } = new int[FEATURE_NUM];
        public Color Turn { get; private set; }
        public int EmptyCount { get; private set; }

        static FeatureBoard()
        {
            FEATURE_POSITIONS = InitFeaturePositions();

            var flipPatternMap = new Func<int, int>[FEATURE_TYPE_NUM];
            flipPatternMap[0] = FlipCorner3x3Pattern;
            flipPatternMap[1] = FlipCornerEdgeXPattern;
            for (var featureID = 2; featureID < FEATURE_TYPE_NUM; featureID++)
            {
                var idx = featureID;
                flipPatternMap[featureID] = (int pat) => MirrorPattern(pat, FeatureSize[idx]);
            }
            FlipPatternMap = new ReadOnlyCollection<Func<int, int>>(flipPatternMap);
        }

        static BoardPosition[][] InitFeaturePositions()
        {
            var featurePositions = new BoardPosition[FEATURE_NUM][]
            {
               // corner3x3 
               new BoardPosition[]{ BoardPosition.A1, BoardPosition.B1, BoardPosition.A2, BoardPosition.B2, BoardPosition.C1, BoardPosition.A3, BoardPosition.C2, BoardPosition.B3, BoardPosition.C3},
               new BoardPosition[]{ BoardPosition.H1, BoardPosition.G1, BoardPosition.H2, BoardPosition.G2, BoardPosition.F1, BoardPosition.H3, BoardPosition.F2, BoardPosition.G3, BoardPosition.F3},
               new BoardPosition[]{ BoardPosition.A8, BoardPosition.A7, BoardPosition.B8, BoardPosition.B7, BoardPosition.A6, BoardPosition.C8, BoardPosition.B6, BoardPosition.C7, BoardPosition.C6},
               new BoardPosition[]{ BoardPosition.H8, BoardPosition.H7, BoardPosition.G8, BoardPosition.G7, BoardPosition.H6, BoardPosition.F8, BoardPosition.G6, BoardPosition.F7, BoardPosition.F6},
          
               // corner edge x 
                new BoardPosition[]{ BoardPosition.A5, BoardPosition.A4, BoardPosition.A3, BoardPosition.A2, BoardPosition.A1, BoardPosition.B2, BoardPosition.B1, BoardPosition.C1, BoardPosition.D1, BoardPosition.E1},
                new BoardPosition[]{ BoardPosition.H5, BoardPosition.H4, BoardPosition.H3, BoardPosition.H2, BoardPosition.H1, BoardPosition.G2, BoardPosition.G1, BoardPosition.F1, BoardPosition.E1, BoardPosition.D1},
                new BoardPosition[]{ BoardPosition.A4, BoardPosition.A5, BoardPosition.A6, BoardPosition.A7, BoardPosition.A8, BoardPosition.B7, BoardPosition.B8, BoardPosition.C8, BoardPosition.D8, BoardPosition.E8},
                new BoardPosition[]{ BoardPosition.H4, BoardPosition.H5, BoardPosition.H6, BoardPosition.H7, BoardPosition.H8, BoardPosition.G7, BoardPosition.G8, BoardPosition.F8, BoardPosition.E8, BoardPosition.D8 },

               // edge 2x 
                new BoardPosition[]{ BoardPosition.B2, BoardPosition.A1, BoardPosition.B1, BoardPosition.C1, BoardPosition.D1, BoardPosition.E1, BoardPosition.F1, BoardPosition.G1, BoardPosition.H1, BoardPosition.G2},
                new BoardPosition[]{ BoardPosition.B7, BoardPosition.A8, BoardPosition.B8, BoardPosition.C8, BoardPosition.D8, BoardPosition.E8, BoardPosition.F8, BoardPosition.G8, BoardPosition.H8, BoardPosition.G7},
                new BoardPosition[]{ BoardPosition.B2, BoardPosition.A1, BoardPosition.A2, BoardPosition.A3, BoardPosition.A4, BoardPosition.A5, BoardPosition.A6, BoardPosition.A7, BoardPosition.A8, BoardPosition.B7},
                new BoardPosition[]{ BoardPosition.G2, BoardPosition.H1, BoardPosition.H2, BoardPosition.H3, BoardPosition.H4, BoardPosition.H5, BoardPosition.H6, BoardPosition.H7, BoardPosition.H8, BoardPosition.G7},

               // edge4x2 2x 
                new BoardPosition[]{ BoardPosition.A1, BoardPosition.C1, BoardPosition.D1, BoardPosition.C2, BoardPosition.D2, BoardPosition.E2, BoardPosition.F2, BoardPosition.E1, BoardPosition.F1, BoardPosition.H1 },
                new BoardPosition[]{ BoardPosition.A8, BoardPosition.C8, BoardPosition.D8, BoardPosition.C7, BoardPosition.D7, BoardPosition.E7, BoardPosition.F7, BoardPosition.E8, BoardPosition.F8, BoardPosition.H8 },
                new BoardPosition[]{ BoardPosition.A1, BoardPosition.A3, BoardPosition.A4, BoardPosition.B3, BoardPosition.B4, BoardPosition.B5, BoardPosition.B6, BoardPosition.A5, BoardPosition.A6, BoardPosition.A8 },
                new BoardPosition[]{ BoardPosition.H1, BoardPosition.H3, BoardPosition.H4, BoardPosition.G3, BoardPosition.G4, BoardPosition.G5, BoardPosition.G6, BoardPosition.H5, BoardPosition.H6, BoardPosition.H8 },

               // horizontal and vertical line (row = 2 or column = 2)
                new BoardPosition[]{ BoardPosition.A2, BoardPosition.B2, BoardPosition.C2, BoardPosition.D2, BoardPosition.E2, BoardPosition.F2, BoardPosition.G2, BoardPosition.H2 },
                new BoardPosition[]{ BoardPosition.A7, BoardPosition.B7, BoardPosition.C7, BoardPosition.D7, BoardPosition.E7, BoardPosition.F7, BoardPosition.G7, BoardPosition.H7 },
                new BoardPosition[]{ BoardPosition.B1, BoardPosition.B2, BoardPosition.B3, BoardPosition.B4, BoardPosition.B5, BoardPosition.B6, BoardPosition.B7, BoardPosition.B8 },
                new BoardPosition[]{ BoardPosition.G1, BoardPosition.G2, BoardPosition.G3, BoardPosition.G4, BoardPosition.G5, BoardPosition.G6, BoardPosition.G7, BoardPosition.G8 },

               // horizontal and vertical line (row = 3 or column = 3)
                new BoardPosition[]{ BoardPosition.A3, BoardPosition.B3, BoardPosition.C3, BoardPosition.D3, BoardPosition.E3, BoardPosition.F3, BoardPosition.G3, BoardPosition.H3 },
                new BoardPosition[]{ BoardPosition.A6, BoardPosition.B6, BoardPosition.C6, BoardPosition.D6, BoardPosition.E6, BoardPosition.F6, BoardPosition.G6, BoardPosition.H6 },
                new BoardPosition[]{ BoardPosition.C1, BoardPosition.C2, BoardPosition.C3, BoardPosition.C4, BoardPosition.C5, BoardPosition.C6, BoardPosition.C7, BoardPosition.C8 },
                new BoardPosition[]{ BoardPosition.F1, BoardPosition.F2, BoardPosition.F3, BoardPosition.F4, BoardPosition.F5, BoardPosition.F6, BoardPosition.F7, BoardPosition.F8 },

               // horizontal and vertical line (row = 4 or column = 4)
                new BoardPosition[]{ BoardPosition.A4, BoardPosition.B4, BoardPosition.C4, BoardPosition.D4, BoardPosition.E4, BoardPosition.F4, BoardPosition.G4, BoardPosition.H4 },
                new BoardPosition[]{ BoardPosition.A5, BoardPosition.B5, BoardPosition.C5, BoardPosition.D5, BoardPosition.E5, BoardPosition.F5, BoardPosition.G5, BoardPosition.H5 },
                new BoardPosition[]{ BoardPosition.D1, BoardPosition.D2, BoardPosition.D3, BoardPosition.D4, BoardPosition.D5, BoardPosition.D6, BoardPosition.D7, BoardPosition.D8 },
                new BoardPosition[]{ BoardPosition.E1, BoardPosition.E2, BoardPosition.E3, BoardPosition.E4, BoardPosition.E5, BoardPosition.E6, BoardPosition.E7, BoardPosition.E8 },

               // diagonal line 0
                new BoardPosition[]{ BoardPosition.A1, BoardPosition.B2, BoardPosition.C3, BoardPosition.D4, BoardPosition.E5, BoardPosition.F6, BoardPosition.G7, BoardPosition.H8 },
                new BoardPosition[]{ BoardPosition.A8, BoardPosition.B7, BoardPosition.C6, BoardPosition.D5, BoardPosition.E4, BoardPosition.F3, BoardPosition.G2, BoardPosition.H1 },

               // diagonal line 1
                new BoardPosition[]{ BoardPosition.B1, BoardPosition.C2, BoardPosition.D3, BoardPosition.E4, BoardPosition.F5, BoardPosition.G6, BoardPosition.H7 },
                new BoardPosition[]{ BoardPosition.H2, BoardPosition.G3, BoardPosition.F4, BoardPosition.E5, BoardPosition.D6, BoardPosition.C7, BoardPosition.B8 },
                new BoardPosition[]{ BoardPosition.A2, BoardPosition.B3, BoardPosition.C4, BoardPosition.D5, BoardPosition.E6, BoardPosition.F7, BoardPosition.G8 },
                new BoardPosition[]{ BoardPosition.G1, BoardPosition.F2, BoardPosition.E3, BoardPosition.D4, BoardPosition.C5, BoardPosition.B6, BoardPosition.A7 },

               // diagonal line 2
                new BoardPosition[]{ BoardPosition.C1, BoardPosition.D2, BoardPosition.E3, BoardPosition.F4, BoardPosition.G5, BoardPosition.H6 },
                new BoardPosition[]{ BoardPosition.A3, BoardPosition.B4, BoardPosition.C5, BoardPosition.D6, BoardPosition.E7, BoardPosition.F8 },
                new BoardPosition[]{ BoardPosition.F1, BoardPosition.E2, BoardPosition.D3, BoardPosition.C4, BoardPosition.B5, BoardPosition.A6 },
                new BoardPosition[]{ BoardPosition.H3, BoardPosition.G4, BoardPosition.F5, BoardPosition.E6, BoardPosition.D7, BoardPosition.C8 },

               // diagonal line 3
                new BoardPosition[]{ BoardPosition.D1, BoardPosition.E2, BoardPosition.F3, BoardPosition.G4, BoardPosition.H5 },
                new BoardPosition[]{ BoardPosition.A4, BoardPosition.B5, BoardPosition.C6, BoardPosition.D7, BoardPosition.E8 },
                new BoardPosition[]{ BoardPosition.E1, BoardPosition.D2, BoardPosition.C3, BoardPosition.B4, BoardPosition.A5 },
                new BoardPosition[]{ BoardPosition.H4, BoardPosition.G5, BoardPosition.F6, BoardPosition.E7, BoardPosition.D8 },

               // diagonal line 4
                new BoardPosition[]{ BoardPosition.D1, BoardPosition.C2, BoardPosition.B3, BoardPosition.A4 },
                new BoardPosition[]{ BoardPosition.A5, BoardPosition.B6, BoardPosition.C7, BoardPosition.D8 },
                new BoardPosition[]{ BoardPosition.E1, BoardPosition.F2, BoardPosition.G3, BoardPosition.H4 },
                new BoardPosition[]{ BoardPosition.H5, BoardPosition.G6, BoardPosition.F7, BoardPosition.E8 },

                // Bias
                new BoardPosition[0]
            };
            return featurePositions;
        }

        public FeatureBoard(Board board)
        {
            for (var i = 0; i < this.Features.Length; i++)
                foreach (var featurePos in FEATURE_POSITIONS[i])
                    this.Features[i] = this.Features[i] * 3 + (int)board.GetDiscColor(featurePos);
            this.Turn = board.Turn;
            this.EmptyCount = board.GetEmptyCount();
        }

        public static int InvertPattern(int pattern, int featureSize)
        {
            var patternInverce = 0;
            for (var i = 0; i < featureSize; i++)
            {
                var color = (Color)((pattern / FastMath.Pow3(i)) % 3);
                if (color == Color.Empty)
                    patternInverce += (int)color * FastMath.Pow3(i);
                else
                    patternInverce += (int)(color ^ Color.White) * FastMath.Pow3(i);
            }
            return patternInverce;
        }

        public static int MirrorPattern(int pattern, int featureSize)
        {
            var mirrorPattern = 0;
            for (var i = 0; i < featureSize; i++)
                mirrorPattern += ((pattern / FastMath.Pow3(featureSize - (i + 1))) % 3) * FastMath.Pow3(i);
            return mirrorPattern;
        }

        static int FlipCorner3x3Pattern(int pattern)
        {
            Span<int> table = stackalloc int[6] { 2187, 729, 81, 27, 9, 3 };
            return FlipPatternByTable(pattern, table);
        }

        static int FlipCornerEdgeXPattern(int pattern)
        {
            Span<int> table = stackalloc int[8] { 19683, 1, 6561, 3, 2187, 9, 729, 27 };
            return FlipPatternByTable(pattern, table);
        }

        static int FlipPatternByTable(int pattern, Span<int> table)
        {
            var flipped = pattern;
            for (var i = 0; i < table.Length; i += 2)
            {
                var digit = table[i];
                var nextDigit = table[i + 1];
                var tmp = (pattern / digit) % 3;
                flipped -= tmp * digit;
                flipped += tmp * nextDigit;

                tmp = (pattern / nextDigit) % 3;
                flipped -= tmp * nextDigit;
                flipped += tmp * digit;
            }
            return flipped;
        }
    }
}
