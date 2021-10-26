//#r "../Kalmia/bin/x64/Release/netcoreapp3.1/Kalmia.dll"
#r "../Kalmia/bin/x64/Debug/netcoreapp3.1/Kalmia.dll"

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Kalmia;
using Kalmia.IO;
using Kalmia.Reversi;
using Kalmia.Evaluation;

const int MOVE_COUNT_PER_STAGE = 60;
const int STAGE_NUM = 1;

/*
 * Helper
 */
struct TrainData
{
    public Bitboard Board { get; set; }
    public BoardPosition Pos { get; set; }
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
        data.Pos = (BoardPosition)Move.StringToPosition(row["next_move"]);
        if (data.Pos == BoardPosition.Null || data.Pos == BoardPosition.Pass)
            continue;
        data.Board = new Bitboard(ulong.Parse(row["current_player_board"]), ulong.Parse(row["opponent_player_board"]));
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
    var featureCount = new int[PolicyFunction.BIAS_IDX + 1];
    var fastboard = new FastBoard();
    var boardFeature = new BoardFeature();
    foreach (var board in (from n in dataSet select n.Board))
    {
        fastboard.SetBitboard(board);
        boardFeature.InitFeatures(fastboard);
        var features = boardFeature.Features;
        for (var i = 0; i < features.Length; i++)
        {
            var featureIdx = features[i] + PolicyFunction.FeatureIdxOffset[i];
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

void SetCurrentDirectory(string path)
{
    var assembly = Assembly.GetEntryAssembly();
    Directory.SetCurrentDirectory(Path.GetDirectoryName(assembly.Location));
}

(Bitboard board, int outputIdx)[] CreateBatch(TrainData[] data)
{
    var batch = new (Bitboard board, int outputIdx)[data.Length * 8];
    var i = 0;
    foreach (var d in data)
    {
        var board = d.Board;
        var pos = d.Pos;
        batch[i++] = (board, (int)pos);
        batch[i++] = (board.Mirror(), (int)Move.MirrorPosition(pos));
        for (var j = 0; j < 3; j++)
        {
            board = board.Rotate90Clockwise();
            pos = Move.RotatePosition90Clockwise(pos);
            batch[i++] = (board, (int)pos);
            batch[i++] = (board.Mirror(), (int)Move.MirrorPosition(pos));
        }
    }
    return batch;
}

/*
 * Optimize Main
 */
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

var trainDataSet = LoadData(TRAIN_DATA_PATH, true);
var testDataSet = LoadData(TEST_DATA_PATH, false);
var policyFunc = new PolicyFunction("Kalmia", 0, MOVE_COUNT_PER_STAGE);
var bestModel = new PolicyFunction(policyFunc);
var logger = new Logger(LOG_FILE_NAME, true);

for (var stage = 0; stage < STAGE_NUM; stage++)
{
    logger.WriteLine("////////////////////////////////////////////////////////////");
    logger.WriteLine($"stage = {stage}");
    logger.WriteLine($"train_data_num = {trainDataSet[stage].Length}");
    logger.WriteLine($"test_data_num = {testDataSet[stage].Length}");
    logger.WriteLine("start optimization.");

    var trainBatch = CreateBatch(trainDataSet[stage]);
    var testBatch = (from n in testDataSet[stage] select (new BoardFeature(new FastBoard(Color.Black, n.Board)), (int)n.Pos)).ToArray();
    var prevTrainLoss = float.PositiveInfinity;
    var prevTestLoss = float.PositiveInfinity;
    var prevTestAccuracy = 0.0f;
    var overfittingCount = 0;
    var learningRate = NormalizeLearningRate(LEARNING_RATE_BASE, MAX_LEARNING_RATE, CountFeature(trainDataSet[stage]));
    var weightGrad = (from _ in Enumerable.Range(0, PolicyFunction.BIAS_IDX + 1) select new float[Board.SQUARE_NUM]).ToArray();

    for (var epoch = 0; epoch < MAX_EPOCH; epoch++)
    {
        logger.WriteLine($"epoch = {epoch + 1} / {MAX_EPOCH}");
        var trainLoss = policyFunc.CalculateGradient(stage, Color.Black, trainBatch, weightGrad);
        logger.WriteLine($"train_loss: {prevTrainLoss} → {trainLoss}");
        prevTrainLoss = trainLoss;
        policyFunc.ApplyGradientToBlackWeight(stage, weightGrad, learningRate);

        if ((epoch + 1) % CHECK_TEST_LOSS_INTERVAL == 0)
        {
            logger.WriteLine("\ncheckpoint.");
            (var testLoss, var testAccuracy) = policyFunc.CalculateLossAndAccuracy(testBatch);
            logger.WriteLine($"test_loss: {prevTestLoss} → {testLoss}");
            logger.WriteLine($"test_accuracy: {prevTestAccuracy:.2f} % → {testAccuracy:.2f} %");
            if (testLoss < prevTestLoss)
            {
                prevTestLoss = testLoss;
                prevTestAccuracy = testAccuracy;
                bestModel = new PolicyFunction(policyFunc);
                overfittingCount = 0;
            }
            else if (++overfittingCount <= PACIENCE)
            {
                logger.WriteLine("rollback.");
                policyFunc = new PolicyFunction(bestModel);
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
    policyFunc.CopyBlackWeightToWhiteWeight();
    policyFunc.SaveToFile(PARAM_FILE_NAME);
}
