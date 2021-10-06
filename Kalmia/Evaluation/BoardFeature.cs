using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Kalmia;
using Kalmia.Reversi;

using static Kalmia.BitManipulations;

namespace Kalmia.Evaluation
{
    public class BoardFeature
    {
        // term explanation
        // "pattern type" means the type of pattern. for example corner3x3 or corner edge x etc.
        // "feature" means the unique integer being calculated from discs position in the specific pattern.

        public const int PATTERN_TYPE_NUM = 13;
        public const int PATTERN_NUM_SUM = 47;

        static readonly BoardPosition[][] PATTERN_POSITIONS = new BoardPosition[PATTERN_NUM_SUM][]
        {
           // corner3x3 
           new BoardPosition[]{ BoardPosition.A1, BoardPosition.B1, BoardPosition.A2, BoardPosition.B2, BoardPosition.C1, BoardPosition.A3, BoardPosition.C2, BoardPosition.B3, BoardPosition.C3 },
           new BoardPosition[]{ BoardPosition.H1, BoardPosition.G1, BoardPosition.H2, BoardPosition.G2, BoardPosition.F1, BoardPosition.H3, BoardPosition.F2, BoardPosition.G3, BoardPosition.F3 },
           new BoardPosition[]{ BoardPosition.A8, BoardPosition.A7, BoardPosition.B8, BoardPosition.B7, BoardPosition.A6, BoardPosition.C8, BoardPosition.B6, BoardPosition.C7, BoardPosition.C6 },
           new BoardPosition[]{ BoardPosition.H8, BoardPosition.H7, BoardPosition.G8, BoardPosition.G7, BoardPosition.H6, BoardPosition.F8, BoardPosition.G6, BoardPosition.F7, BoardPosition.F6 },
          
           // corner edge x 
           new BoardPosition[]{ BoardPosition.A5, BoardPosition.A4, BoardPosition.A3, BoardPosition.A2, BoardPosition.A1, BoardPosition.B2, BoardPosition.B1, BoardPosition.C1, BoardPosition.D1, BoardPosition.E1 },
           new BoardPosition[]{ BoardPosition.H5, BoardPosition.H4, BoardPosition.H3, BoardPosition.H2, BoardPosition.H1, BoardPosition.G2, BoardPosition.G1, BoardPosition.F1, BoardPosition.E1, BoardPosition.D1 },
           new BoardPosition[]{ BoardPosition.A4, BoardPosition.A5, BoardPosition.A6, BoardPosition.A7, BoardPosition.A8, BoardPosition.B7, BoardPosition.B8, BoardPosition.C8, BoardPosition.D8, BoardPosition.E8 },
           new BoardPosition[]{ BoardPosition.H4, BoardPosition.H5, BoardPosition.H6, BoardPosition.H7, BoardPosition.H8, BoardPosition.G7, BoardPosition.G8, BoardPosition.F8, BoardPosition.E8, BoardPosition.D8 },

           // edge 2x 
           new BoardPosition[]{ BoardPosition.B2, BoardPosition.A1, BoardPosition.B1, BoardPosition.C1, BoardPosition.D1, BoardPosition.E1, BoardPosition.F1, BoardPosition.G1, BoardPosition.H1, BoardPosition.G2 },
           new BoardPosition[]{ BoardPosition.B7, BoardPosition.A8, BoardPosition.B8, BoardPosition.C8, BoardPosition.D8, BoardPosition.E8, BoardPosition.F8, BoardPosition.G8, BoardPosition.H8, BoardPosition.G7 },
           new BoardPosition[]{ BoardPosition.B2, BoardPosition.A1, BoardPosition.A2, BoardPosition.A3, BoardPosition.A4, BoardPosition.A5, BoardPosition.A6, BoardPosition.A7, BoardPosition.A8, BoardPosition.B7 },
           new BoardPosition[]{ BoardPosition.G2, BoardPosition.H1, BoardPosition.H2, BoardPosition.H3, BoardPosition.H4, BoardPosition.H5, BoardPosition.H6, BoardPosition.H7, BoardPosition.H8, BoardPosition.G7 },

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

           // bias
           new BoardPosition[0]
        };

