using ShapeMaker;

namespace ShapeMakerTests;

[TestClass]
public class ShapeTests {
    [TestMethod]
    public void TestHashSetForShapes() {
        // do hashset of type test
        var test = new HashSet<Shape<bool>>();
        var shape0 = new Shape<bool>(1, 1, 1);
        test.Add(shape0);
        var shape1 = new Shape<bool>(1, 1, 1);
        test.Add(shape1);
        var shape2 = new Shape<bool>(1, 1, 1);
        shape2.shape[0, 0, 0] = true;
        test.Add(shape2);
        var shape3 = new Shape<bool>(1, 1, 2);
        test.Add(shape3);
        Assert.AreEqual(3, test.Count);
    }

    [TestMethod]
    public void TestHashSetForShapes2() {
        // do hashset of type test
        var test = new HashSet<Shape<bool>>(ShapeBoolEqualityComparer.Instance);
        var shape0 = new Shape<bool>(1, 1, 1);
        test.Add(shape0);
        var shape1 = new Shape<bool>(1, 1, 1);
        test.Add(shape1);
        var shape2 = new Shape<bool>(1, 1, 1);
        shape2.shape[0, 0, 0] = true;
        test.Add(shape2);
        var shape3 = new Shape<bool>(1, 1, 2);
        test.Add(shape3);
        Assert.AreEqual(3, test.Count);
    }

    [TestMethod]
    public void TestLoadShapeCharXYZ() {
        var alpha = "abcdEFGHijklABCDefghIJKL";
        var shape = new Shape<char>(2, 3, 4, alpha.ToCharArray());
        Assert.AreEqual('a', shape.shape[0, 0, 0]);
        Assert.AreEqual('b', shape.shape[0, 0, 1]);
        Assert.AreEqual('c', shape.shape[0, 0, 2]);
        Assert.AreEqual('d', shape.shape[0, 0, 3]);
        Assert.AreEqual('E', shape.shape[0, 1, 0]);
        Assert.AreEqual('A', shape.shape[1, 0, 0]);

        Assert.AreEqual(alpha, new string(shape.Values().ToArray()));
    }

