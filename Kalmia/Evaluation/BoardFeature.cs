using System;
using System.Linq;
using System.Collections.ObjectModel;

using Kalmia;
using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
    public class BoardFeature
    {
        // term explanation
        // "pattern type" means the type of pattern. for example corner3x3 or corner edge x etc.
        // "feature" means the unique integer being calculated from discs position in the specific pattern.

        public const int PATTERN_TYPE = 13;
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

        static readonly int[] FEATURE_IDX_OFFSET;

        // the mapping of feature indices into symmetric feature indices.
        static readonly int[] SYMMETRIC_FEATURE_IDX_MAPPING;

        // the mapping of feature indices into opponent's feature indices.
        static readonly int[] OPPONENT_FEATURE_IDX_MAPPING;

        // the functions which flips the pattern's feature about appropriate axis.
        // e.g.) corner3x3 is flipped about diagonal axis.
        public static ReadOnlyCollection<Func<int, int>> FlipFeatureCallbacks { get; }

        public static ReadOnlySpan<int> PatternSize { get { return PATTERN_SIZE; } }
        public static ReadOnlySpan<int> PatternNum { get { return PATTERN_NUM; } }
        public static ReadOnlySpan<int> PatternFeatureNum { get { return PATTERN_FEATURE_NUM; } }
        public static ReadOnlySpan<int> PatternIdxOffset { get { return FEATURE_IDX_OFFSET; } }
        public static ReadOnlySpan<int> PackedPatternFeatureNum { get { return PACKED_PATTERN_FEATURE_NUM; } }
        public static ReadOnlySpan<int> SymmetricFeatureIdxMapping { get { return SYMMETRIC_FEATURE_IDX_MAPPING; } }
        public static ReadOnlySpan<int> OpponentFeatureIdxMapping { get { return OPPONENT_FEATURE_IDX_MAPPING; } }

        readonly int[] FEATURE_INDICES = new int[PATTERN_NUM_SUM];

        public Color SideToMove { get; private set; }
        public int EmptyCount { get; private set; }
        public ReadOnlySpan<int> FeatureIndices { get { return this.FEATURE_INDICES; } }

        static BoardFeature()
        {
            FEATURE_IDX_OFFSET = new int[PATTERN_NUM_SUM];
            var i = 0;
            var offset = 0;
            for (var patternType = 0; patternType < PATTERN_TYPE; patternType++)
            {
                if (patternType != 0)
                    offset += PATTERN_FEATURE_NUM[patternType - 1];
                for (var j = 0; j < PATTERN_NUM[patternType]; j++)
                        FEATURE_IDX_OFFSET[i++] = offset;
            }

            var flipFeatureCallbacks = new Func<int, int>[PATTERN_TYPE];
            flipFeatureCallbacks[0] = FlipCorner3x3Feature;
            flipFeatureCallbacks[1] = FlipCornerEdgeXFeature;
            for (var featureType = 2; featureType < PATTERN_TYPE; featureType++)
            {
                var idx = featureType;
                flipFeatureCallbacks[featureType] = (int pat) => MirrorFeature(pat, PatternSize[idx]);
            }
            FlipFeatureCallbacks = new ReadOnlyCollection<Func<int, int>>(flipFeatureCallbacks);

            SYMMETRIC_FEATURE_IDX_MAPPING = new int[PATTERN_FEATURE_NUM.Sum()];
            for (var featureIdx = 0; featureIdx < SYMMETRIC_FEATURE_IDX_MAPPING.Length; featureIdx++)
                SYMMETRIC_FEATURE_IDX_MAPPING[featureIdx] = CalcSymmetricFeatureIdx(featureIdx);

            OPPONENT_FEATURE_IDX_MAPPING = new int[PATTERN_FEATURE_NUM.Sum()];
            for (var featureIdx = 0; featureIdx < OPPONENT_FEATURE_IDX_MAPPING.Length; featureIdx++)
                OPPONENT_FEATURE_IDX_MAPPING[featureIdx] = CalcOpponentFeatureIdx(featureIdx);
        }

        public BoardFeature() : this(new FastBoard()) { }

        public BoardFeature(FastBoard board)
        {
            SetBoard(board);
        }

        public BoardFeature(BoardFeature board)
        {
            Buffer.BlockCopy(board.FEATURE_INDICES, 0, this.FEATURE_INDICES, 0, sizeof(int) * PATTERN_NUM_SUM);
            this.SideToMove = board.SideToMove;
            this.EmptyCount = board.EmptyCount;
        }

        public void SetBoard(FastBoard board)
        {
            for (var i = 0; i < PATTERN_NUM_SUM; i++)
            {
                this.FEATURE_INDICES[i] = 0;
                foreach (var patternPos in PATTERN_POSITIONS[i])
                    this.FEATURE_INDICES[i] = this.FEATURE_INDICES[i] * 3 + (int)board.GetDiscColor(patternPos);
                this.FEATURE_INDICES[i] += FEATURE_IDX_OFFSET[i];
            }
            this.SideToMove = board.SideToMove;
            this.EmptyCount = board.GetEmptyCount();
        }

        public void ConvertToOpponentBoard()
        {
            this.SideToMove = (this.SideToMove == Color.Black) ? Color.White : Color.Black;
            for (var i = 0; i < this.FEATURE_INDICES.Length; i++)
                this.FEATURE_INDICES[i] = OPPONENT_FEATURE_IDX_MAPPING[this.FEATURE_INDICES[i]];
        }

        public static (int patternType, int feature) GetPatternTypeAndFeatureFromFeatureIdx(int featureIdx)
        {
            int patternType;
            var offset = PATTERN_FEATURE_NUM.Sum();
            for(patternType = PATTERN_TYPE; patternType > 0;)
            {
                offset -= PATTERN_FEATURE_NUM[--patternType];
                if (featureIdx >= offset)
                    break;
            }
            return (patternType, featureIdx - offset);
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

        static int CalcOpponentFeatureIdx(int featureIdx)
        {
            (var patternType, var feature) = GetPatternTypeAndFeatureFromFeatureIdx(featureIdx);
            return CalcOpponentFeature(feature, PATTERN_SIZE[patternType]) + (featureIdx - feature);
        }

        static int CalcSymmetricFeatureIdx(int featureIdx)
        {
            (var patternType, var feature) = GetPatternTypeAndFeatureFromFeatureIdx(featureIdx);
            return FlipFeatureCallbacks[patternType](feature) + (featureIdx - feature);
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
