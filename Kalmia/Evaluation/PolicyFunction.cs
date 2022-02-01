using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
    public class PolicyFunction
    {
        static readonly int[] FEATURE_IDX_OFFSET;
        static readonly int[] TO_OPPONENT_FEATURE_IDX;
        public static readonly int BIAS_IDX;

        public static ReadOnlySpan<int> FeatureIdxOffset { get { return FEATURE_IDX_OFFSET; } }
        public static ReadOnlySpan<int> ToOpponentFeatureIdx { get { return TO_OPPONENT_FEATURE_IDX; } }

        public float[][][][] Weight { get; }    // Weight[Color][Stage][Feature][Position]
        public EvalParamsFileHeader Header { get; private set; }
        public int StageNum { get; private set; }
        public int MoveCountPerStage { get; private set; }

        static PolicyFunction()
        {
            var i = 0;
            var offset = 0;
            FEATURE_IDX_OFFSET = new int[BoardFeature.PATTERN_NUM_SUM];
            for (var patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM; patternType++) 
            {
                var patternFeatureNum = BoardFeature.PatternFeatureNum[patternType];
                for (var j = 0; j < BoardFeature.PatternNum[patternType]; j++)
                {
                    FEATURE_IDX_OFFSET[i++] = offset;
                    offset += patternFeatureNum;
                }
            }
            BIAS_IDX = FEATURE_IDX_OFFSET[^1];

            var featureNum = (from n in BoardFeature.PatternFeatureNum.ToArray().Zip(BoardFeature.PatternNum.ToArray()) select n.First * n.Second).Sum();
            TO_OPPONENT_FEATURE_IDX = new int[featureNum];
            i = 0;
            offset = 0;
            for(var patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM; patternType++)
            {
                var patternFeatureNum = BoardFeature.PatternFeatureNum[patternType];
                for(var j = 0; j < BoardFeature.PatternNum[patternType]; j++)
                {
                    for(var feature = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var featureIdx = feature + offset;
                        TO_OPPONENT_FEATURE_IDX[featureIdx] = BoardFeature.CalcOpponentFeature(feature, BoardFeature.PatternSize[patternType]) + offset;
                    }
                    offset += patternFeatureNum;
                }
            }
        }

        public PolicyFunction(string label, int version, int moveCountPerStage)
        {
            this.Header = new EvalParamsFileHeader(label, version, DateTime.Now);
            this.MoveCountPerStage = moveCountPerStage;
            this.StageNum = (Board.SQUARE_NUM - 4) / moveCountPerStage;
            this.Weight = new float[2][][][];
            var featureNum = FEATURE_IDX_OFFSET[^1] + 1;

            for (var color = 0; color < this.Weight.Length; color++)
            {
                this.Weight[color] = new float[this.StageNum][][];
                for(var stage = 0; stage < this.Weight[color].Length; stage++)
                {
                    this.Weight[color][stage] = new float[featureNum][];
                    for (var i = 0; i < this.Weight[color][stage].Length; i++)
                        this.Weight[color][stage][i] = new float[Board.SQUARE_NUM];
                }
            }
        }

        public PolicyFunction(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            this.Header = new EvalParamsFileHeader(fs);
            this.Weight = LoadWeight(fs);
            this.StageNum = this.Weight.Length;
            this.MoveCountPerStage = (Board.SQUARE_NUM - 4) / this.StageNum;
        }

        public PolicyFunction(PolicyFunction policy)
        {
            this.Header = policy.Header;
            this.StageNum = policy.StageNum;
            this.MoveCountPerStage = policy.MoveCountPerStage;
            this.Weight = new float[2][][][];
            for(var color = 0; color < this.Weight.Length; color++)
            {
                this.Weight[color] = new float[this.StageNum][][];
                for(var stage = 0; stage < this.StageNum; stage++)
                {
                    this.Weight[color][stage] = new float[FEATURE_IDX_OFFSET[^1] + 1][];
                    for(var featureIdx = 0; featureIdx < this.Weight[color][stage].Length; featureIdx++)
                    {
                        var dest = this.Weight[color][stage][featureIdx] = new float[Board.SQUARE_NUM];
                        Buffer.BlockCopy(policy.Weight[color][stage][featureIdx], 0, dest, 0, sizeof(float) * dest.Length);
                    }
                }
            }
        }

        float[][][][] LoadWeight(FileStream fs)
        {
            fs.Seek(EvalParamsFileHeader.HEADER_SIZE, SeekOrigin.Begin);
            var stageNum = fs.ReadByte();
            var weight = (from _ in Enumerable.Range(0, 2) select new float[stageNum][][]).ToArray();
            var featureNum = FEATURE_IDX_OFFSET[^1] + 1;
            var buffer = new byte[sizeof(float) * Board.SQUARE_NUM];
            for (var stage = 0; stage < stageNum; stage++)
            {
                var bw = weight[(int)StoneColor.Black][stage] = new float[featureNum][];
                var ww = weight[(int)StoneColor.White][stage] = new float[featureNum][];
                for(var featureIdx = 0; featureIdx < bw.Length; featureIdx++)
                {
                    var bbw = bw[featureIdx] = new float[Board.SQUARE_NUM];
                    var www = ww[TO_OPPONENT_FEATURE_IDX[featureIdx]] = new float[Board.SQUARE_NUM];
                    fs.Read(buffer, 0, buffer.Length);
                    Buffer.BlockCopy(buffer, 0, bbw, 0, bbw.Length);
                    Buffer.BlockCopy(buffer, 0, www, 0, www.Length);
                }
            }
            return weight;
        }

        public void SaveToFile(string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            SaveToFile(fs);
        }

        public void SaveToFile(FileStream fs)
        {
            this.Header.WriteToStream(fs); ;
            var weight = this.Weight[(int)StoneColor.Black];
            fs.WriteByte((byte)this.StageNum);
            for (var stage = 0; stage < this.StageNum; stage++)
                for (var featureIdx = 0; featureIdx < weight.Length; featureIdx++)
                    for (var pos = 0; pos < Board.SQUARE_NUM; pos++)
                        fs.Write(BitConverter.GetBytes(weight[stage][featureIdx][pos]), 0, sizeof(float));
        }

        public void F(BoardFeature board, float[] moveProb)
        {
            F(board, moveProb.AsSpan());
        }

        public void F(BoardFeature board, Span<float> moveProb)
        {
            F((Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveCountPerStage, board, moveProb);
        }

        public void F(int stage, BoardFeature board, Span<float> moveProb)
        {
            var color = (int)board.SideToMove;
            var features = board.Features;
            var weight = this.Weight[color][stage];

            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
            {
                var w = weight[features[i] + FEATURE_IDX_OFFSET[i]];
                for (var pos = 0; pos < w.Length; pos++)
                    moveProb[pos] += w[pos];
            }

            var expSum = 0.0f;
            foreach (var p in moveProb)
                expSum += MathF.Exp(p);
            for (var i = 0; i < moveProb.Length; i++)
                moveProb[i] = MathF.Exp(moveProb[i]) / expSum;
        }

        public void F(BoardFeature board, Move[] moves, float[] moveProb, int moveCount)
        {
            F(board, moves, moveProb.AsSpan(), moveCount);
        }

        public void F(BoardFeature board, Move[] moves, Span<float> moveProb, int moveCount)
        {
            var stage = (Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveCountPerStage;
            var color = (int)board.SideToMove;
            var features = board.Features;
            var weight = this.Weight[color][stage];

            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
            {
                var w = weight[features[i] + FEATURE_IDX_OFFSET[i]];
                for (var j = 0; j < moveCount; j++)
                {
                    var pos = (int)moves[j].Pos;
                    moveProb[j] += w[pos];
                }
            }

            var sum = 0.0f;
            for (var i = 0; i < moveCount; i++)
                sum += moveProb[i] = MathF.Exp(moveProb[i]);
            for (var i = 0; i < moveCount; i++)
                moveProb[i] /= sum;
        }

        public float CalculateGradient(int stage, (BoardFeature board, int outputIdx)[] batch, float[][] weightGrad)
        {
            var threadNum = Environment.ProcessorCount;
            var batchSizePerThread = batch.Length / threadNum;
            var loss = new float[threadNum];
            Parallel.For(0, threadNum, threadID => loss[threadID] = calcGradKernel(batchSizePerThread * threadID, batchSizePerThread * (threadID + 1)));
            loss[0] += calcGradKernel(batchSizePerThread * threadNum, batch.Length);
            return loss.Sum() / batch.Length;

            float calcGradKernel(int start, int end)
            {
                var lossSum = 0.0f;
                var y = new float[Board.SQUARE_NUM];
                var delta = new float[Board.SQUARE_NUM];
                for (var batchIdx = start; batchIdx < end; batchIdx++)
                {
                    var data = batch[batchIdx];
                    F(stage, data.board, y);
                    lossSum += MathFunctions.OneHotCrossEntropy(y, data.outputIdx);

                    var features = data.board.Features;
                    for(var i = 0; i < features.Length; i++)
                    {
                        var wg = weightGrad[features[i] + FEATURE_IDX_OFFSET[i]];
                        var pos = 0;
                        for (pos = 0; pos < data.outputIdx; pos++)
                            AtomicOperations.Add(ref wg[pos], y[pos]);
                        AtomicOperations.Add(ref wg[pos], y[pos] - 1.0f);
                        for (pos++; pos < wg.Length; pos++)
                            AtomicOperations.Add(ref wg[pos], y[pos]);
                    }
                }
                return lossSum;
            }
        }

        public float CalculateGradient(int stage, StoneColor color, (Bitboard board, int outputIdx)[] batch, float[][] weightGrad)
        {
            var threadNum = Environment.ProcessorCount;
            var batchSizePerThread = batch.Length / threadNum;
            var loss = new float[threadNum];
            Parallel.For(0, threadNum, threadID => loss[threadID] = calcGradKernel(batchSizePerThread * threadID, batchSizePerThread * (threadID + 1)));
            loss[0] += calcGradKernel(batchSizePerThread * threadNum, batch.Length);
            return loss.Sum() / batch.Length;

            float calcGradKernel(int start, int end)
            {
                var lossSum = 0.0f;
                var y = new float[Board.SQUARE_NUM];
                var delta = new float[Board.SQUARE_NUM];
                var fastBoard = new FastBoard(color, new Bitboard());
                var featureBoard = new BoardFeature();
                for (var batchIdx = start; batchIdx < end; batchIdx++)
                {
                    var data = batch[batchIdx];
                    fastBoard.SetBitboard(data.board);
                    featureBoard.InitFeatures(fastBoard);
                    F(stage, featureBoard, y);
                    lossSum += MathFunctions.OneHotCrossEntropy(y, data.outputIdx);

                    var features = featureBoard.Features;
                    for (var i = 0; i < features.Length; i++)
                    {
                        var wg = weightGrad[features[i] + FEATURE_IDX_OFFSET[i]];
                        var pos = 0;
                        for (pos = 0; pos < data.outputIdx; pos++)
                            AtomicOperations.Add(ref wg[pos], y[pos]);
                        AtomicOperations.Add(ref wg[pos], y[pos] - 1.0f);
                        for (pos++; pos < wg.Length; pos++)
                            AtomicOperations.Add(ref wg[pos], y[pos]);
                    }
                }
                return lossSum;
            }
        }

        public void ApplyGradientToBlackWeight(int stage, float[][] weightGrad, float[] rate)
        {
            var weight = this.Weight[(int)StoneColor.Black][stage];
            Parallel.For(0, weight.Length, featureIdx =>
            {
                var w = weight[featureIdx];
                var wg = weightGrad[featureIdx];
                for (var pos = 0; pos < w.Length; pos++)
                    w[pos] -= rate[featureIdx] * wg[pos];
            });
        }

        public (float loss, float accuracy) CalculateLossAndAccuracy((BoardFeature board, int outputIdx)[] batch)
        {
            var threadNum = Environment.ProcessorCount;
            var loss = new float[threadNum];
            var accuracy = new int[threadNum];
            var batchSizePerThread = batch.Length / threadNum;
            Parallel.For(0, threadNum, threadID => calcLossAndAccuracyKernel(threadID, batchSizePerThread * threadID, batchSizePerThread * (threadID + 1)));
            calcLossAndAccuracyKernel(0, batchSizePerThread * threadNum, batch.Length);
            return (loss.Sum() / batch.Length, (float)accuracy.Sum() / batch.Length);

            void calcLossAndAccuracyKernel(int threadID, int start, int end)
            {
                var lossSum = 0.0f;
                var accuracySum = 0;
                var y = new float[Board.SQUARE_NUM];
                for(var batchIdx = start; batchIdx < end; batchIdx++)
                {
                    var data = batch[batchIdx];
                    F(data.board, y);
                    lossSum += MathFunctions.OneHotCrossEntropy(y, data.outputIdx);
                    if (MathF.Abs(y[data.outputIdx] - y.Max()) < 1.0e-6)
                        accuracySum++;
                }
                loss[threadID] += lossSum;
                accuracy[threadID] += accuracySum;
            }
        }

        public void CopyBlackWeightToWhiteWeight()
        {
            var blackWeight = this.Weight[(int)StoneColor.Black];
            var whiteWeight = this.Weight[(int)StoneColor.White];
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                var bw = blackWeight[stage];
                var ww = whiteWeight[stage];
                for (var featureIdx = 0; featureIdx < ww.Length; featureIdx++)
                {
                    var tmp = ww[TO_OPPONENT_FEATURE_IDX[featureIdx]];
                    Buffer.BlockCopy(bw[featureIdx], 0, tmp, 0, sizeof(float) * tmp.Length);
                }
            }
        }
    }
}
