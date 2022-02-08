using Kalmia.Evaluation;
using Kalmia.IO;
using Kalmia.Reversi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using static Kalmia.Evaluation.LatentFactorValueFunction;

namespace Kalmia.Learning
{
    struct TrainData
    {
        public Bitboard Board { get; set; }
        public GameResult Result { get; set; }
    }

    public class LatentFactorValueFuncOptimizer
    {
        const float FEATURES_COUNT_INVERSE_MAX = 1.0e-2f;
        const string PARAM_FILE_NAME = "optimized_param.dat";
        const string LOG_FILE_NAME = "optimization.log";

        LatentFactorValueFunction currentModel;

        TrainData[][] trainData;
        TrainData[][] testData;

        public int CheckPointInterval { get; set; } = 5;
        public int Pacience { get; set; } = 5;
        public float LearningRate { get; set; } = 0.1f;
        public float LearningRateDecay { get; set; } = 0.5f;
        public float L2FactorForWeight { get; set; } = 1.0e-3f;
        public float L2FactorForVector { get; set; } = 2.0e-3f;
        public float Tolerance { get; set; } = 1.0e-4f;

        public LatentFactorValueFuncOptimizer(LatentFactorValueFunction valueFunc)
        {
            this.currentModel = valueFunc;
        }

        public void LoadTrainData(string path, bool smoothing)
        {
            this.trainData = LoadDataSet(path, smoothing);
        }

        public void LoadTestData(string path)
        {
            this.testData = LoadDataSet(path, false);
        }

        public void StartOptimization(int maxEpoch, string workDir)
        {
            using var logger = new Logger($"{workDir}/{LOG_FILE_NAME}", Console.OpenStandardOutput());
            var currentModel = this.currentModel;
            var bestModel = new LatentFactorValueFunction(this.currentModel);
            var stageNum = this.currentModel.StageNum;
            var weightGrad = new float[BiasIdx + 1];
            var vectorGrad = (from _ in Enumerable.Range(0, BiasIdx + 1) select new float[InteractionVectorLen]).ToArray();

            try
            {
                for (var stage = 0; stage < currentModel.StageNum; stage++)
                {
                    logger.WriteLine("////////////////////////////////////////////////////////////");
                    logger.WriteLine($"stage = {stage + 1}");
                    logger.WriteLine($"train_data_num = {this.trainData[stage].Length}");
                    logger.WriteLine($"test_data_num = {this.testData[stage].Length}");
                    logger.WriteLine("start optimization.");
                    logger.Flush();

                    var trainBatch = (from data in this.trainData[stage] select (board: new BoardFeature(new FastBoard(DiscColor.Black, data.Board)), output: GetValueFromGameResult(data.Result))).ToArray();
                    var testBatch = (from data in this.testData[stage] select (board: new BoardFeature(new FastBoard(DiscColor.Black, data.Board)), output: GetValueFromGameResult(data.Result))).ToArray();
                    var featuresCount = CountFeatures(trainBatch.Select(b => b.board));
                    var featuresCountInverse = featuresCount.Select(n => 1.0f / n).ToArray();
                    var prevTrainLoss = float.PositiveInfinity;
                    var prevTestLoss = float.PositiveInfinity;
                    var learningRate = this.LearningRate;
                    var overfittingCount = 0;

                    for (var epoch = 0; epoch < maxEpoch; epoch++)
                    {
                        logger.WriteLine($"epoch = {epoch + 1} / {maxEpoch}");
                        var trainLoss = CalculateBlackGradient(stage, trainBatch, weightGrad, vectorGrad);
                        logger.WriteLine($"train_loss: {prevTrainLoss} -> {trainLoss}");
                        prevTrainLoss = trainLoss;
                        ApplyBlackGradientToCurrentModel(stage, featuresCount, weightGrad, vectorGrad, learningRate, featuresCountInverse, this.L2FactorForWeight, this.L2FactorForVector);

                        if ((epoch + 1) % this.CheckPointInterval == 0)
                        {
                            logger.WriteLine("\ncheckpoint.");

                            var testLoss = CalculateLoss(testBatch);
                            logger.WriteLine($"test_loss: {prevTestLoss} -> {testLoss}");

                            if (prevTestLoss - testLoss > this.Tolerance)
                            {
                                prevTestLoss = testLoss;
                                bestModel = new LatentFactorValueFunction(this.currentModel);
                                bestModel.CopyBlackParamsToSymmetricFeatureIdx();
                                bestModel.CopyBlackParamsToWhiteParams();
                                bestModel.SaveToFile($"{workDir}/{PARAM_FILE_NAME}");
                                overfittingCount = 0;
                            }
                            else if (++overfittingCount <= this.Pacience)
                            {
                                logger.WriteLine("rollback.");
                                this.currentModel = new LatentFactorValueFunction(bestModel);
                                learningRate *= this.LearningRateDecay;
                            }
                            else
                                break;
                            logger.WriteLine();
                        }

                        Array.Clear(weightGrad);
                        foreach (var g in vectorGrad)
                            Array.Clear(g);

                        logger.WriteLine();
                        logger.Flush();
                    }
                }
                logger.WriteLine("stop optimization.\n");
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex);
                return;
            }
        }

