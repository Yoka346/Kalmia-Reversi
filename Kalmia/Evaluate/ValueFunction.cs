using System;
using System.IO;

using Kalmia.Reversi;

namespace Kalmia.Evaluate
{
    public class ValueFunction
    {
        readonly int STAGE_NUM;
        readonly int MOVE_NUM_PER_STAGE;
        readonly float[][][][] WEIGHT;      // WEIGHT[Color][Stage][FeatureID][Pattern]

        public EvalParamsFileHeader Header { get; }

        public ValueFunction(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            this.Header = new EvalParamsFileHeader(fs);
            var packedWeight = LoadPackedWeight(fs);   
            this.STAGE_NUM = packedWeight.Length;
            this.MOVE_NUM_PER_STAGE = Board.SQUARE_NUM / this.STAGE_NUM;
            this.WEIGHT = new float[2][][][];
            for (var i = 0; i < this.WEIGHT.Length; i++)
            {
                this.WEIGHT[i] = new float[STAGE_NUM][][];
                for (var j = 0; j < this.WEIGHT[i].Length; j++)
                {
                    this.WEIGHT[i][j] = new float[FeatureBoard.PatternNum.Count][];
                    for (var k = 0; k < this.WEIGHT[i][j].Length; k++)
                        this.WEIGHT[i][j][k] = new float[FeatureBoard.PatternNum[k]];
                }
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
                for (var featureID = 0; featureID < weight[stage].Length; featureID++)
                {
                    weight[stage][featureID] = new float[FeatureBoard.PackedPatternNum[featureID]];
                    for (var i = 0; i < weight[stage][featureID].Length; i++)
                    {
                        fs.Read(buffer, 0, buffer.Length);
                        weight[stage][featureID][i] = BitConverter.ToSingle(buffer);
                    }
                }
            }
            return weight;
        }

        void ExpandPackedWeight(float[][][] packedWeight)
        {
            int i;
            for (var stage = 0; stage < STAGE_NUM; stage++)
            {
                int featureID;
                for (featureID = 0; featureID < FeatureBoard.FEATURE_TYPE_NUM - 1; featureID++)
                {
                    for (var pattern = i = 0; pattern < FeatureBoard.PatternNum[featureID]; pattern++)
                    {
                        var featureSize = FeatureBoard.FeatureSize[featureID];
                        var symmetricalPattern = FeatureBoard.FlipPatternMap[featureID](pattern);
                        if (symmetricalPattern < pattern)
                            this.WEIGHT[(int)Color.Black][stage][featureID][pattern] = this.WEIGHT[(int)Color.Black][stage][featureID][symmetricalPattern];
                        else
                            this.WEIGHT[(int)Color.Black][stage][featureID][pattern] = packedWeight[stage][featureID][i++];
                        this.WEIGHT[(int)Color.White][stage][featureID][FeatureBoard.InvertPattern(pattern, featureSize)] = this.WEIGHT[(int)Color.Black][stage][featureID][pattern];
                    }
                }

                // bias
                this.WEIGHT[(int)Color.Black][stage][featureID][0] = packedWeight[stage][featureID][0];
                this.WEIGHT[(int)Color.White][stage][featureID][0] = packedWeight[stage][featureID][0];
            }
        }

        public float F(FeatureBoard board)      // calculate value 
        {
            var stage = (Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MOVE_NUM_PER_STAGE;
            var value = 0.0f;
            var features = board.Features;
            var color = (int)board.Turn;
            var i = 0;
            for (var featureID = 0; featureID < FeatureBoard.FEATURE_TYPE_NUM; featureID++)
                for (var n = 0; n < FeatureBoard.FeatureNum[featureID]; n++)
                    value += this.WEIGHT[color][stage][featureID][features[i++]];
            value = FastMath.StdSigmoid(value);
            return (color == (int)Color.Black) ? value : 1.0f - value;
        }
    }
}
