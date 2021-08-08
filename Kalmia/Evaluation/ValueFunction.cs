#define DISC_DIFF
//#define WIN_RATE

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
    public class ValueFunction
    {
        static readonly float[] FEATURE_PATTERN_OFFSET;

        public float[][][] Weight { get; }      // WEIGHT[Color][Stage][Feature pattern]

        public EvalParamsFileHeader Header { get; }
        public int StageNum { get; }
        public int MoveNumPerStage { get; }

        static ValueFunction()
        {
            FEATURE_PATTERN_OFFSET = new float[FeatureBoard.FEATURE_TYPE_NUM - 1];
            for (var i = 0; i < FEATURE_PATTERN_OFFSET.Length; i++)
                FEATURE_PATTERN_OFFSET[i] = FeatureBoard.FeaturePatternNum[i + 1];
        }

        public ValueFunction(int moveNumPerStage)
        {
            this.MoveNumPerStage = moveNumPerStage;
            this.StageNum = ((Board.SQUARE_NUM - 4) / moveNumPerStage) + 1;
            this.MoveNumPerStage = moveNumPerStage;
            this.Weight = new float[2][][];
            for (var color = 0; color < this.Weight.Length; color++)
            {
                this.Weight[color] = new float[StageNum + 1][];
                for (var stage = 0; stage < this.Weight[color].Length; stage++)
                    this.Weight[color][stage] = new float[FeatureBoard.FeaturePatternNum.Sum()];
            }
        }

        public ValueFunction(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            this.Header = new EvalParamsFileHeader(fs);
            var packedWeight = LoadPackedWeight(fs);
            this.StageNum = packedWeight.Length;
            this.MoveNumPerStage = Board.SQUARE_NUM / (this.StageNum - 1);
            this.Weight = new float[2][][];
            for (var color = 0; color < this.Weight.Length; color++)
            {
                this.Weight[color] = new float[this.StageNum][];
                for (var stage = 0; stage < this.Weight[color].Length; stage++)
                    this.Weight[color][stage] = new float[FeatureBoard.FeaturePatternNum.Sum()];
            }
            ExpandPackedWeight(packedWeight);
        }

        float[][][] LoadPackedWeight(FileStream fs)
        {
            fs.Seek(EvalParamsFileHeader.HEADER_SIZE, SeekOrigin.Begin);
            var stageNum = fs.ReadByte();
            var weight = new float[stageNum][][];
            var buffer = new byte[sizeof(float)];
            for (var stage = 0; stage < stageNum; stage++)
            {
                weight[stage] = new float[FeatureBoard.FEATURE_TYPE_NUM][];
                for (var featureType = 0; featureType < weight[stage].Length; featureType++)
                {
                    weight[stage][featureType] = new float[FeatureBoard.PackedFeaturePatternNum[featureType]];
                    for (var i = 0; i < weight[stage][featureType].Length; i++)
                    {
                        fs.Read(buffer, 0, buffer.Length);
                        weight[stage][featureType][i] = BitConverter.ToSingle(buffer);
                    }
                }
            }
            return weight;
        }

        void ExpandPackedWeight(float[][][] packedWeight)
        {
            int i;
            for (var stage = 0; stage < StageNum; stage++)
            {
                int featureType;
                var offset = 0;
                for (featureType = 0; featureType < FeatureBoard.FEATURE_TYPE_NUM - 1; featureType++)
                {
                    for (var pattern = i = 0; pattern < FeatureBoard.FeaturePatternNum[featureType]; pattern++)
                    {
                        var featureSize = FeatureBoard.FeatureSize[featureType];
                        var symmetricalPattern = FeatureBoard.FlipPatternCallbacks[featureType](pattern);
                        if (symmetricalPattern < pattern)
                            this.Weight[(int)Color.Black][stage][offset + pattern] = this.Weight[(int)Color.Black][stage][offset + symmetricalPattern];
                        else
                            this.Weight[(int)Color.Black][stage][offset + pattern] = packedWeight[stage][featureType][i++];
                        this.Weight[(int)Color.White][stage][offset + FeatureBoard.InvertPattern(pattern, featureSize)] = this.Weight[(int)Color.Black][stage][offset + pattern];
                    }
                    offset += FeatureBoard.FeaturePatternNum[featureType];
                }

                // bias
                this.Weight[(int)Color.Black][stage][offset] = packedWeight[stage][featureType][0];
                this.Weight[(int)Color.White][stage][offset] = packedWeight[stage][featureType][0];
            }
        }

        public void SaveToFile(string label, int version, string path)
        {
            using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
            var header = new EvalParamsFileHeader(label, version, DateTime.Now);
            header.WriteToStream(fs);

            var weight = PackWeight();
            fs.WriteByte((byte)this.StageNum);
            for (var stage = 0; stage < this.StageNum; stage++)
                for (var featureType = 0; featureType < FeatureBoard.FEATURE_TYPE_NUM; featureType++)
                    for (var i = 0; i < FeatureBoard.PackedFeaturePatternNum[featureType]; i++)
                        fs.Write(BitConverter.GetBytes(weight[stage][featureType][i]), 0, sizeof(float));
        }

        public float F(FeatureBoard board)      // calculate value 
        {
            var stage = (Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveNumPerStage;
            var value = 0.0f;
            var patterns = board.FeaturePatterns;
            var color = (int)board.Turn;
            for (var i = 0; i < FeatureBoard.ALL_FEATURE_NUM; i++)
                value += this.Weight[color][stage][patterns[i]];

#if WIN_RATE
            value = FastMath.StdSigmoid(value);
            return (color == (int)Color.Black) ? value : 1.0f - value;
#endif

#if DISC_DIFF
            const int SCORE_MIN = -64;
            const int SCORE_MAX = 64;
            value /= 128;

            if (value <= SCORE_MIN) 
                value = SCORE_MIN + 1;
            else if (value >= SCORE_MAX) 
                value = SCORE_MAX - 1;
#endif
            return value;
        }

        public float Loss(FeatureBoard[] trainInput, float[] trainOutput)
        {
            var loss = 0.0f;
            for (var i = 0; i < trainOutput.Length; i++)
                loss += FastMath.BinaryCrossEntropy(F(trainInput[i]), trainOutput[i]);
            return loss;
        }

        float[][][] PackWeight()
        {
            var packedWeight = new float[this.StageNum][][];
            int packedWIdx;
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                packedWeight[stage] = new float[FeatureBoard.FEATURE_TYPE_NUM][];
                int featureType;
                var offset = 0;
                for (featureType = 0; featureType < FeatureBoard.FEATURE_TYPE_NUM - 1; featureType++)
                {
                    packedWeight[stage][featureType] = new float[FeatureBoard.PackedFeaturePatternNum[featureType]];
                    for (var pattern = packedWIdx = 0; pattern < FeatureBoard.FeaturePatternNum[featureType]; pattern++)
                    {
                        var symmetricalPattern = FeatureBoard.FlipPatternCallbacks[featureType](pattern);
                        if (pattern <= symmetricalPattern)
                            packedWeight[stage][featureType][packedWIdx++] = this.Weight[(int)Color.Black][stage][offset + pattern];
                    }
                    offset += FeatureBoard.FeaturePatternNum[featureType];
                }

                // bias
                packedWeight[stage][featureType] = new float[FeatureBoard.PackedFeaturePatternNum[featureType]];
                packedWeight[stage][featureType][0] = this.Weight[(int)Color.Black][stage][offset];
            }
            return packedWeight;
        }
    }
}