        static float[] GetFeaturesCountInverse(int[] featuresCount)
        {
            var featuresCountInverse = new float[featuresCount.Length];
            for (var featureIdx = 0; featureIdx < featuresCountInverse.Length; featureIdx++)
            {
                var symmetricFeatureIdx = ToSymmetricFeatureIdx[featureIdx];
                if (featureIdx > symmetricFeatureIdx)
                    continue;
                featuresCountInverse[featureIdx] = Math.Min(1.0f / featuresCount[featureIdx], FEATURES_COUNT_INVERSE_MAX);
            }
            return featuresCountInverse;
        }

        static int[] CountFeatures(IEnumerable<BoardFeature> boardFeatures)
        {
            var featureCount = new int[BiasIdx + 1];
            foreach (var bf in boardFeatures)
            {
                var features = bf.Features;
                for (var i = 0; i < features.Length; i++)
                {
                    var featureIdx = features[i] + ValueFunction.FeatureIdxOffset[i];
                    var symmetricFeatureIdx = ToSymmetricFeatureIdx[featureIdx];
                    featureCount[Math.Min(featureIdx, symmetricFeatureIdx)]++;
                }
            }
            return featureCount;
        }

        static float GetValueFromGameResult(GameResult result)
        {
            return result switch
            {
                GameResult.Win => 1.0f,
                GameResult.Loss => 0.0f,
                GameResult.Draw => 0.5f,
                _ => throw new ArgumentException($"Game result {result} is invalid.")
            };
        }

        int CalcStage(int emptyCount)
        {
            return (Board.SQUARE_NUM - 4 - emptyCount) / currentModel.MoveCountPerStage;
        }

        TrainData[][] LoadDataSet(string path, bool smoothing)
        {
            var csv = new CSVReader(path);
            var trainData = (from _ in Enumerable.Range(0, this.currentModel.StageNum) select new List<TrainData>()).ToArray();

            while (csv.Peek() != -1)
            {
                var row = csv.ReadRow();
                var data = new TrainData();
                data.Board = new Bitboard(ulong.Parse(row["current_player_board"]), ulong.Parse(row["opponent_player_board"]));
                data.Result = (GameResult)sbyte.Parse(row["result"]);
                var stage = CalcStage(data.Board.GetEmptyCount());
                trainData[stage].Add(data);

                if (smoothing)
                {
                    if (stage != 0 && CalcStage(data.Board.GetEmptyCount() + 1) != stage)
                        trainData[stage - 1].Add(data);

                    if (stage != this.currentModel.StageNum - 1 && CalcStage(data.Board.GetEmptyCount() - 1) != stage)
                        trainData[stage + 1].Add(data);
                }
            }
            return (from n in trainData select n.ToArray()).ToArray();
        }

        unsafe float CalculateBlackGradient(int stage, (BoardFeature board, float output)[] batch, float[] weightGrad, float[][] vectorGrad)
        {
            var loss = 0.0f;
            var valueFunc = this.currentModel;
            var blackParams = valueFunc.Params[(int)DiscColor.Black][stage];
            var threadNum = Environment.ProcessorCount;
            var batchNumPerThread = batch.Length / threadNum;
            var vecBuffers = stackalloc float[InteractionVectorLen * threadNum];

            Parallel.For(0, threadNum, threadID=>
            {
                var vecBuffer = &vecBuffers[InteractionVectorLen * threadID];
                var end = batchNumPerThread * (threadID + 1);
                for (var batchIdx = batchNumPerThread * threadID; batchIdx < end; batchIdx++)
                    calcGrad(batchIdx, vecBuffer);
            });

            for (var batchIdx = batchNumPerThread * threadNum; batchIdx < batch.Length; batchIdx++)
                calcGrad(batchIdx, vecBuffers);

            void calcGrad(int batchIdx, float* vecBuffer)
            {
                var data = batch[batchIdx];
                var y = valueFunc.F_ForOptimizing(data.board);
                var delta = y - data.output;
                AtomicOperations.Add(ref loss, MathFunctions.BinaryCrossEntropy(y, data.output));

                var features = data.board.Features;
                for (var i = 0; i < features.Length; i++)
                {
                    var featureIdx = features[i] + FeatureIdxOffset[i];
                    var symmetricFeatureIdx = ToSymmetricFeatureIdx[featureIdx];
                    if (featureIdx > symmetricFeatureIdx)
                        featureIdx = symmetricFeatureIdx;
                    AtomicOperations.Add(ref weightGrad[featureIdx], delta);

                    if (featureIdx == BiasIdx)
                        continue;

                    var featureParam = blackParams[featureIdx];
                    var vecSum = Vector256<float>.Zero;
                    for (var j = 0; j < features.Length; j++)
                    {
                        if (i == j)
                            continue;
                        var otherFeatureIdx = features[j] + FeatureIdxOffset[j];
                        var otherSymmetricFeatureIdx = ToSymmetricFeatureIdx[otherFeatureIdx];
                        var otherFeatureParam = blackParams[Math.Min(otherFeatureIdx, otherSymmetricFeatureIdx)];
                        vecSum = Avx.Add(vecSum, otherFeatureParam.Vector);
                    }
                    Avx.Store(vecBuffer, vecSum);

                    var vecGrad = vectorGrad[featureIdx];
                    for (var j = 0; j < InteractionVectorLen; j++)
                        AtomicOperations.Add(ref vecGrad[j], vecBuffer[j]);
                }
            }
            return loss / batch.Length;
        }

