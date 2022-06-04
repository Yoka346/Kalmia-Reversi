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

        static readonly BoardCoordinate[][] PATTERN_POSITIONS = new BoardCoordinate[PATTERN_NUM_SUM][]
        {
           // corner3x3 
           new BoardCoordinate[]{ BoardCoordinate.A1, BoardCoordinate.B1, BoardCoordinate.A2, BoardCoordinate.B2, BoardCoordinate.C1, BoardCoordinate.A3, BoardCoordinate.C2, BoardCoordinate.B3, BoardCoordinate.C3 },
           new BoardCoordinate[]{ BoardCoordinate.H1, BoardCoordinate.G1, BoardCoordinate.H2, BoardCoordinate.G2, BoardCoordinate.F1, BoardCoordinate.H3, BoardCoordinate.F2, BoardCoordinate.G3, BoardCoordinate.F3 },
           new BoardCoordinate[]{ BoardCoordinate.A8, BoardCoordinate.A7, BoardCoordinate.B8, BoardCoordinate.B7, BoardCoordinate.A6, BoardCoordinate.C8, BoardCoordinate.B6, BoardCoordinate.C7, BoardCoordinate.C6 },
           new BoardCoordinate[]{ BoardCoordinate.H8, BoardCoordinate.H7, BoardCoordinate.G8, BoardCoordinate.G7, BoardCoordinate.H6, BoardCoordinate.F8, BoardCoordinate.G6, BoardCoordinate.F7, BoardCoordinate.F6 },
          
           // corner edge x 
           new BoardCoordinate[]{ BoardCoordinate.A5, BoardCoordinate.A4, BoardCoordinate.A3, BoardCoordinate.A2, BoardCoordinate.A1, BoardCoordinate.B2, BoardCoordinate.B1, BoardCoordinate.C1, BoardCoordinate.D1, BoardCoordinate.E1 },
           new BoardCoordinate[]{ BoardCoordinate.H5, BoardCoordinate.H4, BoardCoordinate.H3, BoardCoordinate.H2, BoardCoordinate.H1, BoardCoordinate.G2, BoardCoordinate.G1, BoardCoordinate.F1, BoardCoordinate.E1, BoardCoordinate.D1 },
           new BoardCoordinate[]{ BoardCoordinate.A4, BoardCoordinate.A5, BoardCoordinate.A6, BoardCoordinate.A7, BoardCoordinate.A8, BoardCoordinate.B7, BoardCoordinate.B8, BoardCoordinate.C8, BoardCoordinate.D8, BoardCoordinate.E8 },
           new BoardCoordinate[]{ BoardCoordinate.H4, BoardCoordinate.H5, BoardCoordinate.H6, BoardCoordinate.H7, BoardCoordinate.H8, BoardCoordinate.G7, BoardCoordinate.G8, BoardCoordinate.F8, BoardCoordinate.E8, BoardCoordinate.D8 },

           // edge 2x 
           new BoardCoordinate[]{ BoardCoordinate.B2, BoardCoordinate.A1, BoardCoordinate.B1, BoardCoordinate.C1, BoardCoordinate.D1, BoardCoordinate.E1, BoardCoordinate.F1, BoardCoordinate.G1, BoardCoordinate.H1, BoardCoordinate.G2 },
           new BoardCoordinate[]{ BoardCoordinate.B7, BoardCoordinate.A8, BoardCoordinate.B8, BoardCoordinate.C8, BoardCoordinate.D8, BoardCoordinate.E8, BoardCoordinate.F8, BoardCoordinate.G8, BoardCoordinate.H8, BoardCoordinate.G7 },
           new BoardCoordinate[]{ BoardCoordinate.B2, BoardCoordinate.A1, BoardCoordinate.A2, BoardCoordinate.A3, BoardCoordinate.A4, BoardCoordinate.A5, BoardCoordinate.A6, BoardCoordinate.A7, BoardCoordinate.A8, BoardCoordinate.B7 },
           new BoardCoordinate[]{ BoardCoordinate.G2, BoardCoordinate.H1, BoardCoordinate.H2, BoardCoordinate.H3, BoardCoordinate.H4, BoardCoordinate.H5, BoardCoordinate.H6, BoardCoordinate.H7, BoardCoordinate.H8, BoardCoordinate.G7 },

           // edge4x2 2x 
           new BoardCoordinate[]{ BoardCoordinate.A1, BoardCoordinate.C1, BoardCoordinate.D1, BoardCoordinate.C2, BoardCoordinate.D2, BoardCoordinate.E2, BoardCoordinate.F2, BoardCoordinate.E1, BoardCoordinate.F1, BoardCoordinate.H1 },
           new BoardCoordinate[]{ BoardCoordinate.A8, BoardCoordinate.C8, BoardCoordinate.D8, BoardCoordinate.C7, BoardCoordinate.D7, BoardCoordinate.E7, BoardCoordinate.F7, BoardCoordinate.E8, BoardCoordinate.F8, BoardCoordinate.H8 },
           new BoardCoordinate[]{ BoardCoordinate.A1, BoardCoordinate.A3, BoardCoordinate.A4, BoardCoordinate.B3, BoardCoordinate.B4, BoardCoordinate.B5, BoardCoordinate.B6, BoardCoordinate.A5, BoardCoordinate.A6, BoardCoordinate.A8 },
           new BoardCoordinate[]{ BoardCoordinate.H1, BoardCoordinate.H3, BoardCoordinate.H4, BoardCoordinate.G3, BoardCoordinate.G4, BoardCoordinate.G5, BoardCoordinate.G6, BoardCoordinate.H5, BoardCoordinate.H6, BoardCoordinate.H8 },

           // horizontal and vertical line (row = 2 or column = 2)
           new BoardCoordinate[]{ BoardCoordinate.A2, BoardCoordinate.B2, BoardCoordinate.C2, BoardCoordinate.D2, BoardCoordinate.E2, BoardCoordinate.F2, BoardCoordinate.G2, BoardCoordinate.H2 },
           new BoardCoordinate[]{ BoardCoordinate.A7, BoardCoordinate.B7, BoardCoordinate.C7, BoardCoordinate.D7, BoardCoordinate.E7, BoardCoordinate.F7, BoardCoordinate.G7, BoardCoordinate.H7 },
           new BoardCoordinate[]{ BoardCoordinate.B1, BoardCoordinate.B2, BoardCoordinate.B3, BoardCoordinate.B4, BoardCoordinate.B5, BoardCoordinate.B6, BoardCoordinate.B7, BoardCoordinate.B8 },
           new BoardCoordinate[]{ BoardCoordinate.G1, BoardCoordinate.G2, BoardCoordinate.G3, BoardCoordinate.G4, BoardCoordinate.G5, BoardCoordinate.G6, BoardCoordinate.G7, BoardCoordinate.G8 },

           // horizontal and vertical line (row = 3 or column = 3)
           new BoardCoordinate[]{ BoardCoordinate.A3, BoardCoordinate.B3, BoardCoordinate.C3, BoardCoordinate.D3, BoardCoordinate.E3, BoardCoordinate.F3, BoardCoordinate.G3, BoardCoordinate.H3 },
           new BoardCoordinate[]{ BoardCoordinate.A6, BoardCoordinate.B6, BoardCoordinate.C6, BoardCoordinate.D6, BoardCoordinate.E6, BoardCoordinate.F6, BoardCoordinate.G6, BoardCoordinate.H6 },
           new BoardCoordinate[]{ BoardCoordinate.C1, BoardCoordinate.C2, BoardCoordinate.C3, BoardCoordinate.C4, BoardCoordinate.C5, BoardCoordinate.C6, BoardCoordinate.C7, BoardCoordinate.C8 },
           new BoardCoordinate[]{ BoardCoordinate.F1, BoardCoordinate.F2, BoardCoordinate.F3, BoardCoordinate.F4, BoardCoordinate.F5, BoardCoordinate.F6, BoardCoordinate.F7, BoardCoordinate.F8 },

           // horizontal and vertical line (row = 4 or column = 4)
           new BoardCoordinate[]{ BoardCoordinate.A4, BoardCoordinate.B4, BoardCoordinate.C4, BoardCoordinate.D4, BoardCoordinate.E4, BoardCoordinate.F4, BoardCoordinate.G4, BoardCoordinate.H4 },
           new BoardCoordinate[]{ BoardCoordinate.A5, BoardCoordinate.B5, BoardCoordinate.C5, BoardCoordinate.D5, BoardCoordinate.E5, BoardCoordinate.F5, BoardCoordinate.G5, BoardCoordinate.H5 },
           new BoardCoordinate[]{ BoardCoordinate.D1, BoardCoordinate.D2, BoardCoordinate.D3, BoardCoordinate.D4, BoardCoordinate.D5, BoardCoordinate.D6, BoardCoordinate.D7, BoardCoordinate.D8 },
           new BoardCoordinate[]{ BoardCoordinate.E1, BoardCoordinate.E2, BoardCoordinate.E3, BoardCoordinate.E4, BoardCoordinate.E5, BoardCoordinate.E6, BoardCoordinate.E7, BoardCoordinate.E8 },

           // diagonal line 0
           new BoardCoordinate[]{ BoardCoordinate.A1, BoardCoordinate.B2, BoardCoordinate.C3, BoardCoordinate.D4, BoardCoordinate.E5, BoardCoordinate.F6, BoardCoordinate.G7, BoardCoordinate.H8 },
           new BoardCoordinate[]{ BoardCoordinate.A8, BoardCoordinate.B7, BoardCoordinate.C6, BoardCoordinate.D5, BoardCoordinate.E4, BoardCoordinate.F3, BoardCoordinate.G2, BoardCoordinate.H1 },

           // diagonal line 1
           new BoardCoordinate[]{ BoardCoordinate.B1, BoardCoordinate.C2, BoardCoordinate.D3, BoardCoordinate.E4, BoardCoordinate.F5, BoardCoordinate.G6, BoardCoordinate.H7 },
           new BoardCoordinate[]{ BoardCoordinate.H2, BoardCoordinate.G3, BoardCoordinate.F4, BoardCoordinate.E5, BoardCoordinate.D6, BoardCoordinate.C7, BoardCoordinate.B8 },
           new BoardCoordinate[]{ BoardCoordinate.A2, BoardCoordinate.B3, BoardCoordinate.C4, BoardCoordinate.D5, BoardCoordinate.E6, BoardCoordinate.F7, BoardCoordinate.G8 },
           new BoardCoordinate[]{ BoardCoordinate.G1, BoardCoordinate.F2, BoardCoordinate.E3, BoardCoordinate.D4, BoardCoordinate.C5, BoardCoordinate.B6, BoardCoordinate.A7 },

           // diagonal line 2
           new BoardCoordinate[]{ BoardCoordinate.C1, BoardCoordinate.D2, BoardCoordinate.E3, BoardCoordinate.F4, BoardCoordinate.G5, BoardCoordinate.H6 },
           new BoardCoordinate[]{ BoardCoordinate.A3, BoardCoordinate.B4, BoardCoordinate.C5, BoardCoordinate.D6, BoardCoordinate.E7, BoardCoordinate.F8 },
           new BoardCoordinate[]{ BoardCoordinate.F1, BoardCoordinate.E2, BoardCoordinate.D3, BoardCoordinate.C4, BoardCoordinate.B5, BoardCoordinate.A6 },
           new BoardCoordinate[]{ BoardCoordinate.H3, BoardCoordinate.G4, BoardCoordinate.F5, BoardCoordinate.E6, BoardCoordinate.D7, BoardCoordinate.C8 },

           // diagonal line 3
           new BoardCoordinate[]{ BoardCoordinate.D1, BoardCoordinate.E2, BoardCoordinate.F3, BoardCoordinate.G4, BoardCoordinate.H5 },
           new BoardCoordinate[]{ BoardCoordinate.A4, BoardCoordinate.B5, BoardCoordinate.C6, BoardCoordinate.D7, BoardCoordinate.E8 },
           new BoardCoordinate[]{ BoardCoordinate.E1, BoardCoordinate.D2, BoardCoordinate.C3, BoardCoordinate.B4, BoardCoordinate.A5 },
           new BoardCoordinate[]{ BoardCoordinate.H4, BoardCoordinate.G5, BoardCoordinate.F6, BoardCoordinate.E7, BoardCoordinate.D8 },

           // diagonal line 4
           new BoardCoordinate[]{ BoardCoordinate.D1, BoardCoordinate.C2, BoardCoordinate.B3, BoardCoordinate.A4 },
           new BoardCoordinate[]{ BoardCoordinate.A5, BoardCoordinate.B6, BoardCoordinate.C7, BoardCoordinate.D8 },
           new BoardCoordinate[]{ BoardCoordinate.E1, BoardCoordinate.F2, BoardCoordinate.G3, BoardCoordinate.H4 },
           new BoardCoordinate[]{ BoardCoordinate.H5, BoardCoordinate.G6, BoardCoordinate.F7, BoardCoordinate.E8 },

           // bias
           new BoardCoordinate[0]
        };

        // the number of square of each pattern. e.g.) corner3x3 has 9 squares.
        static readonly int[] PATTERN_SIZE = new int[] { 9, 10, 10, 10, 8, 8, 8, 8, 7, 6, 5, 4, 0 };

        // the number of each pattern in one board. e.g.) the number of corner3x3 is 4, because there are 4 corners for one board.
        static readonly int[] PATTERN_NUM = new int[] { 4, 4, 4, 4, 4, 4, 4, 2, 4, 4, 4, 4, 1 };

        // the number of pattern's features. e.g.) corner3x3 has 3^9(= 19683) features.
        static readonly int[] PATTERN_FEATURE_NUM = new int[] { 19683, 59049, 59049, 59049, 6561, 6561, 6561, 6561, 2187, 729, 243, 81, 1 };

        static readonly int PATTERN_FEATURE_NUM_SUM = PATTERN_FEATURE_NUM.Sum(); 

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
        public static int PatternFeatureNumSum { get { return PATTERN_FEATURE_NUM_SUM; } }
        public static ReadOnlySpan<int> PackedPatternFeatureNum { get { return PACKED_PATTERN_FEATURE_NUM; } }

        readonly int[] FEATURES = new int[PATTERN_NUM_SUM];
        readonly Action<BoardCoordinate, ulong>[] UPDATE_CALLBACKS;

        public DiscColor SideToMove { get; private set; }
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
                    var idx = Array.IndexOf(positions, (BoardCoordinate)pos);
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
            this.UPDATE_CALLBACKS = new Action<BoardCoordinate, ulong>[2] { UpdateAfterBlackMove, UpdateAfterWhiteMove };
        }

        public BoardFeature(BoardFeature board)
        {
            board.CopyTo(this);
            this.UPDATE_CALLBACKS = new Action<BoardCoordinate, ulong>[2] { UpdateAfterBlackMove, UpdateAfterWhiteMove };
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

        public void Update(BoardCoordinate pos, ulong flipped)   
        {
            if (pos != BoardCoordinate.Pass)
            {
                this.UPDATE_CALLBACKS[(int)this.SideToMove](pos, flipped);
                this.EmptyCount--;
            }
            this.SideToMove ^= DiscColor.White;
        }

        public void Flip()
        {
            var j = 0;
            var type = 0;
            foreach(var size in PATTERN_NUM)
            {
                for (var i = 0; i < size; i++)
                    this.FEATURES[j] = FlipFeature(type, this.FEATURES[j++]);
                type++;
            }
        }

        public void CopyTo(BoardFeature dest)
        {
            Buffer.BlockCopy(this.FEATURES, 0, dest.FEATURES, 0, sizeof(int) * PATTERN_NUM_SUM);
            dest.SideToMove = this.SideToMove;
            dest.EmptyCount = this.EmptyCount;
        }

        public override bool Equals(object obj)
        {
            var bf = obj as BoardFeature;
            return bf != null && bf.SideToMove == this.SideToMove && bf.FEATURES.SequenceEqual(this.FEATURES);
        }

        public override int GetHashCode()   // This method is for just suppressing a caution.
        {
            return base.GetHashCode();
        }

        void UpdateAfterBlackMove(BoardCoordinate pos, ulong flipped)
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

        void UpdateAfterWhiteMove(BoardCoordinate pos, ulong flipped)
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
                var color = (DiscColor)((feature / FastMath.Pow3(i)) % 3);
                if (color == DiscColor.Null)
                    patternInverce += (int)color * FastMath.Pow3(i);
                else
                    patternInverce += (int)(color ^ DiscColor.White) * FastMath.Pow3(i);
            }
            return patternInverce;
        }

        public static int FlipFeature(int patternType, int feature)
        {
            return FlipFeatureCallbacks[patternType](feature);
        }

        static int FlipCorner3x3Feature(int feature)
        {
            Span<int> table = stackalloc int[9] { 0, 2, 1, 4, 3, 5, 7, 6, 8 };
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
            Span<int> table = stackalloc int[10] { 9, 8, 7, 6, 4, 5, 3, 2, 1, 0 };
            return FlipFeatureByTable(feature, table);
        }

        static int FlipFeatureByTable(int feature, Span<int> table)
        {
            var flipped = 0;
            for (var i = 0; i < table.Length; i ++)
            {
                var idx = table[i];
                var tmp = (feature / FastMath.Pow3(idx)) % 3;
                flipped += tmp * FastMath.Pow3(i);
            }
            return flipped;
        }
    }
}
