using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Kalmia.Reversi;
using Kalmia.Evaluation;

namespace Kalmia.Learning
{
    public struct TrainData
    {
        public FeatureBoard[][] Boards{ get; }     // Inputs[stage][idx]
        public (int featureType, int pattern, int count)[][][] ApperedFeaturePatternsCount;      // ApperedFeaturePatternCount[stage][idx][]
        public Dictionary<(int featureType, int pattern), int> ApperedFeaturePatternsCountSum { get; }      // ApperedFeaturePatternCount[stage]
        public float[][] BoardValues { get; }        // Output[stage][idx]

        public TrainData(FeatureBoard[] boards, float[] boardValues, int moveNumPerStage)
        {
            var dataNum = boards.Length;
            var stageNum = ((Board.SQUARE_NUM - 4) / moveNumPerStage) + 1;
            var separatedBoards = (from i in Enumerable.Range(0, stageNum) select new List<FeatureBoard>()).ToArray();
            var separatedBoardValues = (from i in Enumerable.Range(0, stageNum) select new List<float>()).ToArray();
            var apperedFeaturePatternsCount = (from i in Enumerable.Range(0, stageNum) select new List<(int featureType, int pattern, int count)[]>()).ToArray();
            this.ApperedFeaturePatternsCountSum = new Dictionary<(int featureType, int pattern), int>();

            for (var i = 0; i < dataNum; i++)
            {
                var board = boards[i];
                var stage = (Board.SQUARE_NUM - 4 - board.EmptyCount) / moveNumPerStage;
                var featureBoard = new FeatureBoard(board);
                separatedBoards[stage].Add(new FeatureBoard(board));
                separatedBoardValues[stage].Add(boardValues[i]);

                var patternsCount = new Dictionary<(int featureType, int pattern), int>();
                var k = 0;
                for (var featureType = 0; featureType < FeatureBoard.FEATURE_TYPE_NUM; featureType++)
                    for (var j = 0; j < FeatureBoard.FeatureNum[featureType]; j++)
                    {
                        var key = (featureType, featureBoard.FeaturePatterns[k++]);
                        if (!this.ApperedFeaturePatternsCountSum.ContainsKey(key))
                        {
                            this.ApperedFeaturePatternsCountSum.Add(key, 0);
                            patternsCount.Add(key, 0);
                        }
                        else if (!patternsCount.ContainsKey(key))
                            patternsCount.Add(key, 0);

                        patternsCount[key]++;
                        this.ApperedFeaturePatternsCountSum[key]++;
                    }

                var keys = patternsCount.Keys;
                var values = patternsCount.Values;
                apperedFeaturePatternsCount[stage].Add((from n in patternsCount.Keys.Zip(patternsCount.Values, (x, y) => (x.featureType, x.pattern, y)) select n).ToArray());
            }

            this.Boards = (from n in separatedBoards select n.ToArray()).ToArray();
            this.BoardValues = (from n in separatedBoardValues select n.ToArray()).ToArray();
            this.ApperedFeaturePatternsCount = (from n in apperedFeaturePatternsCount select n.ToArray()).ToArray();
        }
    }
}
