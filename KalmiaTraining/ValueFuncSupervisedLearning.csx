#r "../Kalmia/bin/x64/Release/netcoreapp3.1/Kalmia.dll"
//#r "../Kalmia/bin/x64/Debug/netcoreapp3.1/Kalmia.dll"

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime;
using System.Reflection;

using Kalmia;
using Kalmia.IO;
using Kalmia.Reversi;
using Kalmia.Evaluation;

const int MOVE_COUNT_PER_STAGE = 4;
const int STAGE_NUM = 16;

/*
 * Helper
 */
struct TrainData
{
    public Bitboard Board { get; set; }
    public GameResult Result { get; set; }
}

int CalcStage(int emptyCount)
{
    return (Board.SQUARE_NUM - 4 - emptyCount) / MOVE_COUNT_PER_STAGE;
}

TrainData[][] LoadData(string path, bool smoothing)
{
    var csv = new CSVReader(path);
    var trainData = (from _ in Enumerable.Range(0, STAGE_NUM) select new List<TrainData>()).ToArray();

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

            if (stage != STAGE_NUM - 1 && CalcStage(data.Board.GetEmptyCount() - 1) != stage)
                trainData[stage + 1].Add(data);
        }
    }
    return (from n in trainData select n.ToArray()).ToArray();
}

int[] CountFeature(TrainData[] dataSet)
{
    var featureCount = new int[ValueFunction.BIAS_IDX + 1];
    var fastboard = new FastBoard();
    var boardFeature = new BoardFeature();
    foreach (var board in (from n in dataSet select n.Board))
    {
        fastboard.SetBitboard(board);
        boardFeature.InitFeatures(fastboard);
        var features = boardFeature.Features;
        for (var i = 0; i < features.Length; i++)
        {
            var featureIdx = features[i] + ValueFunction.FeatureIdxOffset[i];
            featureCount[featureIdx]++;
        }
    }
    return featureCount;
}

float[] NormalizeLearningRate(float learningRateBase, float maxLearningRate, int[] featureCount)
{
    var learningRates = new float[featureCount.Length];
    for (var featureIdx = 0; featureIdx < learningRates.Length; featureIdx++)
        learningRates[featureIdx] = Math.Min(maxLearningRate, learningRateBase / featureCount[featureIdx]);
    return learningRates;
}

float GetValueFromGameResult(GameResult result)
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

void SetCurrentDirectory(string path)
{
    var assembly = Assembly.GetEntryAssembly();
    Directory.SetCurrentDirectory(Path.GetDirectoryName(assembly.Location));
}

/*
 * Optimize Main
 */
const string WORKING_DIR_PATH = "../../ValueFunctionOptimize";
const string LOG_FILE_NAME = "optimize_log.txt";
const string PARAM_FILE_NAME = "optimized_param.dat";
const string TRAIN_DATA_PATH = "../../TrainData/FFO/train_data.csv";
const string TEST_DATA_PATH = "../../TrainData/FFO/test_data.csv";
const int MAX_EPOCH = 100000;
const int CHECK_TEST_LOSS_INTERVAL = 5;
const int PACIENCE = 5;
const float LEARNING_RATE_BASE = 0.1f;
const float MAX_LEARNING_RATE = LEARNING_RATE_BASE / 25.0f;
const float LEARNING_RATE_DECAY = 0.5f;

SetCurrentDirectory(WORKING_DIR_PATH);
var trainDataSet = LoadData(TRAIN_DATA_PATH, true);
var testDataSet = LoadData(TEST_DATA_PATH, false);
var valueFunc = new ValueFunction("Kalmia", 0, 4);
var bestModel = new ValueFunction(valueFunc);
var logger = new Logger(LOG_FILE_NAME, true);

for (var stage = 0; stage < STAGE_NUM; stage++)
{
    logger.WriteLine("////////////////////////////////////////////////////////////");
    logger.WriteLine($"stage = {stage}");
    logger.WriteLine($"train_data_num = {trainDataSet[stage].Length}");
    logger.WriteLine($"test_data_num = {testDataSet[stage].Length}");
    logger.WriteLine("start optimization.");

    var trainBatch = (from data in trainDataSet[stage] select (new BoardFeature(new FastBoard(Color.Black, data.Board)), GetValueFromGameResult(data.Result))).ToArray();
    var testBatch = (from data in testDataSet[stage] select (new BoardFeature(new FastBoard(Color.Black, data.Board)), GetValueFromGameResult(data.Result))).ToArray();
    var prevTrainLoss = float.PositiveInfinity;
    var prevTestLoss = float.PositiveInfinity;
    var overfittingCount = 0;
    var learningRate = NormalizeLearningRate(LEARNING_RATE_BASE, MAX_LEARNING_RATE, CountFeature(trainDataSet[stage]));
    var weightGrad = new float[ValueFunction.BIAS_IDX + 1];

    for (var epoch = 0; epoch < MAX_EPOCH; epoch++)
    {
        logger.WriteLine($"epoch = {epoch + 1} / {MAX_EPOCH}");
        var trainLoss = valueFunc.CalculateGradient(stage, trainBatch, weightGrad);
        logger.WriteLine($"train_loss: {prevTrainLoss} → {trainLoss}");
        prevTrainLoss = trainLoss;
        valueFunc.ApplyGradientToBlackWeight(stage, weightGrad, learningRate);

        if ((epoch + 1) % CHECK_TEST_LOSS_INTERVAL == 0)
        {
            logger.WriteLine("\ncheckpoint.");
            var testLoss = valueFunc.CalculateLoss(testBatch);
            logger.WriteLine($"test_loss: {prevTestLoss} → {testLoss}");
            if (testLoss < prevTestLoss)
            {
                prevTestLoss = testLoss;
                bestModel = new ValueFunction(valueFunc);
                overfittingCount = 0;
            }
            else if (++overfittingCount <= PACIENCE)
            {
                logger.WriteLine("rollback.");
                valueFunc = new ValueFunction(bestModel);
                for (var i = 0; i < learningRate.Length; i++)
                    learningRate[i] *= LEARNING_RATE_DECAY;
            }
            else
                break;
            logger.WriteLine();
        }
        logger.WriteLine();
    }

    logger.WriteLine("stop optimization.\n");
    valueFunc.CopyBlackWeightToWhiteWeight();
    valueFunc.SaveToFile(PARAM_FILE_NAME);
}
