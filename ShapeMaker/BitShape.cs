using System;
using System.Collections;

namespace ShapeMaker;

/// <summary>
/// A series of extension methods to BitArray to support 3-D shape opertaions
/// </summary>
public static class BitShape {
    public static BitArray NewShape(int w, int h, int d) {
        int sz = w * h * d;
        var ba = new BitArray(sz + 8); // 4-bits for w, 4-bits for h, d determined by size
        if (w > 15 || h > 15) throw new ArgumentException("values for w and h > 15 not supported");
        ba[0] = (w & 8) != 0;
        ba[1] = (w & 4) != 0;
        ba[2] = (w & 2) != 0;
        ba[3] = (w & 1) != 0;
        ba[4] = (h & 8) != 0;
        ba[5] = (h & 4) != 0;
        ba[6] = (h & 2) != 0;
        ba[7] = (h & 1) != 0;
        return ba;
    }

    public static BitArray Deserialize(string s) {
        var ss = s.Split(',');
        if (ss.Length != 4) throw new ArgumentException("expected a four part string");
        int w = int.Parse(ss[0]), h = int.Parse(ss[1]), d = int.Parse(ss[2]);
        var chars = ss[3].Replace(" ", "").Replace("\n", "");
        if (chars.Length != w * h * d) throw new ArgumentException("expected string of len w*h*d");
        var shape = BitShape.NewShape(w, h, d);
        int l = w * h * d;
        for (int i = 0; i < l; i++)
            shape[i + 8] = chars[i] == '*';
        return shape;
    }

    public static string Serialize(this BitArray shape) {
        var (w, h, d) = shape.Dimensions();
        int l = w * h * d;
        var ca = new char[l];
        int ci = 0, bi = 8;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    ca[ci++] = shape[bi++] ? '*' : '.';
        if (ci != ca.Length) throw new InvalidProgramException("miscalculated string length");
        return w + "," + h + "," + d + "," + new string(ca);
    }

    public static (int w, int h, int d) Dimensions(this BitArray ba) {
        int sz = ba.Length - 8;
        int w = (ba[0] ? 8 : 0) + (ba[1] ? 4 : 0) + (ba[2] ? 2 : 0) + (ba[3] ? 1 : 0);
        int h = (ba[4] ? 8 : 0) + (ba[5] ? 4 : 0) + (ba[6] ? 2 : 0) + (ba[7] ? 1 : 0);
        int d = sz / w / h;
        return (w, h, d);
    }

    private static int Index(int x, int y, int z, int h, int d) => 8 + z + (y + x * h) * d;

    public static int Index(this BitArray ba, int x, int y, int z) {
        var (_, h, d) = ba.Dimensions();
        return Index(x, y, z, h, d);
    }

    public static bool Get(this BitArray ba, int x, int y, int z) => ba[ba.Index(x, y, z)];

    private static bool Get(this BitArray ba, int x, int y, int z, int h, int d) => ba[Index(x, y, z, h, d)];

    public static bool Set(this BitArray ba, int x, int y, int z, bool v) => ba[ba.Index(x, y, z)] = v;

    private static bool Set(this BitArray ba, int x, int y, int z, int h, int d, bool v) => ba[Index(x, y, z, h, d)] = v;

    public static BitArray PadLeft(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        return ba.Copy(NewShape(w + 1, h, d), 1, w, 0, h, 0, d);
    }

    public static BitArray PadRight(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        return ba.Copy(NewShape(w + 1, h, d), 0, w, 0, h, 0, d);
    }

    public static BitArray PadTop(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        return ba.Copy(NewShape(w, h + 1, d), 0, w, 1, h, 0, d);
    }

    public static BitArray PadBottom(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        return ba.Copy(NewShape(w, h + 1, d), 0, w, 0, h, 0, d);
    }

    public static BitArray PadFront(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        return ba.Copy(NewShape(w, h, d + 1), 0, w, 0, h, 1, d);
    }

