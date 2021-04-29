using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Kalmia;

namespace KalmiaTest
{
    [TestClass]
    public class FlipTable_Test
    {
        [TestMethod]
        public void GetFlippedPattern_Test()
        {
            int linePos = 5;
            byte p = 0b00001000;
            byte o = 0b00010100;
            byte flipped = 0b00010000;
            Assert.AreEqual(FlipTable.GetFlippedPattern(linePos, p ,o), flipped);

            linePos = 4;
            p = 0b01000010;
            o = 0b10101101;
            flipped = 0b00101100;
            Assert.AreEqual(FlipTable.GetFlippedPattern(linePos, p, o), flipped);
        }
    }
}