        // the number of square of each pattern. e.g.) corner3x3 has 9 squares.
        static readonly int[] PATTERN_SIZE = new int[] { 9, 10, 10, 10, 8, 8, 8, 8, 7, 6, 5, 4, 0 };

        // the number of each pattern in one board. e.g.) the number of corner3x3 is 4, because there are 4 corners for one board.
        static readonly int[] PATTERN_NUM = new int[] { 4, 4, 4, 4, 4, 4, 4, 2, 4, 4, 4, 4, 1 };

        // the number of pattern's features. e.g.) corner3x3 has 3^9(= 19683) features.
        static readonly int[] PATTERN_FEATURE_NUM = new int[] { 19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81, 1 };

        // the number of packed pattern's features.
        // e.g.) corner3x3 has 3^9 possible patterns, however 3^9 - 3^6 patterns are asymmetric so total is (3^9 - 3^6) / 2 + 3^6 = 10206 patterns.
        static readonly int[] PACKED_PATTERN_FEATURE_NUM = new int[] { 10206, 29889, 29646, 29646, 3321, 3321, 3321, 3321, 1134, 378, 135, 45, 1 };

        // the converter of disc position to feature
        static readonly (int patternIdx, int feature)[][] POSITION_TO_FEATURE = new (int patternIdx, int feature)[Board.SQUARE_NUM + 1][];

        // the functions which flips the pattern's feature about appropriate axis.
        // e.g.) corner3x3 is flipped about diagonal axis.
        static ReadOnlyCollection<Func<int, int>> FlipFeatureCallbacks { get; }

        public static ReadOnlySpan<int> PatternSize { get { return PATTERN_SIZE; } }
        public static ReadOnlySpan<int> PatternNum { get { return PATTERN_NUM; } }
        public static ReadOnlySpan<int> PatternFeatureNum { get { return PATTERN_FEATURE_NUM; } }
        public static ReadOnlySpan<int> PackedPatternFeatureNum { get { return PACKED_PATTERN_FEATURE_NUM; } }

        readonly int[] FEATURES = new int[PATTERN_NUM_SUM];
        readonly Action<BoardPosition, ulong>[] UPDATE_CALLBACKS;

        public Color SideToMove { get; private set; }
        public int EmptyCount { get; private set; }
        public ReadOnlySpan<int> Features { get { return this.FEATURES; } }

        static BoardFeature()
        {
            var flipFeatureCallbacks = new Func<int, int>[PATTERN_TYPE_NUM];
            flipFeatureCallbacks[0] = FlipCorner3x3Feature;
            flipFeatureCallbacks[1] = FlipCornerEdgeXFeature;
            for (var patternType = 2; patternType < PATTERN_TYPE_NUM; patternType++)
            {
                int patType = patternType;
                flipFeatureCallbacks[patType] = (int pat) => MirrorFeature(pat, PatternSize[patType]);
            }
            FlipFeatureCallbacks = new ReadOnlyCollection<Func<int, int>>(flipFeatureCallbacks);

            for (var pos = 0; pos < Board.SQUARE_NUM + 1; pos++)
            {
                var featureList = new List<(int patternIdx, int feature)>();
                for (var patternIdx = 0; patternIdx < PATTERN_POSITIONS.Length; patternIdx++)
                {
                    var positions = PATTERN_POSITIONS[patternIdx];
                    var size = positions.Length;
                    var idx = Array.IndexOf(positions, (BoardPosition)pos);
                    if (idx == -1)
                        continue;
                    featureList.Add((patternIdx, FastMath.Pow3(size - idx - 1)));
                }
                POSITION_TO_FEATURE[pos] = featureList.ToArray();
            }
        }

        public BoardFeature() : this(new FastBoard()) { }

        public BoardFeature(FastBoard board)
        {
            InitFeatures(board);
            this.UPDATE_CALLBACKS = new Action<BoardPosition, ulong>[2] { UpdateAfterBlackMove, UpdateAfterWhiteMove };
        }

        public BoardFeature(BoardFeature board)
        {
            board.CopyTo(this);
            this.UPDATE_CALLBACKS = new Action<BoardPosition, ulong>[2] { UpdateAfterBlackMove, UpdateAfterWhiteMove };
        }

