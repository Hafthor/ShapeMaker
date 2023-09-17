using ShapeMaker;

namespace ShapeMakerTests;

[TestClass]
public class ShapeMakerEstimatorTests {
    [TestMethod]
    public void TestShapeSizesFromExtendingShapes1File345() {
        var fileList = new List<FileScanner.Results> {
            new() {
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
            new() {
                w = 3,
                h = 4,
                d = 5,
                ext = ".bin",
                size = (3*4*5+7)/8 * 100, // 60 bits each, 7.5 bytes rounds up to 8 bytes, 100 shapes is 800 bytes
            },
            new() {
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