using System;

namespace ShapeMaker;

public class Shape<T> where T : IComparable {
    public readonly int w, h, d;
    public readonly T[,,] shape;

    public Shape(int w, int h, int d) {
        this.w = w;
        this.h = h;
        this.d = d;
        shape = new T[w, h, d];
    }

    public Shape(int w, int h, int d, IEnumerable<T> values) {
        this.w = w;
        this.h = h;
        this.d = d;
        shape = new T[w, h, d];
        using var enumerator = values.GetEnumerator();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    if (enumerator.MoveNext())
                        shape[x, y, z] = enumerator.Current;
    }

    // clone shape
    public Shape(Shape<T> s) {
        w = s.w;
        h = s.h;
        d = s.d;
        shape = new T[w, h, d];
        Array.Copy(s.shape, shape, w * h * d); // works on multidimension arrays just fine
    }

    public IEnumerable<T> Values() {
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    yield return shape[x, y, z];
    }

    public Shape<T> PadLeft() => ShapeCopy(new Shape<T>(w + 1, h, d), 1, w, 0, h, 0, d);

    public Shape<T> PadRight() => ShapeCopy(new Shape<T>(w + 1, h, d), 0, w, 0, h, 0, d);

    public Shape<T> PadTop() => ShapeCopy(new Shape<T>(w, h + 1, d), 0, w, 1, h, 0, d);

    public Shape<T> PadBottom() => ShapeCopy(new Shape<T>(w, h + 1, d), 0, w, 0, h, 0, d);

    public Shape<T> PadFront() => ShapeCopy(new Shape<T>(w, h, d + 1), 0, w, 0, h, 1, d);

    public Shape<T> PadBack() => ShapeCopy(new Shape<T>(w, h, d + 1), 0, w, 0, h, 0, d);

    // copy all of this shape to new shape with x/y/z offsets
    public Shape<T> ShapeCopy(Shape<T> to, int xa, int w, int ya, int h, int za, int d) {
        for (int x = 0, xd = xa; x < w; x++, xd++)
            for (int y = 0, yd = ya; y < h; y++, yd++)
                for (int z = 0, zd = za; z < d; z++, zd++)
                    to.shape[xd, yd, zd] = shape[x, y, z];
        return to;
    }

    // returns shape rotated clockwise on X axis by 90º (swaps h,d)
    public Shape<T> RotateX() {
        if (d == h) { // rotate in-place
            int h2 = h / 2;
            for (int x = 0; x < w; x++)
                for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                    for (int z = y, zn = d - 1 - y, zm = zn; z < zm; z++, zn--) {
                        var t = shape[x, y, z];
                        shape[x, y, z] = shape[x, zn, y];
                        shape[x, zn, y] = shape[x, yn, zn];
                        shape[x, yn, zn] = shape[x, z, yn];
                        shape[x, z, yn] = t;
                    }
            return this;
        }

        var newShape = new Shape<T>(w, d, h);
        for (int x = 0; x < w; x++)
            for (int y = 0, yn = h - 1; y < h; y++, yn--)
                for (int z = 0; z < d; z++)
                    newShape.shape[x, z, y] = shape[x, yn, z];

        return newShape;
    }

    // rotates in-place on X axis by 180º
    public Shape<T> RotateX2() {
        int h2 = h / 2;
        for (int x = 0; x < w; x++) {
            for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                for (int z = 0, zn = d - 1; z < d; z++, zn--)
                    (shape[x, yn, zn], shape[x, y, z]) = (shape[x, y, z], shape[x, yn, zn]);
            if (h % 2 == 1)
                for (int z = 0, zn = d - 1; z < d / 2; z++, zn--)
                    (shape[x, h2, zn], shape[x, h2, z]) = (shape[x, h2, z], shape[x, h2, zn]);
        }
        return this;
    }

