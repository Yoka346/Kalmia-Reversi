using System.Collections.Generic;
using System.Linq;

using Kalmia;
using Kalmia.Evaluation;
using Kalmia.Reversi;

namespace KalmiaTraining
{
    public struct DataSet
    {
        public ITrainData[] Items;
        public int[] FeatureCount;
        public int Count { get { return this.Items.Length; } }

        public DataSet(List<ITrainData> data)
        {
            this.Items = data.ToArray();
            this.FeatureCount = new int[BoardFeature.PatternFeatureNum.Sum()];
            var board = new Board(Color.Black, InitialBoardState.Cross);
            var boardFeature = new BoardFeature();
            foreach (var bitboard in this.Items.Select(n => n.Board))
            {
                board.Init(Color.Black, bitboard);
                boardFeature.SetBoard(board);
                foreach (var featureIdx in boardFeature.FeatureIndices)
                {
                    this.FeatureCount[featureIdx]++;
                    var symmetricFeatureIdx = BoardFeature.SymmetricFeatureIdxMapping[featureIdx];
                    if (symmetricFeatureIdx != featureIdx)
                        this.FeatureCount[symmetricFeatureIdx]++;
                }
            }
        }
    }
}
