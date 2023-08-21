using System;
using ShapeMaker;

namespace ShapeMakerTests;

[TestClass]
public class BitShapeTests {

    [TestMethod]
    public void TestHashSetForByteArray() {
        // do hashset of BitArray test
        var test = new HashSet<byte[]>(ByteArrayEqualityComparer.Instance);
        var ba1 = new byte[] { 0x55, 0xAA };
        Assert.IsTrue(test.Add(ba1));
        var ba2 = new byte[] { 0x55, 0xAA };
        Assert.IsFalse(test.Add(ba2));
        Assert.AreEqual(1, test.Count);

        var test2 = new HashSet<(byte, byte)>();
        test2.Add((1, 1));
        test2.Add((1, 1));
        test2.Add((2, 2));
        Assert.AreEqual(2, test2.Count);
    }

    [TestMethod]
    public void TestSerializeDeserialize() {
        var shape = new BitShape("1,1,1,*");
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.IsTrue(shape[0, 0, 0]);

        shape = new BitShape("3,3,3,*** *** ***\n*** *.* ***\n*** *** ***");
        Assert.AreEqual(3, shape.w);
        Assert.AreEqual(3, shape.h);
        Assert.AreEqual(3, shape.d);

        for (int x = 0; x < shape.w; x++)
            for (int y = 0; y < shape.h; y++)
                for (int z = 0; z < shape.d; z++)
                    Assert.AreEqual(x != 1 || y != 1 || z != 1, shape[x, y, z]);

        var s = shape.Serialize();
        Assert.AreEqual("3,3,3,*** *** ***\n*** *.* ***\n*** *** ***".Replace(" ", "").Replace("\n", ""), s);
    }