    // mirrors in-place
    public Shape<T> MirrorX() {
        for (int x = 0, xn = w - 1; x < w / 2; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    (shape[x, y, z], shape[xn, y, z]) = (shape[xn, y, z], shape[x, y, z]);
        return this;
    }

    // returns shape rotated clockwise on Y axis by 90º (swaps w,d)
    public Shape<T> RotateY() {
        if (w == d) { // rotate in-place
            int d2 = d / 2;
            for (int y = 0; y < h; y++)
                for (int z = 0, zn = d - 1; z < d2; z++, zn--)
                    for (int x = z, xn = w - 1 - z, xm = xn; x < xm; x++, xn--) {
                        var t = shape[x, y, z];
                        shape[x, y, z] = shape[zn, y, x];
                        shape[zn, y, x] = shape[xn, y, zn];
                        shape[xn, y, zn] = shape[z, y, xn];
                        shape[z, y, xn] = t;
                    }
            return this;
        }

        var newShape = new Shape<T>(d, h, w);
        for (int x = 0, xn = w - 1; x < w; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    newShape.shape[z, y, x] = shape[xn, y, z];

        return newShape;
    }

    // rotates in-place on Y axis by 180º
    public Shape<T> RotateY2() {
        int w2 = w / 2;
        for (int y = 0; y < h; y++) {
            for (int x = 0, xn = w - 1; x < w2; x++, xn--)
                for (int z = 0, zn = d - 1; z < d; z++, zn--)
                    (shape[xn, y, zn], shape[x, y, z]) = (shape[x, y, z], shape[xn, y, zn]);
            if (w % 2 == 1)
                for (int z = 0, zn = d - 1; z < d / 2; z++, zn--)
                    (shape[w2, y, zn], shape[w2, y, z]) = (shape[w2, y, z], shape[w2, y, zn]);
        }
        return this;
    }

    // mirrors in-place
    public Shape<T> MirrorY() {
        for (int x = 0; x < w; x++)
            for (int y = 0, yn = h - 1; y < h / 2; y++, yn--)
                for (int z = 0; z < d; z++)
                    (shape[x, y, z], shape[x, yn, z]) = (shape[x, yn, z], shape[x, y, z]);
        return this;
    }

    // returns shape rotated clockwise on Z axis by 90º (swaps w,h)
    public Shape<T> RotateZ() {
        if (w == h) { // rotate in-place
            int h2 = h / 2;
            for (int z = 0; z < d; z++)
                for (int y = 0, yn = h - 1; y < h2; y++, yn--)
                    for (int x = y, xn = w - 1 - y, xm = xn; x < xm; x++, xn--) {
                        var t = shape[x, y, z];
                        shape[x, y, z] = shape[yn, x, z];
                        shape[yn, x, z] = shape[xn, yn, z];
                        shape[xn, yn, z] = shape[y, xn, z];
                        shape[y, xn, z] = t;
                    }
            return this;
        }

        var newShape = new Shape<T>(h, w, d);
        for (int x = 0, xn = w - 1; x < w; x++, xn--)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++)
                    newShape.shape[y, x, z] = shape[xn, y, z];

        return newShape;
    }

    // rotates in-place on Z axis by 180º
    public Shape<T> RotateZ2() {
        int w2 = w / 2;
        for (int z = 0; z < d; z++) {
            for (int x = 0, xn = w - 1; x < w2; x++, xn--)
                for (int y = 0, yn = h - 1; y < h; y++, yn--)
                    (shape[xn, yn, z], shape[x, y, z]) = (shape[x, y, z], shape[xn, yn, z]);
            if (w % 2 == 1)
                for (int y = 0, yn = h - 1; y < h / 2; y++, yn--)
                    (shape[w2, yn, z], shape[w2, y, z]) = (shape[w2, y, z], shape[w2, yn, z]);
        }
        return this;
    }

    // mirrors in-place
    public Shape<T> MirrorZ() {
        int d2 = d / 2;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0, zn = d - 1; z < d2; z++, zn--)
                    (shape[x, y, z], shape[x, y, zn]) = (shape[x, y, zn], shape[x, y, z]);
        return this;
    }

    // returns the shape rotated such that the dimensions are narrowest in the X and broadest in the Z
    public Shape<T> MinDimension() {
        if (w <= h && h <= d)
            return this;

        // w,h,d possibilities
        // 1,2,3 => fine
        // 1,3,2 => swap h,d
        // 2,1,3 => swap w,h
        // 2,3,1 => swap h,d swap w,h
        // 3,1,2 => swap w,h swap h,d
        // 3,2,1 => swap w,d

        // we need to rotate
        if (w <= h && w <= d && h > d)
            return RotateX(); // swaps h,d
        if (w <= d && h <= d && w > h)
            return RotateY(); // swaps w,h
        if (w > h && h > d)
            return RotateZ(); // swaps w,d
        if (h > w && w > d)
            return RotateX().RotateY();
        if (w > h && w > d && h <= d)
            return RotateY().RotateX();

        throw new ApplicationException("not sure how we got here");
    }

