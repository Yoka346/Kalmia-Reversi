using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using Kalmia.Reversi;
using System.Diagnostics.CodeAnalysis;

namespace Kalmia.Evaluation
{
    public struct ValueFuncParam
    {
        public int FeatureID { get; set; }
        public float Weight { get; set; }
        public Vector256<float> Vector { get; set; }

        public static bool operator ==(ValueFuncParam left, ValueFuncParam right)
        {
            return left.FeatureID == right.FeatureID && left.Weight == right.Weight && left.Vector.Equals(right.Vector);
        }

        public static bool operator!=(ValueFuncParam left, ValueFuncParam right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return string.Format("ValueFuncParam {0} FeatureID = {1}, Weight = {2}, Vector = {3} {4}", "{", this.FeatureID, this.Weight, this.Vector, "}");
        }
    }

    public class LatentFactorValueFunction
    {
        const int INTERACTION_VEC_LEN = 8;

        static readonly int[] FEATURE_IDX_OFFSET;
        static readonly int[] TO_OPPONENT_FEATURE_IDX;
        static readonly int[] TO_SYMMETRIC_FEATURE_IDX;
        public static readonly int BIAS_IDX;

        public static ReadOnlySpan<int> FeatureIdxOffset { get { return FEATURE_IDX_OFFSET; } }
        public static ReadOnlySpan<int> ToOpponentFeatureIdx { get { return TO_OPPONENT_FEATURE_IDX; } }
        public static ReadOnlySpan<int> ToSymmetricFeatureIdx { get { return TO_SYMMETRIC_FEATURE_IDX; } }

        public ValueFuncParam[][][] Params { get; }      // Weight[Color][Stage][Feature]

        public EvalParamsFileHeader Header { get; private set; }
        public int StageNum { get; private set; }
        public int MoveCountPerStage { get; private set; }

        static LatentFactorValueFunction()
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

            TO_OPPONENT_FEATURE_IDX = new int[BoardFeature.PatternFeatureNumSum];
            TO_SYMMETRIC_FEATURE_IDX = new int[BoardFeature.PatternFeatureNumSum];
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

        public LatentFactorValueFunction(string label, int version, int moveCountPerStage)
        {
            this.Header = new EvalParamsFileHeader(label, version, DateTime.Now);
            this.MoveCountPerStage = moveCountPerStage;
            this.StageNum = ((Board.SQUARE_NUM - 4) / moveCountPerStage) + 1;
            this.Params = new ValueFuncParam[2][][];

            var blackParams = this.Params[(int)DiscColor.Black] = new ValueFuncParam[this.StageNum][];
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                var paramsPerStage = blackParams[stage] = new ValueFuncParam[BoardFeature.PatternFeatureNumSum];
                for (var featureIdx = 0; featureIdx < paramsPerStage.Length; featureIdx++)
                {
                    var symmetricFeatureIdx = TO_SYMMETRIC_FEATURE_IDX[featureIdx];
                    paramsPerStage[featureIdx].FeatureID = (featureIdx < symmetricFeatureIdx) ? featureIdx : symmetricFeatureIdx;
                }
            }

            var whiteParams = this.Params[(int)DiscColor.White] = new ValueFuncParam[this.StageNum][];
            for (var stage = 0; stage < this.StageNum; stage++)
                whiteParams[stage] = new ValueFuncParam[BoardFeature.PatternFeatureNumSum];
            CopyBlackParamsToWhiteParams();
        }

        public LatentFactorValueFunction(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            this.Header = new EvalParamsFileHeader(fs);
            var packedParams = LoadPackedParams(fs);
            this.StageNum = packedParams.Length;
            this.MoveCountPerStage = Board.SQUARE_NUM / (this.StageNum - 1);
            this.Params = new ValueFuncParam[2][][];
            for (var color = 0; color < 2; color++)
            {
                this.Params[color] = new ValueFuncParam[this.StageNum][];
                for (var stage = 0; stage < this.StageNum; stage++)
                    this.Params[color][stage] = new ValueFuncParam[BoardFeature.PatternFeatureNumSum];
            }
            ExpandPackedParams(packedParams);
        }