    [TestMethod]
    public void TestPadLeft() {
        var shape = new BitShape(1, 1, 1);
        shape[0, 0, 0] = true;
        shape = shape.PadLeft();
        Assert.AreEqual(2, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.IsFalse(shape[0, 0, 0]);
        Assert.IsTrue(shape[1, 0, 0]);

        Assert.AreEqual("2,1,1,.*", shape.Serialize());
    }

    [TestMethod]
    public void TestPadRight() {
        var shape = new BitShape(1, 1, 1);
        shape[0, 0, 0] = true;
        shape = shape.PadRight();
        Assert.AreEqual(2, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.IsFalse(shape[1, 0, 0]);
        Assert.IsTrue(shape[0, 0, 0]);

        Assert.AreEqual("2,1,1,*.", shape.Serialize());
    }

    [TestMethod]
    public void TestPadTop() {
        var shape = new BitShape(1, 1, 1);
        shape[0, 0, 0] = true;
        shape = shape.PadTop();
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(2, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.IsFalse(shape[0, 0, 0]);
        Assert.IsTrue(shape[0, 1, 0]);

        Assert.AreEqual("1,2,1,.*", shape.Serialize());
    }

    [TestMethod]
    public void TestPadBottom() {
        var shape = new BitShape(1, 1, 1);
        shape[0, 0, 0] = true;
        shape = shape.PadBottom();
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(2, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.IsTrue(shape[0, 0, 0]);
        Assert.IsFalse(shape[0, 1, 0]);

        Assert.AreEqual("1,2,1,*.", shape.Serialize());
    }

    [TestMethod]
    public void TestPadFront() {
        var shape = new BitShape(1, 1, 1);
        shape[0, 0, 0] = true;
        shape = shape.PadFront();
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(2, shape.d);
        Assert.IsFalse(shape[0, 0, 0]);
        Assert.IsTrue(shape[0, 0, 1]);

        Assert.AreEqual("1,1,2,.*", shape.Serialize());
    }

    [TestMethod]
    public void TestPadBack() {
        var shape = new BitShape(1, 1, 1);
        shape[0, 0, 0] = true;
        shape = shape.PadBack();
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(2, shape.d);
        Assert.IsTrue(shape[0, 0, 0]);
        Assert.IsFalse(shape[0, 0, 1]);

        Assert.AreEqual("1,1,2,*.", shape.Serialize());
    }

    [TestMethod]
    public void TestInPlaceRotateXMini() {
        var shape = new BitShape("1,3,3,.** .** ...");
        var newShape = shape.RotateX(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.AreEqual("1,3,3,... .** .**".Replace(" ", ""), shape.Serialize());

        shape.RotateX().RotateX().RotateX();
        Assert.AreEqual("1,3,3,.** .** ...".Replace(" ", ""), shape.Serialize());
    }

    [TestMethod]
    public void TestRotateX() {
        // abcd    iea
        // efgh => jfb
        // ijkl    kgc
        //         lhd
        var shape = new BitShape(2, 3, 4);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        Assert.AreEqual(2, newShape.w);
        Assert.AreEqual(4, newShape.h);
        Assert.AreEqual(3, newShape.d);
        Assert.IsTrue(newShape[0, 0, 2]);
        Assert.IsTrue(newShape[1, 1, 1]);

        newShape = newShape.RotateX().RotateX().RotateX();
        Assert.AreEqual(2, newShape.w);
        Assert.AreEqual(3, newShape.h);
        Assert.AreEqual(4, newShape.d);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 4; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2Opt(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2Opt();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceMirrorX() {
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.MirrorX(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 1, 1]);
        Assert.IsTrue(shape[1, 0, 0]);

        shape.MirrorX();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceMirrorXOpt() {
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.MirrorXOpt(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 1, 1]);
        Assert.IsTrue(shape[1, 0, 0]);

        shape.MirrorXOpt();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateY() {
        // alpha = "abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var bits = "*...*.***....*.***.*....*****......******...*.***."; // vowels,CONSONANTS
        var shape = new BitShape("5,2,5," + bits);
        var newShape = shape.RotateY(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("upkfaUPKFAvqlgbVQLGBwrmhcWRMHCxsnidXSNIDytojeYTOJE", s);
        Assert.AreEqual("5,2,5,*...*.***......*****.....*****...*.***.**.*.*.*.*.", s);

        shape.RotateY().RotateY().RotateY();
        Assert.AreEqual("5,2,5," + bits, shape.Serialize().Replace(" ", "").Replace("\n", ""));
    }

    [TestMethod]
    public void TestRotateY() {
        // alpha = "abcdABCDefghEFGHijklIJKL";
        var bits = "*....****....****....***"; // vowels,CONSONANTS
        var shape = new BitShape("3,2,4," + bits); // y is case, x=row, z=letter
        var newShape = shape.RotateY();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        Assert.AreEqual(4, newShape.w);
        Assert.AreEqual(2, newShape.h);
        Assert.AreEqual(3, newShape.d);
        var s = newShape.Serialize();
        //    Assert.AreEqual("ieaIEAjfbJFBkgcKGClhdLHD", s);
        Assert.AreEqual("4,2,3,***......***...***...***", s);

        newShape = newShape.RotateY().RotateY().RotateY();
        Assert.AreEqual("3,2,4," + bits, newShape.Serialize());
    }

    [TestMethod]
    public void TestInPlaceRotateY2() {
        // alpha = "abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var bits = "*...*.***....*.***.*....*****......******...*.***.";
        var shape = new BitShape("5,2,5," + bits);
        var newShape = shape.RotateY2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("yxwvuYXWVUtsrqpTSRQPonmlkONMLKjihgfJIHGFedcbaEDCBA", s);
        Assert.AreEqual("5,2,5,*...*.***......******.....****.*...*.****...*.***.", s);

        Assert.AreEqual("5,2,5," + bits, shape.RotateY2().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorY() {
        // abcde    uvwxy
        // fghij    pqrst
        // klmno => klmno
        // pqrst    fghij
        // uvwxy    abcde
        // alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var bits = "*...*...*.....*.....*...*.***.***.*****.*****.***.";
        var shape = new BitShape("2,5,5," + bits);
        var newShape = shape.MirrorY(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE", s);
        Assert.AreEqual("2,5,5,*...*.........*...*.*...*.***.*********.***.*.***.", s);

        Assert.AreEqual("2,5,5," + bits, shape.MirrorY().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorYOpt() {
        // abcde    uvwxy
        // fghij    pqrst
        // klmno => klmno
        // pqrst    fghij
        // uvwxy    abcde
        // alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var bits = "*...*...*.....*.....*...*.***.***.*****.*****.***.";
        var shape = new BitShape("2,5,5," + bits);
        var newShape = shape.MirrorYOpt(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE", s);
        Assert.AreEqual("2,5,5,*...*.........*...*.*...*.***.*********.***.*.***.", s);

        Assert.AreEqual("2,5,5," + bits, shape.MirrorYOpt().Serialize());
    }

    [TestMethod]
    public void TestInPlaceRotateZ() {
        // alpha = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyY";
        var bits = "*..*.*.**..*.*.**..*.*.*.*.**..*.*.*.*.**..*.*.**.";
        var shape = new BitShape("5,5,2," + bits);
        var newShape = shape.RotateZ(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("uUpPkKfFaAvVqQlLgGbBwWrRmMhHcCxXsSnNiIdDyYtToOjJeE", s);
        Assert.AreEqual("5,5,2,*..*.*.**..*.*.*.*.*.*.*.*.*.*.*.*.**..**..**..**.", s);

        shape.RotateZ().RotateZ().RotateZ();
        Assert.AreEqual("5,5,2," + bits, shape.Serialize());
    }

    [TestMethod]
    public void TestRotateZ() {
        // alpha = "aAbBcCdDeEfFgGhHiIjJkKlL";
        var bits = "*..*.*.**..*.*.**..*.*.*";
        var shape = new BitShape("3,4,2," + bits);
        var newShape = shape.RotateZ();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        Assert.AreEqual(4, newShape.w);
        Assert.AreEqual(3, newShape.h);
        Assert.AreEqual(2, newShape.d);
        var s = newShape.Serialize();
        //    Assert.AreEqual("iIeEaAjJfFbBkKgGcClLhHdD", s);
        Assert.AreEqual("4,3,2,*.*.*..*.*.*.*.*.*.*.*.*", s);

        newShape = newShape.RotateZ().RotateZ().RotateZ();
        Assert.AreEqual("3,4,2," + bits, newShape.Serialize());
    }

    [TestMethod]
    public void TestInPlaceRotateZ2() {
        // alpha = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyY";
        var bits = "*..*.*.**..*.*.**..*.*.*.*.**..*.*.*.*.**..*.*.**.";
        var shape = new BitShape("5,5,2," + bits);
        var newShape = shape.RotateZ2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("yYxXwWvVuUtTsSrRqQpPoOnNmMlLkKjJiIhHgGfFeEdDcCbBaA", s);
        Assert.AreEqual("5,5,2,*..*.*.**..*.*.*.*.**..*.*.*.*.**..*.*.**..*.*.**.", s);

        Assert.AreEqual("5,5,2," + bits, shape.RotateZ2().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorZ() {
        // abcde    edcba
        // fghij    jihgf
        // klmno => onmlk
        // pqrst    tsrqp
        // uvwxy    yxwvu
        // alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var bits = "*...*...*.....*.....*...*.***.***.*****.*****.***.";
        var shape = new BitShape("2,5,5," + bits);
        var newShape = shape.MirrorZ(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //       Assert.AreEqual("edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU", s);
        Assert.AreEqual(s, "2,5,5,*...*.*...*.........*...*.***.*.***.*********.***.");

        Assert.AreEqual("2,5,5," + bits, shape.MirrorZ().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorZOpt() {
        // abcde    edcba
        // fghij    jihgf
        // klmno => onmlk
        // pqrst    tsrqp
        // uvwxy    yxwvu
        // alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var bits = "*...*...*.....*.....*...*.***.***.*****.*****.***.";
        var shape = new BitShape("2,5,5," + bits);
        var newShape = shape.MirrorZOpt(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //       Assert.AreEqual("edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU", s);
        Assert.AreEqual(s, "2,5,5,*...*.*...*.........*...*.***.*.***.*********.***.");

        Assert.AreEqual("2,5,5," + bits, shape.MirrorZOpt().Serialize());
    }

    [TestMethod]
    public void TestShapeCounts() {
        var shape = new BitShape("1,1,1,*");
        Assert.AreEqual((1, 0, 0), shape.CornerEdgeFaceCount());
        shape = new BitShape("1,1,1,.");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCount());

        shape = new BitShape("2,2,2,********");
        Assert.AreEqual((8, 0, 0), shape.CornerEdgeFaceCount());
        shape = new BitShape("2,2,2,........");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCount());

        shape = new BitShape("3,3,3,***************************");
        Assert.AreEqual((8, 12, 6), shape.CornerEdgeFaceCount());
        shape = new BitShape("3,3,3,...........................");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCount());
    }
}
