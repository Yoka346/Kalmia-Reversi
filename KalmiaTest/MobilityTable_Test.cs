using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Kalmia;

namespace KalmiaTest
{
    [TestClass]
    public class MobilityTable_Test
    {
        [TestMethod]
        public void GetMobilityPattern_Test()
        {
            byte p = 0b00001000;
            byte o = 0b00010100;
            byte mobility = 0b00100010;
            Assert.AreEqual(MobilityTable.GetMobilityPattern(p, o), mobility);

            p = 0b01000010;
            o = 0b10101101;
            mobility = 0b00010000;
            Assert.AreEqual(MobilityTable.GetMobilityPattern(p, o), mobility);

            p = 0b10101101;
            o = 0b01000010;
            mobility = 0b00000000;
            Assert.AreEqual(MobilityTable.GetMobilityPattern(p, o), mobility);
        }
    }
}
