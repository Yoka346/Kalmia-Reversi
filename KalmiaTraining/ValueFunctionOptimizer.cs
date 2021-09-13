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
    struct ValueFuncTrainData : ITrainData
    {
        public Bitboard Board { get; set; }
        public GameResult Result { get; set; }
    }

    public class ValueFunctionOptimizer
    {
        const string LOG_FILE_NAME = "optimize_log.txt";
        const string OPTIMIZED_PARAM_FILE_NAME = "optimized_param_{0}.dat";
        const string BACKUP_PARAM_FILE_NAME = "param_bk.dat";

        readonly DataSet[] TRAIN_DATA_SETS;
        readonly DataSet[] TEST_DATA_SETS;
        ValueFunction valueFunc;

        public ValueFunctionOptimizer(ValueFunction valueFunc, string trainDataPath, string testDataPath)
        {
            this.valueFunc = valueFunc;
            this.TRAIN_DATA_SETS = LoadData(trainDataPath);
            this.TEST_DATA_SETS = LoadData(testDataPath);
        }

        DataSet[] LoadData(string path)
        {
            var csv = new CSVReader(path);
            var stageNum = this.valueFunc.StageNum;
            var moveCountPerStage = this.valueFunc.MoveCountPerStage;
            var trainDataList = (from _ in Enumerable.Range(0, stageNum) select new List<ValueFuncTrainData>()).ToArray();

            while(csv.Peek() != -1)
            {
                var row = csv.ReadRow();
                var data = new ValueFuncTrainData();
                data.Board = new Bitboard(ulong.Parse(row["current_player_board"]), ulong.Parse(row["opponent_player_board"]));
                data.Result = (GameResult)sbyte.Parse(row["result"]);
                var stage = (Board.SQUARE_NUM - 4 - data.Board.GetEmptyCount()) / this.valueFunc.MoveCountPerStage;
                trainDataList[stage].Add(data);
            }
            return (from d in trainDataList select new DataSet(d.ConvertAll(n => (ITrainData)n))).ToArray();
        }

        public void Optimize(string workDir, bool showLog, int epochNum, float learningRate, float learningRateDecay, float[] weightDecay, int pacience, int checkpointInterval)
        {
            var logFilePath = $"{workDir}\\{LOG_FILE_NAME}";
            var paramFilePath = $"{workDir}\\{OPTIMIZED_PARAM_FILE_NAME}";
            var logger = new Logger(logFilePath, showLog);
            var preValueFunc = new ValueFunction(valueFunc);
            var header = this.valueFunc.Header;

            BackupValueFunc(workDir);

            var stageNum = this.valueFunc.StageNum;
            var learningRates = NormalizeLearningRate(learningRate);
            var weightGrad = new float[BoardFeature.PatternFeatureNum.Sum()];
            for (var stage = 0; stage < stageNum; stage++)
            {
                try
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
                    var weight = this.valueFunc.Weight[(int)Color.Black][stage];
                    float lambda = weightDecay[stage];
                    var overfittingCount = 0;

                    for (var epoch = 0; epoch < epochNum; epoch++)
                    {
                        logger.WriteLine($"epoch = {epoch + 1}");
                        var trainLoss = CalcWeightGradient(trainDataSet, weightGrad);
                        logger.WriteLine($"train_loss: {prevTrainLoss} → {trainLoss}");
                        prevTrainLoss = trainLoss;
                        UpdateWeight(weight, weightGrad, eta, lambda);

                        if (epoch % checkpointInterval == checkpointInterval - 1)
                        {
                            logger.WriteLine("\ncheck point.");
                            var testLoss = CalcTestLoss(testDataSet);
                            if (testLoss < minTestLoss)
                            {
                                logger.WriteLine($"test_loss: {minTestLoss} → {testLoss}");
                                minTestLoss = testLoss;
                                preValueFunc = new ValueFunction(valueFunc);
                                overfittingCount = 0;
                            }
                            else
                            {
                                if (++overfittingCount > pacience)
                                    break;
                                logger.WriteLine("rollback.");
                                for (var i = 0; i < eta.Length; i++)
                                    eta[i] *= learningRateDecay;
                                this.valueFunc = new ValueFunction(preValueFunc);
                                weight = this.valueFunc.Weight[(int)Color.Black][stage];
                                epoch -= checkpointInterval;
                            }
                        }
                    }
                }catch(Exception e)
                {
                    logger.WriteLine($"{e.Message}\n{e.StackTrace}");
                    logger.WriteLine("an error occured. stop optimization.");
                    this.valueFunc.CopyBlackWeightToWhiteWeight();
                    this.valueFunc.SaveToFile(header.Label, header.Version + 1, string.Format(paramFilePath, $"{stage}"));
                    throw;
                }

                this.valueFunc.CopyBlackWeightToWhiteWeight();
                header = this.valueFunc.Header;
                this.valueFunc.SaveToFile(header.Label, header.Version + 1, string.Format(paramFilePath, $"{stage}"));
                logger.WriteLine("stop optimization.");
            }
        }

        void BackupValueFunc(string dir)
        {
            var path = dir + "\\" + BACKUP_PARAM_FILE_NAME;
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            var header = this.valueFunc.Header;
            this.valueFunc.SaveToFile(header.Label, header.Version, fs);
        }

        float[][] NormalizeLearningRate(float learningRate)
        {
            var stageNum = this.valueFunc.StageNum;
            var normLearningRates = new float[stageNum][];
            for (var stage = 0; stage < stageNum; stage++)
                normLearningRates[stage] = (from n in this.TRAIN_DATA_SETS[stage].FeatureCount select Math.Min(learningRate / 50.0f, learningRate / n)).ToArray();
            return normLearningRates;
        }

        float CalcWeightGradient(DataSet trainDataSet, float[] weightGrad)
        {
            var trainLoss = 0.0f;
            var threadNum = Environment.ProcessorCount;
            var dataNumPerThread = trainDataSet.Count / threadNum;
            Parallel.For(0, threadNum, threadID => calcWeightGradKernel(threadID * dataNumPerThread, (threadID + 1) * dataNumPerThread));
            calcWeightGradKernel(threadNum * dataNumPerThread, trainDataSet.Count);
            return trainLoss / trainDataSet.Count;

            void calcWeightGradKernel(int start, int end)
            {
                var board = new FastBoard();
                var boardFeature = new BoardFeature();
                for (var i = start; i < end; i++)
                {
                    var data = (ValueFuncTrainData)trainDataSet.Items[i];
                    board.Init(Color.Black, data.Board);
                    boardFeature.InitBoard(board);
                    var y = this.valueFunc.F(boardFeature);
                    var t = GetValue(data.Result);
                    var delta = t - y;
                    var loss = FastMath.BinaryCrossEntropy(y, t);

                    AtomicOperations.Add(ref trainLoss, loss);
                    foreach (var featureIdx in boardFeature.FeatureIndices)
                    {
                        var symmetricFeatureIdx = BoardFeature.SymmetricFeatureIdxMapping[featureIdx];
                        AtomicOperations.Add(ref weightGrad[featureIdx], delta);
                        if (symmetricFeatureIdx != featureIdx)
                            AtomicOperations.Add(ref weightGrad[symmetricFeatureIdx], delta);
                    }
                }
            }
        }

        void UpdateWeight(float[] weight, float[] weightGrad, float[] eta, float weightDecay)
        {
            Parallel.For(0, weight.Length - 1, featureIdx =>
            {
                weight[featureIdx] += eta[featureIdx] * (weightGrad[featureIdx] - weightDecay * weight[featureIdx]);
                weightGrad[featureIdx] = 0.0f;
            });

            var bias = weight.Length - 1;
            weight[bias] += eta[bias] * weightGrad[bias];
            weightGrad[bias] = 0.0f;
        }

        float CalcTestLoss(DataSet testDataSet)
        {
            var testLoss = 0.0f;
            var threadNum = Environment.ProcessorCount;
            var boardFeatures = (from _ in Enumerable.Range(0, threadNum) select new BoardFeature()).ToArray();
            var dataNumPerThread = testDataSet.Count / threadNum;
            Parallel.For(0, threadNum, threadID => calcTestLossKernel(threadID * dataNumPerThread, (threadID + 1) * dataNumPerThread));
            calcTestLossKernel(threadNum * dataNumPerThread, testDataSet.Count % threadNum);
            return testLoss / testDataSet.Count;

            void calcTestLossKernel(int start, int end)
            {
                var board = new FastBoard();
                var boardFeature = new BoardFeature();
                for (var i = start; i < end; i++)
                {
                    var data = (ValueFuncTrainData)testDataSet.Items[i];
                    board.Init(Color.Black, data.Board);
                    boardFeature.InitBoard(board);
                    var y = this.valueFunc.F(boardFeature);
                    var t = GetValue(data.Result);
                    var loss = FastMath.BinaryCrossEntropy(y, t);
                    AtomicOperations.Add(ref testLoss, loss);
                }
            }
        }

        static float GetValue(GameResult result)
        {
            switch (result) 
            {
                case GameResult.Win:
                    return 1.0f;

                case GameResult.Loss:
                    return 0.0f;

                default:
                    return 0.5f;
            }
        }
    }
}
