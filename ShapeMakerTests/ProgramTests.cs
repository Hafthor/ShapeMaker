using System;
using ShapeMaker;

namespace ShapeMakerTests;

[TestClass]
public class ProgramTests {
	[TestMethod]
	public void TestPolycubes() {
        // ref: https://en.wikipedia.org/wiki/Polycube
        var shapes1 = new HashSet<string>() { "1,1,1,1" };
        var shapes2 = Program.ShapesFromExtendingShapes(shapes1);
        Assert.AreEqual(1, shapes2.Count);
        Assert.AreEqual(1, Program.ChiralShapes(shapes2).Count);
        var shapes3 = Program.ShapesFromExtendingShapes(shapes2);
        Assert.AreEqual(2, shapes3.Count);
        Assert.AreEqual(2, Program.ChiralShapes(shapes3).Count);
        var shapes4 = Program.ShapesFromExtendingShapes(shapes3);
        Assert.AreEqual(8, shapes4.Count);
        Assert.AreEqual(7, Program.ChiralShapes(shapes4).Count);
        var shapes5 = Program.ShapesFromExtendingShapes(shapes4);
        Assert.AreEqual(29, shapes5.Count);
        Assert.AreEqual(23, Program.ChiralShapes(shapes5).Count);
        var shapes6 = Program.ShapesFromExtendingShapes(shapes5);
        Assert.AreEqual(166, shapes6.Count);
        Assert.AreEqual(112, Program.ChiralShapes(shapes6).Count);
        var shapes7 = Program.ShapesFromExtendingShapes(shapes6);
        Assert.AreEqual(1023, shapes7.Count);
        Assert.AreEqual(607, Program.ChiralShapes(shapes7).Count);
        var shapes8 = Program.ShapesFromExtendingShapes(shapes7);
        Assert.AreEqual(6922, shapes8.Count);
        Assert.AreEqual(3811, Program.ChiralShapes(shapes8).Count);
    }
}