    public static BitArray PadBack(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        return ba.Copy(NewShape(w, h, d + 1), 0, w, 0, h, 0, d);
    }

    public static BitArray Copy(this BitArray src, BitArray dest, int xa, int w, int ya, int h, int za, int d) {
        var (_, dh, dd) = dest.Dimensions();
        var (_, sh, sd) = src.Dimensions();
        for (int x = 0, xd = xa; x < w; x++, xd++)
            for (int y = 0, yd = ya; y < h; y++, yd++)
                for (int z = 0, zd = za; z < d; z++, zd++)
                    dest.Set(xd, yd, zd, dh, dd, src.Get(x, y, z, sh, sd));
        return dest;
    }

    // returns shape rotated clockwise on X axis by 90º (swaps h,d)
    public static BitArray RotateX(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        if (d == h) { // rotate in-place
            int h2 = h / 2;
            for (int x = 0; x < w; x++)
                for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                    for (int z = y, zn = d - 1 - y, zm = zn; z < zm; z++, zn--) {
                        var t = ba.Get(x, y, z);
                        ba.Set(x, y, z, h, d, ba.Get(x, zn, y, h, d));
                        ba.Set(x, zn, y, h, d, ba.Get(x, yn, zn, h, d));
                        ba.Set(x, yn, zn, h, d, ba.Get(x, z, yn, h, d));
                        ba.Set(x, z, yn, h, d, t);
                    }
            return ba;
        }

        var newShape = NewShape(w, d, h);
        for (int x = 0; x < w; x++)
            for (int y = 0, yn = h - 1; y < h; y++, yn--)
                for (int z = 0; z < d; z++)
                    newShape.Set(x, z, y, d, h, ba.Get(x, yn, z, h, d));

        return newShape;
    }

    // rotates in-place on X axis by 180º
    public static BitArray RotateX2(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        int h2 = h / 2;
        for (int x = 0; x < w; x++) {
            for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                for (int z = 0, zn = d - 1; z < d; z++, zn--) {
                    var t = ba.Get(x, yn, zn, h, d);
                    ba.Set(x, yn, zn, h, d, ba.Get(x, y, z, h, d));
                    ba.Set(x, y, z, h, d, t);
                }
            if (h % 2 == 1)
                for (int z = 0, zn = d - 1; z < d / 2; z++, zn--) {
                    var t = ba.Get(x, h2, zn, h, d);
                    ba.Set(x, h2, zn, h, d, ba.Get(x, h2, z, h, d));
                    ba.Set(x, h2, z, h, d, t);
                }
        }
        return ba;
    }

    // mirrors in-place
    public static BitArray MirrorX(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        int w2 = w / 2;
        for (int x = 0, xn = w - 1; x < w2; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    var t = ba.Get(x, y, z, h, d);
                    ba.Set(x, y, z, h, d, ba.Get(xn, y, z, h, d));
                    ba.Set(xn, y, z, h, d, t);
                }
        return ba;
    }