    [TestMethod]
    public void TestPadLeft() {
        var shape = NewShape(1, 1, 1, "a").PadLeft();
        Assert.AreEqual(2, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.AreEqual('\0', shape.shape[0, 0, 0]);
        Assert.AreEqual('a', shape.shape[1, 0, 0]);
    }

    [TestMethod]
    public void TestPadRight() {
        var shape = NewShape(1, 1, 1, "a").PadRight();
        Assert.AreEqual(2, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.AreEqual('a', shape.shape[0, 0, 0]);
        Assert.AreEqual('\0', shape.shape[1, 0, 0]);
    }

    [TestMethod]
    public void TestPadTop() {
        var shape = NewShape(1, 1, 1, "a").PadTop();
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(2, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.AreEqual('\0', shape.shape[0, 0, 0]);
        Assert.AreEqual('a', shape.shape[0, 1, 0]);
    }

    [TestMethod]
    public void TestPadBottom() {
        var shape = NewShape(1, 1, 1, "a").PadBottom();
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(2, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.AreEqual('a', shape.shape[0, 0, 0]);
        Assert.AreEqual('\0', shape.shape[0, 1, 0]);
    }

    [TestMethod]
    public void TestPadFront() {
        var shape = NewShape(1, 1, 1, "a").PadFront();
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(2, shape.d);
        Assert.AreEqual('\0', shape.shape[0, 0, 0]);
        Assert.AreEqual('a', shape.shape[0, 0, 1]);
    }

    [TestMethod]
    public void TestPadBack() {
        var shape = NewShape(1, 1, 1, "a").PadBack();
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(2, shape.d);
        Assert.AreEqual('a', shape.shape[0, 0, 0]);
        Assert.AreEqual('\0', shape.shape[0, 0, 1]);
    }

    [TestMethod]
    public void TestInPlaceRotateXMini() {
        // Expected:<741852963>.
        //   Actual:<721456983>.
        var nums = "123456789";
        var shape = NewShape(1, 3, 3, nums);
        var newShape = shape.RotateX(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("741852963", s);

        shape.RotateX().RotateX().RotateX();
        Assert.AreEqual(nums, SerializeShape(shape));
    }

    // Expected:<upkfavqlgbwrmhcxsnidytojeUPKFAVQLGBWRMHCXSNIDYTOJE>.
    //   Actual:<upkfavtqgbwlmhcxsrniydojeUPKFAVTQGBWLMHCXSRNIYDOJE>.
    [TestMethod]
    public void TestInPlaceRotateX() {
        // abcde    upkfa
        // fghij    vqlgb
        // klmno => wrmhc
        // pqrst    xsnid
        // uvwxy    ytoje
        var alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var shape = NewShape(2, 5, 5, alpha);
        var newShape = shape.RotateX(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("upkfavqlgbwrmhcxsnidytojeUPKFAVQLGBWRMHCXSNIDYTOJE", s);

        shape.RotateX().RotateX().RotateX();
        Assert.AreEqual(alpha, SerializeShape(shape));
    }

    [TestMethod]
    public void TestRotateX() {
        // abcd    iea
        // efgh => jfb
        // ijkl    kgc
        //         lhd
        var alpha = "abcdefghijklABCDEFGHIJKL";
        var shape = NewShape(2, 3, 4, alpha); // x is case, y=row, z=letter
        var newShape = shape.RotateX();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        Assert.AreEqual(2, newShape.w);
        Assert.AreEqual(4, newShape.h);
        Assert.AreEqual(3, newShape.d);
        var s = SerializeShape(newShape);
        Assert.AreEqual("ieajfbkgclhdIEAJFBKGCLHD", s);

        newShape = newShape.RotateX().RotateX().RotateX();
        Assert.AreEqual(alpha, SerializeShape(newShape));
    }

    [TestMethod]
    public void TestInPlaceRotateX2() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var shape = NewShape(2, 5, 5, alpha);
        var newShape = shape.RotateX2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA", s);

        Assert.AreEqual(alpha, SerializeShape(shape.RotateX2()));
    }

    [TestMethod]
    public void TestInPlaceMirrorX() {
        var alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var shape = NewShape(2, 5, 5, alpha);
        var newShape = shape.MirrorX(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYabcdefghijklmnopqrstuvwxy", s);

        Assert.AreEqual(alpha, SerializeShape(shape.MirrorX()));
    }

    [TestMethod]
    public void TestInPlaceRotateY() {
        var alpha = "abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var shape = NewShape(5, 2, 5, alpha);
        var newShape = shape.RotateY(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("upkfaUPKFAvqlgbVQLGBwrmhcWRMHCxsnidXSNIDytojeYTOJE", s);

        shape.RotateY().RotateY().RotateY();
        Assert.AreEqual(alpha, SerializeShape(shape));
    }

    [TestMethod]
    public void TestRotateY() {
        var alpha = "abcdABCDefghEFGHijklIJKL";
        var shape = NewShape(3, 2, 4, alpha); // y is case, x=row, z=letter
        var newShape = shape.RotateY();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        Assert.AreEqual(4, newShape.w);
        Assert.AreEqual(2, newShape.h);
        Assert.AreEqual(3, newShape.d);
        var s = SerializeShape(newShape);
        Assert.AreEqual("ieaIEAjfbJFBkgcKGClhdLHD", s);

        newShape = newShape.RotateY().RotateY().RotateY();
        Assert.AreEqual(alpha, SerializeShape(newShape));
    }

    [TestMethod]
    public void TestInPlaceRotateY2() {
        var alpha = "abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var shape = NewShape(5, 2, 5, alpha);
        var newShape = shape.RotateY2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("yxwvuYXWVUtsrqpTSRQPonmlkONMLKjihgfJIHGFedcbaEDCBA", s);

        Assert.AreEqual(alpha, SerializeShape(shape.RotateY2()));
    }

    [TestMethod]
    public void TestInPlaceMirrorY() {
        // abcde    uvwxy
        // fghij    pqrst
        // klmno => klmno
        // pqrst    fghij
        // uvwxy    abcde
        var alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var shape = NewShape(2, 5, 5, alpha);
        var newShape = shape.MirrorY(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE", s);

        Assert.AreEqual(alpha, SerializeShape(shape.MirrorY()));
    }

    [TestMethod]
    public void TestInPlaceRotateZ() {
        var alpha = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyY";
        var shape = NewShape(5, 5, 2, alpha);
        var newShape = shape.RotateZ(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("uUpPkKfFaAvVqQlLgGbBwWrRmMhHcCxXsSnNiIdDyYtToOjJeE", s);

        shape.RotateZ().RotateZ().RotateZ();
        Assert.AreEqual(alpha, SerializeShape(shape));
    }

    [TestMethod]
    public void TestRotateZ() {
        var alpha = "aAbBcCdDeEfFgGhHiIjJkKlL";
        var shape = NewShape(3, 4, 2, alpha); // z is case, x=row, y=letter
        var newShape = shape.RotateZ();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        Assert.AreEqual(4, newShape.w);
        Assert.AreEqual(3, newShape.h);
        Assert.AreEqual(2, newShape.d);
        var s = SerializeShape(newShape);
        Assert.AreEqual("iIeEaAjJfFbBkKgGcClLhHdD", s);

        newShape = newShape.RotateZ().RotateZ().RotateZ();
        Assert.AreEqual(alpha, SerializeShape(newShape));
    }

    [TestMethod]
    public void TestInPlaceRotateZ2() {
        var alpha = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyY";
        var shape = NewShape(5, 5, 2, alpha);
        var newShape = shape.RotateZ2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("yYxXwWvVuUtTsSrRqQpPoOnNmMlLkKjJiIhHgGfFeEdDcCbBaA", s);

        Assert.AreEqual(alpha, SerializeShape(shape.RotateZ2()));
    }

    [TestMethod]
    public void TestInPlaceMirrorZ() {
        // abcde    edcba
        // fghij    jihgf
        // klmno => onmlk
        // pqrst    tsrqp
        // uvwxy    yxwvu
        var alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var shape = NewShape(2, 5, 5, alpha);
        var newShape = shape.MirrorZ(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        Assert.AreEqual("edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU", s);

        Assert.AreEqual(alpha, SerializeShape(shape.MirrorZ()));
    }

    private Shape<char> NewShape(int w, int h, int d, string chars) {
        return new Shape<char>(w, h, d, chars.ToCharArray());
    }

    private string SerializeShape(Shape<char> shape) {
        return new string(shape.Values().ToArray());
    }
}
