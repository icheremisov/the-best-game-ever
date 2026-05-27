using NUnit.Framework;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class RotationTests
    {
        [Test]
        public void Rotate90CW_TurnsRowsIntoColumns()
        {
            // Input: 2x3 (rows=2, cols=3)
            //  T T F
            //  F T F
            var src = new bool[2, 3] { { true, true, false }, { false, true, false } };
            // Expected after 90° CW: 3x2 (rows=3, cols=2)
            //  F T
            //  T T
            //  F F
            var rotated = RotationUtil.Rotate90CW(src);

            Assert.AreEqual(3, rotated.GetLength(0));
            Assert.AreEqual(2, rotated.GetLength(1));
            Assert.IsFalse(rotated[0, 0]); Assert.IsTrue (rotated[0, 1]);
            Assert.IsTrue (rotated[1, 0]); Assert.IsTrue (rotated[1, 1]);
            Assert.IsFalse(rotated[2, 0]); Assert.IsFalse(rotated[2, 1]);
        }

        [Test]
        public void Rotate_FourTimes_ReturnsToOriginal()
        {
            var src = new bool[2, 3] { { true, false, true }, { false, true, false } };
            var rotated = RotationUtil.Rotate90CW(RotationUtil.Rotate90CW(RotationUtil.Rotate90CW(RotationUtil.Rotate90CW(src))));

            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                    Assert.AreEqual(src[r, c], rotated[r, c], $"mismatch at [{r},{c}]");
        }
    }
}
