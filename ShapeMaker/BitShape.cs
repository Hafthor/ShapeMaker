using System;

namespace ShapeMaker;

/// <summary>
/// A series of extension methods to BitArray to support 3-D shape opertaions
/// </summary>
public static class BitShape {
    public static byte[] NewShape2(int w, int h, int d) {
        int sz = w * h * d + 11; // 11 reserved bits
        var bytes = new byte[(sz + 7) / 8];
        int r = sz % 8; // remainder bits in last byte
        // we store w, h and remainder in first 11 bits - done to minimize memory used
        bytes[0] = (byte)((w << 4) + h);
        bytes[1] = (byte)(r << 5);
        return bytes;
    }

    public static byte[] Copy(this byte[] bytes) {
        var copy = new byte[bytes.Length];
        Array.Copy(bytes, copy, bytes.Length);
        return copy;
    }

    public static byte[] Deserialize2(string s) {
        var ss = s.Split(',');
        if (ss.Length != 4) throw new ArgumentException("expected a four part string");
        int w = int.Parse(ss[0]), h = int.Parse(ss[1]), d = int.Parse(ss[2]);
        var chars = ss[3].Replace(" ", "").Replace("\n", "");
        if (chars.Length != w * h * d) throw new ArgumentException("expected string of len w*h*d");
        var shape = BitShape.NewShape2(w, h, d);
        int l = w * h * d;
        int di = 1;
        byte mask = 1 << 4;
        for (int i = 0; i < l; i++) {
            if (chars[i] == '*') shape[di] |= mask;
            if (mask == 1) { mask = 128; di++; } else mask >>= 1;
        }
        return shape;
    }