        public LatentFactorValueFunction(LatentFactorValueFunction valueFunc)
        {
            this.Header = valueFunc.Header;
            this.StageNum = valueFunc.StageNum;
            this.MoveCountPerStage = valueFunc.MoveCountPerStage;
            this.Params = new ValueFuncParam[2][][];
            for (var color = 0; color < 2; color++)
            {
                var srcParams = valueFunc.Params[color];
                var destParams = this.Params[color] = new ValueFuncParam[this.StageNum][];
                for (var stage = 0; stage < this.StageNum; stage++)
                {
                    var sp = srcParams[stage];
                    var dp = destParams[stage] = new ValueFuncParam[BoardFeature.PatternFeatureNumSum];
                    Array.Copy(sp, 0, dp, 0, dp.Length);
                }
            }
        }

        public void InitVectorsAtRandom()
        {
            InitVectorsAtRandom(0.0f, 0.01f);
        }

        public unsafe void InitVectorsAtRandom(float mu, float sigma)
        {
            var rand = new NormalRandom(mu, sigma);
            var buffer = stackalloc float[INTERACTION_VEC_LEN];
            var blackParams = this.Params[(int)DiscColor.Black];
            for (var stage = 0; stage < blackParams.Length; stage++)
            {
                var blackParamsPerStage = blackParams[stage];
                for (var feature = 0; feature < blackParamsPerStage.Length - 1; feature++)
                {
                    var symmetricFeature = TO_SYMMETRIC_FEATURE_IDX[feature];
                    if (symmetricFeature < feature)
                        blackParamsPerStage[feature] = blackParamsPerStage[symmetricFeature];
                    else
                    {
                        for (var i = 0; i < INTERACTION_VEC_LEN; i++)
                            buffer[i] = rand.NextSingle();
                        blackParamsPerStage[feature].Vector = Avx.LoadVector256(buffer);
                    }
                }
            }
            CopyBlackParamsToWhiteParams();
        }

        public void CopyBlackParamsToWhiteParams()
        {
            var blackParams = this.Params[(int)DiscColor.Black];
            var whiteParams = this.Params[(int)DiscColor.White];
            for (var stage = 0; stage < blackParams.Length; stage++)
            {
                var blackParamsPerStage = blackParams[stage];
                var whiteParamsPerStage = whiteParams[stage];
                for (var feature = 0; feature < blackParamsPerStage.Length; feature++)
                    whiteParamsPerStage[TO_OPPONENT_FEATURE_IDX[feature]] = blackParamsPerStage[feature];
            }
        }

