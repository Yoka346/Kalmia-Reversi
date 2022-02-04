using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
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

        public float[][][] Weight { get; }      // Weight[Color][Stage][Feature]
        public Vector256<float>[][][] InteractionVector { get; }    // InteractionVector[Color][Stage][Feature]

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

        public LatentFactorValueFunction(string label, int version, int moveCountPerStage)
        {
            this.Header = new EvalParamsFileHeader(label, version, DateTime.Now);
            this.MoveCountPerStage = moveCountPerStage;
            this.StageNum = ((Board.SQUARE_NUM - 4) / moveCountPerStage) + 1;
            this.Weight = new float[2][][];
            this.InteractionVector = new Vector256<float>[2][][];
            for (var color = 0; color < 2; color++)
            {
                this.Weight[color] = new float[this.StageNum][];
                this.InteractionVector[color] = new Vector256<float>[this.StageNum][];
                for (var stage = 0; stage < this.StageNum; stage++)
                {
                    var len = BoardFeature.PatternFeatureNum.Sum();
                    this.Weight[color][stage] = new float[len];
                    this.InteractionVector[color][stage] = new Vector256<float>[len - 1];
                }
            }
        }

        public LatentFactorValueFunction(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            this.Header = new EvalParamsFileHeader(fs);
            var packedParams = LoadPackedParams(fs);
            this.StageNum = packedParams.weight.Length;
            this.MoveCountPerStage = Board.SQUARE_NUM / (this.StageNum - 1);
            this.Weight = new float[2][][];
            this.InteractionVector = new Vector256<float>[2][][];
            for (var color = 0; color < 2; color++)
            {
                this.Weight[color] = new float[this.StageNum][];
                this.InteractionVector[color] = new Vector256<float>[2][];
                for (var stage = 0; stage < this.StageNum; stage++)
                {
                    var len = BoardFeature.PatternFeatureNum.Sum();
                    this.Weight[color][stage] = new float[len];
                    this.InteractionVector[color][stage] = new Vector256<float>[len - 1];
                }
            }
            ExpandPackedWeight(packedParams.weight);
            ExpandPackedInteractionVector(packedParams.interactionVec);
        }

        public LatentFactorValueFunction(LatentFactorValueFunction valueFunc)
        {
            this.Header = valueFunc.Header;
            this.StageNum = valueFunc.StageNum;
            this.MoveCountPerStage = valueFunc.MoveCountPerStage;
            this.Weight = new float[2][][];
            this.InteractionVector = new Vector256<float>[2][][];
            for (var color = 0; color < 2; color++)
            {
                var srcWeight = valueFunc.Weight[color];
                var destWeight = this.Weight[color] = new float[this.StageNum][];
                var srcVec = valueFunc.InteractionVector[color];
                var destVec = this.InteractionVector[color] = new Vector256<float>[this.StageNum][];
                for (var stage = 0; stage < this.StageNum; stage++)
                {
                    var sw = srcWeight[stage];
                    var dw = destWeight[stage] = new float[BoardFeature.PatternFeatureNumSum];
                    Buffer.BlockCopy(sw, 0, dw, 0, sizeof(float) * dw.Length);

                    var sv = srcVec[stage];
                    var dv = destVec[stage] = new Vector256<float>[BoardFeature.PatternFeatureNumSum - 1];
                    Array.Copy(sv, 0, dv, 0, dv.Length);
                }
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
            
        }

        unsafe (float[][][] weight, Vector256<float>[][][] interactionVec) LoadPackedParams(FileStream fs)
        {
            const int INTERACTION_VEC_BUFFER_SIZE = sizeof(float) * INTERACTION_VEC_LEN;

            fs.Seek(EvalParamsFileHeader.HEADER_SIZE, SeekOrigin.Begin);
            var stageNum = fs.ReadByte();
            var weight = new float[stageNum][][];
            var interactionVec = new Vector256<float>[stageNum][][];
            var weightBuffer = new byte[sizeof(float)];
            var interactionVecBuffer = stackalloc byte[INTERACTION_VEC_BUFFER_SIZE];
            var interactionVecBufferSpan = new Span<byte>(interactionVecBuffer, INTERACTION_VEC_BUFFER_SIZE);

            for (var stage = 0; stage < weight.Length; stage++)
            {
                weight[stage] = new float[BoardFeature.PATTERN_TYPE_NUM][];
                for (var patternType = 0; patternType < weight[stage].Length; patternType++)
                {
                    weight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]];
                    for (var i = 0; i < weight[stage][patternType].Length; i++)
                    {
                        fs.Read(weightBuffer, 0, weightBuffer.Length);
                        weight[stage][patternType][i] = BitConverter.ToSingle(weightBuffer);
                    }
                }

                interactionVec[stage] = new Vector256<float>[BoardFeature.PATTERN_TYPE_NUM - 1][];
                for (var patternType = 0; patternType < interactionVec[stage].Length; patternType++)
                {
                    interactionVec[stage][patternType] = new Vector256<float>[BoardFeature.PackedPatternFeatureNum[patternType]];
                    for (var i = 0; i < interactionVec[stage][patternType].Length; i++)
                    {
                        fs.Read(interactionVecBufferSpan);
                        interactionVec[stage][patternType][i] = Avx.LoadVector256(interactionVecBuffer).AsSingle();
                    }
                }
            }
            return (weight, interactionVec);
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

        void ExpandPackedInteractionVector(Vector256<float>[][][] packedInteractionVec)
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
                            this.InteractionVector[(int)DiscColor.Black][stage][featureIdx] = this.InteractionVector[(int)DiscColor.Black][stage][symmetricFeatureIdx];
                        else
                            this.InteractionVector[(int)DiscColor.Black][stage][featureIdx] = packedInteractionVec[stage][patternType][i++];
                        this.InteractionVector[(int)DiscColor.White][stage][TO_OPPONENT_FEATURE_IDX[featureIdx]] = this.InteractionVector[(int)DiscColor.Black][stage][featureIdx];
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }
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

        Vector256<float>[][][] PackInteractionVector()
        {
            var packedInteractionVec = new Vector256<float>[this.StageNum][][];
            int packedWIdx;
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                packedInteractionVec[stage] = new Vector256<float>[BoardFeature.PATTERN_TYPE_NUM][];
                int patternType;
                var offset = 0;
                for (patternType = 0; patternType < BoardFeature.PATTERN_TYPE_NUM - 1; patternType++)
                {
                    packedInteractionVec[stage][patternType] = new Vector256<float>[BoardFeature.PackedPatternFeatureNum[patternType]];
                    for (var feature = packedWIdx = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var symmetricalPattern = BoardFeature.FlipFeature(patternType, feature);
                        if (feature <= symmetricalPattern)
                            packedInteractionVec[stage][patternType][packedWIdx++] = this.InteractionVector[(int)DiscColor.Black][stage][offset + feature];
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }
            }
            return packedInteractionVec;
        }
    }
}
