using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
    public class ValueFunction : IValueFunction
    {
        static readonly int[] FEATURE_IDX_OFFSET;
        static readonly int[] TO_OPPONENT_FEATURE_IDX;
        static readonly int[] TO_SYMMETRIC_FEATURE_IDX;
        public static readonly int BIAS_IDX;

        public static ReadOnlySpan<int> FeatureIdxOffset { get { return FEATURE_IDX_OFFSET; } }
        public static ReadOnlySpan<int> ToOpponentFeatureIdx { get { return TO_OPPONENT_FEATURE_IDX; } }
        public static ReadOnlySpan<int> ToSymmetricFeatureIdx { get { return TO_SYMMETRIC_FEATURE_IDX; } }

        public float[][][] Weight { get; }      // WEIGHT[Color][Stage][Feature]

        public EvalParamsFileHeader Header { get; private set; }
        public int StageNum { get; private set; }
        public int MoveCountPerStage { get; private set; }

        static ValueFunction()
        {
            FEATURE_IDX_OFFSET = new int[BoardFeature.PATTERN_NUM_SUM];
            var i = 0;
            var offset = 0;
            for (var patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM; patternType++)
            {
                for (var j = 0; j < BoardFeature.PatternNum[patternType]; j++)
                    FEATURE_IDX_OFFSET[i++] = offset;
                offset += BoardFeature.PatternFeatureNum[patternType];
            }
            BIAS_IDX = FEATURE_IDX_OFFSET[^1];

            TO_OPPONENT_FEATURE_IDX = new int[BoardFeature.PatternFeatureNum.Sum()];
            TO_SYMMETRIC_FEATURE_IDX = new int[BoardFeature.PatternFeatureNum.Sum()];
            offset = 0;
            for (var patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM; patternType++)
            {
                var patternSize = BoardFeature.PatternSize[patternType];
                for (var feature = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                {
                    TO_OPPONENT_FEATURE_IDX[feature + offset] = BoardFeature.CalcOpponentFeature(feature, patternSize) + offset;
                    TO_SYMMETRIC_FEATURE_IDX[feature + offset] = BoardFeature.FlipFeature(patternType, feature) + offset;
                }
                offset += BoardFeature.PatternFeatureNum[patternType];
            }
        }

        public ValueFunction(string label, int version, int moveCountPerStage)
        {
            this.Header = new EvalParamsFileHeader(label, version, DateTime.Now);
            this.MoveCountPerStage = moveCountPerStage;
            this.StageNum = ((Board.SQUARE_NUM - 4) / moveCountPerStage) + 1;
            this.Weight = new float[2][][];
            for (var color = 0; color < this.Weight.Length; color++)
            {
                this.Weight[color] = new float[this.StageNum][];
                for (var stage = 0; stage < this.Weight[color].Length; stage++)
                    this.Weight[color][stage] = new float[BoardFeature.PatternFeatureNum.Sum()];
            }
        }
        
        public ValueFunction(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            this.Header = new EvalParamsFileHeader(fs);
            var packedWeight = LoadPackedWeight(fs);
            this.StageNum = packedWeight.Length;
            this.MoveCountPerStage = Board.SQUARE_NUM / (this.StageNum - 1);
            this.Weight = new float[2][][];
            for (var color = 0; color < this.Weight.Length; color++)
            {
                this.Weight[color] = new float[this.StageNum][];
                for (var stage = 0; stage < this.Weight[color].Length; stage++)
                    this.Weight[color][stage] = new float[BoardFeature.PatternFeatureNum.Sum()];
            }
            ExpandPackedWeight(packedWeight);
        }

        public ValueFunction(ValueFunction valueFunc)
        {
            this.Header = valueFunc.Header;
            this.StageNum = valueFunc.StageNum;
            this.MoveCountPerStage = valueFunc.MoveCountPerStage;
            this.Weight = new float[2][][];
            for(var color = 0; color < this.Weight.Length; color++)
            {
                var srcWeight = valueFunc.Weight[color];
                var destWeight = this.Weight[color] = new float[this.StageNum][];
                for(var stage = 0; stage < this.StageNum; stage++)
                {
                    var sw = srcWeight[stage];
                    var dw = destWeight[stage] = new float[BoardFeature.PatternFeatureNum.Sum()];
                    Buffer.BlockCopy(sw, 0, dw, 0, sizeof(float) * dw.Length);
                }
            }
        }

        public void InitWeightsAtRandom()
        {
            InitWeightsAtRandom(0.0f, 0.01f);
        }

        public void InitWeightsAtRandom(float mu, float sigma)
        {
            var rand = new NormalRandom(mu, sigma);
            var blackWeight = this.Weight[(int)DiscColor.Black];
            for (var stage = 0; stage < blackWeight.Length; stage++)
            {
                var blackParamsPerStage = blackWeight[stage];
                for (var feature = 0; feature < blackParamsPerStage.Length - 1; feature++)
                {
                    var symmetricFeature = TO_SYMMETRIC_FEATURE_IDX[feature];
                    if (symmetricFeature < feature)
                        blackParamsPerStage[feature] = blackParamsPerStage[symmetricFeature];
                    else
                        blackParamsPerStage[feature] = rand.NextSingle();
                }
            }
            CopyBlackWeightToWhiteWeight();
        }

        public void CopyBlackParamsToSymmetricFeatureIdx()
        {
            var blackWeight = this.Weight[(int)DiscColor.Black];
            for (var stage = 0; stage < blackWeight.Length; stage++)
            {
                var blackParamsPerStage = blackWeight[stage];
                for (var featureIdx = 0; featureIdx < blackParamsPerStage.Length; featureIdx++)
                {
                    var symmetricFeatureIdx = TO_SYMMETRIC_FEATURE_IDX[featureIdx];
                    if (symmetricFeatureIdx > featureIdx)
                        blackParamsPerStage[symmetricFeatureIdx] = blackParamsPerStage[featureIdx];
                }
            }
        }

        public void CopyBlackParamsToWhiteParams()
        {
            var blackWeight = this.Weight[(int)DiscColor.Black];
            var whiteWeight = this.Weight[(int)DiscColor.White];
            for (var stage = 0; stage < blackWeight.Length; stage++)
            {
                var blackParamsPerStage = blackWeight[stage];
                var whiteParamsPerStage = whiteWeight[stage];
                for (var feature = 0; feature < blackParamsPerStage.Length; feature++)
                    whiteParamsPerStage[TO_OPPONENT_FEATURE_IDX[feature]] = blackParamsPerStage[feature];
            }
        }

        float[][][] LoadPackedWeight(FileStream fs)
        {
            fs.Seek(EvalParamsFileHeader.HEADER_SIZE, SeekOrigin.Begin);
            var stageNum = fs.ReadByte();
            var weight = new float[stageNum][][];
            var buffer = new byte[sizeof(float)];
            for (var stage = 0; stage < weight.Length; stage++)
            {
                weight[stage] = new float[BoardFeature.PATTERN_TYPE_NUM][];
                for (var patternType = 0; patternType < weight[stage].Length; patternType++)
                {
                    weight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]];
                    for (var i = 0; i < weight[stage][patternType].Length; i++)
                    {
                        fs.Read(buffer, 0, buffer.Length);
                        weight[stage][patternType][i] = BitConverter.ToSingle(buffer);
                    }
                }
            }
            return weight;
        }

        void ExpandPackedWeight(float[][][] packedWeight)
        {
            int i;
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                int patternType;
                var offset = 0;
                for (patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM - 1; patternType++)
                {
                    for (var feature = i = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var featureIdx = feature + offset;
                        var symmetricFeatureIdx = TO_SYMMETRIC_FEATURE_IDX[featureIdx];
                        if (symmetricFeatureIdx < featureIdx)
                            this.Weight[(int)DiscColor.Black][stage][featureIdx] = this.Weight[(int)DiscColor.Black][stage][symmetricFeatureIdx];
                        else
                            this.Weight[(int)DiscColor.Black][stage][featureIdx] = packedWeight[stage][patternType][i++];
                        this.Weight[(int)DiscColor.White][stage][TO_OPPONENT_FEATURE_IDX[featureIdx]] = this.Weight[(int)DiscColor.Black][stage][featureIdx];
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }

                // bias
                this.Weight[(int)DiscColor.Black][stage][offset] = packedWeight[stage][patternType][0];
                this.Weight[(int)DiscColor.White][stage][offset] = packedWeight[stage][patternType][0];
            }
        }

        public void SaveToFile(string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            SaveToFile(fs);
        }

        public void SaveToFile(FileStream fs)
        {
            this.Header.WriteToStream(fs);
            var weight = PackWeight();
            fs.WriteByte((byte)this.StageNum);
            for (var stage = 0; stage < this.StageNum; stage++)
                for (var patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM; patternType++)
                    for (var i = 0; i < BoardFeature.PackedPatternFeatureNum[patternType]; i++)
                        fs.Write(BitConverter.GetBytes(weight[stage][patternType][i]), 0, sizeof(float));
        }

        public float F(BoardFeature board)      // calculate value 
        {
            return F((Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveCountPerStage, board);
        }

        public float F(int stage, BoardFeature board)
        {
            var value = 0.0f;
            var features = board.Features;
            var color = (int)board.SideToMove;
            var weight = this.Weight[color][stage];
            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
                value += weight[features[i] + FEATURE_IDX_OFFSET[i]];
            value = MathFunctions.StdSigmoid(value);
            return value;
        }

        public float F_ForOptimizing(BoardFeature board)
        {
            return F_ForOptimizing((Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveCountPerStage, board);
        }

        public unsafe float F_ForOptimizing(int stage, BoardFeature board)
        {
            var value = 0.0f;
            var features = board.Features;
            var weight = this.Weight[(int)DiscColor.Black][stage];
            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
            {
                var featureIdx = features[i] + FEATURE_IDX_OFFSET[i];
                var symmetricFeatureIdx = TO_SYMMETRIC_FEATURE_IDX[featureIdx];
                value += weight[(featureIdx < symmetricFeatureIdx) ? featureIdx : symmetricFeatureIdx];
            }
            value = MathFunctions.StdSigmoid(value);
            return value;
        }

        public float CalculateGradient(int stage, (BoardFeature board, float output)[] batch, float[] weightGrad)
        {
            var loss = 0.0f;
            Parallel.ForEach(batch, data =>
            {
                var y = F(stage, data.board);
                var delta = y - data.output;
                AtomicOperations.Add(ref loss, MathFunctions.BinaryCrossEntropy(y, data.output));

                var features = data.board.Features;
                for (var i = 0; i < features.Length; i++)
                {
                    var featureIdx = features[i] + FEATURE_IDX_OFFSET[i];
                    AtomicOperations.Add(ref weightGrad[featureIdx], delta);

                    var symmetricFeatureIdx = TO_SYMMETRIC_FEATURE_IDX[featureIdx];
                    if(symmetricFeatureIdx != featureIdx)
                        AtomicOperations.Add(ref weightGrad[symmetricFeatureIdx], delta);
                }
            });
            return loss / batch.Length;
        }

        public void ApplyGradientToBlackWeight(int stage, float[] weightGrad, float[] rate)
        {
            var weight = this.Weight[(int)DiscColor.Black][stage];
            Parallel.For(0, weight.Length, featureIdx => weight[featureIdx] -= rate[featureIdx] * weightGrad[featureIdx]);
        }

        public float CalculateLoss((BoardFeature board, float output)[] batch)
        {
            var threadNum = Environment.ProcessorCount;
            var lossSum = new float[threadNum];
            var batchSizePerThread = batch.Length / threadNum;

            Parallel.For(0, threadNum, threadID =>
            {
                for (var i = batchSizePerThread * threadID; i < batchSizePerThread * (threadID + 1); i++)
                {
                    var data = batch[i];
                    lossSum[threadID] += MathFunctions.BinaryCrossEntropy(F(data.board), data.output);
                }
            });
            
            for(var i = batchSizePerThread * threadNum; i < batch.Length; i++)
            {
                var data = batch[i];
                lossSum[0] += MathFunctions.BinaryCrossEntropy(F(data.board), data.output);
            }
            return lossSum.Sum() / batch.Length;
        }

        public void CopyBlackWeightToWhiteWeight()
        {
            var blackWeight = this.Weight[(int)DiscColor.Black];
            var whiteWeight = this.Weight[(int)DiscColor.White];
            for(var stage = 0; stage < this.StageNum; stage++)
            {
                var bw = blackWeight[stage];
                var ww = whiteWeight[stage];
                for (var featureIdx = 0; featureIdx < ww.Length; featureIdx++)
                    ww[TO_OPPONENT_FEATURE_IDX[featureIdx]] = bw[featureIdx];
            }
        }

        float[][][] PackWeight()
        {
            var packedWeight = new float[this.StageNum][][];
            int packedWIdx;
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                packedWeight[stage] = new float[BoardFeature.PATTERN_TYPE_NUM][];
                int patternType;
                var offset = 0;
                for (patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM - 1; patternType++)
                {
                    packedWeight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]];
                    for (var feature = packedWIdx = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var symmetricalPattern = BoardFeature.FlipFeature(patternType, feature);
                        if (feature <= symmetricalPattern)
                            packedWeight[stage][patternType][packedWIdx++] = this.Weight[(int)DiscColor.Black][stage][offset + feature];
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }

                // bias
                packedWeight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]];
                packedWeight[stage][patternType][0] = this.Weight[(int)DiscColor.Black][stage][offset];
            }
            return packedWeight;
        }
    }
}