        public float F(BoardFeature board)
        {
            return F((Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveCountPerStage, board);
        }

        // ToDo: Finish implementing of F method.
        public unsafe float F(int stage, BoardFeature board)
        {
            var value = 0.0f;
            var features = board.Features;
            var color = (int)board.SideToMove;

            fixed (ValueFuncParam* parameters = this.Params[color][stage])
            {
                var requiredParams = stackalloc ValueFuncParam*[BoardFeature.PATTERN_NUM_SUM];
                for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
                {
                    requiredParams[i] = &parameters[features[i] + FEATURE_IDX_OFFSET[i]];
                    value += (*requiredParams[i]).Weight;
                }

                for(var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
                    for(var j = i + 1; j < BoardFeature.PATTERN_NUM_SUM; j++)
                    {
                        Avx.Multiply()
                    }
            }
            value = MathFunctions.StdSigmoid(value);
            return value;
        }

        public void SaveToFile(string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            SaveToFile(fs);
        }

        public unsafe void SaveToFile(FileStream fs)
        {
            const int VEC_BUFFER_SIZE = sizeof(float) * INTERACTION_VEC_LEN;

            this.Header.WriteToStream(fs);
            var packedParams = PackParams();
            fs.WriteByte((byte)this.StageNum);
            var vecBuffer = stackalloc byte[VEC_BUFFER_SIZE];
            var vecBufferSpan = new Span<byte>(vecBuffer, VEC_BUFFER_SIZE);

            for (var stage = 0; stage < this.StageNum; stage++)
                for (var patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM; patternType++) 
                {
                    var packedP = packedParams[stage][patternType];
                    for (var i = 0; i < BoardFeature.PackedPatternFeatureNum[patternType]; i++)
                    {
                        fs.Write(BitConverter.GetBytes(packedP[i].Weight), 0, sizeof(float));
                        Avx.Store(vecBuffer, packedP[i].Vector.AsByte());
                        fs.Write(vecBufferSpan);
                    }
                }
        }

        unsafe ValueFuncParam[][][] LoadPackedParams(FileStream fs)
        {
            const int BUFFER_SIZE = sizeof(float) * (INTERACTION_VEC_LEN + 1);

            fs.Seek(EvalParamsFileHeader.HEADER_SIZE, SeekOrigin.Begin);
            var stageNum = fs.ReadByte();
            var packedParams = new ValueFuncParam[stageNum][][];
            var buffer = stackalloc byte[BUFFER_SIZE];
            var bufferSpan = new Span<byte>(buffer, BUFFER_SIZE);

            for (var stage = 0; stage < packedParams.Length; stage++)
            {
                packedParams[stage] = new ValueFuncParam[BoardFeature.PATTERN_TYPE_NUM][];
                for (var patternType = 0; patternType < packedParams[stage].Length; patternType++)
                {
                    packedParams[stage][patternType] = new ValueFuncParam[BoardFeature.PackedPatternFeatureNum[patternType]];
                    for (var i = 0; i < packedParams[stage][patternType].Length; i++)
                    {
                        fs.Read(bufferSpan);
                        packedParams[stage][patternType][i].Weight = BitConverter.ToSingle(bufferSpan);
                        packedParams[stage][patternType][i].Vector = Avx.LoadVector256(buffer + sizeof(float)).AsSingle();
                    }
                }
            }
            return packedParams;
        }

        void ExpandPackedParams(ValueFuncParam[][][] packedParams)
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
                            this.Params[(int)DiscColor.Black][stage][featureIdx] = this.Params[(int)DiscColor.Black][stage][symmetricFeatureIdx];
                        else
                        {
                            this.Params[(int)DiscColor.Black][stage][featureIdx] = packedParams[stage][patternType][i++];
                            this.Params[(int)DiscColor.Black][stage][featureIdx].FeatureID = featureIdx;
                        }
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }

                // bias
                this.Params[(int)DiscColor.Black][stage][offset] = packedParams[stage][patternType][0];
                this.Params[(int)DiscColor.Black][stage][offset].FeatureID = offset;
            }
            CopyBlackParamsToWhiteParams();
        }

        ValueFuncParam[][][] PackParams()
        {
            var packedParams = new ValueFuncParam[this.StageNum][][];
            int packedWIdx;
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                packedParams[stage] = new ValueFuncParam[BoardFeature.PATTERN_TYPE_NUM][];
                int patternType;
                var offset = 0;
                for (patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM - 1; patternType++)
                {
                    packedParams[stage][patternType] = new ValueFuncParam[BoardFeature.PackedPatternFeatureNum[patternType]];
                    for (var feature = packedWIdx = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var symmetricalPattern = BoardFeature.FlipFeature(patternType, feature);
                        if (feature <= symmetricalPattern)
                            packedParams[stage][patternType][packedWIdx++] = this.Params[(int)DiscColor.Black][stage][offset + feature];
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }

                // bias
                packedParams[stage][patternType] = new ValueFuncParam[BoardFeature.PackedPatternFeatureNum[patternType]];
                packedParams[stage][patternType][0] = this.Params[(int)DiscColor.Black][stage][offset];
            }
            return packedParams;
        }

        static float Dot(ref Vector256<float> left, ref Vector256<float> right)
        {
            var product = Avx.Multiply(left, right);
            var productHi = Avx.ExtractVector128(product, 1);
            var productLow = Avx.ExtractVector128(product, 0);
            var quadSum = Sse.Add(productLow, productHi);
            var dualSumLow = quadSum;
            var dualSumHi = Sse.MoveHighToLow(quadSum, quadSum);
            var dualSum = Sse.Add(dualSumLow, dualSumHi);
            var sumLow = dualSum;
            var sumHi = Sse.Shuffle(dualSum, dualSum, 0x1);
            return Sse.Add(sumLow, sumHi).GetElement(0);
        }
    }
}