    // returns shape rotated clockwise on Y axis by 90º (swaps w,d)
    public static BitArray RotateY(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        if (w == d) { // rotate in-place
            int d2 = d / 2;
            for (int y = 0; y < h; y++)
                for (int z = 0, zn = d - 1; z < d2; z++, zn--)
                    for (int x = z, xn = w - 1 - z, xm = xn; x < xm; x++, xn--) {
                        var t = ba.Get(x, y, z, h, d);
                        ba.Set(x, y, z, h, d, ba.Get(zn, y, x, h, d));
                        ba.Set(zn, y, x, h, d, ba.Get(xn, y, zn, h, d));
                        ba.Set(xn, y, zn, h, d, ba.Get(z, y, xn, h, d));
                        ba.Set(z, y, xn, h, d, t);
                    }
            return ba;
        }

        var newShape = NewShape(d, h, w);
        for (int x = 0, xn = w - 1; x < w; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    newShape.Set(z, y, x, h, w, ba.Get(xn, y, z, h, d));

        return newShape;
    }

    // rotates in-place on Y axis by 180º
    public static BitArray RotateY2(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        int w2 = w / 2;
        for (int y = 0; y < h; y++) {
            for (int x = 0, xn = w - 1; x < w2; x++, xn--)
                for (int z = 0, zn = d - 1; z < d; z++, zn--) {
                    var t = ba.Get(xn, y, zn, h, d);
                    ba.Set(xn, y, zn, h, d, ba.Get(x, y, z, h, d));
                    ba.Set(x, y, z, h, d, t);
                }
            if (w % 2 == 1)
                for (int z = 0, zn = d - 1; z < d / 2; z++, zn--) {
                    var t = ba.Get(w2, y, zn, h, d);
                    ba.Set(w2, y, zn, h, d, ba.Get(w2, y, z, h, d));
                    ba.Set(w2, y, z, h, d, t);
                }
        }
        return ba;
    }

    // mirrors in-place
    public static BitArray MirrorY(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        int h2 = h / 2;
        for (int x = 0; x < w; x++)
            for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                for (int z = 0; z < d; z++) {
                    var t = ba.Get(x, y, z, h, d);
                    ba.Set(x, y, z, h, d, ba.Get(x, yn, z, h, d));
                    ba.Set(x, yn, z, h, d, t);
                }
        return ba;
    }

    // returns shape rotated clockwise on Z axis by 90º (swaps w,h)
    public static BitArray RotateZ(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        if (w == h) { // rotate in-place
            int h2 = h / 2;
            for (int z = 0; z < d; z++)
                for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                    for (int x = y, xn = w - 1 - y, xm = xn; x < xm; x++, xn--) {
                        var t = ba.Get(x, y, z, h, d);
                        ba.Set(x, y, z, h, d, ba.Get(yn, x, z, h, d));
                        ba.Set(yn, x, z, h, d, ba.Get(xn, yn, z, h, d));
                        ba.Set(xn, yn, z, h, d, ba.Get(y, xn, z, h, d));
                        ba.Set(y, xn, z, h, d, t);
                    }
            return ba;
        }

        var newShape = NewShape(h, w, d);
        for (int x = 0, xn = w - 1; x < w; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    newShape.Set(y, x, z, w, d, ba.Get(xn, y, z, h, d));

        return newShape;
    }

    // rotates in-place on Z axis by 180º
    public static BitArray RotateZ2(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        int w2 = w / 2;
        for (int z = 0; z < d; z++) {
            for (int x = 0, xn = w - 1; x < w2; x++, xn--)
                for (int y = 0, yn = h - 1; y < h; y++, yn--) {
                    var t = ba.Get(xn, yn, z, h, d);
                    ba.Set(xn, yn, z, h, d, ba.Get(x, y, z, h, d));
                    ba.Set(x, y, z, h, d, t);
                }
            if (w % 2 == 1)
                for (int y = 0, yn = h - 1; y < h / 2; y++, yn--) {
                    var t = ba.Get(w2, yn, z, h, d);
                    ba.Set(w2, yn, z, h, d, ba.Get(w2, y, z, h, d));
                    ba.Set(w2, y, z, h, d, t);
                }
        }
        return ba;
    }

    // mirrors in-place
    public static BitArray MirrorZ(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        int d2 = d / 2;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0, zn = d - 1; z < d2; z++, zn--) {
                    var t = ba.Get(x, y, z, h, d);
                    ba.Set(x, y, z, h, d, ba.Get(x, y, zn, h, d));
                    ba.Set(x, y, zn, h, d, t);
                }
        return ba;
    }

    // returns the shape rotated such that the dimensions are narrowest in the X and broadest in the Z
    public static BitArray MinDimension(this BitArray ba) {
        var (w, h, d) = ba.Dimensions();
        if (w <= h && h <= d)
            return ba;

        // w,h,d possibilities
        // 1,2,3 => fine
        // 1,3,2 => swap h,d
        // 2,1,3 => swap w,h
        // 2,3,1 => swap h,d swap w,h
        // 3,1,2 => swap w,h swap h,d
        // 3,2,1 => swap w,d

        // we need to rotate
        if (w <= h && w <= d && h > d)
            return ba.RotateX(); // swaps h,d
        if (w <= d && h <= d && w > h)
            return ba.RotateY(); // swaps w,h
        if (w > h && h > d)
            return ba.RotateZ(); // swaps w,d
        if (h > w && w > d)
            return ba.RotateX().RotateY();
        if (w > h && w > d && h <= d)
            return ba.RotateY().RotateX();

        throw new ApplicationException("not sure how we got here");
    }

    // generates all 24 possible rotations of this shape
    private static IEnumerable<BitArray> AllRotations(this BitArray ba) {
        var shape = new BitArray(ba);
        foreach (var s in ba.AllRotationsOfX()) yield return s;

        shape = shape.RotateY();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        foreach (var s in new BitArray(ba).RotateY2().AllRotationsOfX()) yield return s;

        shape = shape.RotateY2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = new BitArray(ba).RotateZ();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateZ2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;
    }

    // generates all 4 possible rotations on X axis of this shape
    private static IEnumerable<BitArray> AllRotationsOfX(this BitArray ba) {
        var (_, h, d) = ba.Dimensions();
        if (h == d) // rotates in-place but restores it
            for (int i = 0; i < 4; i++)
                yield return ba.RotateX();
        else {
            yield return ba;
            var shape90 = ba.RotateX();
            yield return shape90;
            yield return new BitArray(ba).RotateX2();
            yield return shape90.RotateX2();
        }
    }

    // returns the shape rotated such that it is the mimimum serialization of the shape
    public static BitArray MinRotation(this BitArray ba) {
        // a rubiks cube, for example, is a 3x3x3 shape
        // it can be resting on any of its six colors
        // and could have any of four sides facing you. 6*4=24

        // we will need to apply rotations to get all 24 combinations
        // and see which one compares to be the lowest

        BitArray best = null;
        foreach (var s in ba.AllRotations())
            if (best == null || best.CompareTo(s) > 0)
                best = new BitArray(s); // must clone it to prevent it from being mutated in-place
        return best;
    }

    // generates all 8 possible 180º rotations of shape
    // note these are generated in-place on this shape
    public static IEnumerable<BitArray> All180DegreeRotations(this BitArray ba) {
        yield return ba;
        yield return ba.RotateX2();
        yield return ba.RotateY2();
        yield return ba.RotateX2();
        yield return ba.RotateZ2();
        yield return ba.RotateX2();
        yield return ba.RotateY2();
        yield return ba.RotateX2();
        ba.RotateZ2(); // restore
    }

    // generate all 8 possible mirrorings of shape
    // note these are generated in-place on this shape
    public static IEnumerable<BitArray> AllMirrors(this BitArray ba) {
        yield return ba;
        yield return ba.MirrorX();
        yield return ba.MirrorY();
        yield return ba.MirrorX();
        yield return ba.MirrorZ();
        yield return ba.MirrorX();
        yield return ba.MirrorY();
        yield return ba.MirrorX();
        ba.MirrorZ(); // restore
    }

    // check all rotations for each mirrored possibility (8)
    // note that we do this by rotations first, since mirrors
    // mutate object in-place and should be more cache friendly
    // to do on the inside loop.
    public static BitArray MinChiralRotation(this BitArray ba) {
        BitArray best = null;
        foreach (var s in ba.AllRotations())
            foreach (var ss in s.AllMirrors())
                if (best == null || best.CompareTo(ss) > 0)
                    best = new BitArray(ss); // must clone it to prevent it from being mutated in-place
        return best;
    }

    // returns <0 if this is lower than other, 0 if equal, >0 if other is lower
    public static int CompareTo(this BitArray ba, BitArray other) {
        int dl = ba.Length - other.Length;
        if (dl != 0) return dl;

        for (int i = 0; i < ba.Length; i++)
            if (ba[i] != other[i])
                return ba[i] ? 1 : -1;

        return 0;
    }
}
