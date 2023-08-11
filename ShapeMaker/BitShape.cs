namespace ShapeMaker;

/// <summary>
/// A series of extension methods to byte[] to support 3-D shape opertaions
/// </summary>
public class BitShape {
    public readonly byte w, h, d;
    public readonly byte[] bytes;

    public BitShape(byte w, byte h, byte d) {
        this.w = w;
        this.h = h;
        this.d = d;
        int sz = w * h * d;
        bytes = new byte[(sz + 7) / 8];
    }

    public BitShape(byte w, byte h, byte d, byte[] bytes) {
        this.w = w;
        this.h = h;
        this.d = d;
        this.bytes = bytes;
    }

    public BitShape(BitShape shape) {
        this.w = shape.w;
        this.h = shape.h;
        this.d = shape.d;
        this.bytes = new byte[shape.bytes.Length];
        Array.Copy(shape.bytes, bytes, bytes.Length);
    }

    public BitShape(string s) {
        var ss = s.Split(',');
        if (ss.Length != 4) throw new ArgumentException("expected a four part string");
        this.w = byte.Parse(ss[0]);
        this.h = byte.Parse(ss[1]);
        this.d = byte.Parse(ss[2]);
        var chars = ss[3].Replace(" ", "").Replace("\n", "");
        if (chars.Length != w * h * d) throw new ArgumentException("expected string of len w*h*d");
        int sz = w * h * d;
        this.bytes = new byte[(sz + 7) / 8];
        int di = 0;
        byte mask = 128;
        for (int i = 0; i < sz; i++) {
            if (chars[i] == '*') bytes[di] |= mask;
            if (mask == 1) { mask = 128; di++; } else mask >>= 1;
        }
    }

