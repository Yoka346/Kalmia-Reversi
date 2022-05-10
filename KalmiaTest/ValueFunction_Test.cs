using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Kalmia.Evaluation;

namespace KalmiaTest
{
    [TestClass]
    public class ValueFunction_Test
    {
        [TestMethod]
        public void FlipEval_Test()
        {
            const int TEST_NUM = 10000;
            var rand = new Random();
            var vf = new ValueFunction(@"C:\Users\yu_ok\source\repos\Kalmia\KalmiaTest\test_data\valueFuncParams.dat");

            for (var i = 0; i < TEST_NUM; i++)
            {
                var board = Utils.CreateRandomBoard(rand, rand.Next(0, 60));
                var bf = new BoardFeature(board);
                var flipped = new BoardFeature(bf);
                flipped.Flip();
                Assert.IsTrue(Math.Abs(vf.F(bf) - vf.F(flipped)) < 1.0e-6f);
            }
        }
    }
}
