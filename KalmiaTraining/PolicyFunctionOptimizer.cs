using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Kalmia;
using Kalmia.IO;
using Kalmia.Reversi;
using Kalmia.Evaluation;

namespace KalmiaTraining
{
    struct PolicyFuncTrainData : ITrainData
    {
        public Bitboard Board { get; set; }
        public Move NextMove { get; set; }
    }

    public class PolicyFunctionOptimizer
    {
        const string LOG_FILE_NAME = "optimize_log.txt";
        const string OPTIMIZED_PARAM_FILE_NAME = "optimized_param_{0}.dat";
        const string BACKUP_PARAM_FILE_NAME = "param_bk.dat";

        readonly DataSet[] TRAIN_DATA_SETS;
        readonly DataSet[] TEST_DATA_SETS;
        PolicyFunction policyFunc;

        public PolicyFunctionOptimizer(PolicyFunction policyFunc, string trainDataPath, string testDataPath)
        {
            this.policyFunc = policyFunc;
            this.TRAIN_DATA_SETS = LoadData(trainDataPath);
            this.TEST_DATA_SETS = LoadData(testDataPath);
        }

        DataSet[] LoadData(string path)
        {
            var csv = new CSVReader(path);
            var stageNum = this.policyFunc.StageNum;
            var moveCountPerStage = this.policyFunc.MoveCountPerStage;
            var trainDataList = (from _ in Enumerable.Range(0, stageNum) select new List<PolicyFuncTrainData>()).ToArray();

            while (csv.Peek() != -1)
            {
                var row = csv.ReadRow();
                var data = new PolicyFuncTrainData();
                data.Board = new Bitboard(ulong.Parse(row["current_player_board"]), ulong.Parse(row["opponent_player_board"]));
                data.NextMove = new Move(Color.Black, row["next_move"]);
                var stage = (Board.SQUARE_NUM - 4 - data.Board.GetEmptyCount()) / this.policyFunc.MoveCountPerStage;
                trainDataList[stage].Add(data);
            }
            return (from d in trainDataList select new DataSet(d.ConvertAll(n => (ITrainData)n))).ToArray();
        }

        public void Optimize(string workDir, bool showLog, int epochNum, float learningRate, float learningRateDecay, float weightDecay, int pacience, int saveInterval)
        {
            var logFilePath = $"{workDir}\\{LOG_FILE_NAME}";
            var paramFilePath = $"{workDir}\\{OPTIMIZED_PARAM_FILE_NAME}";
            var logger = new Logger(logFilePath, showLog);
            var prevPolicyFunc = new PolicyFunction(policyFunc);

            BackupValueFunc(workDir);

            var stageNum = this.policyFunc.StageNum;
            var learningRates = NormalizeLearningRate(learningRate);
            var weightGrad = (from _ in Enumerable.Range(0, BoardFeature.PatternFeatureNum.Sum()) select new float[Board.SQUARE_NUM]).ToArray();
            var aggregatedAccuracy = 0.0f;
            var accuracy = 0.0f;
            for (var stage = 0; stage < stageNum; stage++)
            {
                logger.WriteLine("////////////////////////////////////////////////////////////");
                logger.WriteLine($"stage = {stage}");
                logger.WriteLine($"train_data_num = {this.TRAIN_DATA_SETS[stage].Count}");
                logger.WriteLine($"test_data_num = {this.TEST_DATA_SETS[stage].Count}");
                logger.WriteLine("start optimization.");

                var trainDataSet = this.TRAIN_DATA_SETS[stage];
                var testDataSet = this.TEST_DATA_SETS[stage];
                var prevTrainLoss = float.PositiveInfinity;
                var minTestLoss = float.PositiveInfinity;
                var eta = learningRates[stage];
                var weight = this.policyFunc.Weight[(int)Color.Black][stage];
                var overfittingCount = 0;

                for (var epoch = 0; epoch < epochNum; epoch++)
                {
                    logger.WriteLine($"epoch = {epoch + 1}");
                    var trainLoss = CalcWeightGradient(trainDataSet, weightGrad);
                    logger.WriteLine($"train_loss: {prevTrainLoss} → {trainLoss}");
                    prevTrainLoss = trainLoss;
                    UpdateWeight(weight, weightGrad, eta, weightDecay);

                    if (epoch % saveInterval == saveInterval - 1)
                    {
                        logger.WriteLine("\ncheck point.");
                        float testLoss;
                        (testLoss, accuracy) = CalcTestLossAndAccuracy(testDataSet);
                        if (testLoss < minTestLoss)
                        {
                            logger.WriteLine($"test_loss: {minTestLoss} → {testLoss}");
                            logger.WriteLine($"accuracy_score = {accuracy * 100.0f} %\n");
                            logger.WriteLine($"aggregated_accuracy_score = {(aggregatedAccuracy / stage + accuracy) * 50.0f}");
                            minTestLoss = testLoss;
                            this.policyFunc.CopyBlackWeightToWhiteWeight();
                            var header = this.policyFunc.Header;
                            this.policyFunc.SaveToFile(header.Label, header.Version + 1, string.Format(paramFilePath, $"{stage}"));
                            prevPolicyFunc = new PolicyFunction(policyFunc);
                            overfittingCount = 0;
                        }
                        else
                        {
                            if (++overfittingCount > pacience)
                                break;
                            logger.WriteLine("rollback.");
                            for (var i = 0; i < eta.Length; i++)
                                eta[i] *= learningRateDecay;
                            this.policyFunc = new PolicyFunction(prevPolicyFunc);
                            weight = this.policyFunc.Weight[(int)Color.Black][stage];
                            epoch -= saveInterval;
                        }
                    }
                }
                aggregatedAccuracy += (1 / (stage + 1)) * accuracy;
                logger.WriteLine("stop optimization.");
            }
        }

