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
        int size = w * h * d;
        bytes = new byte[(size + BITS_PER_MINUS_1) >> BITS_SHR];
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
        var split = s.Split(',');
        if (split.Length != 4) throw new ArgumentException("expected a four part string");
        this.w = byte.Parse(split[0]);
        this.h = byte.Parse(split[1]);
        this.d = byte.Parse(split[2]);
        var chars = split[3].Replace(" ", "").Replace("\n", "");
        if (chars.Length != w * h * d) throw new ArgumentException("expected string of len w*h*d");
        int size = w * h * d;
        this.bytes = new byte[(size + BITS_PER_MINUS_1) >> BITS_SHR];
        int byteIndex = 0;
        byte mask = MASK_FIRST;
        for (int i = 0; i < size; i++) {
            if (chars[i] == '*') bytes[byteIndex] |= mask;
            mask >>= 1; if (mask == 0) { mask = MASK_FIRST; byteIndex++; }
        }
    }

    public string Serialize() {
        int size = w * h * d;
        var chars = new char[size];
        int charIndex = 0, byteIndex = 0;
        byte mask = MASK_FIRST;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    chars[charIndex++] = (bytes[byteIndex] & mask) != 0 ? '*' : '.';
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; byteIndex++; }
                }
        if (charIndex != chars.Length) throw new InvalidProgramException("miscalculated string length");
        return w + "," + h + "," + d + "," + new string(chars);
    }

    public bool this[int x, int y, int z] {
        get {
            var bitIndex = d * (h * x + y) + z;
            int byteIndex = bitIndex >> BITS_SHR, shr = bitIndex & BITS_PER_MINUS_1;
            byte mask = (byte)(MASK_FIRST >> shr);
            return (bytes[byteIndex] & mask) != 0;
        }
        set {
            var bitIndex = d * (h * x + y) + z;
            int byteIndex = bitIndex >> BITS_SHR, shr = bitIndex & BITS_PER_MINUS_1;
            byte mask = (byte)(MASK_FIRST >> shr);
            byte b = bytes[byteIndex];
            if (value != ((b & mask) != 0)) bytes[byteIndex] = (byte)(b ^ mask);
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

    public BitShape SetCopy(BitShape dest, int xOffset, int w, int yOffset, int h, int zOffset, int d) {
        for (int sx = 0, dx = xOffset; sx < w; sx++, dx++)
            for (int sy = 0, dy = yOffset; sy < h; sy++, dy++)
                for (int sz = 0, dz = zOffset; sz < d; sz++, dz++)
                    if (this[sx, sy, sz]) dest[dx, dy, dz] = true;
        return dest;
    }

    // returns shape rotated clockwise on X axis by 90º (swaps h,d)
    public BitShape RotateX() {
        if (d == h) { // rotate in-place
            for (int x = 0, h2 = h / 2, yLimit = h - 1, zLimit = d - 1; x < w; x++)
                for (int y = 0, yNot = yLimit; y < h2; y++, yNot--)
                    for (int z = y, zNot = zLimit - y, zMax = zNot; z < zMax; z++, zNot--) {
                        // (this[x, y, z], this[x, zn, y], this[x, yn, zn], this[x, z, yn]) = (this[x, zn, y], this[x, yn, zn], this[x, z, yn], this[x, y, z]);
                        int hx = h * x;
                        int index0 = d * (hx + y) + z;
                        int index1 = d * (hx + zNot) + y;
                        int index2 = d * (hx + yNot) + zNot;
                        int index3 = d * (hx + z) + yNot;
                        int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                        int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                        int byteIndex2 = index2 >> BITS_SHR, shr2 = index2 & BITS_PER_MINUS_1;
                        int byteIndex3 = index3 >> BITS_SHR, shr3 = index3 & BITS_PER_MINUS_1;
                        byte mask0 = (byte)(MASK_FIRST >> shr0);
                        byte mask1 = (byte)(MASK_FIRST >> shr1);
                        byte mask2 = (byte)(MASK_FIRST >> shr2);
                        byte mask3 = (byte)(MASK_FIRST >> shr3);
                        bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                        bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                        bool isSet2 = (bytes[byteIndex2] & mask2) != 0;
                        bool isSet3 = (bytes[byteIndex3] & mask3) != 0;
                        if (isSet0 != isSet1) bytes[byteIndex0] ^= mask0;
                        if (isSet1 != isSet2) bytes[byteIndex1] ^= mask1;
                        if (isSet2 != isSet3) bytes[byteIndex2] ^= mask2;
                        if (isSet3 != isSet0) bytes[byteIndex3] ^= mask3;
                    }
            return this;
        }

        var newShape = new BitShape(w, d, h);
        byte mask = MASK_FIRST;
        int byteIndex = 0;
        byte[] newBytes = newShape.bytes;
        for (int x = 0, yLimit = h - 1; x < w; x++)
            for (int z = 0; z < d; z++)
                for (int y = 0, yNot = yLimit; y < h; y++, yNot--) {
                    if (this[x, yNot, z]) newBytes[byteIndex] |= mask; // newShape[x, z, y] = true;
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; byteIndex++; }
                }

        return newShape;
    }

    // rotates in-place on X axis by 180º
    public BitShape RotateX2Org() {
        for (int x = 0, h2 = h / 2, yLimit = h - 1, zLimit = d - 1; x < w; x++) {
            for (int y = 0, yNot = yLimit; y < h2; y++, yNot--)
                for (int z = 0, zNot = zLimit; z < d; z++, zNot--) {
                    // (this[x, y, z], this[x, yn, zn]) = (this[x, yn, zn], this[x, y, z]);
                    int hx = h * x;
                    int index0 = d * (hx + y) + z;
                    int index1 = d * (hx + yNot) + zNot;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
            if (h % 2 == 1)
                for (int z = 0, zNot = d - 1; z < d / 2; z++, zNot--) {
                    // (this[x, h2, z], this[x, h2, zn]) = (this[x, h2, zn], this[x, h2, z]);
                    int index = d * (h * x + h2);
                    int index0 = index + z;
                    int index1 = index + zNot;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
        }
        return this;
    }

    public BitShape RotateX2Opt() {
        int hd = h * d, hd2 = hd / 2;
        int index0 = 0, index1 = hd - 1;
        for (int x = 0; x < w; x++, index0 += hd, index1 += hd) {
            int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
            int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
            byte mask0 = (byte)(MASK_FIRST >> shr0);
            byte mask1 = (byte)(MASK_FIRST >> shr1);
            for (int yz = 0; yz < hd2; yz++) {
                bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                mask0 >>= 1; if (mask0 == 0) { mask0 = MASK_FIRST; byteIndex0++; }
                mask1 <<= 1; if (mask1 == 0) { mask1 = 1; byteIndex1--; }
            }
        }
        return this;
    }

    public BitShape RotateX2Opt1() {
        int hd = h * d, hd2 = hd / 2;
        int index0 = 0, index1 = hd - 1;
        for (int x = 0; x < w; x++, index0 += hd, index1 += hd) {
            int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
            int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
            byte mask0 = (byte)(MASK_FIRST >> shr0);
            byte mask1 = (byte)(MASK_FIRST >> shr1);
            for (int yz = 0; yz < hd2; yz++) {
                bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                mask0 >>= 1;
                mask1 <<= 1;
                if (mask0 == 0) { mask0 = MASK_FIRST; byteIndex0++; }
                if (mask1 == 0) { mask1 = 1; byteIndex1--; }
            }
        }
        return this;
    }

    public BitShape RotateX2Opt2() {
        int hd = h * d, hd2 = hd / 2;
        int index0 = 0, index1 = hd - 1;
        for (int x = 0; x < w; x++, index0 += hd, index1 += hd) {
            int index0a = index0, index1a = index1;
            for (int yz = 0; yz < hd2; yz++, index0a++, index1a--) {
                int byteIndex0 = index0a >> BITS_SHR, shr0 = index0a & BITS_PER_MINUS_1;
                int byteIndex1 = index1a >> BITS_SHR, shr1 = index1a & BITS_PER_MINUS_1;
                byte mask0 = (byte)(MASK_FIRST >> shr0);
                byte mask1 = (byte)(MASK_FIRST >> shr1);
                bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
            }
        }
        return this;
    }

    public BitShape RotateX2Opt3() {
        int hd = h * d, hd2 = hd / 2;
        int index0 = 0, index1 = hd - 1;
        for (int x = 0; x < w; x++, index0 += hd, index1 += hd) {
            int index0a = index0, index1a = index1;
            for (int yz = 0; yz < hd2; yz++, index0a++, index1a--) {
                int byteIndex0 = index0a >> BITS_SHR, shr0 = index0a & BITS_PER_MINUS_1;
                byte mask0 = (byte)(MASK_FIRST >> shr0);
                bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                int byteIndex1 = index1a >> BITS_SHR, shr1 = index1a & BITS_PER_MINUS_1;
                byte mask1 = (byte)(MASK_FIRST >> shr1);
                bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
            }
        }
        return this;
    }

    public BitShape RotateX2() { // was RotateX2Opt4
        int hd = h * d, hd2 = hd / 2;
        int index0 = 0, index1 = hd - 1;
        for (int x = 0; x < w; x++, index0 += hd, index1 += hd) {
            int index0a = index0, index1a = index1;
            for (int yz = 0; yz < hd2; yz++, index0a++, index1a--) {
                int byteIndex0 = index0a >> BITS_SHR;
                int byteIndex1 = index1a >> BITS_SHR;
                int shr0 = index0a & BITS_PER_MINUS_1;
                int shr1 = index1a & BITS_PER_MINUS_1;
                byte mask0 = (byte)(MASK_FIRST >> shr0);
                byte mask1 = (byte)(MASK_FIRST >> shr1);
                bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
            }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorXOrg() {
        for (int x = 0, w2 = w / 2, xNot = w - 1; x < w2; x++, xNot--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    // (this[x, y, z], this[xn, y, z]) = (this[xn, y, z], this[x, y, z]);
                    int index0 = d * (h * x + y) + z;
                    int index1 = d * (h * xNot + y) + z;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
        return this;
    }

    public BitShape MirrorXOpt() {
        int byteIndex0 = 0;
        byte mask0 = MASK_FIRST;
        int hd = h * d;
        int index1 = hd * w;
        for (int x = 0, w2 = w / 2; x < w2; x++) {
            index1 -= hd;
            int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
            byte mask1 = (byte)(MASK_FIRST >> shr1);
            for (int yz = 0; yz < hd; yz++) {
                bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                mask0 >>= 1; if (mask0 == 0) { mask0 = MASK_FIRST; byteIndex0++; }
                mask1 >>= 1; if (mask1 == 0) { mask1 = MASK_FIRST; byteIndex1++; }
            }
        }
        return this;
    }

    public BitShape MirrorX() { // was MirrorXOpt2
        int index0 = 0;
        int hd = h * d;
        int index1 = hd * w;
        for (int x = 0, w2 = w / 2; x < w2; x++) {
            index1 -= hd;
            for (int yz = 0; yz < hd; yz++, index0++, index1++) {
                int byteIndex0 = index0 >> BITS_SHR;
                int byteIndex1 = index1 >> BITS_SHR;
                int shr0 = index0 & BITS_PER_MINUS_1;
                int shr1 = index1 & BITS_PER_MINUS_1;
                byte mask0 = (byte)(MASK_FIRST >> shr0);
                byte mask1 = (byte)(MASK_FIRST >> shr1);
                bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
            }
            index1 -= hd;
        }
        return this;
    }


    // returns shape rotated clockwise on Y axis by 90º (swaps w,d)
    public BitShape RotateY() {
        if (w == d) { // rotate in-place
            for (int y = 0, d2 = d / 2, zLimit = d - 1, xLimit = w - 1; y < h; y++)
                for (int z = 0, zNot = zLimit; z < d2; z++, zNot--)
                    for (int x = z, xNot = xLimit - z, xMax = xNot; x < xMax; x++, xNot--) {
                        // (this[x, y, z], this[zn, y, x], this[xn, y, zn], this[z, y, xn]) = (this[zn, y, x], this[xn, y, zn], this[z, y, xn], this[x, y, z]);
                        int index0 = d * (h * x + y) + z;
                        int index1 = d * (h * zNot + y) + x;
                        int index2 = d * (h * xNot + y) + zNot;
                        int index3 = d * (h * z + y) + xNot;
                        int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                        int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                        int byteIndex2 = index2 >> BITS_SHR, shr2 = index2 & BITS_PER_MINUS_1;
                        int byteIndex3 = index3 >> BITS_SHR, shr3 = index3 & BITS_PER_MINUS_1;
                        byte mask0 = (byte)(MASK_FIRST >> shr0);
                        byte mask1 = (byte)(MASK_FIRST >> shr1);
                        byte mask2 = (byte)(MASK_FIRST >> shr2);
                        byte mask3 = (byte)(MASK_FIRST >> shr3);
                        bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                        bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                        bool isSet2 = (bytes[byteIndex2] & mask2) != 0;
                        bool isSet3 = (bytes[byteIndex3] & mask3) != 0;
                        if (isSet0 != isSet1) bytes[byteIndex0] ^= mask0;
                        if (isSet1 != isSet2) bytes[byteIndex1] ^= mask1;
                        if (isSet2 != isSet3) bytes[byteIndex2] ^= mask2;
                        if (isSet3 != isSet0) bytes[byteIndex3] ^= mask3;
                    }
            return this;
        }

        var newShape = new BitShape(d, h, w);
        byte mask = MASK_FIRST;
        int byteIndex = 0;
        byte[] newBytes = newShape.bytes;
        for (int z = 0, xLimit = w - 1; z < d; z++)
            for (int y = 0; y < h; y++)
                for (int x = 0, xNot = xLimit; x < w; x++, xNot--) {
                    if (this[xNot, y, z]) newBytes[byteIndex] |= mask; // newShape[z, y, x] = true;
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; byteIndex++; }
                }

        return newShape;
    }

    // rotates in-place on Y axis by 180º
    public BitShape RotateY2() {
        for (int y = 0, w2 = w / 2, zLimit = d - 1, xLimit = w - 1; y < h; y++) {
            for (int x = 0, xNot = xLimit; x < w2; x++, xNot--)
                for (int z = 0, zNot = zLimit; z < d; z++, zNot--) {
                    // (this[x, y, z], this[xn, y, zn]) = (this[xn, y, zn], this[x, y, z]);
                    int index0 = d * (h * x + y) + z;
                    int index1 = d * (h * xNot + y) + zNot;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
            if (w % 2 == 1)
                for (int z = 0, zNot = zLimit, d2 = d / 2; z < d2; z++, zNot--) {
                    // (this[w2, y, z], this[w2, y, zn]) = (this[w2, y, zn], this[w2, y, z]);
                    int index = d * (h * w2 + y);
                    int index0 = index + z;
                    int index1 = index + zNot;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorY() {
        for (int x = 0, h2 = h / 2, yLimit = h - 1; x < w; x++)
            for (int y = 0, yNot = yLimit; y < h2; y++, yNot--)
                for (int z = 0; z < d; z++) {
                    // (this[x, y, z], this[x, yn, z]) = (this[x, yn, z], this[x, y, z]);
                    int hx = h * x;
                    int index0 = d * (hx + y) + z;
                    int index1 = d * (hx + yNot) + z;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
        return this;
    }

    public BitShape MirrorYOpt() {
        int yLimit = h - 1, h2 = h / 2;
        int hd = h * d;
        int index0 = 0, index1 = yLimit * d;
        for (int x = 0; x < w; x++, index0 += hd, index1 += hd) {
            int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
            byte mask0 = (byte)(MASK_FIRST >> shr0);
            int index1a = index1;
            for (int y = 0, yNot = yLimit; y < h2; y++, yNot--, index1a -= d) {
                int byteIndex1 = index1a >> BITS_SHR, shr1 = index1a & BITS_PER_MINUS_1;
                byte mask1 = (byte)(MASK_FIRST >> shr1);
                for (int z = 0; z < d; z++) {
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                    mask0 >>= 1; if (mask0 == 0) { mask0 = MASK_FIRST; byteIndex0++; }
                    mask1 >>= 1; if (mask1 == 0) { mask1 = MASK_FIRST; byteIndex1++; }
                }
            }
        }
        return this;
    }

    public BitShape MirrorYOpt2() {
        int yLimit = h - 1, h2 = h / 2;
        int hd = h * d;
        int index0 = 0, index1 = yLimit * d;
        for (int x = 0; x < w; x++, index0 += hd, index1 += hd) {
            int index0a = index0;
            int index1a = index1;
            for (int y = 0, yNot = yLimit; y < h2; y++, yNot--, index1a -= d) {
                for (int z = 0; z < d; z++, index0a++, index1a++) {
                    int byteIndex0 = index0a >> BITS_SHR;
                    int byteIndex1 = index1a >> BITS_SHR;
                    int shr0 = index0a & BITS_PER_MINUS_1;
                    int shr1 = index1a & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
                index1a -= d;
            }
        }
        return this;
    }


    // returns shape rotated clockwise on Z axis by 90º (swaps w,h)
    public BitShape RotateZ() {
        if (w == h) { // rotate in-place
            for (int z = 0, h2 = h / 2, xLimit = w - 1, yLimit = h - 1; z < d; z++)
                for (int y = 0, yNot = yLimit; y < h2; y++, yNot--)
                    for (int x = y, xNot = xLimit - y, xm = xNot; x < xm; x++, xNot--) {
                        // (this[x, y, z], this[yn, x, z], this[xn, yn, z], this[y, xn, z]) = (this[yn, x, z], this[xn, yn, z], this[y, xn, z], this[x, y, z]);
                        int index0 = d * (h * x + y) + z;
                        int index1 = d * (h * yNot + x) + z;
                        int index2 = d * (h * xNot + yNot) + z;
                        int index3 = d * (h * y + xNot) + z;
                        int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                        int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                        int byteIndex2 = index2 >> BITS_SHR, shr2 = index2 & BITS_PER_MINUS_1;
                        int byteIndex3 = index3 >> BITS_SHR, shr3 = index3 & BITS_PER_MINUS_1;
                        byte mask0 = (byte)(MASK_FIRST >> shr0);
                        byte mask1 = (byte)(MASK_FIRST >> shr1);
                        byte mask2 = (byte)(MASK_FIRST >> shr2);
                        byte mask3 = (byte)(MASK_FIRST >> shr3);
                        bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                        bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                        bool isSet2 = (bytes[byteIndex2] & mask2) != 0;
                        bool isSet3 = (bytes[byteIndex3] & mask3) != 0;
                        if (isSet0 != isSet1) bytes[byteIndex0] ^= mask0;
                        if (isSet1 != isSet2) bytes[byteIndex1] ^= mask1;
                        if (isSet2 != isSet3) bytes[byteIndex2] ^= mask2;
                        if (isSet3 != isSet0) bytes[byteIndex3] ^= mask3;
                    }
            return this;
        }

        var newShape = new BitShape(h, w, d);
        byte mask = MASK_FIRST;
        int byteIndex = 0;
        byte[] newBytes = newShape.bytes;
        for (int y = 0, xLimit = w - 1; y < h; y++)
            for (int x = 0, xNot = xLimit; x < w; x++, xNot--)
                for (int z = 0; z < d; z++) {
                    if (this[xNot, y, z]) newBytes[byteIndex] |= mask; // newShape[y, x, z] = true;
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; byteIndex++; }
                }

        return newShape;
    }

    // rotates in-place on Z axis by 180º
    public BitShape RotateZ2() {
        for (int z = 0, w2 = w / 2, xLimit = w - 1, yLimit = h - 1; z < d; z++) {
            for (int x = 0, xNot = xLimit; x < w2; x++, xNot--)
                for (int y = 0, yNot = yLimit; y < h; y++, yNot--) {
                    // (this[x, y, z], this[xn, yn, z]) = (this[xn, yn, z], this[x, y, z]);
                    int index0 = d * (h * x + y) + z;
                    int index1 = d * (h * xNot + yNot) + z;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
            if (w % 2 == 1)
                for (int y = 0, h2 = h / 2, yNot = yLimit; y < h2; y++, yNot--) {
                    // (this[w2, y, z], this[w2, yn, z]) = (this[w2, yn, z], this[w2, y, z]);
                    int index = h * w2;
                    int index0 = d * (index + y) + z;
                    int index1 = d * (index + yNot) + z;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
        }
        return this;
    }

    // mirrors in-place
    public BitShape MirrorZOrg() {
        for (int x = 0, d2 = d / 2, zLimit = d - 1; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0, zNot = zLimit; z < d2; z++, zNot--) {
                    // (this[x, y, z], this[x, y, zn]) = (this[x, y, zn], this[x, y, z]);
                    int index = d * (h * x + y);
                    int index0 = index + z;
                    int index1 = index + zNot;
                    int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                    int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
        return this;
    }

    public BitShape MirrorZ() { // was MirrorZOpt
        for (int x = 0, d2 = d / 2, zLimit = d - 1; x < w; x++)
            for (int y = 0; y < h; y++) {
                int index0 = d * (h * x + y);
                int index1 = index0 + zLimit;
                int byteIndex0 = index0 >> BITS_SHR, shr0 = index0 & BITS_PER_MINUS_1;
                int byteIndex1 = index1 >> BITS_SHR, shr1 = index1 & BITS_PER_MINUS_1;
                byte mask0 = (byte)(MASK_FIRST >> shr0);
                byte mask1 = (byte)(MASK_FIRST >> shr1);
                for (int z = 0; z < d2; z++) {
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                    mask0 >>= 1; if (mask0 == 0) { mask0 = MASK_FIRST; byteIndex0++; }
                    mask1 <<= 1; if (mask1 == 0) { mask1 = 1; byteIndex1--; }
                }
            }
        return this;
    }

    public BitShape MirrorZOpt2() {
        for (int x = 0, d2 = d / 2, zLimit = d - 1; x < w; x++) {
            int hx = h * x;
            for (int y = 0; y < h; y++) {
                int index0 = d * (hx + y);
                int index1 = index0 + zLimit;
                for (int z = 0; z < d2; z++, index0++, index1--) {
                    int byteIndex0 = index0 >> BITS_SHR;
                    int byteIndex1 = index1 >> BITS_SHR;
                    int shr0 = index0 & BITS_PER_MINUS_1;
                    int shr1 = index1 & BITS_PER_MINUS_1;
                    byte mask0 = (byte)(MASK_FIRST >> shr0);
                    byte mask1 = (byte)(MASK_FIRST >> shr1);
                    bool isSet0 = (bytes[byteIndex0] & mask0) != 0;
                    bool isSet1 = (bytes[byteIndex1] & mask1) != 0;
                    if (isSet0 != isSet1) { bytes[byteIndex0] ^= mask0; bytes[byteIndex1] ^= mask1; }
                }
            }
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
        var inputShape = new BitShape(this);
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;

        inputShape = inputShape.RotateY2();
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;

        inputShape = inputShape.RotateZ2();
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;

        inputShape = inputShape.RotateY2();
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;
    }

    // generates all 16 possible rotations for when w=h but d is different
    private IEnumerable<BitShape> All16RotationsX2Y2() {
        var inputShape = new BitShape(this);
        foreach (var shape in inputShape.AllRotationsOfZ()) yield return shape;

        inputShape = inputShape.RotateY2();
        foreach (var shape in inputShape.AllRotationsOfZ()) yield return shape;

        inputShape = inputShape.RotateX2();
        foreach (var shape in inputShape.AllRotationsOfZ()) yield return shape;

        inputShape = inputShape.RotateY2();
        foreach (var shape in inputShape.AllRotationsOfZ()) yield return shape;
    }

    // generates all 24 possible rotations of this shape for when w, h and d are the same
    private IEnumerable<BitShape> All24Rotations() {
        var inputShape = new BitShape(this);
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;

        inputShape = inputShape.RotateY();
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;

        var inputShapeRotateY2 = new BitShape(this).RotateY2();
        foreach (var shape in inputShapeRotateY2.AllRotationsOfX()) yield return shape;

        inputShape.RotateY2();
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;

        inputShape = inputShape.RotateZ();
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;

        inputShape.RotateZ2();
        foreach (var shape in inputShape.AllRotationsOfX()) yield return shape;
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

        BitShape minShape = null;
        foreach (var shape in AllMinRotations())
            if (minShape == null || minShape.CompareTo(shape) > 0)
                minShape = new BitShape(shape); // must clone it to prevent it from being mutated in-place
        return minShape;
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
        BitShape minShape = null;
        foreach (var rotatedShape in AllMinRotations())
            foreach (var mirroredShape in rotatedShape.AllMirrors())
                if (minShape == null || minShape.CompareTo(mirroredShape) > 0)
                    minShape = new BitShape(mirroredShape); // must clone it to prevent it from being mutated in-place
        return minShape;
    }

    // check all rotations for each mirrored possibility (8)
    // to see if current shape is the minimal rotation.
    public bool IsMinChiralRotation() {
        var inputShape = new BitShape(this);
        foreach (var rotatedShape in AllMinRotations())
            foreach (var mirroredShape in rotatedShape.AllMirrors())
                if (inputShape.CompareTo(mirroredShape) > 0)
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
        int bitIndex = 0;
        int xLimit = w - 1, yLimit = h - 1, zLimit = d - 1;
        for (int x = 0; x <= xLimit; x++) {
            bool xFace = x == 0 || x == xLimit;
            for (int y = 0; y <= yLimit; y++) {
                bool yFace = y == 0 || y == yLimit;
                for (int z = 0; z <= zLimit; z++, bitIndex++) {
                    bool zFace = z == 0 || z == zLimit;
                    if (xFace || yFace || zFace) {
                        int byteIndex = bitIndex >> BITS_SHR;
                        int shr = bitIndex & BITS_PER_MINUS_1;
                        byte mask = (byte)(MASK_FIRST >> shr);
                        if ((bytes[byteIndex] & mask) != 0)
                            if (xFace && yFace || yFace && zFace || xFace && zFace)
                                if (xFace && yFace && zFace)
                                    corners++;
                                else
                                    edges++;
                            else
                                faces++;
                    }
                }
            }
        }
        return (corners, edges, faces);
    }

    public (int corners, int edges, int faces) CornerEdgeFaceCountOrg() {
        int corners = 0, edges = 0, faces = 0;
        int byteIndex = 0;
        byte mask = MASK_FIRST;
        int bitIndex = 0;
        int xLimit = w - 1, yLimit = h - 1, zLimit = d - 1;
        for (int x = 0; x <= xLimit; x++) {
            bool xFace = x == 0 || x == xLimit;
            for (int y = 0; y <= yLimit; y++) {
                bool yFace = y == 0 || y == yLimit;
                for (int z = 0; z <= zLimit; z++, bitIndex++) {
                    bool zFace = z == 0 || z == zLimit;
                    if (xFace || yFace || zFace) {
                        if ((bytes[byteIndex] & mask) != 0)
                            if (xFace && yFace || yFace && zFace || xFace && zFace)
                                if (xFace && yFace && zFace)
                                    corners++;
                                else
                                    edges++;
                            else
                                faces++;
                    }
                    mask >>= 1; if (mask == 0) { mask = MASK_FIRST; byteIndex++; }
                }
            }
        }
        return (corners, edges, faces);
    }
}
