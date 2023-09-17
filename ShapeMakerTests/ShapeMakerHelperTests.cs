using ShapeMaker;

namespace ShapeMakerTests;

[TestClass]
public class ShapeMakerHelperTests {
    [TestMethod]
    public void TestMinRotation() {
        // All dimension unique
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(3, 4, 5));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(3, 5, 4));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(4, 3, 5));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(4, 5, 3));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(5, 3, 4));
        Assert.AreEqual(((byte)3, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(5, 4, 3));

        // Two dimensions equal, two low
        Assert.AreEqual(((byte)4, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(4, 4, 5));
        Assert.AreEqual(((byte)4, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(4, 5, 4));
        Assert.AreEqual(((byte)4, (byte)4, (byte)5), ShapeMakerHelper.MinRotation(5, 4, 4));

        // Two dimensions equal, two high
        Assert.AreEqual(((byte)4, (byte)5, (byte)5), ShapeMakerHelper.MinRotation(4, 5, 5));
        Assert.AreEqual(((byte)4, (byte)5, (byte)5), ShapeMakerHelper.MinRotation(5, 4, 5));
        Assert.AreEqual(((byte)4, (byte)5, (byte)5), ShapeMakerHelper.MinRotation(5, 5, 4));

        // All dimensions equal
        Assert.AreEqual(((byte)5, (byte)5, (byte)5), ShapeMakerHelper.MinRotation(5, 5, 5));
    }
}