        void BackupValueFunc(string dir)
        {
            var path = dir + "\\" + BACKUP_PARAM_FILE_NAME;
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            var header = this.policyFunc.Header;
            this.policyFunc.SaveToFile(header.Label, header.Version, fs);
        }

        float[][] NormalizeLearningRate(float learningRate)
        {
            var stageNum = this.policyFunc.StageNum;
            var normLearningRates = new float[stageNum][];
            for (var stage = 0; stage < stageNum; stage++)
                normLearningRates[stage] = (from n in this.TRAIN_DATA_SETS[stage].FeatureCount select Math.Min(learningRate / 50.0f, learningRate / n)).ToArray();
            return normLearningRates;
        }

        float CalcWeightGradient(DataSet trainDataSet, float[][] weightGrad)
        {
            var threadNum = Environment.ProcessorCount;
            var trainLosses = new float[threadNum];
            var dataNumPerThread = trainDataSet.Count / threadNum;
            Parallel.For(0, threadNum, threadID => kernel(dataNumPerThread * threadID, dataNumPerThread * (threadID + 1), threadID));
            kernel(dataNumPerThread * threadNum, trainDataSet.Count, 0);
            return trainLosses.Sum() / trainDataSet.Count;

            void kernel(int start, int end, int threadID)
            {
                var board = new Board(Color.Black, InitialBoardState.Cross);
                var boardFeature = new BoardFeature();
                var y = new float[Board.SQUARE_NUM];
                var delta = new float[Board.SQUARE_NUM];
                for (var i = start; i < end; i++)
                {
                    var data = (PolicyFuncTrainData)trainDataSet.Items[i];
                    board.Init(Color.Black, data.Board);
                    boardFeature.SetBoard(board);
                    this.policyFunc.F(boardFeature, y);
                    var nextPos = (int)data.NextMove.Pos;
                    for (var pos = 0; pos < nextPos; pos++)
                        delta[pos] = -y[pos];
                    delta[nextPos] = 1.0f - y[nextPos];
                    for (var pos = nextPos + 1; pos < delta.Length; pos++)
                        delta[pos] = -y[pos];

                    trainLosses[threadID] += FastMath.OneHotCrossEntropy(y, nextPos);
                    foreach (var featureIdx in boardFeature.FeatureIndices)
                    {
                        var wg = weightGrad[featureIdx];
                        for (var pos = 0; pos < wg.Length; pos++)
                            AtomicOperations.Add(ref wg[pos], delta[pos]);
                    }
                }
            }
        }

        void UpdateWeight(float[][] weight, float[][] weightGrad, float[] eta, float weightDecay)
        {
            Parallel.For(0, weight.Length - 1, featureIdx =>
            {
                var w = weight[featureIdx];
                var wg = weightGrad[featureIdx];
                var e = eta[featureIdx];
                for (var pos = 0; pos < w.Length; pos++)
                {
                    w[pos] += e * (wg[pos] - weightDecay * w[pos]);
                    wg[pos] = 0.0f;
                }
            });

            // bias
            (var w, var wg, var e) = (weight[weight.Length - 1], weightGrad[weight.Length - 1], eta[weight.Length - 1]);
            for (var pos = 0; pos < w.Length; pos++)
            {
                w[pos] += e * wg[pos];
                wg[pos] = 0.0f;
            }
        }

        (float testLoss, float accuracy) CalcTestLossAndAccuracy(DataSet testDataSet)
        {
            var threadNum = Environment.ProcessorCount;
            var testLosses = new float[threadNum];
            var accuracySum = new float[threadNum];
            var dataNumPerThread = testDataSet.Count / threadNum;
            Parallel.For(0, threadNum, threadID => kernel(dataNumPerThread * threadID, dataNumPerThread * (threadID + 1), threadID));
            kernel(dataNumPerThread * threadNum, testDataSet.Count, 0);
            return (testLosses.Sum() / testDataSet.Count, accuracySum.Sum() / testDataSet.Count);

            void kernel(int start, int end, int threadID)
            {
                var board = new Board(Color.Black, InitialBoardState.Cross);
                var boardFeature = new BoardFeature();
                var y = new float[Board.SQUARE_NUM];
                for (var i = start; i < end; i++)
                {
                    var data = (PolicyFuncTrainData)testDataSet.Items[i];
                    board.Init(Color.Black, data.Board);
                    boardFeature.SetBoard(board);
                    this.policyFunc.F(boardFeature, y);
                    var nextPos = (int)data.NextMove.Pos;
                    testLosses[threadID] += FastMath.OneHotCrossEntropy(y, nextPos);
                    accuracySum[threadID] += y[nextPos];
                }
            }
        }
    }
}