        unsafe void ApplyBlackGradientToCurrentModel(int stage, int[] featuresCount, float[] weightGrad, float[][] vectorGrad, float rate, float[] featureNumInverse, float l2FactorForWeight, float l2FactorForVector)
        {
            var blackParams = this.currentModel.Params[(int)DiscColor.Black][stage];

            Parallel.For(0, weightGrad.Length, featureIdx =>
            {
                if (featureIdx > ToSymmetricFeatureIdx[featureIdx])
                    return;

                if (featuresCount[featureIdx] != 0)
                {
                    blackParams[featureIdx].Weight -= rate * (featureNumInverse[featureIdx] * weightGrad[featureIdx] + l2FactorForWeight * blackParams[featureIdx].Weight);

                    Vector256<float> featureNumInvVec;
                    Vector256<float> gradVec;
                    fixed (float* fNumInv = &featureNumInverse[featureIdx]) featureNumInvVec = Avx.BroadcastScalarToVector256(fNumInv);
                    fixed (float* g = vectorGrad[featureIdx]) gradVec = Avx.LoadVector256(g);

                    var tmp = rate;
                    var rateVec = Avx.BroadcastScalarToVector256(&tmp);
                    tmp = l2FactorForVector;
                    var l2Vec = Avx.BroadcastScalarToVector256(&tmp);
                    var vecNrm = VectorOperator.Nrm(ref blackParams[featureIdx].Vector);
                    tmp = 1.0f / vecNrm;
                    var vecNrmInverse = Avx.BroadcastScalarToVector256(&tmp);
                    var penalty = Avx.Multiply(Avx.Multiply(l2Vec, blackParams[featureIdx].Vector), vecNrmInverse);
                    var grad = Avx.Multiply(featureNumInvVec, gradVec);
                    var diff = Avx.Multiply(rateVec, Avx.Add(grad, penalty));
                    blackParams[featureIdx].Vector = Avx.Subtract(blackParams[featureIdx].Vector, diff);
                }
                else
                {
                    blackParams[featureIdx].Weight -= rate * l2FactorForWeight * blackParams[featureIdx].Weight; 
                    var tmp = rate;
                    var rateVec = Avx.BroadcastScalarToVector256(&tmp);
                    tmp = l2FactorForVector;
                    var l2Vec = Avx.BroadcastScalarToVector256(&tmp);
                    var vecNrm = VectorOperator.Nrm(ref blackParams[featureIdx].Vector);
                    tmp = 1.0f / vecNrm;
                    var vecNrmInverse = Avx.BroadcastScalarToVector256(&tmp);
                    var penalty = Avx.Multiply(Avx.Multiply(l2Vec, blackParams[featureIdx].Vector), vecNrmInverse);
                    var diff = Avx.Multiply(rateVec, penalty);
                    blackParams[featureIdx].Vector = Avx.Subtract(blackParams[featureIdx].Vector, diff);
                }
            });
        }

        float CalculateLoss((BoardFeature board, float output)[] batch)
        {
            var threadNum = Environment.ProcessorCount;
            var lossSum = new float[threadNum];
            var batchSizePerThread = batch.Length / threadNum;

            Parallel.For(0, threadNum, threadID =>
            {
                for (var i = batchSizePerThread * threadID; i < batchSizePerThread * (threadID + 1); i++)
                {
                    var data = batch[i];
                    lossSum[threadID] += MathFunctions.BinaryCrossEntropy(this.currentModel.F_ForOptimizing(data.board), data.output);
                }
            });

            for (var i = batchSizePerThread * threadNum; i < batch.Length; i++)
            {
                var data = batch[i];
                lossSum[0] += MathFunctions.BinaryCrossEntropy(this.currentModel.F_ForOptimizing(data.board), data.output);
            }
            return lossSum.Sum() / batch.Length;
        }
    }
}