        public void InitFeatures(FastBoard board)
        {
            for (var i = 0; i < PATTERN_NUM_SUM; i++)
            {
                this.FEATURES[i] = 0;
                foreach (var patternPos in PATTERN_POSITIONS[i])
                    this.FEATURES[i] = this.FEATURES[i] * 3 + (int)board.GetDiscColor(patternPos);
            }
            this.SideToMove = board.SideToMove;
            this.EmptyCount = board.GetEmptyCount();
        }

        public void Update(BoardPosition pos, ulong flipped)   
        {
            if (pos != BoardPosition.Pass)
            {
                this.UPDATE_CALLBACKS[(int)this.SideToMove](pos, flipped);
                this.EmptyCount--;
            }
            this.SideToMove ^= Color.White;
        }

        public void CopyTo(BoardFeature dest)
        {
            Buffer.BlockCopy(this.FEATURES, 0, dest.FEATURES, 0, sizeof(int) * PATTERN_NUM_SUM);
            dest.SideToMove = this.SideToMove;
            dest.EmptyCount = this.EmptyCount;
        }

        void UpdateAfterBlackMove(BoardPosition pos, ulong flipped)
        {
            var features = this.FEATURES;
            var posToFeature = POSITION_TO_FEATURE[(int)pos];

            foreach (var n in posToFeature)
                features[n.patternIdx] -= 2 * n.feature;

            for(var p = FindFirstSet(flipped); flipped != 0UL; p = FindNextSet(ref flipped))
            {
                posToFeature = POSITION_TO_FEATURE[p];
                foreach(var n in posToFeature)
                    features[n.patternIdx] -= n.feature;
            }
        }

        void UpdateAfterWhiteMove(BoardPosition pos, ulong flipped)
        {
            var features = this.FEATURES;
            var posToFeature = POSITION_TO_FEATURE[(int)pos];

            foreach (var n in posToFeature)
                features[n.patternIdx] -= n.feature;

            for (var p = FindFirstSet(flipped); flipped != 0UL; p = FindNextSet(ref flipped))
            {
                posToFeature = POSITION_TO_FEATURE[p];
                foreach (var n in posToFeature)
                    features[n.patternIdx] += n.feature;
            }
        }

        public static int CalcOpponentFeature(int feature, int patternSize)
        {
            var patternInverce = 0;
            for (var i = 0; i < patternSize; i++)
            {
                var color = (Color)((feature / FastMath.Pow3(i)) % 3);
                if (color == Color.Empty)
                    patternInverce += (int)color * FastMath.Pow3(i);
                else
                    patternInverce += (int)(color ^ Color.White) * FastMath.Pow3(i);
            }
            return patternInverce;
        }

        public static int FlipFeature(int patternType, int feature)
        {
            return FlipFeatureCallbacks[patternType](feature);
        }

        static int FlipCorner3x3Feature(int feature)
        {
            Span<int> table = stackalloc int[6] { 2187, 729, 81, 27, 9, 3 };
            return FlipFeatureByTable(feature, table);
        }

        static int MirrorFeature(int feature, int patternSize)
        {
            var mirrorPattern = 0;
            for (var i = 0; i < patternSize; i++)
                mirrorPattern += ((feature / FastMath.Pow3(patternSize - (i + 1))) % 3) * FastMath.Pow3(i);
            return mirrorPattern;
        }

        static int FlipCornerEdgeXFeature(int feature)
        {
            Span<int> table = stackalloc int[8] { 19683, 1, 6561, 3, 2187, 9, 729, 27 };
            return FlipFeatureByTable(feature, table);
        }

        static int FlipFeatureByTable(int feature, Span<int> table)
        {
            var flipped = feature;
            for (var i = 0; i < table.Length; i += 2)
            {
                var digit = table[i];
                var nextDigit = table[i + 1];
                var tmp = (feature / digit) % 3;
                flipped -= tmp * digit;
                flipped += tmp * nextDigit;

                tmp = (feature / nextDigit) % 3;
                flipped -= tmp * nextDigit;
                flipped += tmp * digit;
            }
            return flipped;
        }
    }
}