    // generates all 24 possible rotations of this shape
    private IEnumerable<Shape<T>> AllRotations() {
        var shape = new Shape<T>(this);
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateY();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        foreach (var s in RotateY2().AllRotationsOfX()) yield return s;
        RotateY2();

        shape = shape.RotateY2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = RotateZ();
        foreach (var s in shape.AllRotationsOfX()) yield return s;

        shape = shape.RotateZ2();
        foreach (var s in shape.AllRotationsOfX()) yield return s;
    }

    // generates all 4 possible rotations on X axis of this shape
    private IEnumerable<Shape<T>> AllRotationsOfX() {
        if (h == d) // rotates in-place but restores it
            for (int i = 0; i < 4; i++)
                yield return RotateX();
        else {
            yield return this;
            var shape90 = RotateX();
            yield return shape90;
            yield return RotateX2();
            yield return shape90.RotateX2();
            RotateX2();
        }
    }

    // returns the shape rotated such that it is the mimimum serialization of the shape
    public Shape<T> MinRotation() {
        // a rubiks cube, for example, is a 3x3x3 shape
        // it can be resting on any of its six colors
        // and could have any of four sides facing you. 6*4=24

        // we will need to apply rotations to get all 24 combinations
        // and see which one compares to be the lowest

        Shape<T> best = null;
        foreach (var s in AllRotations())
            if (best == null || best.CompareTo(s) > 0)
                best = new Shape<T>(s); // must clone it to prevent it from being mutated in-place
        return best;
    }

    private IEnumerable<Shape<T>> AllRotationsOpt() {
        if (w < h && h < d) // we just want the 8 180º rotations
            foreach (var s in All180DegreeRotations()) yield return s;
        else if (h < w && w < d) // we just want the 8 180º rotations after we rotate Z (swaps w,h)
            foreach (var s in RotateY().All180DegreeRotations()) yield return s;
        else if (w < d && d < h) // we just want the 8 180º rotations after we rotate X (swaps h,d)
            foreach (var s in RotateX().All180DegreeRotations()) yield return s;
        else if (h < d && d < w) // we just want the 8 180º rotations after we rotate Z (swaps w,h) then X (swaps h,d)
            foreach (var s in RotateZ().RotateX().All180DegreeRotations()) yield return s;
        else if (d < w && w < h) // we just want the 8 180º rotations after we rotate X (swaps h,d) then Z (swaps w,h)
            foreach (var s in RotateX().RotateZ().All180DegreeRotations()) yield return s;
        else if (d < h && h < w) // we just want the 8 180º rotations after we rotate Y (swaps w,d)
            foreach (var s in RotateY().All180DegreeRotations()) yield return s;
        else
            foreach (var s in AllRotations()) yield return s; // could be further optimized
    }

    // generates all 8 possible 180º rotations of shape
    // note these are generated in-place on this shape
    public IEnumerable<Shape<T>> All180DegreeRotations() {
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

    // generate all 8 possible mirrorings of shape
    // note these are generated in-place on this shape
    public IEnumerable<Shape<T>> AllMirrors() {
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
    public Shape<T> MinChiralRotation() {
        Shape<T> best = null;
        foreach (var s in AllRotations())
            foreach (var ss in s.AllMirrors())
                if (best == null || best.CompareTo(ss) > 0)
                    best = new Shape<T>(ss); // must clone it to prevent it from being mutated in-place
        return best;
    }

    // returns <0 if this is lower than other, 0 if equal, >0 if other is lower
    public int CompareTo(Shape<T> other) {
        int dw = w - other.w;
        if (dw != 0) return dw;

        int dh = h - other.h;
        if (dh != 0) return dh;

        int dd = d - other.d;
        if (dd != 0) return dd;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                for (int z = 0; z < d; z++) {
                    var diff = shape[x, y, z].CompareTo(other.shape[x, y, z]);
                    if (diff != 0) return diff;
                }

        return 0;
    }

    public override bool Equals(object? obj) {
        if (obj is Shape<T> other)
            return CompareTo(other) == 0;

        return base.Equals(obj);
    }

    // optimized for where T is bool
    public override int GetHashCode() {
        int hashCode = w * 37 * 37 + h * 37 + d;
        if (shape is bool[,,] ba)
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    for (int z = 0; z < d; z++)
                        hashCode = hashCode * 3 + (ba[x, y, z] ? 1 : 0);
        else
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    for (int z = 0; z < d; z++)
                        hashCode = hashCode * 37 + shape[x, y, z].GetHashCode();
        return hashCode;
    }
}
