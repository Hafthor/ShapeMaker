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
    }
    
    [TestMethod]
    public void TestSerializeDeserialize() {
        var shape = Deserialize("1,1,1,*");
        var (w, h, d) = shape.Dimensions();
        Assert.AreEqual(1, w);
        Assert.AreEqual(1, h);
        Assert.AreEqual(1, d);
        Assert.IsTrue(shape.Get(0, 0, 0));

        shape = Deserialize("3,3,3,*** *** ***\n*** *.* ***\n*** *** ***");
        (w, h, d) = shape.Dimensions();
        Assert.AreEqual(3, w);
        Assert.AreEqual(3, h);
        Assert.AreEqual(3, d);

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    Assert.AreEqual(x != 1 || y != 1 || z != 1, shape.Get(x, y, z));

        var s = SerializeShape(shape);
        Assert.AreEqual("3,3,3,*** *** ***\n*** *.* ***\n*** *** ***".Replace(" ", "").Replace("\n", ""), s);
    }
    
    [TestMethod]
    public void TestPadLeft() {
        var shape = BitShape.NewShape(1, 1, 1);
        shape.Set(0, 0, 0, true);
        shape = shape.PadLeft();
        var (w, h, d) = shape.Dimensions();
        Assert.AreEqual(2, w);
        Assert.AreEqual(1, h);
        Assert.AreEqual(1, d);
        Assert.IsFalse(shape.Get(0, 0, 0));
        Assert.IsTrue(shape.Get(1, 0, 0));

        Assert.AreEqual("2,1,1,.*", SerializeShape(shape));
    }

    [TestMethod]
    public void TestPadRight() {
        var shape = BitShape.NewShape(1, 1, 1);
        shape.Set(0, 0, 0, true);
        shape = shape.PadRight();
        var (w, h, d) = shape.Dimensions();
        Assert.AreEqual(2, w);
        Assert.AreEqual(1, h);
        Assert.AreEqual(1, d);
        Assert.IsFalse(shape.Get(1, 0, 0));
        Assert.IsTrue(shape.Get(0, 0, 0));

        Assert.AreEqual("2,1,1,*.", SerializeShape(shape));
    }

    [TestMethod]
    public void TestPadTop() {
        var shape = BitShape.NewShape(1, 1, 1);
        shape.Set(0, 0, 0, true);
        shape = shape.PadTop();
        var (w, h, d) = shape.Dimensions();
        Assert.AreEqual(1, w);
        Assert.AreEqual(2, h);
        Assert.AreEqual(1, d);
        Assert.IsFalse(shape.Get(0, 0, 0));
        Assert.IsTrue(shape.Get(0, 1, 0));

        Assert.AreEqual("1,2,1,.*", SerializeShape(shape));
    }

    [TestMethod]
    public void TestPadBottom() {
        var shape = BitShape.NewShape(1, 1, 1);
        shape.Set(0, 0, 0, true);
        shape = shape.PadBottom();
        var (w, h, d) = shape.Dimensions();
        Assert.AreEqual(1, w);
        Assert.AreEqual(2, h);
        Assert.AreEqual(1, d);
        Assert.IsTrue(shape.Get(0, 0, 0));
        Assert.IsFalse(shape.Get(0, 1, 0));

        Assert.AreEqual("1,2,1,*.", SerializeShape(shape));
    }

    [TestMethod]
    public void TestPadFront() {
        var shape = BitShape.NewShape(1, 1, 1);
        shape.Set(0, 0, 0, true);
        shape = shape.PadFront();
        var (w, h, d) = shape.Dimensions();
        Assert.AreEqual(1, w);
        Assert.AreEqual(1, h);
        Assert.AreEqual(2, d);
        Assert.IsFalse(shape.Get(0, 0, 0));
        Assert.IsTrue(shape.Get(0, 0, 1));

        Assert.AreEqual("1,1,2,.*", SerializeShape(shape));
    }

    [TestMethod]
    public void TestPadBack() {
        var shape = BitShape.NewShape(1, 1, 1);
        shape.Set(0, 0, 0, true);
        shape = shape.PadBack();
        var (w, h, d) = shape.Dimensions();
        Assert.AreEqual(1, w);
        Assert.AreEqual(1, h);
        Assert.AreEqual(2, d);
        Assert.IsTrue(shape.Get(0, 0, 0));
        Assert.IsFalse(shape.Get(0, 0, 1));

        Assert.AreEqual("1,1,2,*.", SerializeShape(shape));
    }
    
    [TestMethod]
    public void TestInPlaceRotateXMini() {
        var shape = Deserialize("1,3,3,.** .** ...");
        var newShape = shape.RotateX(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.AreEqual("1,3,3,... .** .**".Replace(" ", ""), SerializeShape(shape));

        shape.RotateX().RotateX().RotateX();
        Assert.AreEqual("1,3,3,.** .** ...".Replace(" ", ""), SerializeShape(shape));
    }

    [TestMethod]
    public void TestRotateX() {
        // abcd    iea
        // efgh => jfb
        // ijkl    kgc
        //         lhd
        var shape = BitShape.NewShape(2, 3, 4);
        shape.Set(0, 0, 0, true);
        shape.Set(1, 1, 1, true);
        var newShape = shape.RotateX();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        var (w, h, d) = newShape.Dimensions();
        Assert.AreEqual(2, w);
        Assert.AreEqual(4, h);
        Assert.AreEqual(3, d);
        // Assert.IsTrue(shape.Get(0, 0, 2)); //why didn't this work?
        Assert.IsTrue(shape.Get(1, 1, 1));

        newShape = newShape.RotateX().RotateX().RotateX();
        (w, h, d) = newShape.Dimensions();
        Assert.AreEqual(2, w);
        Assert.AreEqual(3, h);
        Assert.AreEqual(4, d);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 3; y++)
                for (int z = 0; z < 4; z++)
                    Assert.AreEqual(x == y && y == z, newShape.Get(x, y, z));
    }

    [TestMethod]
    public void TestInPlaceRotateX2() {
        // abcde    yxwvu
        // fghij    tsrqp
        // klmno => onmlk
        // pqrst    jihgf
        // uvwxy    edcba
        var shape = BitShape.NewShape(2, 5, 5);
        shape.Set(0, 0, 0, true);
        shape.Set(1, 1, 1, true);
        var newShape = shape.RotateX2(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape.Get(0, 4, 4));
        Assert.IsTrue(shape.Get(1, 3, 3));

        shape.RotateX2();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape.Get(x, y, z));
    }

    [TestMethod]
    public void TestInPlaceMirrorX() {
        var shape = BitShape.NewShape(2, 5, 5);
        shape.Set(0, 0, 0, true);
        shape.Set(1, 1, 1, true);
        var newShape = shape.MirrorX(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));

        Assert.IsTrue(shape.Get(0, 1, 1));
        Assert.IsTrue(shape.Get(1, 0, 0));

        shape.MirrorX();
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 5; y++)
                for (int z = 0; z < 5; z++)
                    Assert.AreEqual(x == y && y == z, newShape.Get(x, y, z));
    }

    [TestMethod]
    public void TestInPlaceRotateY() {
        // alpha = "abcdeABCDEfghijFGHIJklmnoKLMNOpqrstPQRSTuvwxyUVWXY";
        var bits = "*...*.***....*.***.*....*****......******...*.***."; // vowels,CONSONANTS
        var shape = Deserialize("5,2,5," + bits);
        var newShape = shape.RotateY(); // should rotate in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = SerializeShape(shape);
        //    Assert.AreEqual("upkfaUPKFAvqlgbVQLGBwrmhcWRMHCxsnidXSNIDytojeYTOJE", s);
        Assert.AreEqual("5,2,5,*...*.***......*****.....*****...*.***.**.*.*.*.*.", s);

        shape.RotateY().RotateY().RotateY();
        Assert.AreEqual("5,2,5," + bits, SerializeShape(shape).Replace(" ", "").Replace("\n", ""));
    }

    [TestMethod]
    public void TestRotateY() {
        // alpha = "abcdABCDefghEFGHijklIJKL";
        var bits = "*....****....****....***"; // vowels,CONSONANTS
        var shape = Deserialize("3,2,4," + bits); // y is case, x=row, z=letter
        var newShape = shape.RotateY();
        Assert.IsFalse(object.ReferenceEquals(shape, newShape));
        var (w, h, d) = newShape.Dimensions();
        Assert.AreEqual(4, w);
        Assert.AreEqual(2, h);
        Assert.AreEqual(3, d);
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
        var shape = Deserialize("5,2,5," + bits);
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
        var shape = Deserialize("2,5,5," + bits);
        var newShape = shape.MirrorY(); // should mirror in-place
        Assert.IsTrue(object.ReferenceEquals(shape, newShape));
        var s = shape.Serialize();
        //    Assert.AreEqual("uvwxypqrstklmnofghijabcdeUVWXYPQRSTKLMNOFGHIJABCDE", s);
        Assert.AreEqual("2,5,5,*...*.........*...*.*...*.***.*********.***.*.***.", s);

        Assert.AreEqual("2,5,5," + bits, shape.MirrorY().Serialize());
    }

    /*
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
    */

    private byte[] Deserialize(string s) {
        var ss = s.Split(',');
        if (ss.Length != 4) throw new ArgumentException("expected a four part string");
        int w = int.Parse(ss[0]), h = int.Parse(ss[1]), d = int.Parse(ss[2]);
        var chars = ss[3].Replace(" ", "").Replace("\n", "");
        if (chars.Length != w * h * d) throw new ArgumentException("expected string of len w*h*d");
        var shape = BitShape.NewShape(w, h, d);
        int l = w * h * d;
        byte mask = 1 << 4;
        int di = 1;
        for (int i = 0; i < l; i++) {
            if (chars[i] == '*')
                shape[di] |= mask;
            if (mask == 1) { mask = 1 << 7; di++; } else mask >>= 1;
        }
        return shape;
    }

    private string SerializeShape(byte[] shape) {
        var (w, h, d) = shape.Dimensions();
        int l = w * h * d;
        var ca = new char[l];
        int ci = 0, si = 1;
        byte mask = 1 << 4;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    ca[ci++] = (shape[si] & mask) != 0 ? '*' : '.';
                    if (mask == 1) { mask = 1 << 7; si++; } else mask >>= 1;
                }
        if (ci != ca.Length) throw new InvalidProgramException("miscalculated string length");
        return w + "," + h + "," + d + "," + new string(ca);
    }
}
