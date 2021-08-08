using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Kalmia.IO;
using Kalmia.Reversi;
using Kalmia.Evaluation;

namespace Kalmia.Learning
{
    public class SupervisedLearning
    {
        readonly TrainData TRAIN_DATA;
        readonly ValueFunction MODEL;
        float learningRate;

        public float LearningRate 
        { 
            get { return this.learningRate; }
            set { if (value < 0.0f) throw new ArgumentOutOfRangeException(); else this.learningRate = value; }
        }

        public SupervisedLearning(WTHORFile[] trainDataFiles, ValueFunction model, float learningRate = 0.1f)
        {
            this.MODEL = model;
            this.learningRate = learningRate;

            var boardsFromAllFiles = new List<FeatureBoard>[trainDataFiles.Length];
            var valuesFromAllFiles = new List<float>[trainDataFiles.Length];
            for(var i = 0; i < trainDataFiles.Length; i++)
            {
                var trainDataFile = trainDataFiles[i];
                var depth = trainDataFile.WtbHeader.Depth;
                var records = trainDataFile.GameRecords;
                boardsFromAllFiles[i] = new List<FeatureBoard>();
                valuesFromAllFiles[i] = new List<float>();
                var boards = boardsFromAllFiles[i];
                var values = valuesFromAllFiles[i];
                foreach (var record in records)
                {
                    var board = new Board(Color.Black, InitialBoardState.Cross);
                    boards.Add(new FeatureBoard(board));
                    values.Add(CalcValue(record.BestBlackDiscCount));
                    foreach (var move in record.MoveRecord)
                    {
                        board.Update(move);
                        if (move.Pos == BoardPosition.Pass)
                            continue;

                        boards.Add(new FeatureBoard(board));
                        if (board.GetEmptyCount() > depth)
                            values.Add(CalcValue(record.BestBlackDiscCount));
                        else
                            values.Add(CalcValue(record.BlackDiscCount));
                    }
                }
            }

            var boardsMerged = new List<FeatureBoard>();
            var valuesMerged = new List<float>();
            for(var i = 0; i < trainDataFiles.Length; i++) 
            {
                boardsMerged.AddRange(boardsFromAllFiles[i]);
                valuesMerged.AddRange(valuesFromAllFiles[i]);
            }
            this.TRAIN_DATA = new TrainData(boardsMerged.ToArray(), valuesMerged.ToArray(), model.MoveNumPerStage);
        }

        public void Optimize(int epochNum = 50)
        {
            var eta = new float[this.MODEL.StageNum][][];
            for(var stage = 0; stage < eta.Length; stage++) 
            {
                eta[stage] = new float[FeatureBoard.FEATURE_TYPE_NUM][];    // ToDo: etaは配列のまま実装 
                for(var featureType = 0; featureType < eta[stage].Length; featureType++) 
                {
                    eta[stage][featureType] = new float[FeatureBoard.FeaturePatternNum[featureType]];
                    for (var pattern = 0; pattern < eta[stage][featureType].Length; pattern++)
                        if (this.TRAIN_DATA.ApperedFeaturePatternsCountSum.ContainsKey((featureType, pattern)))
                            eta[stage][featureType][pattern] = Math.Min(this.learningRate / 50.0f, this.learningRate / this.TRAIN_DATA.ApperedFeaturePatternsCountSum[(featureType, pattern)]);
                }
            }
        }

        static float CalcValue(int blackDiscCount)
        {
            if (blackDiscCount > Board.SQUARE_NUM / 2)
                return 1.0f;
            else if (blackDiscCount < Board.SQUARE_NUM / 2)
                return 0.0f;
            else
                return 0.5f;
        }
    }
}
