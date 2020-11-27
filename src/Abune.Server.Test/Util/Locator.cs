using System.Linq;
using NUnit.Framework;
using Abune.Shared.Util;
using Abune.Shared.DataType;

namespace Abune.Server.Tests
{
    public class LocatorTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [TestCase(0.0f  ,    0.0f,    0.0f, 5000000500000050000)]
        [TestCase(200.0f ,   0.0f,    0.0f, 5000100500000050000)]
        [TestCase(-200.0f,   0.0f,    0.0f, 4999900500000050000)]
        [TestCase(0.0f,    200.0f,    0.0f, 5000000500010050000)]
        [TestCase(0.0f,   -200.0f,    0.0f, 5000000499990050000)]
        [TestCase(0.0f,      0.0f,  200.0f, 5000000500000050001)]
        [TestCase(0.0f,      0.0f, -200.0f, 5000000500000049999)]
        public void GetAreaIdFromWorldPositionTest(float x, float y, float z, long expectedArea)
        {
            AVector3 wp = new AVector3
            {
                X = x,
                Y = y,
                Z = z,
            };
            ulong actual = Locator.GetAreaIdFromWorldPosition(wp);
            Assert.AreEqual(expectedArea, actual);
        }

        [TestCase(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1)]
        [TestCase(0.0f, 0.0f, 0.0f, 200.0f, 0.0f, 0.0f, 2)]
        [TestCase(0.0f, 0.0f, 0.0f, 200.0f, 200.0f, 0.0f, 4)]
        [TestCase(0.0f, 0.0f, 0.0f, 200.0f, 200.0f, 200.0f, 8)]
        public void GetAreaIdsWithinWorldBoundariesTest(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, int expectedAreaCount)
        {
            AVector3 min = new AVector3
            {
                X = minX,
                Y = minY,
                Z = minZ,
            };
            AVector3 max = new AVector3
            {
                X = maxX,
                Y = maxY,
                Z = maxZ,
            };
            var actual = Locator.GetAreaIdsWithinWorldBoundaries(min, max);
            Assert.AreEqual(expectedAreaCount, actual.Count);
        }


        [TestCase(5000000500000050000, 0.0f,    0.0f,   0.0f)]
        [TestCase(5000100500000050000, 200.0f,  0.0f,   0.0f)]
        [TestCase(4999900500000050000, -200.0f, 0.0f,   0.0f)]
        [TestCase(5000000500010050000, 0.0f,  200.0f,   0.0f)]
        [TestCase(5000000499990050000, 0.0f, -200.0f,   0.0f)]
        [TestCase(5000000500000050001, 0.0f,    0.0f, 200.0f)]
        [TestCase(5000000500000049999, 0.0f,    0.0f, -200.0f)]
        public void GetAreaBoundaryTest(long area, float expectedX, float expectedY, float expectedZ)
        {
            float minX, minY, minZ, maxX, maxY, maxZ;
            Locator.GetAreaBoundary((ulong)area, out minX, out minY, out minZ, out maxX, out maxY, out maxZ);
            Assert.AreEqual(expectedX, minX);
            Assert.AreEqual(expectedY, minY);
            Assert.AreEqual(expectedZ, minZ);
            Assert.AreEqual(expectedX + Locator.AREASIZE, maxX );
            Assert.AreEqual(expectedY + Locator.AREASIZE, maxY);
            Assert.AreEqual(expectedZ + Locator.AREASIZE, maxZ);
        }
    }
}

