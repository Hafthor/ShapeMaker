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
    public void TestNewBitShape() {
        var shape = new BitShape(3, 4, 5, new byte[100]);
        Assert.AreEqual((byte)3, shape.w);
        Assert.AreEqual((byte)4, shape.h);
        Assert.AreEqual((byte)5, shape.d);
        Assert.AreEqual(100, shape.bytes.Length);
    }

    [TestMethod]
    public void TestCopyBitShape() {
        var shape = new BitShape("3x3x3,*** *** ***\n*** *.* ***\n*** *** ***");
        var copy = new BitShape(shape);
        Assert.AreEqual(shape.w, copy.w);
        Assert.AreEqual(shape.h, copy.h);
        Assert.AreEqual(shape.d, copy.d);
        Assert.IsFalse(object.ReferenceEquals(shape.bytes, copy.bytes));
        Assert.IsTrue(shape.bytes.SequenceEqual(copy.bytes));
    }

    [TestMethod]
    public void TestParseNonTwoPartString() {
        Assert.ThrowsException<ArgumentException>(() => new BitShape("3x3x3"));
        Assert.ThrowsException<ArgumentException>(() => new BitShape("3x3x3,***************************,extra"));
        Assert.ThrowsException<ArgumentException>(() => new BitShape("3x3,***************************"));
        Assert.ThrowsException<ArgumentException>(() => new BitShape("3x3x3x3,***************************"));
        Assert.ThrowsException<ArgumentException>(() => new BitShape("3x3x3,**************************"));
        Assert.ThrowsException<ArgumentException>(() => new BitShape("3x3x3,****************************"));
    }

    [TestMethod]
    public void TestSerializeDeserialize() {
        var shape = new BitShape("1x1x1,*");
        Assert.AreEqual(1, shape.w);
        Assert.AreEqual(1, shape.h);
        Assert.AreEqual(1, shape.d);
        Assert.IsTrue(shape[0, 0, 0]);

        shape = new BitShape("3x3x3,*** *** ***\n*** *.* ***\n*** *** ***");
        Assert.AreEqual(3, shape.w);
        Assert.AreEqual(3, shape.h);
        Assert.AreEqual(3, shape.d);

        for (int x = 0; x < shape.w; x++)
            for (int y = 0; y < shape.h; y++)
                for (int z = 0; z < shape.d; z++)
                    Assert.AreEqual(x != 1 || y != 1 || z != 1, shape[x, y, z]);

        var s = shape.Serialize();
        Assert.AreEqual("3x3x3,*** *** ***\n*** *.* ***\n*** *** ***".Replace(" ", "").Replace("\n", ""), s);
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

        Assert.AreEqual("2x1x1,.*", shape.Serialize());
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

        Assert.AreEqual("2x1x1,*.", shape.Serialize());
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

        Assert.AreEqual("1x2x1,.*", shape.Serialize());
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

        Assert.AreEqual("1x2x1,*.", shape.Serialize());
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

        Assert.AreEqual("1x1x2,.*", shape.Serialize());
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

        Assert.AreEqual("1x1x2,*.", shape.Serialize());
    }

    [TestMethod]
    public void TestInPlaceRotateXMini() {
        var shape = new BitShape("1x3x3,.** .** ...");
        var newShape = shape.RotateX(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.AreEqual("1x3x3,... .** .**".Replace(" ", ""), shape.Serialize());

        shape.RotateX().RotateX().RotateX();
        Assert.AreEqual("1x3x3,.** .** ...".Replace(" ", ""), shape.Serialize());
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
    public void TestRotateX_() {
        var starting = "2x3x4,abcdefghijklABCDEFGHIJKL";
        var ending = "2x4x3,ieajfbkgclhdIEAJFBKGCLHD";
        TestOperation(starting, (s) => s.RotateX(), ending, (s) => s.RotateX().RotateX().RotateX());
    }

    [TestMethod]
    public void TestInPlaceRotateX2Org() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2Org(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2Org();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2Org_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2Org(), ending, (s) => s.RotateX2Org());
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
    public void TestInPlaceRotateX2Opt_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2Opt(), ending, (s) => s.RotateX2Opt());
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt1() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2Opt1(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2Opt1();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt1_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2Opt1(), ending, (s) => s.RotateX2Opt1());
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt2() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2Opt2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2Opt2();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt2_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2Opt2(), ending, (s) => s.RotateX2Opt2());
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt3() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2Opt3(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2Opt3();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt3_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2Opt3(), ending, (s) => s.RotateX2Opt3());
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt4() {
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
    public void TestInPlaceRotateX2Opt4_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2(), ending, (s) => s.RotateX2());
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt5() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2Opt5(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2Opt5();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt5_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2Opt5(), ending, (s) => s.RotateX2Opt5());
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt6() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2Opt6(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2Opt6();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt6_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2Opt6(), ending, (s) => s.RotateX2Opt6());
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt7() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.RotateX2Opt7(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 4, 4]);
        Assert.IsTrue(shape[1, 3, 3]);

        shape.RotateX2Opt7();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceRotateX2Opt7_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,yxwvutsrqponmlkjihgfedcbaYXWVUTSRQPONMLKJIHGFEDCBA";
        TestOperation(starting, (s) => s.RotateX2Opt7(), ending, (s) => s.RotateX2Opt7());
    }

    [TestMethod]
    public void TestInPlaceMirrorXOrg() {
        var shape = new BitShape(2, 5, 5);
        shape[0, 0, 0] = true;
        shape[1, 1, 1] = true;
        var newShape = shape.MirrorXOrg(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape[0, 1, 1]);
        Assert.IsTrue(shape[1, 0, 0]);

        shape.MirrorXOrg();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape[x, y, z]);
    }

    [TestMethod]
    public void TestInPlaceMirrorXOrg_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,ABCDEFGHIJKLMNOPQRSTUVWXYabcdefghijklmnopqrstuvwxy";
        TestOperation(starting, (s) => s.MirrorXOrg(), ending, (s) => s.MirrorXOrg());
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
    public void TestInPlaceMirrorXOpt_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,ABCDEFGHIJKLMNOPQRSTUVWXYabcdefghijklmnopqrstuvwxy";
        TestOperation(starting, (s) => s.MirrorXOpt(), ending, (s) => s.MirrorXOpt());
    }

    [TestMethod]
    public void TestInPlaceMirrorXOpt2() {
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
    public void TestInPlaceMirrorXOpt2_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,ABCDEFGHIJKLMNOPQRSTUVWXYabcdefghijklmnopqrstuvwxy";
        TestOperation(starting, (s) => s.MirrorX(), ending, (s) => s.MirrorX());
    }

    [TestMethod]
    public void TestInPlaceRotateY() {
        // alpha = "abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var bits = "*...*.***....*.***.*....*****......******...*.***."; // vowels,CONSONANTS
        var shape = new BitShape("5x2x5," + bits);
        var newShape = shape.RotateY(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("upkfaUPKFAvqlgbVQLGBwrmhcWRMHCxsnidXSNIDytojeYTOJE", s);
        Assert.AreEqual("5x2x5,*...*.***......*****.....*****...*.***.**.*.*.*.*.", s);

        shape.RotateY().RotateY().RotateY();
        Assert.AreEqual("5x2x5," + bits, shape.Serialize().Replace(" ", "").Replace("\n", ""));
    }

    [TestMethod]
    public void TestInPlaceRotateY_() {
        var starting = "5x2x5,abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var ending = "5x2x5,upkfaUPKFAvqlgbVQLGBwrmhcWRMHCxsnidXSNIDytojeYTOJE";
        TestOperation(starting, (s) => s.RotateY(), ending, (s) => s.RotateY().RotateY().RotateY());
    }

    [TestMethod]
    public void TestRotateY() {
        // alpha = "abcdABCDefghEFGHijklIJKL";
        var bits = "*....****....****....***"; // vowels,CONSONANTS
        var shape = new BitShape("3x2x4," + bits); // y is case, x=row, z=letter
        var newShape = shape.RotateY();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        Assert.AreEqual(4, newShape.w);
        Assert.AreEqual(2, newShape.h);
        Assert.AreEqual(3, newShape.d);
        var s = newShape.Serialize();
        //    Assert.AreEqual("ieaIEAjfbJFBkgcKGClhdLHD", s);
        Assert.AreEqual("4x2x3,***......***...***...***", s);

        newShape = newShape.RotateY().RotateY().RotateY();
        Assert.AreEqual("3x2x4," + bits, newShape.Serialize());
    }

    [TestMethod]
    public void TestRotateY_() {
        var starting = "3x2x4,abcdABCDefghEFGHijklIJKL";
        var ending = "4x2x3,ieaIEAjfbJFBkgcKGClhdLHD";
        TestOperation(starting, (s) => s.RotateY(), ending, (s) => s.RotateY().RotateY().RotateY());
    }

    [TestMethod]
    public void TestInPlaceRotateY2() {
        // alpha = "abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var bits = "*...*.***....*.***.*....*****......******...*.***.";
        var shape = new BitShape("5x2x5," + bits);
        var newShape = shape.RotateY2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("yxwvuYXWVUtsrqpTSRQPonmlkONMLKjihgfJIHGFedcbaEDCBA", s);
        Assert.AreEqual("5x2x5,*...*.***......******.....****.*...*.****...*.***.", s);

        Assert.AreEqual("5x2x5," + bits, shape.RotateY2().Serialize());
    }

    [TestMethod]
    public void TestInPlaceRotateY2_() {
        var starting = "5x2x5,abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var ending = "5x2x5,yxwvuYXWVUtsrqpTSRQPonmlkONMLKjihgfJIHGFedcbaEDCBA";
        TestOperation(starting, (s) => s.RotateY2(), ending, (s) => s.RotateY2());
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
        var shape = new BitShape("2x5x5," + bits);
        var newShape = shape.MirrorY(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE", s);
        Assert.AreEqual("2x5x5,*...*.........*...*.*...*.***.*********.***.*.***.", s);

        Assert.AreEqual("2x5x5," + bits, shape.MirrorY().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorY_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE";
        TestOperation(starting, (s) => s.MirrorY(), ending, (s) => s.MirrorY());
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
        var shape = new BitShape("2x5x5," + bits);
        var newShape = shape.MirrorYOpt(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE", s);
        Assert.AreEqual("2x5x5,*...*.........*...*.*...*.***.*********.***.*.***.", s);

        Assert.AreEqual("2x5x5," + bits, shape.MirrorYOpt().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorYOpt_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE";
        TestOperation(starting, (s) => s.MirrorYOpt(), ending, (s) => s.MirrorYOpt());
    }

    [TestMethod]
    public void TestInPlaceMirrorYOpt2() {
        // abcde    uvwxy
        // fghij    pqrst
        // klmno => klmno
        // pqrst    fghij
        // uvwxy    abcde
        // alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var bits = "*...*...*.....*.....*...*.***.***.*****.*****.***.";
        var shape = new BitShape("2x5x5," + bits);
        var newShape = shape.MirrorYOpt2(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE", s);
        Assert.AreEqual("2x5x5,*...*.........*...*.*...*.***.*********.***.*.***.", s);

        Assert.AreEqual("2x5x5," + bits, shape.MirrorYOpt2().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorYOpt2_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE";
        TestOperation(starting, (s) => s.MirrorYOpt2(), ending, (s) => s.MirrorYOpt2());
    }

    [TestMethod]
    public void TestInPlaceRotateZ() {
        // alpha = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyY";
        var bits = "*..*.*.**..*.*.**..*.*.*.*.**..*.*.*.*.**..*.*.**.";
        var shape = new BitShape("5x5x2," + bits);
        var newShape = shape.RotateZ(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("uUpPkKfFaAvVqQlLgGbBwWrRmMhHcCxXsSnNiIdDyYtToOjJeE", s);
        Assert.AreEqual("5x5x2,*..*.*.**..*.*.*.*.*.*.*.*.*.*.*.*.**..**..**..**.", s);

        shape.RotateZ().RotateZ().RotateZ();
        Assert.AreEqual("5x5x2," + bits, shape.Serialize());
    }

    [TestMethod]
    public void TestInPlaceRotateZ_() {
        var starting = "5x5x2,aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyY";
        var ending = "5x5x2,uUpPkKfFaAvVqQlLgGbBwWrRmMhHcCxXsSnNiIdDyYtToOjJeE";
        TestOperation(starting, (s) => s.RotateZ(), ending, (s) => s.RotateZ().RotateZ().RotateZ());
    }

    [TestMethod]
    public void TestRotateZ() {
        // alpha = "aAbBcCdDeEfFgGhHiIjJkKlL";
        var bits = "*..*.*.**..*.*.**..*.*.*";
        var shape = new BitShape("3x4x2," + bits);
        var newShape = shape.RotateZ();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        Assert.AreEqual(4, newShape.w);
        Assert.AreEqual(3, newShape.h);
        Assert.AreEqual(2, newShape.d);
        var s = newShape.Serialize();
        //    Assert.AreEqual("iIeEaAjJfFbBkKgGcClLhHdD", s);
        Assert.AreEqual("4x3x2,*.*.*..*.*.*.*.*.*.*.*.*", s);

        newShape = newShape.RotateZ().RotateZ().RotateZ();
        Assert.AreEqual("3x4x2," + bits, newShape.Serialize());
    }

    [TestMethod]
    public void TestRotateZ_() {
        var starting = "3x4x2,aAbBcCdDeEfFgGhHiIjJkKlL";
        var ending = "4x3x2,iIeEaAjJfFbBkKgGcClLhHdD";
        TestOperation(starting, (s) => s.RotateZ(), ending, (s) => s.RotateZ().RotateZ().RotateZ());
    }

    [TestMethod]
    public void TestInPlaceRotateZ2() {
        // alpha = "aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyY";
        var bits = "*..*.*.**..*.*.**..*.*.*.*.**..*.*.*.*.**..*.*.**.";
        var shape = new BitShape("5x5x2," + bits);
        var newShape = shape.RotateZ2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("yYxXwWvVuUtTsSrRqQpPoOnNmMlLkKjJiIhHgGfFeEdDcCbBaA", s);
        Assert.AreEqual("5x5x2,*..*.*.**..*.*.*.*.**..*.*.*.*.**..*.*.**..*.*.**.", s);

        Assert.AreEqual("5x5x2," + bits, shape.RotateZ2().Serialize());
    }

    [TestMethod]
    public void TestInPlaceRotateZ2_() {
        var starting = "5x5x2,aAbBcCdDeEfFgGhHiIjJkKlLmMnNoOpPqQrRsStTuUvVwWxXyY";
        var ending = "5x5x2,yYxXwWvVuUtTsSrRqQpPoOnNmMlLkKjJiIhHgGfFeEdDcCbBaA";
        TestOperation(starting, (s) => s.RotateZ2(), ending, (s) => s.RotateZ2());
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
        var shape = new BitShape("2x5x5," + bits);
        var newShape = shape.MirrorZOrg(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //       Assert.AreEqual("edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU", s);
        Assert.AreEqual(s, "2x5x5,*...*.*...*.........*...*.***.*.***.*********.***.");

        Assert.AreEqual("2x5x5," + bits, shape.MirrorZOrg().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorZ_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU";
        TestOperation(starting, (s) => s.MirrorZOrg(), ending, (s) => s.MirrorZOrg());
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
        var shape = new BitShape("2x5x5," + bits);
        var newShape = shape.MirrorZ(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //       Assert.AreEqual("edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU", s);
        Assert.AreEqual(s, "2x5x5,*...*.*...*.........*...*.***.*.***.*********.***.");

        Assert.AreEqual("2x5x5," + bits, shape.MirrorZ().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorZOpt_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU";
        TestOperation(starting, (s) => s.MirrorZ(), ending, (s) => s.MirrorZ());
    }

    [TestMethod]
    public void TestInPlaceMirrorZOpt2() {
        // abcde    edcba
        // fghij    jihgf
        // klmno => onmlk
        // pqrst    tsrqp
        // uvwxy    yxwvu
        // alpha = "abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var bits = "*...*...*.....*.....*...*.***.***.*****.*****.***.";
        var shape = new BitShape("2x5x5," + bits);
        var newShape = shape.MirrorZOpt2(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //       Assert.AreEqual("edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU", s);
        Assert.AreEqual(s, "2x5x5,*...*.*...*.........*...*.***.*.***.*********.***.");

        Assert.AreEqual("2x5x5," + bits, shape.MirrorZOpt2().Serialize());
    }

    [TestMethod]
    public void TestInPlaceMirrorZOpt2_() {
        var starting = "2x5x5,abcdefghijklmnopqrstuvwxyABCDEFGHIJKLMNOPQRSTUVWXY";
        var ending = "2x5x5,edcbajihgfonmlktsrqpyxwvuEDCBAJIHGFONMLKTSRQPYXWVU";
        TestOperation(starting, (s) => s.MirrorZOpt2(), ending, (s) => s.MirrorZOpt2());
    }

    [TestMethod]
    public void TestMinRotation() {
        var minRotation = "3x3x3,.........................**";
        for (int cx = 0; cx <= 2; cx += 2)
            for (int cy = 0; cy <= 2; cy += 2)
                for (int cz = 0; cz <= 2; cz += 2)
                    for (int x = 0; x < 3; x++)
                        for (int y = 0; y < 3; y++)
                            for (int z = 0; z < 3; z++) {
                                var shape = new BitShape(3, 3, 3);
                                shape[cx, cy, cz] = true;
                                if (!shape[x, y, z] && shape.HasSetNeighbor(x, y, z)) {
                                    shape[x, y, z] = true;
                                    Assert.AreEqual(minRotation, shape.MinRotation().Serialize());
                                }
                            }

    }

    [TestMethod]
    public void TestMinChiralRotation() {
        var minRotation = "3x3x3,......................*..**";
        for (int cx = 0; cx <= 2; cx += 2)
            for (int cy = 0; cy <= 2; cy += 2)
                for (int cz = 0; cz <= 2; cz += 2)
                    for (int x = 0; x < 3; x++)
                        for (int y = 0; y < 3; y++)
                            for (int z = 0; z < 3; z++) {
                                var shape = new BitShape(3, 3, 3);
                                shape[cx, cy, cz] = true;
                                if (!shape[x, y, z] && shape.HasSetNeighbor(x, y, z)) {
                                    shape[x, y, z] = true;
                                    if (shape.HasSetNeighbor(0, 1, 1)) {
                                        var newShape = new BitShape(shape);
                                        newShape[0, 1, 1] = true;
                                        Assert.AreEqual(minRotation, newShape.MinChiralRotation().Serialize());
                                    }
                                    if (shape.HasSetNeighbor(2, 1, 1)) {
                                        var newShape = new BitShape(shape);
                                        newShape[2, 1, 1] = true;
                                        Assert.AreEqual(minRotation, newShape.MinChiralRotation().Serialize());
                                    }
                                    if (shape.HasSetNeighbor(1, 0, 1)) {
                                        var newShape = new BitShape(shape);
                                        newShape[1, 0, 1] = true;
                                        Assert.AreEqual(minRotation, newShape.MinChiralRotation().Serialize());
                                    }
                                    if (shape.HasSetNeighbor(1, 2, 1)) {
                                        var newShape = new BitShape(shape);
                                        newShape[1, 2, 1] = true;
                                        Assert.AreEqual(minRotation, newShape.MinChiralRotation().Serialize());
                                    }
                                    if (shape.HasSetNeighbor(1, 1, 0)) {
                                        var newShape = new BitShape(shape);
                                        newShape[1, 1, 0] = true;
                                        Assert.AreEqual(minRotation, newShape.MinChiralRotation().Serialize());
                                    }
                                    if (shape.HasSetNeighbor(1, 1, 2)) {
                                        var newShape = new BitShape(shape);
                                        newShape[1, 1, 2] = true;
                                        Assert.AreEqual(minRotation, newShape.MinChiralRotation().Serialize());
                                    }
                                }
                            }
    }

    [TestMethod]
    public void TestHasSetNeighbor() {
        var shape = new BitShape(3, 3, 3);
        Assert.IsFalse(shape.HasSetNeighbor(1, 1, 1));
        shape[0, 1, 1] = true; Assert.IsTrue(shape.HasSetNeighbor(1, 1, 1)); shape[0, 1, 1] = false;
        shape[2, 1, 1] = true; Assert.IsTrue(shape.HasSetNeighbor(1, 1, 1)); shape[2, 1, 1] = false;
        shape[1, 0, 1] = true; Assert.IsTrue(shape.HasSetNeighbor(1, 1, 1)); shape[1, 0, 1] = false;
        shape[1, 2, 1] = true; Assert.IsTrue(shape.HasSetNeighbor(1, 1, 1)); shape[1, 2, 1] = false;
        shape[1, 1, 0] = true; Assert.IsTrue(shape.HasSetNeighbor(1, 1, 1)); shape[1, 1, 0] = false;
        shape[1, 1, 2] = true; Assert.IsTrue(shape.HasSetNeighbor(1, 1, 1)); shape[1, 1, 2] = false;

        Assert.IsFalse(shape.HasSetNeighbor(0, 1, 1));
        Assert.IsFalse(shape.HasSetNeighbor(2, 1, 1));
        Assert.IsFalse(shape.HasSetNeighbor(1, 0, 1));
        Assert.IsFalse(shape.HasSetNeighbor(1, 2, 1));
        Assert.IsFalse(shape.HasSetNeighbor(1, 1, 0));
        Assert.IsFalse(shape.HasSetNeighbor(1, 1, 2));

        shape[1, 1, 1] = true;
        Assert.IsTrue(shape.HasSetNeighbor(0, 1, 1));
        Assert.IsTrue(shape.HasSetNeighbor(2, 1, 1));
        Assert.IsTrue(shape.HasSetNeighbor(1, 0, 1));
        Assert.IsTrue(shape.HasSetNeighbor(1, 2, 1));
        Assert.IsTrue(shape.HasSetNeighbor(1, 1, 0));
        Assert.IsTrue(shape.HasSetNeighbor(1, 1, 2));
    }

    [TestMethod]
    public void TestShapeCounts() {
        var shape = new BitShape("1x1x1,*");
        Assert.AreEqual((1, 0, 0), shape.CornerEdgeFaceCount());
        shape = new BitShape("1x1x1,.");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCount());

        shape = new BitShape("2x2x2,********");
        Assert.AreEqual((8, 0, 0), shape.CornerEdgeFaceCount());
        shape = new BitShape("2x2x2,........");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCount());

        shape = new BitShape("3x3x3,***************************");
        Assert.AreEqual((8, 12, 6), shape.CornerEdgeFaceCount());
        shape = new BitShape("3x3x3,...........................");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCount());
    }

    [TestMethod]
    public void TestShapeCountsOrg() {
        var shape = new BitShape("1x1x1,*");
        Assert.AreEqual((1, 0, 0), shape.CornerEdgeFaceCountOrg());
        shape = new BitShape("1x1x1,.");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCountOrg());

        shape = new BitShape("2x2x2,********");
        Assert.AreEqual((8, 0, 0), shape.CornerEdgeFaceCountOrg());
        shape = new BitShape("2x2x2,........");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCountOrg());

        shape = new BitShape("3x3x3,***************************");
        Assert.AreEqual((8, 12, 6), shape.CornerEdgeFaceCountOrg());
        shape = new BitShape("3x3x3,...........................");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCountOrg());
    }

    [TestMethod]
    public void TestShapeCountsOpt() {
        var shape = new BitShape("1x1x1,*");
        Assert.AreEqual((1, 0, 0), shape.CornerEdgeFaceCountOpt());
        shape = new BitShape("1x1x1,.");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCountOpt());

        shape = new BitShape("2x2x2,********");
        Assert.AreEqual((8, 0, 0), shape.CornerEdgeFaceCountOpt());
        shape = new BitShape("2x2x2,........");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCountOpt());

        shape = new BitShape("3x3x3,***************************");
        Assert.AreEqual((8, 12, 6), shape.CornerEdgeFaceCountOpt());
        shape = new BitShape("3x3x3,...........................");
        Assert.AreEqual((0, 0, 0), shape.CornerEdgeFaceCountOpt());
    }

    [TestMethod]
    public void TestShapeCornerEdgeCounts() {
        var shape = new BitShape("1x1x1,*");
        Assert.AreEqual((1, 0), shape.CornerEdgeCount());
        shape = new BitShape("1x1x1,.");
        Assert.AreEqual((0, 0), shape.CornerEdgeCount());

        shape = new BitShape("2x2x2,********");
        Assert.AreEqual((8, 0), shape.CornerEdgeCount());
        shape = new BitShape("2x2x2,........");
        Assert.AreEqual((0, 0), shape.CornerEdgeCount());

        shape = new BitShape("3x3x3,***************************");
        Assert.AreEqual((8, 12), shape.CornerEdgeCount());
        shape = new BitShape("3x3x3,...........................");
        Assert.AreEqual((0, 0), shape.CornerEdgeCount());
    }


    [TestMethod]
    public void TestShapeCornerCount() {
        var shape = new BitShape("1x1x1,*");
        Assert.AreEqual(1, shape.CornerCount());
        shape = new BitShape("1x1x1,.");
        Assert.AreEqual(0, shape.CornerCount());

        shape = new BitShape("2x2x2,********");
        Assert.AreEqual(8, shape.CornerCount());
        shape = new BitShape("2x2x2,........");
        Assert.AreEqual(0, shape.CornerCount());

        shape = new BitShape("3x3x3,***************************");
        Assert.AreEqual(8, shape.CornerCount());
        shape = new BitShape("3x3x3,...........................");
        Assert.AreEqual(0, shape.CornerCount());
    }

    private void TestOperation(string starting, Func<BitShape, BitShape> op, string ending, Func<BitShape, BitShape> restore) {
        var startingSplit = starting.Split(',');
        Assert.AreEqual(2, startingSplit.Length);
        var endingSplit = ending.Split(',');
        Assert.AreEqual(2, endingSplit.Length);
        var startingWhd = startingSplit[0].Split('x');
        Assert.AreEqual(3, startingWhd.Length);
        var endingWhd = endingSplit[0].Split('x');
        Assert.AreEqual(3, endingWhd.Length);
        int sw = int.Parse(startingWhd[0]), sh = int.Parse(startingWhd[1]), sd = int.Parse(startingWhd[2]);
        int ew = int.Parse(endingWhd[0]), eh = int.Parse(endingWhd[1]), ed = int.Parse(endingWhd[2]);

        Assert.AreEqual(sw * sh * sd, ew * eh * ed);
        Assert.AreEqual(startingSplit[1].Length, sw * sh * sd);
        Assert.AreEqual(endingSplit[1].Length, ew * eh * ed);

        for (byte mask = 64; mask > 0; mask >>= 1) {
            var ca = new char[startingSplit[1].Length];
            for (int i = 0; i < ca.Length; i++) {
                char c = startingSplit[1][i];
                bool isSet = (byte)((byte)c & mask) != 0;
                ca[i] = isSet ? '*' : '.';
            }

            var shape = new BitShape(sw + "x" + sh + "x" + sd + "," + new string(ca));

            shape = op(shape);
            var s = shape.Serialize();

            for (int i = 0; i < ca.Length; i++) {
                char c = endingSplit[1][i];
                bool isSet = (byte)((byte)c & mask) != 0;
                ca[i] = isSet ? '*' : '.';
            }
            Assert.AreEqual(ew + "x" + eh + "x" + ed + "," + new string(ca), s);

            shape = restore(shape);
            s = shape.Serialize();
            for (int i = 0; i < ca.Length; i++) {
                char c = startingSplit[1][i];
                bool isSet = (byte)((byte)c & mask) != 0;
                ca[i] = isSet ? '*' : '.';
            }
            Assert.AreEqual(sw + "x" + sh + "x" + sd + "," + new string(ca), s);
        }
    }
}
