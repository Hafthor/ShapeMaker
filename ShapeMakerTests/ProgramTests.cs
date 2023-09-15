using ShapeMaker;

namespace ShapeMakerTests;

[TestClass]
public class ProgramTests {
    [TestMethod]
    public void TestPolycubes() {
        // ref: https://en.wikipedia.org/wiki/Polycube
    }
}

[TestClass]
public class ShapeMakerEstimatorTests {
    [TestMethod]
    public void TestShapeSizesFromExtendingShapes1File345() {
        var fileList = new List<FileScanner.Results> {
            new FileScanner.Results {
                w = 3,
                h = 4,
                d = 5,
                ext = ".bin",
                size = (3*4*5+7)/8 * 100, // 60 bits each, 7.5 bytes rounds up to 8 bytes, 100 shapes is 800 bytes
            }
        };
        var targets = ShapeMakerEstimator.ShapeSizesFromExtendingShapes(fileList).ToList();
        Assert.AreEqual(4, targets.Count);
        Assert.IsTrue(targets.Contains((3, 4, 5, 800)));
        Assert.IsTrue(targets.Contains((4, 4, 5, 800)));
        Assert.IsTrue(targets.Contains((3, 5, 5, 800)));
        Assert.IsTrue(targets.Contains((3, 4, 6, 800)));
    }

    [TestMethod]
    public void TestShapeSizesFromExtendingShapes2Files() {
        var fileList = new List<FileScanner.Results> {
            new FileScanner.Results {
                w = 3,
                h = 4,
                d = 5,
                ext = ".bin",
                size = (3*4*5+7)/8 * 100, // 60 bits each, 7.5 bytes rounds up to 8 bytes, 100 shapes is 800 bytes
            },
            new FileScanner.Results {
                w = 4,
                h = 4,
                d = 4,
                ext = ".bin",
                size = (4*4*4+7)/8 * 100, // 64 bits each, 8 bytes, 100 shapes is 800 bytes
            }
        };
        var targets = ShapeMakerEstimator.ShapeSizesFromExtendingShapes(fileList).ToList();
        Assert.AreEqual(5, targets.Count);
        Assert.IsTrue(targets.Contains((3, 4, 5, 800)));
        Assert.IsTrue(targets.Contains((4, 4, 5, 800 * 4)));
        Assert.IsTrue(targets.Contains((3, 5, 5, 800)));
        Assert.IsTrue(targets.Contains((3, 4, 6, 800)));
        Assert.IsTrue(targets.Contains((4, 4, 4, 800)));
    }
}

[TestClass]
public class ShapeMakerHelperTests {
    [TestMethod]
    public void TestMinRotation() {
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(3, 4, 5));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(3, 5, 4));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(4, 3, 5));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(4, 5, 3));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(5, 3, 4));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(5, 4, 3));

        Assert.AreEqual(((byte)4, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(4, 4, 5));
        Assert.AreEqual(((byte)4, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(4, 5, 4));
        Assert.AreEqual(((byte)4, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(5, 4, 4));

        Assert.AreEqual(((byte)4, (byte)5, (byte)5), ShapeMakerHelper.MinRotation(4, 5, 5));
        Assert.AreEqual(((byte)4, (byte)5, (byte)5), ShapeMakerHelper.MinRotation(5, 4, 5));
        Assert.AreEqual(((byte)4, (byte)5, (byte)5), ShapeMakerHelper.MinRotation(5, 5, 4));

        Assert.AreEqual(((byte)5, (byte)5, (byte)5), ShapeMakerHelper.MinRotation(5, 5, 5));
    }
}