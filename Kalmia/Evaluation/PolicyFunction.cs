using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;

using Kalmia.Reversi;

namespace Kalmia.Evaluation
{
    public class PolicyFunction
    {
        public float[][][][] Weight { get; }    // Weight[Color][Stage][Feature][Position]
        public EvalParamsFileHeader Header { get; private set; }
        public int StageNum { get; private set; }
        public int MoveCountPerStage { get; private set; }

        public PolicyFunction(string label, int version, int moveCountPerStage)
        {
            this.Header = new EvalParamsFileHeader(label, version, DateTime.Now);
            this.MoveCountPerStage = moveCountPerStage;
            this.StageNum = (Board.SQUARE_NUM - 4) / moveCountPerStage;
            this.Weight = new float[2][][][];
            for(var color = 0; color < this.Weight.Length; color++)
            {
                this.Weight[color] = new float[this.StageNum][][];
                for(var stage = 0; stage < this.Weight[color].Length; stage++)
                {
                    this.Weight[color][stage] = new float[BoardFeature.PatternFeatureNum.Sum()][];
                    for (var i = 0; i < this.Weight[color][stage].Length; i++)
                        this.Weight[color][stage][i] = new float[Board.SQUARE_NUM];
                }
            }
        }

        public PolicyFunction(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            this.Header = new EvalParamsFileHeader(fs);
            var packedWeight = LoadPackedWeight(fs);
            this.StageNum = packedWeight.Length;
            this.MoveCountPerStage = (Board.SQUARE_NUM - 4) / this.StageNum;
            this.Weight = new float[2][][][];
            for (var color = 0; color < this.Weight.Length; color++)
            {
                this.Weight[color] = new float[this.StageNum][][];
                for (var stage = 0; stage < this.Weight[color].Length; stage++)
                {
                    this.Weight[color][stage] = new float[BoardFeature.PatternFeatureNum.Sum()][];
                    for (var i = 0; i < this.Weight[color][stage].Length; i++)
                        this.Weight[color][stage][i] = new float[Board.SQUARE_NUM];
                }
            }
            ExpandPackedWeight(packedWeight);
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
                    this.Weight[color][stage] = new float[BoardFeature.PatternFeatureNum.Sum()][];
                    for(var featureIdx = 0; featureIdx < this.Weight[color][stage].Length; featureIdx++)
                    {
                        var dest = this.Weight[color][stage][featureIdx] = new float[Board.SQUARE_NUM];
                        Buffer.BlockCopy(policy.Weight[color][stage][featureIdx], 0, dest, 0, sizeof(float) * dest.Length);
                    }
                }
            }
        }

        float[][][][] LoadPackedWeight(FileStream fs)
        {
            fs.Seek(EvalParamsFileHeader.HEADER_SIZE, SeekOrigin.Begin);
            var stageNum = fs.ReadByte();
            var weight = new float[stageNum][][][];
            var buffer = new byte[sizeof(float) * Board.SQUARE_NUM];
            for(var stage = 0; stage < stageNum; stage++)
            {
                weight[stage] = new float[BoardFeature.PATTERN_TYPE][][];
                for(var patternType = 0; patternType < weight[stage].Length; patternType++)
                {
                    weight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]][];
                    for (var i = 0; i < weight[stage][patternType].Length; i++) 
                    {
                        weight[stage][patternType][i] = new float[Board.SQUARE_NUM];
                        fs.Read(buffer, 0, buffer.Length);
                        for (var pos = 0; pos < Board.SQUARE_NUM; pos++)
                            weight[stage][patternType][i][pos] = BitConverter.ToSingle(buffer, sizeof(float) * pos);
                    }
                }
            }
            return weight;
        }

        void ExpandPackedWeight(float[][][][] packedWeight)
        {
            int i;
            for(var stage = 0; stage < this.StageNum; stage++)
            {
                int patternType;
                var offset = 0;
                for(patternType = 0; patternType < BoardFeature.PATTERN_TYPE - 1; patternType++)
                {
                    for(var feature = i = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var patternSize = BoardFeature.PatternSize[patternType];
                        var symmetricalFeature = BoardFeature.FlipFeatureCallbacks[patternType](feature);
                        if (symmetricalFeature < feature)
                            Buffer.BlockCopy(this.Weight[(int)Color.Black][stage][offset + symmetricalFeature], 0, this.Weight[(int)Color.Black][stage][offset + feature], 0, sizeof(float) * Board.SQUARE_NUM);
                        else
                            Buffer.BlockCopy(packedWeight[stage][patternType][i++], 0, this.Weight[(int)Color.Black][stage][offset + feature], 0, sizeof(float) * Board.SQUARE_NUM);
                        Buffer.BlockCopy(this.Weight[(int)Color.Black][stage][offset + feature], 0, this.Weight[(int)Color.White][stage][offset + BoardFeature.CalcOpponentFeature(feature, patternSize)], 0, sizeof(float) * Board.SQUARE_NUM);
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }

                // bias
                Buffer.BlockCopy(packedWeight[stage][patternType][0], 0, this.Weight[(int)Color.Black][stage][offset], 0, sizeof(float) * Board.SQUARE_NUM);
                Buffer.BlockCopy(packedWeight[stage][patternType][0], 0, this.Weight[(int)Color.White][stage][offset], 0, sizeof(float) * Board.SQUARE_NUM);
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
                        for(var pos = 0; pos < Board.SQUARE_NUM; pos++) 
                            fs.Write(BitConverter.GetBytes(weight[stage][patternType][i][pos]), 0, sizeof(float));
        }

        public void F(BoardFeature board, float[] moveDist)
        {
            F(board, moveDist.AsSpan());
        }

        public void F(BoardFeature board, Span<float> moveDist)
        {
            var stage = (Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveCountPerStage;
            var color = (int)board.SideToMove;
            var featureIndices = board.FeatureIndices;
            var weight = this.Weight[color][stage];
            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
            {
                var w = weight[featureIndices[i]];
                for (var pos = 0; pos < w.Length; pos++)
                    moveDist[pos] += w[pos];
            }
            FastMath.Softmax(moveDist, moveDist);
        }

        public void F(BoardFeature board, Move[] moves, float[] moveDist, int moveCount)
        {
            F(board, moves, moveDist.AsSpan(), moveCount);
        }

        public void F(BoardFeature board, Move[] moves, Span<float> moveDist, int moveCount)
        {
            var stage = (Board.SQUARE_NUM - 4 - board.EmptyCount) / this.MoveCountPerStage;
            var color = (int)board.SideToMove;
            var featureIndices = board.FeatureIndices;
            var weight = this.Weight[color][stage];

            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
            {
                var w = weight[featureIndices[i]];
                for (var j = 0; j < moveCount; j++)
                {
                    var pos = (int)moves[j].Pos;
                    moveDist[j] += w[pos];
                }
            }

            var sum = 0.0f;
            for (var i = 0; i < moveCount; i++)
                sum += moveDist[i] = FastMath.Exp(moveDist[i]);
            for (var i = 0; i < moveCount; i++)
                moveDist[i] /= sum;
        }

        public void CopyBlackWeightToWhiteWeight()
        {
            var blackWeight = this.Weight[(int)Color.Black];
            var whiteWeight = this.Weight[(int)Color.White];
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                var bw = blackWeight[stage];
                var ww = whiteWeight[stage];
                for (var featureIdx = 0; featureIdx < ww.Length; featureIdx++)
                {
                    var tmp = ww[BoardFeature.OpponentFeatureIdxMapping[featureIdx]];
                    Buffer.BlockCopy(bw[featureIdx], 0, tmp, 0, sizeof(float) * tmp.Length);
                }
            }
        }

        float[][][][] PackWeight()
        {
            var packedWeight = new float[this.StageNum][][][];
            int packedWIdx;
            for (var stage = 0; stage < this.StageNum; stage++)
            {
                packedWeight[stage] = new float[BoardFeature.PATTERN_TYPE][][];
                int patternType;
                var offset = 0;
                for (patternType = 0; patternType < BoardFeature.PATTERN_TYPE - 1; patternType++)
                {
                    packedWeight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]][];
                    for (var feature = packedWIdx = 0; feature < BoardFeature.PatternFeatureNum[patternType]; feature++)
                    {
                        var symmetricalPattern = BoardFeature.FlipFeatureCallbacks[patternType](feature);
                        if (feature <= symmetricalPattern)
                        {
                            var pw = packedWeight[stage][patternType][packedWIdx++] = new float[Board.SQUARE_NUM];
                            Buffer.BlockCopy(this.Weight[(int)Color.Black][stage][offset + feature], 0, pw, 0, sizeof(float) * pw.Length);
                        }
                    }
                    offset += BoardFeature.PatternFeatureNum[patternType];
                }

                // bias
                packedWeight[stage][patternType] = new float[BoardFeature.PackedPatternFeatureNum[patternType]][];
                packedWeight[stage][patternType][0] = new float[Board.SQUARE_NUM];
                Buffer.BlockCopy(this.Weight[(int)Color.Black][stage][offset], 0, packedWeight[stage][patternType][0], 0, sizeof(float) * Board.SQUARE_NUM);
            }
            return packedWeight;
        }
    }
}
