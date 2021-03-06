using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Kalmia.Reversi;
using Kalmia.Evaluation;

namespace KalmiaTest
{
    [TestClass]
    public class BoardFeature_Test
    {
        [TestMethod]
        public void Update_Test()
        {
            var rand = new Random();
            var board = new FastBoard();
            var positions = new BoardPosition[Board.MAX_MOVE_CANDIDATE_COUNT];
            var bf0 = new BoardFeature(board);
            var bf1 = new BoardFeature(board);
            while (board.GetGameResult() == GameResult.NotOver)
            {
                var posCount = board.GetNextPositionCandidates(positions);
                var pos = positions[rand.Next(posCount)];
                var flipped = board.Update(pos);
                bf0.InitFeatures(board);
                bf1.Update(pos, flipped);
                AssertAreEqual(bf0, bf1);
            }
        }

        void AssertAreEqual(BoardFeature bf0, BoardFeature bf1)
        {
            for (var i = 0; i < BoardFeature.PATTERN_NUM_SUM; i++)
                Assert.AreEqual(bf0.Features[i], bf1.Features[i]);
        }
    }
}