    public string Serialize() {
        int l = w * h * d;
        var ca = new char[l];
        int ci = 0, bi = 1;
        byte mask = 1 << 4;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    ca[ci++] = (bytes[bi] & mask) != 0 ? '*' : '.';
                    if (mask == 1) { mask = 128; bi++; } else mask >>= 1;
                }
        if (ci != ca.Length) throw new InvalidProgramException("miscalculated string length");
        return w + "," + h + "," + d + "," + new string(ca);
    }

    public int Index(int x, int y, int z) => z + (y + x * h) * d;

    public bool Get(int x, int y, int z) {
        var bi = Index(x, y, z);
        byte mask = (byte)(1 << (7 - (bi % 8)));
        return (bytes[bi / 8] & mask) != 0;
    }

    public void Set(int x, int y, int z, bool v) {
        var bi = Index(x, y, z);
        byte mask = (byte)(1 << (7 - (bi % 8)));
        if (v) bytes[bi / 8] |= mask; else bytes[bi / 8] &= (byte)~mask;
    }

    public BitShape PadLeft() {
        return Copy(new BitShape((byte)(w + 1), h, d), 1, w, 0, h, 0, d);
    }

    public BitShape PadRight() {
        return Copy(new BitShape((byte)(w + 1), h, d), 0, w, 0, h, 0, d);
    }

    public BitShape PadTop() {
        return Copy(new BitShape(w, (byte)(h + 1), d), 0, w, 1, h, 0, d);
    }

    public BitShape PadBottom() {
        return Copy(new BitShape(w, (byte)(h + 1), d), 0, w, 0, h, 0, d);
    }

    public BitShape PadFront() {
        return Copy(new BitShape(w, h, (byte)(d + 1)), 0, w, 0, h, 1, d);
    }

    public BitShape PadBack() {
        return Copy(new BitShape(w, h, (byte)(d + 1)), 0, w, 0, h, 0, d);
    }

    public BitShape Copy(BitShape dest, int xa, int w, int ya, int h, int za, int d) {
        for (int x = 0, xd = xa; x < w; x++, xd++)
            for (int y = 0, yd = ya; y < h; y++, yd++)
                for (int z = 0, zd = za; z < d; z++, zd++)
                    dest.Set(xd, yd, zd, Get(x, y, z));
        return dest;
    }

    // returns shape rotated clockwise on X axis by 90º (swaps h,d)
    public BitShape RotateX() {
        if (d == h) { // rotate in-place
            int h2 = h / 2;
            for (int x = 0; x < w; x++)
                for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                    for (int z = y, zn = d - 1 - y, zm = zn; z < zm; z++, zn--) {
                        var t = Get(x, y, z);
                        Set(x, y, z, Get(x, zn, y));
                        Set(x, zn, y, Get(x, yn, zn));
                        Set(x, yn, zn, Get(x, z, yn));
                        Set(x, z, yn, t);
                    }
            return this;
        }

        var newShape = new BitShape(w, d, h);
        for (int x = 0; x < w; x++)
            for (int y = 0, yn = h - 1; y < h; y++, yn--)
                for (int z = 0; z < d; z++)
                    newShape.Set(x, z, y, Get(x, yn, z));

        return newShape;
    }

    // rotates in-place on X axis by 180º
    public BitShape RotateX2() {
        int h2 = h / 2;
        for (int x = 0; x < w; x++) {
            for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                for (int z = 0, zn = d - 1; z < d; z++, zn--) {
                    var t = Get(x, yn, zn);
                    Set(x, yn, zn, Get(x, y, z));
                    Set(x, y, z, t);
                }
            if (h % 2 == 1)
                for (int z = 0, zn = d - 1; z < d / 2; z++, zn--) {
                    var t = Get(x, h2, zn);
                    Set(x, h2, zn, Get(x, h2, z));
                    Set(x, h2, z, t);
                }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorX() {
        int w2 = w / 2;
        for (int x = 0, xn = w - 1; x < w2; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    var t = Get(x, y, z);
                    Set(x, y, z, Get(xn, y, z));
                    Set(xn, y, z, t);
                }
        return this;
    }

    // returns shape rotated clockwise on Y axis by 90º (swaps w,d)
    public BitShape RotateY() {
        if (w == d) { // rotate in-place
            int d2 = d / 2;
            for (int y = 0; y < h; y++)
                for (int z = 0, zn = d - 1; z < d2; z++, zn--)
                    for (int x = z, xn = w - 1 - z, xm = xn; x < xm; x++, xn--) {
                        var t = Get(x, y, z);
                        Set(x, y, z, Get(zn, y, x));
                        Set(zn, y, x, Get(xn, y, zn));
                        Set(xn, y, zn, Get(z, y, xn));
                        Set(z, y, xn, t);
                    }
            return this;
        }

        var newShape = new BitShape(d, h, w);
        for (int x = 0, xn = w - 1; x < w; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    newShape.Set(z, y, x, Get(xn, y, z));

        return newShape;
    }

    // rotates in-place on Y axis by 180º
    public BitShape RotateY2() {
        int w2 = w / 2;
        for (int y = 0; y < h; y++) {
            for (int x = 0, xn = w - 1; x < w2; x++, xn--)
                for (int z = 0, zn = d - 1; z < d; z++, zn--) {
                    var t = Get(xn, y, zn);
                    Set(xn, y, zn, Get(x, y, z));
                    Set(x, y, z, t);
                }
            if (w % 2 == 1)
                for (int z = 0, zn = d - 1; z < d / 2; z++, zn--) {
                    var t = Get(w2, y, zn);
                    Set(w2, y, zn, Get(w2, y, z));
                    Set(w2, y, z, t);
                }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorY() {
        int h2 = h / 2;
        for (int x = 0; x < w; x++)
            for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                for (int z = 0; z < d; z++) {
                    var t = Get(x, y, z);
                    Set(x, y, z, Get(x, yn, z));
                    Set(x, yn, z, t);
                }
        return this;
    }

    // returns shape rotated clockwise on Z axis by 90º (swaps w,h)
    public BitShape RotateZ() {
        if (w == h) { // rotate in-place
            int h2 = h / 2;
            for (int z = 0; z < d; z++)
                for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                    for (int x = y, xn = w - 1 - y, xm = xn; x < xm; x++, xn--) {
                        var t = Get(x, y, z);
                        Set(x, y, z, Get(yn, x, z));
                        Set(yn, x, z, Get(xn, yn, z));
                        Set(xn, yn, z, Get(y, xn, z));
                        Set(y, xn, z, t);
                    }
            return this;
        }

        var newShape = new BitShape(h, w, d);
        for (int x = 0, xn = w - 1; x < w; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    newShape.Set(y, x, z, Get(xn, y, z));

        return newShape;
    }

    // rotates in-place on Z axis by 180º
    public BitShape RotateZ2() {
        int w2 = w / 2;
        for (int z = 0; z < d; z++) {
            for (int x = 0, xn = w - 1; x < w2; x++, xn--)
                for (int y = 0, yn = h - 1; y < h; y++, yn--) {
                    var t = Get(xn, yn, z);
                    Set(xn, yn, z, Get(x, y, z));
                    Set(x, y, z, t);
                }
            if (w % 2 == 1)
                for (int y = 0, yn = h - 1; y < h / 2; y++, yn--) {
                    var t = Get(w2, yn, z);
                    Set(w2, yn, z, Get(w2, y, z));
                    Set(w2, y, z, t);
                }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorZ() {
        int d2 = d / 2;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0, zn = d - 1; z < d2; z++, zn--) {
                    var t = Get(x, y, z);
                    Set(x, y, z, Get(x, y, zn));
                    Set(x, y, zn, t);
                }
        return this;
    }

    // not working yet :(
    private IEnumerable<BitShape> AllMinRotations() {
        if (w == h && h == d)
            return All24Rotations();

        BitShape newShape = this;
        if (w > h || h > d) {
            if (w == h) {
                if (w > d) newShape = RotateY(); else throw new ApplicationException("how did we end up here?");
            } else if (h == d) {
                if (w > d) newShape = RotateY(); else throw new ApplicationException("how did we end up here?");
            } else if (w == d) {
                if (w > h) newShape = RotateZ(); else newShape = RotateX();
            } else {
                if (w < h && h < d)
                    throw new ApplicationException("how did we end up here?"); // 1,2,3 - no rotation
                else if (w < d && d < h) {
                    newShape = RotateX(); // 1,3,2 - x
                } else if (d < h && h < w) {
                    newShape = RotateY(); // 3,2,1 - y
                } else if (d < w && w < h) {
                    newShape = RotateX().RotateY(); // 3,1,2 - xy
                } else if (h < w && w < d) {
                    newShape = RotateZ(); // 2,1,3 - z
                } else if (h < d && d < w) {
                    newShape = RotateY().RotateX(); // 2,3,1 - yx
                } else
                    throw new ApplicationException("how did we end up here?");
            }
            if (newShape.w > newShape.h || newShape.h > newShape.d) throw new ApplicationException("Unexpected non minimal rotation");
        }

        if (newShape.w < newShape.h && newShape.h < newShape.d) return newShape.All8Rotations();
        if (newShape.w < newShape.h && newShape.h == newShape.d) return newShape.All16RotationsY2Z2();
        if (newShape.w == newShape.h && newShape.h < newShape.d) return newShape.All16RotationsX2Y2();
        throw new ApplicationException("Unexpected situation");
    }

    private IEnumerable<BitShape> All8Rotations() {
        yield return this;
        yield return RotateX2();
        yield return RotateY2();
        yield return RotateX2();
        yield return RotateZ2();
        yield return RotateX2();
        yield return RotateY2();
        yield return RotateX2();
        RotateZ2(); // restore
    }

    private IEnumerable<BitShape> All16RotationsY2Z2() {
        var shape = new BitShape(this);
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateY2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateZ2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateY2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;
    }

    private IEnumerable<BitShape> All16RotationsX2Y2() {
        var shape = new BitShape(this);
        foreach (var s in shape.AllRotationsOfZ()) yield return s;

        shape = shape.RotateY2();
        foreach (var s in shape.AllRotationsOfZ()) yield return s;

        shape = shape.RotateX2();
        foreach (var s in shape.AllRotationsOfZ()) yield return s;

        shape = shape.RotateY2();
        foreach (var s in shape.AllRotationsOfZ()) yield return s;
    }

    // generates all 24 possible rotations of this shape
    private IEnumerable<BitShape> All24Rotations() {
        var shape = new BitShape(this);
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateY();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        var shape2 = new BitShape(this).RotateY2();
        foreach (var s in shape2.AllRotationsOfX()) yield return s;

        shape.RotateY2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateZ();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape.RotateZ2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;
    }

    // generates all 4 possible rotations on X axis of this shape
    private IEnumerable<BitShape> AllRotationsOfX() {
        if (h == d) { // rotates in-place but restores it
            yield return this;
            yield return RotateX();
            yield return RotateX();
            yield return RotateX();
            RotateX();
        } else {
            yield return this;
            var shape90 = this.RotateX();
            yield return shape90;
            yield return RotateX2();
            yield return shape90.RotateX2();
            RotateX2();
        }
    }

    // generates all 4 possible rotations on Z axis of this shape
    private IEnumerable<BitShape> AllRotationsOfZ() {
        if (w == h) { // rotates in-place but restores it
            yield return this;
            yield return RotateZ();
            yield return RotateZ();
            yield return RotateZ();
            RotateZ();
        } else {
            yield return this;
            var shape90 = this.RotateZ();
            yield return shape90;
            yield return RotateZ2();
            yield return shape90.RotateZ2();
            RotateZ2();
        }
    }

    // returns the shape rotated such that it is the mimimum serialization of the shape
    public BitShape MinRotation() {
        // a rubiks cube, for example, is a 3x3x3 shape
        // it can be resting on any of its six colors
        // and could have any of four sides facing you. 6*4=24

        // we will need to apply rotations to get all 24 combinations
        // and see which one compares to be the lowest

        BitShape best = null;
        foreach (var s in AllMinRotations())
            if (best == null || best.CompareTo(s) > 0)
                best = new BitShape(s); // must clone it to prevent it from being mutated in-place
        return best;
    }

    // generate all 8 possible mirrorings of shape
    // note these are generated in-place on this shape
    public IEnumerable<BitShape> AllMirrors() {
        yield return this;
        yield return MirrorX();
        yield return MirrorY();
        yield return MirrorX();
        yield return MirrorZ();
        yield return MirrorX();
        yield return MirrorY();
        yield return MirrorX();
        MirrorZ(); // restore
    }

    // check all rotations for each mirrored possibility (8)
    // note that we do this by rotations first, since mirrors
    // mutate object in-place and should be more cache friendly
    // to do on the inside loop.
    public BitShape MinChiralRotation() {
        BitShape best = null;
        foreach (var s in AllMinRotations())
            foreach (var ss in s.AllMirrors())
                if (best == null || best.CompareTo(ss) > 0)
                    best = new BitShape(ss); // must clone it to prevent it from being mutated in-place
        return best;
    }

    public bool IsMinChiralRotation() {
        BitShape best = this;
        foreach (var s in AllMinRotations())
            foreach (var ss in s.AllMirrors())
                if (best.CompareTo(ss) > 0)
                    return false;
        return true;
    }

    // returns <0 if this is lower than other, 0 if equal, >0 if other is lower
    // assumes equal sizes
    public int CompareTo(BitShape other) {
        int dw = w - other.w; if (dw != 0) return dw;
        int dh = h - other.h; if (dh != 0) return dh;
        int dd = d - other.d; if (dd != 0) return dd;
        for (int i = 0; i < bytes.Length; i++)
            if (bytes[i] != other.bytes[i])
                return bytes[i] - other.bytes[i];
        return 0;
    }

    public override bool Equals(object? obj) {
        return obj != null && obj is BitShape b && w == b.w && h == b.h && d == b.d && bytes.SequenceEqual(b.bytes);
    }

    public bool HasSetNeighbor(int x, int y, int z) {
        // minor opt: we do easier comparisons first with short-circuiting
        return (x > 0 && Get(x - 1, y, z)) ||
            (y > 0 && Get(x, y - 1, z)) ||
            (z > 0 && Get(x, y, z - 1)) ||
            (x + 1 < w && Get(x + 1, y, z)) ||
            (y + 1 < h && Get(x, y + 1, z)) ||
            (z + 1 < d && Get(x, y, z + 1));
    }

    // returns the number of corners, edges and faces set - this is rotationally independent so can be used
    // to shard shape work - value returned will be between 0 and 98 for a 5x5x5 shape, for example.
    public (int corners, int edges, int faces) CornerEdgeFaceCount() {
        int corners = 0, edges = 0, faces = 0;
        int bi = 0;
        byte mask = 1 << 7;
        int xl = w - 1, yl = h - 1, zl = d - 1;
        for (int x = 0; x < w; x++) {
            bool xyes = x == 0 || x == xl;
            for (int y = 0; y < h; y++) {
                bool yyes = y == 0 || y == yl;
                for (int z = 0; z < d; z++) {
                    bool zyes = z == 0 || z == zl;
                    if (xyes || yyes || zyes)
                        if ((bytes[bi] & mask) != 0)
                            if (xyes && yyes || yyes && zyes || xyes && zyes)
                                if (!xyes || !yyes || !zyes)
                                    edges++;
                                else
                                    corners++;
                            else
                                faces++;
                    mask >>= 1; if (mask == 0) { mask = 1 << 7; bi++; }
                }
            }
        }
        return (corners, edges, faces);
    }
}