    public static string Serialize(this byte[] shape) {
        var (w, h, d) = shape.Dimensions();
        int l = w * h * d;
        var ca = new char[l];
        int ci = 0, bi = 1;
        byte mask = 1 << 4;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    ca[ci++] = (shape[bi] & mask) != 0 ? '*' : '.';
                    if (mask == 1) { mask = 128; bi++; } else mask >>= 1;
                }
        if (ci != ca.Length) throw new InvalidProgramException("miscalculated string length");
        return w + "," + h + "," + d + "," + new string(ca);
    }

    public static (int w, int h, int d) Dimensions(this byte[] shape) {
        byte wh = shape[0];
        int r = shape[1] >> 5;
        int l = shape.Length*8-11;
        if (r > 0) l = l - 8 + r;
        int w = wh >> 4, h = wh & 0xF, d = l / w / h;
        return (w, h, d);
    }

    private static int Index2(int x, int y, int z, int h, int d) => 11 + z + (y + x * h) * d;

    public static int Index(this byte[] bytes, int x, int y, int z) {
        var (_, h, d) = bytes.Dimensions();
        return Index2(x, y, z, h, d);
    }

    public static bool Get(this byte[] bytes, int x, int y, int z) {
        var bi = bytes.Index(x, y, z);
        byte mask = (byte)(1 << (7 - (bi % 8)));
        return (bytes[bi / 8] & mask) != 0;
    }

    private static bool Get(this byte[] bytes, int x, int y, int z, int h, int d) {
        var bi = Index2(x, y, z, h, d);
        byte mask = (byte)(1 << (7 - (bi % 8)));
        return (bytes[bi / 8] & mask) != 0;
    }

    public static void Set(this byte[] bytes, int x, int y, int z, bool v) {
        var bi = bytes.Index(x, y, z);
        byte mask = (byte)(1 << (7 - (bi % 8)));
        if (v) bytes[bi / 8] |= mask; else bytes[bi / 8] &= (byte)~mask;
    }

    private static void Set(this byte[] bytes, int x, int y, int z, int h, int d, bool v) {
        var bi = Index2(x, y, z, h, d);
        byte mask = (byte)(1 << (7 - (bi % 8)));
        if (v) bytes[bi / 8] |= mask; else bytes[bi / 8] &= (byte)~mask;
    }

    public static byte[] PadLeft(this byte[] bytes) {
        var (w, h, d) = bytes.Dimensions();
        return bytes.Copy(NewShape2(w + 1, h, d), 1, w, 0, h, 0, d);
    }

    public static byte[] PadRight(this byte[] bytes) {
        var (w, h, d) = bytes.Dimensions();
        return bytes.Copy(NewShape2(w + 1, h, d), 0, w, 0, h, 0, d);
    }

    public static byte[] PadTop(this byte[] bytes) {
        var (w, h, d) = bytes.Dimensions();
        return bytes.Copy(NewShape2(w, h + 1, d), 0, w, 1, h, 0, d);
    }

    public static byte[] PadBottom(this byte[] bytes) {
        var (w, h, d) = bytes.Dimensions();
        return bytes.Copy(NewShape2(w, h + 1, d), 0, w, 0, h, 0, d);
    }

    public static byte[] PadFront(this byte[] bytes) {
        var (w, h, d) = bytes.Dimensions();
        return bytes.Copy(NewShape2(w, h, d + 1), 0, w, 0, h, 1, d);
    }

    public static byte[] PadBack(this byte[] bytes) {
        var (w, h, d) = bytes.Dimensions();
        return bytes.Copy(NewShape2(w, h, d + 1), 0, w, 0, h, 0, d);
    }

    public static byte[] Copy(this byte[] src, byte[] dest, int xa, int w, int ya, int h, int za, int d) {
        var (_, dh, dd) = dest.Dimensions();
        var (_, sh, sd) = src.Dimensions();
        for (int x = 0, xd = xa; x < w; x++, xd++)
            for (int y = 0, yd = ya; y < h; y++, yd++)
                for (int z = 0, zd = za; z < d; z++, zd++)
                    dest.Set(xd, yd, zd, dh, dd, src.Get(x, y, z, sh, sd));
        return dest;
    }

    // returns shape rotated clockwise on X axis by 90º (swaps h,d)
    public static byte[] RotateX(this byte[] ba) {
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

        var newShape = NewShape2(w, d, h);
        for (int x = 0; x < w; x++)
            for (int y = 0, yn = h - 1; y < h; y++, yn--)
                for (int z = 0; z < d; z++)
                    newShape.Set(x, z, y, d, h, ba.Get(x, yn, z, h, d));

        return newShape;
    }

    // rotates in-place on X axis by 180º
    public static byte[] RotateX2(this byte[] ba) {
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
    public static byte[] MirrorX(this byte[] ba) {
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
    public static byte[] RotateY(this byte[] ba) {
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

        var newShape = NewShape2(d, h, w);
        for (int x = 0, xn = w - 1; x < w; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    newShape.Set(z, y, x, h, w, ba.Get(xn, y, z, h, d));

        return newShape;
    }

    // rotates in-place on Y axis by 180º
    public static byte[] RotateY2(this byte[] ba) {
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
    public static byte[] MirrorY(this byte[] ba) {
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
    public static byte[] RotateZ(this byte[] ba) {
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

        var newShape = NewShape2(h, w, d);
        for (int x = 0, xn = w - 1; x < w; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    newShape.Set(y, x, z, w, d, ba.Get(xn, y, z, h, d));

        return newShape;
    }

    // rotates in-place on Z axis by 180º
    public static byte[] RotateZ2(this byte[] ba) {
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
    public static byte[] MirrorZ(this byte[] ba) {
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
    public static byte[] MinDimension(this byte[] ba) {
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
    private static IEnumerable<byte[]> AllRotations(this byte[] ba) {
        var shape = ba.Copy();
        foreach (var s in ba.AllRotationsOfX()) yield return s;

        shape = shape.RotateY();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        foreach (var s in ba.Copy().RotateY2().AllRotationsOfX()) yield return s;

        shape = shape.RotateY2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = ba.Copy().RotateZ();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateZ2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;
    }

    // generates all 4 possible rotations on X axis of this shape
    private static IEnumerable<byte[]> AllRotationsOfX(this byte[] ba) {
        var (_, h, d) = ba.Dimensions();
        if (h == d) // rotates in-place but restores it
            for (int i = 0; i < 4; i++)
                yield return ba.RotateX();
        else {
            yield return ba;
            var shape90 = ba.RotateX();
            yield return shape90;
            yield return ba.Copy().RotateX2();
            yield return shape90.RotateX2();
        }
    }

    // returns the shape rotated such that it is the mimimum serialization of the shape
    public static byte[] MinRotation(this byte[] ba) {
        // a rubiks cube, for example, is a 3x3x3 shape
        // it can be resting on any of its six colors
        // and could have any of four sides facing you. 6*4=24

        // we will need to apply rotations to get all 24 combinations
        // and see which one compares to be the lowest

        byte[] best = null;
        foreach (var s in ba.AllRotations())
            if (best == null || best.CompareTo(s) > 0)
                best = s.Copy(); // must clone it to prevent it from being mutated in-place
        return best;
    }

    // generates all 8 possible 180º rotations of shape
    // note these are generated in-place on this shape
    public static IEnumerable<byte[]> All180DegreeRotations(this byte[] ba) {
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
    public static IEnumerable<byte[]> AllMirrors(this byte[] ba) {
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
    public static byte[] MinChiralRotation(this byte[] ba) {
        byte[] best = null;
        foreach (var s in ba.AllRotations())
            foreach (var ss in s.AllMirrors())
                if (best == null || best.CompareTo(ss) > 0)
                    best = ss.Copy(); // must clone it to prevent it from being mutated in-place
        return best;
    }

    // returns <0 if this is lower than other, 0 if equal, >0 if other is lower
    public static int CompareTo(this BitArray ba, BitArray other) {
        var (w, h, d) = ba.Dimensions();
        var (ow, oh, od) = other.Dimensions();
        int dw = w - ow; if (dw != 0) return dw;
        int dh = h - oh; if (dh != 0) return dh;
        int dd = d - od; if (dd != 0) return dd;

        for (int i = 8; i < ba.Length; i++)
            if (ba[i] != other[i])
                return ba[i] ? 1 : -1;

        return 0;
    }

    public static int CompareTo(this byte[] bytes, byte[] other) {
        var (w, h, d) = bytes.Dimensions();
        var (ow, oh, od) = other.Dimensions();
        int dw = w - ow; if (dw != 0) return dw;
        int dh = h - oh; if (dh != 0) return dh;
        int dd = d - od; if (dd != 0) return dd;
        for (int i = 1; i < bytes.Length; i++)
            if (bytes[i] != other[i])
                return bytes[i] - other[i];
        return 0;
    }
}
