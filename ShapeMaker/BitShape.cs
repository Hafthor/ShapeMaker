using System.Collections;

namespace ShapeMaker;

/// <summary>
/// Stores a shape of given dimensions with w*h*d bits inside to represent a shape. Supports various 3-D operations on shape.
/// </summary>
public class BitShape {
    public readonly byte w, h, d;
    public readonly byte[] bytes;
    const int BITS_PER = sizeof(byte) * 8;
    const int BITS_SHR = 3;
    const int BITS_PER_MINUS_1 = BITS_PER - 1;
    const byte MASK_FIRST = 1 << BITS_PER_MINUS_1;

    public BitShape(byte w, byte h, byte d) {
        this.w = w;
        this.h = h;
        this.d = d;
        int sz = w * h * d;
        bytes = new byte[(sz + BITS_PER_MINUS_1) >> BITS_SHR];
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
        this.bytes = new byte[(sz + BITS_PER_MINUS_1) >> BITS_SHR];
        int di = 0;
        byte mask = MASK_FIRST;
        for (int i = 0; i < sz; i++) {
            if (chars[i] == '*') bytes[di] |= mask;
            mask >>= 1; if (mask == 0) { mask = MASK_FIRST; di++; }
        }
    }

    public string Serialize() {
        int l = w * h * d;
        var ca = new char[l];
        int ci = 0, bi = 0;
        byte mask = MASK_FIRST;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    ca[ci++] = (bytes[bi] & mask) != 0 ? '*' : '.';
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; bi++; }
                }
        if (ci != ca.Length) throw new InvalidProgramException("miscalculated string length");
        return w + "," + h + "," + d + "," + new string(ca);
    }

    public bool this[int x, int y, int z] {
        get {
            var bi = d * (h * x + y) + z;
            //var si = Math.DivRem(bi, BITS_PER, out int shr);
            int si = bi >> BITS_SHR, shr = bi & BITS_PER_MINUS_1;
            byte mask = (byte)(MASK_FIRST >> shr);
            return (bytes[si] & mask) != 0;
        }
        set {
            var bi = d * (h * x + y) + z;
            //var di = Math.DivRem(bi, BITS_PER, out int shr);
            int di = bi >> BITS_SHR, shr = bi & BITS_PER_MINUS_1;
            byte mask = (byte)(MASK_FIRST >> shr);
            if (value) bytes[di] |= mask; else bytes[di] &= (byte)~mask;
        }
    }

    public BitShape PadLeft() {
        return SetCopy(new BitShape((byte)(w + 1), h, d), 1, w, 0, h, 0, d);
    }

    public BitShape PadRight() {
        return SetCopy(new BitShape((byte)(w + 1), h, d), 0, w, 0, h, 0, d);
    }

    public BitShape PadTop() {
        return SetCopy(new BitShape(w, (byte)(h + 1), d), 0, w, 1, h, 0, d);
    }

    public BitShape PadBottom() {
        return SetCopy(new BitShape(w, (byte)(h + 1), d), 0, w, 0, h, 0, d);
    }

    public BitShape PadFront() {
        return SetCopy(new BitShape(w, h, (byte)(d + 1)), 0, w, 0, h, 1, d);
    }

    public BitShape PadBack() {
        return SetCopy(new BitShape(w, h, (byte)(d + 1)), 0, w, 0, h, 0, d);
    }

    public BitShape SetCopy(BitShape dest, int xa, int w, int ya, int h, int za, int d) {
        for (int x = 0, xd = xa; x < w; x++, xd++)
            for (int y = 0, yd = ya; y < h; y++, yd++)
                for (int z = 0, zd = za; z < d; z++, zd++)
                    if (this[x, y, z]) dest[xd, yd, zd] = true;
        return dest;
    }

    private void Swap(int x0, int y0, int z0, int x1, int y1, int z1) {
        // (this[x0, y0, z0], this[x1, y1, z1]) = (this[x1, y1, z1], this[x0, y0, z0]);
        int i0 = d * (h * x0 + y0) + z0;
        int i1 = d * (h * x1 + y1) + z1;
        //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
        //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
        int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
        int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
        byte mask0 = (byte)(MASK_FIRST >> shr0);
        byte mask1 = (byte)(MASK_FIRST >> shr1);
        bool t0 = (bytes[bi0] & mask0) != 0;
        bool t1 = (bytes[bi1] & mask1) != 0;
        //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
        //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
        if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
    }

    private void Swap(int x0, int y0, int z0, int x1, int y1, int z1, int x2, int y2, int z2, int x3, int y3, int z3) {
        // (this[x0, y0, z0], this[x1, y1, z1], this[x2, y2, z2], this[x3, y3, z3]) = (this[x1, y1, z1], this[x2, y2, z2], this[x3, y3, z3], this[x0, y0, z0]);
        int i0 = d * (h * x0 + y0) + z0;
        int i1 = d * (h * x1 + y1) + z1;
        int i2 = d * (h * x2 + y2) + z2;
        int i3 = d * (h * x3 + y3) + z3;
        //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
        //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
        //int bi2 = Math.DivRem(i2, BITS_PER, out int shr2);
        //int bi3 = Math.DivRem(i3, BITS_PER, out int shr3);
        int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
        int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
        int bi2 = i2 >> BITS_SHR, shr2 = i2 & BITS_PER_MINUS_1;
        int bi3 = i3 >> BITS_SHR, shr3 = i3 & BITS_PER_MINUS_1;
        byte mask0 = (byte)(MASK_FIRST >> shr0);
        byte mask1 = (byte)(MASK_FIRST >> shr1);
        byte mask2 = (byte)(MASK_FIRST >> shr2);
        byte mask3 = (byte)(MASK_FIRST >> shr3);
        bool t0 = (bytes[bi0] & mask0) != 0;
        bool t1 = (bytes[bi1] & mask1) != 0;
        bool t2 = (bytes[bi2] & mask2) != 0;
        bool t3 = (bytes[bi3] & mask3) != 0;
        if (t0 != t1) bytes[bi0] ^= mask0; //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
        if (t1 != t2) bytes[bi1] ^= mask1; //if (t2) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
        if (t2 != t3) bytes[bi2] ^= mask2; //if (t3) bytes[bi2] |= mask2; else bytes[bi2] &= (byte)(~mask2);
        if (t3 != t0) bytes[bi3] ^= mask3; //if (t0) bytes[bi3] |= mask3; else bytes[bi3] &= (byte)(~mask3);
    }

    // returns shape rotated clockwise on X axis by 90º (swaps h,d)
    public BitShape RotateX() {
        if (d == h) { // rotate in-place
            for (int x = 0, h2 = h / 2, yl = h - 1, zl = d - 1; x < w; x++)
                for (int y = 0, yn = yl; y < h2; y++, yn--)
                    for (int z = y, zn = zl - y, zm = zn; z < zm; z++, zn--) {
                        // Swap(x, y, z, x, zn, y, x, yn, zn, x, z, yn); // (this[x, y, z], this[x, zn, y], this[x, yn, zn], this[x, z, yn]) = (this[x, zn, y], this[x, yn, zn], this[x, z, yn], this[x, y, z]);
                        int hx = h * x;
                        int i0 = d * (hx + y) + z;
                        int i1 = d * (hx + zn) + y;
                        int i2 = d * (hx + yn) + zn;
                        int i3 = d * (hx + z) + yn;
                        //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                        //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                        //int bi2 = Math.DivRem(i2, BITS_PER, out int shr2);
                        //int bi3 = Math.DivRem(i3, BITS_PER, out int shr3);
                        int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                        int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                        int bi2 = i2 >> BITS_SHR, shr2 = i2 & BITS_PER_MINUS_1;
                        int bi3 = i3 >> BITS_SHR, shr3 = i3 & BITS_PER_MINUS_1;
                        byte mask0 = (byte)(MASK_FIRST >> shr0);
                        byte mask1 = (byte)(MASK_FIRST >> shr1);
                        byte mask2 = (byte)(MASK_FIRST >> shr2);
                        byte mask3 = (byte)(MASK_FIRST >> shr3);
                        bool t0 = (bytes[bi0] & mask0) != 0;
                        bool t1 = (bytes[bi1] & mask1) != 0;
                        bool t2 = (bytes[bi2] & mask2) != 0;
                        bool t3 = (bytes[bi3] & mask3) != 0;
                        if (t0 != t1) bytes[bi0] ^= mask0; //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                        if (t1 != t2) bytes[bi1] ^= mask1; //if (t2) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                        if (t2 != t3) bytes[bi2] ^= mask2; //if (t3) bytes[bi2] |= mask2; else bytes[bi2] &= (byte)(~mask2);
                        if (t3 != t0) bytes[bi3] ^= mask3; //if (t0) bytes[bi3] |= mask3; else bytes[bi3] &= (byte)(~mask3);
                    }
            return this;
        }

        var newShape = new BitShape(w, d, h);
        byte mask = MASK_FIRST;
        int bi = 0;
        byte[] newBytes = newShape.bytes;
        for (int x = 0, yl = h - 1; x < w; x++)
            for (int z = 0; z < d; z++)
                for (int y = 0, yn = yl; y < h; y++, yn--) {
                    if (this[x, yn, z]) newBytes[bi] |= mask; // newShape[x, z, y] = true;
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; bi++; }
                }

        return newShape;
    }

    // rotates in-place on X axis by 180º
    public BitShape RotateX2() {
        for (int x = 0, h2 = h / 2, yl = h - 1, zl = d - 1; x < w; x++) {
            for (int y = 0, yn = yl; y < h2; y++, yn--)
                for (int z = 0, zn = zl; z < d; z++, zn--) {
                    // Swap(x, y, z, x, yn, zn); // (this[x, y, z], this[x, yn, zn]) = (this[x, yn, zn], this[x, y, z]);
                    int hx = h * x;
                    int i0 = d * (hx + y) + z;
                    int i1 = d * (hx + yn) + zn;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
            if (h % 2 == 1)
                for (int z = 0, zn = d - 1; z < d / 2; z++, zn--) {
                    // Swap(x, h2, z, x, h2, zn); // (this[x, h2, z], this[x, h2, zn]) = (this[x, h2, zn], this[x, h2, z]);
                    int i = d * (h * x + h2);
                    int i0 = i + z;
                    int i1 = i + zn;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorX() {
        for (int x = 0, w2 = w / 2, xn = w - 1; x < w2; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    // Swap(x, y, z, xn, y, z); // (this[x, y, z], this[xn, y, z]) = (this[xn, y, z], this[x, y, z]);
                    int i0 = d * (h * x + y) + z;
                    int i1 = d * (h * xn + y) + z;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
        return this;
    }

    // returns shape rotated clockwise on Y axis by 90º (swaps w,d)
    public BitShape RotateY() {
        if (w == d) { // rotate in-place
            for (int y = 0, d2 = d / 2, zl = d - 1, xl = w - 1; y < h; y++)
                for (int z = 0, zn = zl; z < d2; z++, zn--)
                    for (int x = z, xn = xl - z, xm = xn; x < xm; x++, xn--) {
                        // Swap(x, y, z, zn, y, x, xn, y, zn, z, y, xn); // (this[x, y, z], this[zn, y, x], this[xn, y, zn], this[z, y, xn]) = (this[zn, y, x], this[xn, y, zn], this[z, y, xn], this[x, y, z]);
                        int i0 = d * (h * x + y) + z;
                        int i1 = d * (h * zn + y) + x;
                        int i2 = d * (h * xn + y) + zn;
                        int i3 = d * (h * z + y) + xn;
                        //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                        //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                        //int bi2 = Math.DivRem(i2, BITS_PER, out int shr2);
                        //int bi3 = Math.DivRem(i3, BITS_PER, out int shr3);
                        int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                        int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                        int bi2 = i2 >> BITS_SHR, shr2 = i2 & BITS_PER_MINUS_1;
                        int bi3 = i3 >> BITS_SHR, shr3 = i3 & BITS_PER_MINUS_1;
                        byte mask0 = (byte)(MASK_FIRST >> shr0);
                        byte mask1 = (byte)(MASK_FIRST >> shr1);
                        byte mask2 = (byte)(MASK_FIRST >> shr2);
                        byte mask3 = (byte)(MASK_FIRST >> shr3);
                        bool t0 = (bytes[bi0] & mask0) != 0;
                        bool t1 = (bytes[bi1] & mask1) != 0;
                        bool t2 = (bytes[bi2] & mask2) != 0;
                        bool t3 = (bytes[bi3] & mask3) != 0;
                        if (t0 != t1) bytes[bi0] ^= mask0; //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                        if (t1 != t2) bytes[bi1] ^= mask1; //if (t2) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                        if (t2 != t3) bytes[bi2] ^= mask2; //if (t3) bytes[bi2] |= mask2; else bytes[bi2] &= (byte)(~mask2);
                        if (t3 != t0) bytes[bi3] ^= mask3; //if (t0) bytes[bi3] |= mask3; else bytes[bi3] &= (byte)(~mask3);
                    }
            return this;
        }

        var newShape = new BitShape(d, h, w);
        byte mask = MASK_FIRST;
        int bi = 0;
        byte[] newBytes = newShape.bytes;
        for (int z = 0, xl = w - 1; z < d; z++)
            for (int y = 0; y < h; y++)
                for (int x = 0, xn = xl; x < w; x++, xn--) {
                    if (this[xn, y, z]) newBytes[bi] |= mask; // newShape[z, y, x] = true;
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; bi++; }
                }

        return newShape;
    }

    // rotates in-place on Y axis by 180º
    public BitShape RotateY2() {
        for (int y = 0, w2 = w / 2, zl = d - 1; y < h; y++) {
            for (int x = 0, xn = w - 1; x < w2; x++, xn--)
                for (int z = 0, zn = zl; z < d; z++, zn--) {
                    // Swap(x, y, z, xn, y, zn); // (this[x, y, z], this[xn, y, zn]) = (this[xn, y, zn], this[x, y, z]);
                    int i0 = d * (h * x + y) + z;
                    int i1 = d * (h * xn + y) + zn;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
            if (w % 2 == 1)
                for (int z = 0, zn = zl, d2 = d / 2; z < d2; z++, zn--) {
                    // Swap(w2, y, z, w2, y, zn); // (this[w2, y, z], this[w2, y, zn]) = (this[w2, y, zn], this[w2, y, z]);
                    int i = d * (h * w2 + y);
                    int i0 = i + z;
                    int i1 = i + zn;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorY() {
        for (int x = 0, h2 = h / 2, yl = h - 1; x < w; x++)
            for (int y = 0, yn = yl; y < h2; y++, yn--)
                for (int z = 0; z < d; z++) {
                    // Swap(x, y, z, x, yn, z); // (this[x, y, z], this[x, yn, z]) = (this[x, yn, z], this[x, y, z]);
                    int hx = h * x;
                    int i0 = d * (hx + y) + z;
                    int i1 = d * (hx + yn) + z;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
        return this;
    }

    // returns shape rotated clockwise on Z axis by 90º (swaps w,h)
    public BitShape RotateZ() {
        if (w == h) { // rotate in-place
            for (int z = 0, h2 = h / 2, xl = w - 1, yl = h - 1; z < d; z++)
                for (int y = 0, yn = yl; y < h2; y++, yn--)
                    for (int x = y, xn = xl - y, xm = xn; x < xm; x++, xn--) {
                        // Swap(x, y, z, yn, x, z, xn, yn, z, y, xn, z); // (this[x, y, z], this[yn, x, z], this[xn, yn, z], this[y, xn, z]) = (this[yn, x, z], this[xn, yn, z], this[y, xn, z], this[x, y, z]);
                        int i0 = d * (h * x + y) + z;
                        int i1 = d * (h * yn + x) + z;
                        int i2 = d * (h * xn + yn) + z;
                        int i3 = d * (h * y + xn) + z;
                        //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                        //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                        //int bi2 = Math.DivRem(i2, BITS_PER, out int shr2);
                        //int bi3 = Math.DivRem(i3, BITS_PER, out int shr3);
                        int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                        int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                        int bi2 = i2 >> BITS_SHR, shr2 = i2 & BITS_PER_MINUS_1;
                        int bi3 = i3 >> BITS_SHR, shr3 = i3 & BITS_PER_MINUS_1;
                        byte mask0 = (byte)(MASK_FIRST >> shr0);
                        byte mask1 = (byte)(MASK_FIRST >> shr1);
                        byte mask2 = (byte)(MASK_FIRST >> shr2);
                        byte mask3 = (byte)(MASK_FIRST >> shr3);
                        bool t0 = (bytes[bi0] & mask0) != 0;
                        bool t1 = (bytes[bi1] & mask1) != 0;
                        bool t2 = (bytes[bi2] & mask2) != 0;
                        bool t3 = (bytes[bi3] & mask3) != 0;
                        if (t0 != t1) bytes[bi0] ^= mask0; //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                        if (t1 != t2) bytes[bi1] ^= mask1; //if (t2) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                        if (t2 != t3) bytes[bi2] ^= mask2; //if (t3) bytes[bi2] |= mask2; else bytes[bi2] &= (byte)(~mask2);
                        if (t3 != t0) bytes[bi3] ^= mask3; //if (t0) bytes[bi3] |= mask3; else bytes[bi3] &= (byte)(~mask3);
                    }
            return this;
        }

        var newShape = new BitShape(h, w, d);
        byte mask = MASK_FIRST;
        int bi = 0;
        byte[] newBytes = newShape.bytes;
        for (int y = 0, xl = w - 1; y < h; y++)
            for (int x = 0, xn = xl; x < w; x++, xn--)
                for (int z = 0; z < d; z++) {
                    if (this[xn, y, z]) newBytes[bi] |= mask; // newShape[y, x, z] = true;
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; bi++; }
                }

        return newShape;
    }

    // rotates in-place on Z axis by 180º
    public BitShape RotateZ2() {
        for (int z = 0, w2 = w / 2, xl = w - 1, yl = h - 1; z < d; z++) {
            for (int x = 0, xn = xl; x < w2; x++, xn--)
                for (int y = 0, yn = yl; y < h; y++, yn--) {
                    // Swap(x, y, z, xn, yn, z); // (this[x, y, z], this[xn, yn, z]) = (this[xn, yn, z], this[x, y, z]);
                    int i0 = d * (h * x + y) + z;
                    int i1 = d * (h * xn + yn) + z;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
            if (w % 2 == 1)
                for (int y = 0, h2 = h / 2, yn = yl; y < h2; y++, yn--) {
                    // Swap(w2, y, z, w2, yn, z); // (this[w2, y, z], this[w2, yn, z]) = (this[w2, yn, z], this[w2, y, z]);
                    int hx = h * w2;
                    int i0 = d * (hx + y) + z;
                    int i1 = d * (hx + yn) + z;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorZ() {
        for (int x = 0, d2 = d / 2, zl = d - 1; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0, zn = zl; z < d2; z++, zn--) {
                    // Swap(x, y, z, x, y, zn); // (this[x, y, z], this[x, y, zn]) = (this[x, y, zn], this[x, y, z]);
                    int i = d * (h * x + y);
                    int i0 = i + z;
                    int i1 = i + zn;
                    //int bi0 = Math.DivRem(i0, BITS_PER, out int shr0);
                    //int bi1 = Math.DivRem(i1, BITS_PER, out int shr1);
                    int bi0 = i0 >> BITS_SHR, shr0 = i0 & BITS_PER_MINUS_1;
                    int bi1 = i1 >> BITS_SHR, shr1 = i1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool t0 = (bytes[bi0] & mask0) != 0;
                    bool t1 = (bytes[bi1] & mask1) != 0;
                    //if (t1) bytes[bi0] |= mask0; else bytes[bi0] &= (byte)(~mask0);
                    //if (t0) bytes[bi1] |= mask1; else bytes[bi1] &= (byte)(~mask1);
                    if (t0 != t1) { bytes[bi0] ^= mask0; bytes[bi1] ^= mask1; }
                }
        return this;
    }

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

    // generates all 8 possible rotations for when w, h, and d are unique
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

    // generates all 16 possible rotations for when h=d but w is different
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

    // generates all 16 possible rotations for when w=h but d is different
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

    // generates all 24 possible rotations of this shape for when w, h and d are the same
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

    // check all rotations for each mirrored possibility (8)
    // to see if current shape is the minimal rotation.
    public bool IsMinChiralRotation() {
        var that = new BitShape(this);
        foreach (var s in AllMinRotations())
            foreach (var ss in s.AllMirrors())
                if (that.CompareTo(ss) > 0)
                    return false;
        return true;
    }

    // returns <0 if this is lower than other, 0 if equal, >0 if other is lower
    public int CompareTo(BitShape other) {
        if (this == other) return 0;
        int dw = w - other.w; if (dw != 0) return dw;
        int dh = h - other.h; if (dh != 0) return dh;
        int dd = d - other.d; if (dd != 0) return dd;
        return ((IStructuralComparable)bytes).CompareTo(other.bytes, Comparer<byte>.Default);
    }

    public override bool Equals(object? obj) {
        return obj != null && obj is BitShape b && w == b.w && h == b.h && d == b.d && bytes.SequenceEqual(b.bytes);
    }

    public bool HasSetNeighbor(int x, int y, int z) {
        // minor opt: we do easier comparisons first
        if (x > 0 && this[x - 1, y, z]) return true;
        if (y > 0 && this[x, y - 1, z]) return true;
        if (z > 0 && this[x, y, z - 1]) return true;
        int x1 = x + 1; if (x1 < w && this[x1, y, z]) return true;
        int y1 = y + 1; if (y1 < h && this[x, y1, z]) return true;
        int z1 = z + 1; if (z1 < d && this[x, y, z1]) return true;
        return false;
    }

    // returns the number of corners, edges and faces set - this is rotationally independent so can be used to shard
    // shape work without having to find minimal rotation first - values returned will be, for an example 5x5x5 shape:
    // 0 and 8 for corner count, 0 and 3*12(36) for edge count, 0 and 3*3*6(54) for face count.
    public (int corners, int edges, int faces) CornerEdgeFaceCount() {
        int corners = 0, edges = 0, faces = 0;
        int bi = 0;
        byte mask = MASK_FIRST;
        int xl = w - 1, yl = h - 1, zl = d - 1;
        for (int x = 0; x <= xl; x++) {
            bool xyes = x == 0 || x == xl;
            for (int y = 0; y <= yl; y++) {
                bool yyes = y == 0 || y == yl;
                for (int z = 0; z <= zl; z++) {
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
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; bi++; }
                }
            }
        }
        return (corners, edges, faces);
    }
}
