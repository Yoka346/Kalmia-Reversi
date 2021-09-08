//#define DISC_DIFF
#define WIN_RATE

using System;
using System.IO;

using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
    public class ValueFunction
    {
        public static int BiasIdx { get; } = BoardFeature.PatternIdxOffset[^1];

        public float[][][] Weight { get; }      // WEIGHT[Color][Stage][Feature]

        public EvalParamsFileHeader Header { get; private set; }
        public int StageNum { get; private set; }
        public int MoveCountPerStage { get; private set; }

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

        float[][][] LoadPackedWeight(FileStream fs)
        {
            fs.Seek(EvalParamsFileHeader.HEADER_SIZE, SeekOrigin.Begin);
            var stageNum = fs.ReadByte();
            var weight = new float[stageNum][][];
            var buffer = new byte[sizeof(float)];
            for (var stage = 0; stage < stageNum; stage++)
            {
                weight[stage] = new float[BoardFeature.PATTERN_TYPE][];
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
                for (patternType = 0; patternType < BoardFeature.PATTERN_TYPE - 1; patternType++)
                {
                    for (var feature = i = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var featureIdx = offset + feature;
                        var symmetricFeatureIdx = BoardFeature.SymmetricFeatureIdxMapping[featureIdx];
                        if (symmetricFeatureIdx < featureIdx)
                            this.Weight[(int)Color.Black][stage][featureIdx] = this.Weight[(int)Color.Black][stage][symmetricFeatureIdx];
                        else
                            this.Weight[(int)Color.Black][stage][featureIdx] = packedWeight[stage][patternType][i++];
                        this.Weight[(int)Color.White][stage][BoardFeature.OpponentFeatureIdxMapping[featureIdx]] = this.Weight[(int)Color.Black][stage][featureIdx];
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }

                // bias
                this.Weight[(int)Color.Black][stage][offset] = packedWeight[stage][patternType][0];
                this.Weight[(int)Color.White][stage][offset] = packedWeight[stage][patternType][0];
            }
        }

        public void SaveToFile(string label, int version, string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            SaveToFile(label, version, fs);
        }

        public void SaveToFile(string label, int version, FileStream fs)
        {
            var header = new EvalParamsFileHeader(label, version, DateTime.Now);
            header.WriteToStream(fs);
            var weight = PackWeight();
            fs.WriteByte((byte)this.StageNum);
            for (var stage = 0; stage < this.StageNum; stage++)
                for (var patternType = 0; patternType < BoardFeature.PATTERN_TYPE; patternType++)
                    for (var i = 0; i < BoardFeature.PackedPatternFeatureNum[patternType]; i++)
                        fs.Write(BitConverter.GetBytes(weight[stage][patternType][i]), 0, sizeof(float));
        }

        public float F(BoardFeature board)      // calculate value 
        {
            var stage = (Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveCountPerStage;
            var value = 0.0f;
            var featureIndices = board.FeatureIndices;
            var color = (int)board.SideToMove;
            var weight = this.Weight[color][stage];
            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
                value += weight[featureIndices[i]];
#if WIN_RATE
            value = FastMath.StdSigmoid(value);
            return value;
#endif

#if DISC_DIFF
            const int SCORE_MIN = -64;
            const int SCORE_MAX = 64;
            value /= 128;

            if (value <= SCORE_MIN) 
                value = SCORE_MIN + 1;
            else if (value >= SCORE_MAX) 
                value = SCORE_MAX - 1;
            return value;
#endif
        }

        public void CopyBlackWeightToWhiteWeight()
        {
            var blackWeight = this.Weight[(int)Color.Black];
            var whiteWeight = this.Weight[(int)Color.White];
            for(var stage = 0; stage < this.StageNum; stage++)
            {
                var bw = blackWeight[stage];
                var ww = whiteWeight[stage];
                for (var featureIdx = 0; featureIdx < ww.Length; featureIdx++)
                    ww[BoardFeature.OpponentFeatureIdxMapping[featureIdx]] = bw[featureIdx];
            }
        }

        float[][][] PackWeight()
        {
            var packedWeight = new float[this.StageNum][][];
            int packedWIdx;
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                packedWeight[stage] = new float[BoardFeature.PATTERN_TYPE][];
                int patternType;
                var offset = 0;
                for (patternType = 0; patternType < BoardFeature.PATTERN_TYPE - 1; patternType++)
                {
                    packedWeight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]];
                    for (var feature = packedWIdx = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var symmetricalPattern = BoardFeature.FlipFeatureCallbacks[patternType](feature);
                        if (feature <= symmetricalPattern)
                            packedWeight[stage][patternType][packedWIdx++] = this.Weight[(int)Color.Black][stage][offset + feature];
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }

                // bias
                packedWeight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]];
                packedWeight[stage][patternType][0] = this.Weight[(int)Color.Black][stage][offset];
            }
            return packedWeight;
        }
    }
}
